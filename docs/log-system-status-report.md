# Log 系統現況報告 (給設計用 AI)

- 對象: 負責後續設計 Log 系統的 AI。假設讀者看不到原始碼, 本文已附上必要的型別定義。
- 基準: 提交 `52b53ec` (2026-11-02), 分支 `main`, 加上同日的問題修正 (第 7 節) 與 Unity 引擎訊息收集。
- 權威說明: [`docs/log-system.md`](log-system.md) (行為與 API); 本報告補上資料結構全貌、限制、缺口與已知問題。兩者衝突時以原始碼為準。

---

## 1. 專案背景與設計限制

設計新功能時必須遵守以下規則 (摘自專案的 `AGENTS.md`):

| 項目 | 規則 |
|---|---|
| 引擎 | Unity `6000.5.5f1` |
| 目標平台 | Windows、macOS、Android、iOS。行動平台用 IL2CPP (AOT), 執行期不能產生程式碼 |
| 分層 | **Core**: 純 C#, 不得引用 `UnityEngine` / `UnityEditor`, 以 `dotnet test` 測試 (netstandard2.1, C# 9.0)。**Runtime**: 只放必須用 Unity API 的部分, 盡量精簡, 以 PlayMode 測試。**Editor**: Editor 專用工具 |
| 新功能歸屬 | 先判斷能否放進 Core, 不行才放 Runtime |
| 依賴方向 | Samples 可引用函式庫; 函式庫不得引用 Samples |
| AOT | 通用程式避免執行期反射、`System.Reflection.Emit`、`dynamic`、執行期才決定型別的泛型 |
| GC | 每幀路徑零配置為目標; 載入期或一次性 API 可配置。公開 API 的 XML 註解需標明配置行為 (零配置 / 僅初始化時配置 / 每次呼叫配置) |
| 資料結構 | 優先用內建方案; 自製結構需同時符合「在每幀路徑」「量測證實內建方案有問題」「成本可接受」 |
| 委派 | Inspector 要設定的事件用 `UnityEvent`; Core 與熱路徑用 `System.Action` / `System.Func` 或自訂 delegate |
| 寫法 | 非 Core 程式不單獨 `using System;`, 用完整名稱。註解用中文, 半形標點 + 空格 |
| 測試 | 新增公開 API 時一併新增測試 |

---

## 2. 完成狀況總覽

| 功能 | 狀態 | 位置 |
|---|---|---|
| 靜態 Log 入口 (4 個等級) | 完成 | Core `Log` |
| 關閉時零配置的格式化 API | 完成 | Core `Log.IsEnabled`、`Log.XxxFormat` |
| 總開關、Debug 等級開關 | 完成 | Core `Log.isPrintLog` / `isDebugLog` |
| 多 sink (copy-on-write) | 完成 | Core `Log.AddSink` 等 |
| 分類標籤 + 標籤顏色 | 完成 | Core `LogCategory` |
| 歷史紀錄 (環狀緩衝區, 含 Clear) | 完成 | Core `LogHistorySink` |
| Unity Console 輸出 | 完成 | Runtime `DebugLogs` |
| 啟動時自動安裝 sink | 完成 | Runtime `DebugLogs.Install` |
| 畫面視窗 (IMGUI) | 完成 | Runtime `DebugWindow` |
| ├ 等級篩選 (N/W/E/D)、搜尋 | 完成 | |
| ├ 等級文字顏色 | 完成 | |
| ├ 拖曳、超出畫面時拉回、尺寸過大時縮為半個畫面 | 完成 | |
| ├ 依畫面高度自動縮放 | 完成 (行動平台未驗證) | |
| ├ Clear (兩段式確認)、Close 按鈕 | 完成 | |
| └ 自動捲到最下方 | 完成 | |
| Unity 引擎訊息收集 (獨立開關 `isUnityLog`, 預設開) | 完成 | Runtime `DebugLogs`、`UnityLogCategory` |
| ├ 視窗 Log / Unity 來源篩選 | 完成 | `DebugWindow.ShowLogMessages` / `ShowUnityMessages` |
| └ Clear 只清 Log, 保留 Unity 訊息 | 完成 | |
| 跨場景宿主 | 完成 | Runtime `CrossObject` |
| Editor 工具 | 無 | — |
| Android / iOS 實機 | **未驗證** | — |

---

## 3. 架構與資料流

```text
呼叫端: Log.Information / Warning / Error / Debug(message, category)
        或 Log.XxxFormat([category,] format, arg1..arg3)  ← 先檢查 IsEnabled, 開啟才 string.Format
        │
        │  (1)(2) IsEnabled(level) 為 false → 直接 return
        │         (isPrintLog 關閉、沒有 sink, 或 Debug 等級但 isDebugLog 關閉)
        │  (3) category == null → 改用 LogCategoryEmpty.None
        │  (4) 建立 LogEntry (readonly struct, 不配置)
        ▼
  sinks 陣列 (volatile, copy-on-write), 在「呼叫端的執行緒」上依序同步呼叫 Write(in entry)
        ├─ DebugLogs (Runtime)        → UnityEngine.Debug.Log / LogWarning / LogError
        │     isGUILog 開且 isConsoleWithGUI 關時略過
        └─ LogHistorySink (Core, 200 筆) → 環狀緩衝區, 每次寫入 Version++
                    ▲
                    │ CopyTo(snapshot, sequences, out version)  (只在 IMGUI Layout 事件)
              DebugWindow (Runtime, MonoBehaviour, 掛在 CrossObject 上)
                    │ 依流水號合併兩份紀錄
                    ▼
              LogHistorySink (Unity 用, 200 筆) ── DebugWindow.UnityHistory
                    ▲
Unity 引擎訊息 → Application.logMessageReceivedThreaded → DebugLogs.OnUnityLog
                (isUnityLog 開啟時; 不受 isPrintLog 影響; 排除 DebugLogs 自己輸出到 Console 的訊息)
```

組件 (asmdef):

| 組件 | 引用 | 含有的 Log 檔案 |
|---|---|---|
| `Torben.Core` (`noEngineReferences: true`) | 無 | `Log.cs`、`LogModels.cs`、`LogCategory.cs`、`LogHistorySink.cs` |
| `Torben.Runtime` | `Torben.Core` | `DebugLogs.cs`、`DebugWindow.cs`、`UnityLogCategory.cs`、`StaticObject/CrossObject.cs` |
| `Library.Tests.PlayMode` | Core、Runtime | `DebugWindowTests.cs` |
| dotnet `Library.Tests` | `Torben.Core` (以 `Compile Include` 直接編譯 `Scripts/Core/**/*.cs`) | `LogTests.cs`、`LogHistorySinkTests.cs` |

路徑皆相對於 `Assets/[TorbenJuniorUtility]/Scripts/`, dotnet 測試在倉庫根的 `dotnet/Library.Tests/`。所有型別都在 `namespace Torben`。

---

## 4. 資料結構 (Core)

### 4.1 `LogLevel`

```csharp
public enum LogLevel { Normal, Warning, Error, Debug }
```

- 數值順序 **不是** 嚴重度順序 (`Debug` 排在 `Error` 之後), 不能用 `>=` 做最低等級篩選。
- `DebugWindow` 以 `(int)level` 當陣列索引, 並用 `Enum.GetNames` 取得等級數量與按鈕文字 (取名稱第一個字母)。新增等級會自動多一個篩選按鈕, 但第一個字母可能重複。

### 4.2 `ILogSink`

```csharp
public interface ILogSink { void Write(in LogEntry entry); }
```

- 同步呼叫, 在呼叫 `Log.Xxx` 的執行緒上執行。沒有非同步佇列、沒有例外隔離 (某個 sink 丟例外會中斷後續 sink 並傳回呼叫端)。

### 4.3 `LogEntry`

```csharp
public readonly struct LogEntry
{
    public readonly LogLevel Level;
    public readonly LogCategory Category;   // 經過 Log 時不會是 null
    public readonly string Message;
}
```

- **沒有** 時間戳記、影格編號、執行緒 ID、堆疊追蹤、`UnityEngine.Object` context。

### 4.4 `LogCategory`

```csharp
public abstract class LogCategory
{
    protected LogCategory(string identifierName = "");
    public string Tag { get; }                 // 延遲建立並快取; 預設 "[IdentifierName]", 名稱空白時為 ""
    public string IdentifierName { get; set; } // 設定時重建 Tag
    public string tagColorHex;                 // 公開欄位, 只影響標籤顏色, 例如 "#4FC3F7"
    protected virtual string UpdateTag();      // 覆寫可自訂 Tag 格式
    protected string UpdateTag(string customTag);
}
internal sealed class LogCategoryEmpty : LogCategory   // 單例 None, Tag = ""
public sealed class SystemCategory : LogCategory        // Tag = "[型別名][tag1][tag2]..."
{
    public SystemCategory(object value, params string[] tags);
}
```

- 訊息與標籤不跳脫 rich text, `<color>`、`<b>` 等標記會被 Console 與視窗解讀; 這是刻意保留的格式功能。
- 分類是「類別實例」, 不是 enum 或字串; 使用者自訂分類的慣例是繼承後提供靜態單例 (見 `docs/log-system.md`)。
- 目前專案內唯一使用處: `CrossObject` 建立 `new SystemCategory(this, "DOL", "Text")`, 標籤色 `#379761`。

### 4.5 `LogHistorySink`

```csharp
public sealed class LogHistorySink : ILogSink
{
    public LogHistorySink(int capacity);                         // capacity < 1 丟 ArgumentOutOfRangeException
    public int Version { get; }                                  // 每次 Write 或 Clear 遞增
    public void Write(in LogEntry entry);                        // 滿了就覆蓋最舊
    public void Clear();                                         // 清空並 Version++
    public int CopyTo(LogEntry[] destination);                   // 最新 n 筆, 舊→新
    public int CopyTo(LogEntry[] destination, out int copiedVersion); // 同一次鎖定內取版本
    public int CopyTo(LogEntry[] destination, long[] sequenceDestination, out int copiedVersion); // 另取流水號
}
```

- 流水號 (`long`) 由所有 `LogHistorySink` 共用 (`static` + `Interlocked.Increment`), 用於合併多份紀錄的先後順序。不是時間戳記, 也不對外顯示。

- 內部: `LogEntry[] buffer`、`head` (下一筆位置)、`count`、`version`, 全部以 `lock` 保護, 可跨執行緒寫入。
- 讀取端靠 `Version` 差值判斷新增幾筆 (`DebugWindow` 用 `unchecked` 處理溢位)。

### 4.6 `Log` (靜態入口)

```csharp
public static class Log
{
    public static bool isPrintLog = false;   // 總開關
    public static bool isDebugLog = false;   // 只管 LogLevel.Debug
    public static bool HasSink { get; }
    public static bool IsEnabled(LogLevel level);  // isPrintLog && 有 sink && (非 Debug 或 isDebugLog)
    public static void AddSink(ILogSink sink);     // 重複或 null 不做事; 配置新陣列
    public static void RemoveSink(ILogSink sink);  // 有移除時配置新陣列
    public static void ClearSinks();
    public static void Information(string message, LogCategory category = default);
    public static void Warning(string message, LogCategory category = default);
    public static void Error(string message, LogCategory category = default);
    public static void Debug(string message, LogCategory category = default);

    // 每個等級各 6 個多載 (InformationFormat / WarningFormat / ErrorFormat / DebugFormat):
    public static void DebugFormat<T1>(string format, T1 arg1);
    public static void DebugFormat<T1, T2>(string format, T1 arg1, T2 arg2);
    public static void DebugFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3);
    public static void DebugFormat<T1>(LogCategory category, string format, T1 arg1);
    // ... 帶 category 的 2、3 參數版本同理
}
```

- `XxxFormat` 關閉時零配置 (泛型參數不裝箱、不呼叫 `ToString`); 開啟時每次呼叫配置。
- 刻意不用 `Debug(string, T)` 同名多載: 傳入分類子類別時泛型多載會勝出, 分類會被當成格式參數。
- 字串插值 `$"..."` 與 `+` 串接仍會在呼叫前配置; 要嘛改用 `XxxFormat`, 要嘛先檢查 `IsEnabled`。

---

## 5. Runtime 元件

### 5.1 `DebugLogs` (Console sink + 安裝)

| 成員 | 預設 | 說明 |
|---|---|---|
| `static bool isGUILog` | `false` | 開: 顯示視窗並停止 Console 輸出; 關: 隱藏視窗並恢復 Console |
| `static bool isConsoleWithGUI` | `false` | `isGUILog` 開啟時仍輸出到 Console |
| `static bool isUnityLog` | `true` | 收集 Unity 引擎訊息到 `DebugWindow.UnityHistory`; 不受 `isPrintLog` 影響; 設定時訂閱 / 取消訂閱 `Application.logMessageReceivedThreaded` |
| `Write(in LogEntry)` | — | 組 `tag + message` (標籤有色時包 `<color>`), 依等級呼叫 `Debug.Log` / `LogWarning` / `LogError`。`Debug` 等級走 `Debug.Log`。輸出期間設 `[ThreadStatic]` 旗標, 讓 Unity 訊息收集略過自己 |

Unity 訊息的等級對應: `Warning` → Warning; `Error` / `Assert` / `Exception` → Error; 其他 → Normal。分類為 `UnityLogCategory.Instance` (public, 標籤 `[Unity]`)。只存 `condition`, 不存 stack trace。`Application.quitting` 時取消訂閱。

安裝 (`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`): 重設 `isGUILog`、`Log.ClearSinks()`、加入 `new DebugLogs()` 與 `DebugWindow.ResetStatics()` 回傳的歷史紀錄。進入 Play 都會重設 (含關閉 Domain Reload)。**注意**: 會清掉使用者在更早階段加入的自訂 sink, 自訂 sink 必須在 `BeforeSceneLoad` 之後加入。

### 5.2 `DebugWindow` (畫面視窗, `MonoBehaviour`)

- 兩份歷史紀錄: `History` (Log) 與 `UnityHistory` (Unity), 各 200 筆。`ShowLogMessages` / `ShowUnityMessages` 依來源篩選 (預設都開)。
- 常數: 容量 `200` (每份, 不可設定)、`WindowId 0x4C4F47`、邊距 `10`、標題列高 `18`、參考高度 `720`。
- 靜態設定 (視窗建立前就能設定, 進入 Play 時重設): `History`、`Size` (預設 600×180, 最小 80×60)、`Position` / `ResetPosition()`、`IsDraggable`、`AutoScroll`、`Scale` (0 = `max(1, Screen.height / 720)`)、`SearchText`、`IsLevelVisible` / `SetLevelVisible`、`GetLevelColor` / `SetLevelColor` (預設 Warning `#FFD54F`、Error `#FF6B6B`、Debug `#9E9E9E`)。
- 內部快取: 每份紀錄一個 `Source` (`Snapshot`、`Sequences`、`Lines` 各 200), `visible[400]` (篩選後索引, Unity 的索引加 200 區分)。以 `Version` / `filterVersion` / `styleVersion` 三個版本號決定是否重組字串或重算篩選; 只在 `EventType.Layout` 更新資料。
- 介面: 上方一列 N/W/E/D 開關 + Log/Unity 開關 + `Clear` + `X`; 搜尋框; 捲動清單。
  - `Clear` 只清 `History`, 不清 `UnityHistory`。兩段式: 第一次點擊變黃 (進入清除狀態), 再點一次才呼叫 `History.Clear()`; 按住時紅色; 在視窗其他位置按下滑鼠取消。
  - `X` 等同 `DebugLogs.isGUILog = false`。
- 生命週期: `AfterSceneLoad` 之前不建立 GameObject; 首次開啟時 `AddComponent` 到 `CrossObject.Instance`; 隱藏時 `enabled = false`。`CrossObject` 被銷毀後再開啟會重建。
- 開啟時與拖曳結束時 `FitToScreen()`: 尺寸超過畫面改為該方向的一半; 自訂位置超出畫面就拉回。

### 5.3 `CrossObject`

`DontDestroyOnLoad` 單例, 存取 `Instance` 時找不到就建立。目前只用來承載 `DebugWindow`, 並在 `PrintInfo()` 示範使用 `SystemCategory`。

---

## 6. 測試現況

| 測試 | 數量 | 本次結果 |
|---|---|---|
| dotnet `LogTests` + `LogHistorySinkTests` | 31 | **通過** (2026-11-02 `dotnet test Library.Tests/Library.Tests.csproj`, 整體 81/81) |
| PlayMode `DebugWindowTests` | 16 | **通過** (2026-11-02 `unity command test run --mode playmode --async_tests true`, 全部 PlayMode 23/23) |

PlayMode 涵蓋: 安裝歷史紀錄、建立 / 隱藏視窗、`isGUILog` 抑制 Console、`isConsoleWithGUI`、Debug 過濾、設定讀寫、右下角預設位置、過大視窗縮為半個畫面、超出畫面位置拉回、自動縮放、Unity 訊息收集 (等級與分類)、不重複收自己的 Log、`isUnityLog` 關閉、不受 `isPrintLog` 影響、清 Log 不影響 Unity、來源篩選設定。

沒有自動化測試: 視窗實際畫面、拖曳手感、搜尋操作、Clear 兩段式按鈕、Log / Unity 合併順序與開關 (IMGUI 互動), 需在 Play 中人工確認。背景執行緒產生的 Unity 訊息未測試。

---

## 7. 已修正的問題 (2026-11-02)

| # | 位置 | 原本的問題 | 處理 |
|---|---|---|---|
| 1 | `DebugWindow.Size` 的 XML 註解 | 寫「預設 200 x 120」, 實際為 600 × 180 | 註解改為實際值 |
| 2 | `SystemCategory.UpdateTag()` | 內部呼叫 `Log.Debug`, 建立分類時產生 Log | 移除該呼叫; 建構子間接呼叫虛擬方法的時序加註解說明 |
| 3 | `SystemCategory.Instance` | 每次建構都覆蓋, 專案內無使用處 | 移除 (公開 API 刪除) |
| 4 | `Log.cs` 檔尾筆記 | 仍寫「三個靜態方法」 | 更新為四個, 並補 `XxxFormat` / `IsEnabled` |
| 5 | `CrossObject` | 使用舊名 `GroundStaticObject`; `?? null` 多餘 | 改名; 條件改用 `Log.IsEnabled(LogLevel.Normal)` (行為相同) |
| 6 | `LogHistorySink(int capacity)` | 傳 0 會在寫入時除以零 | 小於 1 丟 `ArgumentOutOfRangeException` |
| 7 | rich text | 訊息內標記會被解讀 | 決定當成功能, 寫進文件, 程式不變 |
| 8 | `docs/log-system.md` | 缺 Clear / X 按鈕、`FitToScreen`、`History.Clear` | 已補上, 並加入新 API 與 rich text 說明 |
| 9 | 呼叫端組字串 | Log 關閉時 `$"..."` 仍配置 | 新增 `IsEnabled` 與 `XxxFormat` (見 4.6) |

## 8. 目前沒有的能力 (設計時可考慮的缺口)

以下只列出現況缺少的部分, 不代表都需要做:

- **編譯期移除**: 沒有 `[Conditional]`, 正式建置仍會保留所有 Log 呼叫 (只是關閉時不輸出)。
- **Entry 資訊**: 沒有時間、影格、行號 / 呼叫端資訊、堆疊追蹤 (Unity 例外的 stack trace 也未保存)、context 物件; Console 點兩下會跳到 `DebugLogs.Write`, 不是呼叫端。(待與使用者討論要加入哪些參數)
- **等級**: 沒有 Fatal / Exception, 沒有「最低等級」篩選 (enum 順序也不支援)。
- **分類篩選**: 視窗只能依等級、來源 (Log / Unity) 與文字搜尋, 不能依自訂分類開關; `Log` 也沒有依分類過濾。
- **sink 錯誤隔離 / 非同步**: sink 例外會往上拋; 沒有背景執行緒或佇列。
- **持久化**: 沒有寫檔 sink、沒有匯出或複製功能 (行動平台寫檔涉及路徑與權限, 屬平台風險)。
- **視窗開關**: 沒有快捷鍵或手勢, 需專案自行呼叫 `DebugLogs.isGUILog`。
- **容量**: 200 筆為常數, 無法設定。
- **相同訊息合併 (collapse)**: 無。
- **Editor 工具**: 無 (例如 Inspector 或 Project Settings 設定面板)。
- **設定方式**: 全部是靜態欄位, 沒有 ScriptableObject 或 Inspector 設定入口。

---

## 9. 給設計者的提醒

- 新的資料處理 (篩選、格式化、緩衝) 優先放 Core 並以 dotnet 測試; 只有繪製、Unity Console、場景物件放 Runtime。
- 每次呼叫 `Log.Xxx` 是熱路徑候選: 新增欄位到 `LogEntry` (例如時間) 時, 留意取值成本與 Core 不能用 `UnityEngine.Time`。
- 變更 `LogLevel` 數值或順序會影響 `DebugWindow` 的陣列索引與按鈕文字。
- 變更 `DebugLogs.Install` 的清空行為會影響「關閉 Domain Reload 時不殘留 sink」的保證。
- 行動平台 (Android / iOS) 的視窗縮放與觸控操作尚未驗證, 設計若依賴這些平台, 需安排實機驗證。
