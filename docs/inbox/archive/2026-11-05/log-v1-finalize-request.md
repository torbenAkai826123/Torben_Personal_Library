# Log v1 收尾需求: Development Alert、sink 例外隔離、複製功能

- 對象: 負責實作的 AI。前提是讀者已讀過 `docs/log-system.md`、現況報告, 以及先前的 `log-editor-tools-request.md` 與其結果報告。
- 目的: 完成 Log 系統桌面版 v1。本次之後, Log 系統只剩 Android 實機驗證與讀寫系統完成後的寫檔 sink。
- 三個項目彼此獨立, 可以分開提交; 全部完成才算本需求完成。

---

## 1. Development Alert

### 1.1 用途

執行時 (`DebugLogs.Install()`) 發現設定有問題時, 依設定的等級用 Unity 的 Log 發出提醒。Development Build 遇到 Error 會自動顯示開發主控台, GUI 視窗開啟時也會收到 (Unity 訊息收集)。

### 1.2 資料

```csharp
public enum DevelopmentAlertLevel { Off, Warning, Error }

[System.Serializable]
public sealed class DevelopmentAlertOptions
{
    public DevelopmentAlertLevel level = DevelopmentAlertLevel.Warning;
}
```

- 放在 Runtime。`LogSettings` 新增 `[SerializeField, HideInInspector] DevelopmentAlertOptions developmentAlert;`, 並提供讀取用的屬性。
- **只在 Project Settings 頁面編輯**: `LogSettingsProvider` 以 `SerializedObject.FindProperty` 編輯, 支援 Undo, 顯示名稱 `Development Alert`。編輯對象為生效中的檔案 (有覆蓋時為覆蓋檔), 與 Save Current State 的規則一致。
- 確認自訂的 `LogSettingsInspector` 不會畫出這個欄位。
- Save Current State 不寫入這個欄位 (它不是執行中的狀態)。

### 1.3 執行時檢查

| 項目 | 條件 |
|---|---|
| 找不到設定檔 | 現有行為。此時讀不到等級, 一律以 `Warning` 處理, 訊息內容不變 |
| 容量超出範圍 | `logCapacity` 或 `unityCapacity` 小於 1, 或達到 Editor 狀態檢查的警告上限 (現為 10000)。上限常數與 Editor 共用一處定義 |
| 等級陣列重複 | 堆疊追蹤開關或等級顏色中同一等級出現多次 (現行為: 最上面那筆生效) |

- 等級為 `Off` 時不輸出; `Warning` 用 `UnityEngine.Debug.LogWarning`; `Error` 用 `UnityEngine.Debug.LogError`。
- **不得經過 `Log`**。
- 多個問題合併為 **一則** 訊息, 每次 Install 最多一則 (避免 Error 等級時一次跳出多則)。
- **發出時機必須在開始收集 Unity 訊息 (`isUnityLog` 訂閱) 之後**, 讓 GUI 視窗收得到。請確認現有順序並在回報中說明; 若順序不對則修正。
- 容量小於 1 時的處理沿用現況 (若現況會丟例外或修正為 1, 維持原行為並在回報中說明)。

### 1.4 Editor 狀態檢查 (選做, 成本低)

覆蓋連結變成 Missing (原本有連結, 但檔案被刪除) 時, 建置版會悄悄改用預設檔。Editor 狀態檢查可以區分:

```csharp
var prop = serializedObject.FindProperty("projectOverride");
bool isMissing = prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0;
```

`isMissing` 時加入一項警告「覆蓋檔已遺失 (Missing)」。不需要修改任何 Unity 設定。

### 1.5 測試

| 範圍 | 測試 |
|---|---|
| PlayMode | 三種等級各自的輸出 (Off 無輸出; Warning / Error 各一則); 多個問題合併為一則; 找不到設定檔時為 Warning; GUI 視窗的 `UnityHistory` 收得到這則提醒 |
| PlayMode | Error 等級的測試以 `LogAssert.Expect` 標明預期中的錯誤 |
| EditMode | 頁面編輯的是生效中的檔案; Inspector 不顯示此欄位 (若可自動測試); 1.4 若有實作, 以暫存 asset 測試 Missing 判斷 |

---

## 2. sink 例外隔離

### 2.1 現況問題

`Log` 依序同步呼叫各 sink 的 `Write`。任一 sink 拋例外時, 後面的 sink 收不到該筆訊息, 例外並傳回呼叫 `Log.Xxx` 的地方, 可能中斷遊戲邏輯。

### 2.2 修改

```csharp
for (int i = 0; i < current.Length; i++)
{
    try { current[i].Write(in entry); }
    catch (System.Exception e) { ReportSinkError(current[i], e); }
}
```

| 項目 | 規格 |
|---|---|
| 回報方式 | Core 新增 `public static System.Action<ILogSink, System.Exception> SinkErrorHandler`。Runtime 在 `Install` 時設定為以 `UnityEngine.Debug.LogException` 輸出 (訊息需包含 sink 型別名稱)。未設定時不輸出 |
| 只回報一次 | 每個 sink 實例只回報第一次失敗。記錄用的結構只在發生例外時建立 (不影響無例外時的零配置)。`RemoveSink` / `ClearSinks` 時清除對應記錄。需執行緒安全 |
| 防止遞迴 | 回報過程不得經過 `Log`。以 `[ThreadStatic]` 旗標標記「正在回報」; 旗標為 true 時, 同一執行緒上的 `Log` 寫入直接 return。`SinkErrorHandler` 自己拋例外時捕捉並忽略 |
| 不重新拋出 | 例外不傳回呼叫端 |
| 不自動移除 | 失敗的 sink 保留在清單中 (自動移除會讓問題更難追查) |
| 效能 | 無例外時的路徑不得新增配置 |

- XML 註解與 `docs/log-system.md` 說明以上行為。
- 現況報告第 4.2 節「沒有例外隔離」改為新行為。

### 2.3 測試 (dotnet)

- 第一個 sink 拋例外時, 第二個 sink 仍收到訊息, 呼叫端不收到例外。
- 同一個 sink 連續失敗, `SinkErrorHandler` 只被呼叫一次; `RemoveSink` 後重新加入, 再次失敗時會再回報。
- `SinkErrorHandler` 為 null 時不拋例外。
- `SinkErrorHandler` 內呼叫 `Log.Error` 不會遞迴 (該次寫入被略過)。
- `SinkErrorHandler` 自己拋例外時, 呼叫端不收到例外。
- 若測試框架支援 `System.GC.GetAllocatedBytesForCurrentThread()`, 加一項「無例外時寫入不配置」的測試; 不支援則在回報中說明。

---

## 3. 複製功能

### 3.1 API

使用 `UnityEngine.GUIUtility.systemCopyBuffer` (Runtime API, 建置版也可用; 不要用 Editor 專用的 `EditorGUIUtility.systemCopyBuffer`)。

### 3.2 介面

| 位置 | 行為 |
|---|---|
| 每一列 | 新增複製按鈕 (位置與尺寸和展開按鈕一致), 複製該列 |
| 工具列 | 新增 `Copy` 按鈕, 複製 **目前篩選後看得到的全部內容** (套用等級、來源、搜尋與 Collapse), 依顯示順序, 每筆之間空一行 |
| 回饋 | 按下後該按鈕短暫顯示已複製狀態 (約 1 秒, 以 `Time.realtimeSinceStartup` 判斷), 不另開提示 |

- `DebugWindowButtons` 新增 `Copy` 旗標, 同時控制兩種按鈕。
- **舊設定檔的問題**: 已存成特定組合 (非 `All`) 的設定檔, 新增的位元為 0, 複製按鈕會被隱藏。請在 `docs/log-system.md` 註明, 並檢查專案中現有設定檔的 `visibleButtons` 值, 在回報中說明是否受影響。

### 3.3 文字格式

格式化放在 Core `LogText`, 例如 `FormatForCopy(in LogEntry entry, int count)`:

```text
[Error] [Tag] 訊息內容
(19:47:27.20) Assets/Exceptions/LogTest/CallLog.cs:68
  at CallLog.LogError (System.String message) in Assets/Exceptions/LogTest/CallLog.cs:68
  at ...
```

| 項目 | 規則 |
|---|---|
| 第一行 | `[等級] ` + 分類 Tag (空白則省略) + 訊息。Collapse 合併多次時在行尾加 ` (x次數)` |
| 第二行 | 時間與位置, 格式與視窗第二行相同 (`LogText.FormatTime`、`ToDisplayPath`)。**不受** 視窗 Time / Loc 開關影響, 一律輸出; 沒有資料的部分省略, 兩者都沒有就省略整行 |
| 堆疊 | 有堆疊時輸出 `StackTrace.Text` (已清理的版本), 不受展開狀態影響 |
| rich text | 去掉 Unity rich text 標籤: `<b>` `</b>` `<i>` `</i>` `<color=...>` `</color>` `<size=...>` `</size>` `<material=...>` `</material>` `<quad .../>`。**只去掉這些已知標籤**, 其他 `<...>` (例如 `List<int>`、堆疊中的 `<OnGUIFunc>`) 必須保留 |
| 換行 | 使用 `\n` |

- 配置: 只在按下按鈕時發生, XML 註解標明「每次呼叫配置」。
- Editor 專案根目錄: 沿用視窗目前取得專案根目錄的方式傳入 `ToDisplayPath`, 建置版傳 `null`。

### 3.4 測試

| 範圍 | 測試 |
|---|---|
| dotnet | 格式: 有 / 無分類、有 / 無時間與位置、有 / 無堆疊、Collapse 次數; rich text 去除與 `List<int>`、`<OnGUIFunc>`、`<0x...>` 保留; 巢狀與未閉合標籤不拋例外 |
| PlayMode | 單筆複製與全部複製後 `GUIUtility.systemCopyBuffer` 內容正確 (含篩選與搜尋); `Copy` 旗標關閉時兩種按鈕都不顯示 |

---

## 4. 文件

- `docs/log-system.md`: 加入 Development Alert、sink 例外隔離、複製功能的說明。
- 現況報告:
  - 第 2 節表格加入三個項目。
  - 第 4.2 節更新例外行為。
  - 第 8 節缺口: 移除「sink 錯誤隔離」「沒有複製功能」; 「持久化」改為「等待讀寫系統完成後, 以寫檔 sink 實作」。
  - 新增「Android 待驗證」清單: 視窗縮放是否需納入像素密度 (橫向 1080p 推測核取方塊約 1.3 mm)、觸控與拖曳、正式版關閉 Log 時每幀 GC Alloc (Profiler)、`GUIUtility.systemCopyBuffer` 行為。
  - 新增「刻意不做」清單: Console 點兩下跳到呼叫端、建置前檢查、依建置類型切換設定、逐欄位覆蓋、執行中修改容量。
  - 標示版本: Log 系統桌面版 v1 完成。

---

## 5. 完成標準

全部符合才算完成:

1. dotnet、EditMode、PlayMode 測試全部通過, 第 1.5、2.3、3.4 節列出的測試都存在 (1.4 為選做)。
2. Core 沒有引用 `UnityEngine` / `UnityEditor` / `System.Text.RegularExpressions`。
3. 人工確認 (Editor): Project Settings 可切換 Development Alert 三種等級; 刪除設定檔後 GUI 視窗看得到提醒; 單筆與全部複製後貼到文字編輯器的內容正確。
4. 人工確認 (macOS IL2CPP 建置): 複製功能可用; Error 等級時開發主控台顯示提醒 (Development Build)。
5. 第 4 節文件已更新。
6. 回報: 修改的檔案清單、測試數量變化、Development Alert 發出時機的確認結果 (1.3)、容量小於 1 的現有處理方式、現有設定檔 `visibleButtons` 是否受新旗標影響 (3.2)、遇到但未處理的問題。

---

## 6. 不在本次範圍

- 讀寫系統與寫檔 sink (之後獨立規劃)。
- Android 實機驗證。
- 第 4 節「刻意不做」清單中的項目。
