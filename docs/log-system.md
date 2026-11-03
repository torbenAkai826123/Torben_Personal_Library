# Log 系統說明

本文說明 `Torben.Log` 與畫面 Log 視窗 (`DebugWindow`) 的架構、開關、API 與已知限制。快速上手見 [Log 快速使用說明](manual/log-quickstart.md)。

## 架構

```text
Log.Information / Warning / Error / Debug   (Core, 純 C#)
        │  isPrintLog、isDebugLog 過濾
        ▼
   sink 清單 (copy-on-write 陣列)
        ├─ DebugLogs         → Unity Console        (Runtime)
        └─ LogHistorySink    → Log 歷史紀錄 200 筆  (Core) ── DebugWindow.History
                                   ▲
                              DebugWindow 依流水號合併兩份紀錄並繪製 (Runtime, IMGUI)
                                   ▼
Unity 引擎訊息 ─ Application.logMessageReceivedThreaded (isUnityLog 開啟時)
        └─ DebugLogs.OnUnityLog → Unity 歷史紀錄 200 筆 ── DebugWindow.UnityHistory
                                  (排除 DebugLogs 自己輸出到 Console 的訊息)
```

| 檔案 | 組件 | 責任 |
|---|---|---|
| `Scripts/Core/Logs/Log.cs` | Torben.Core | 對外入口; 總開關與 Debug 開關; 管理 sink 清單 |
| `Scripts/Core/Logs/LogModels.cs` | Torben.Core | `LogLevel`、`ILogSink`、`LogEntry`、`LogArgsEnd` |
| `Scripts/Core/Logs/LogStackTrace.cs` | Torben.Core | 堆疊追蹤 (擷取時只存框架, 讀取時才轉文字) |
| `Scripts/Core/Logs/LogText.cs` | Torben.Core | 顯示用文字轉換: 時間、短路徑、解析 Unity 堆疊位置 |
| `Scripts/Core/Logs/LogCategory.cs` | Torben.Core | 分類標籤 (`Tag`) 與標籤顏色 (`tagColorHex`) |
| `Scripts/Core/Logs/LogHistorySink.cs` | Torben.Core | 固定容量環狀緩衝區, 保留最新的 N 筆 |
| `Scripts/Runtime/Logs/DebugLogs.cs` | Torben.Runtime | Console 輸出; 啟動時安裝 sink; `isGUILog` 開關; 收集 Unity 引擎訊息 |
| `Scripts/Runtime/Logs/UnityLogCategory.cs` | Torben.Runtime | Unity 引擎訊息的分類 (`[Unity]`) |
| `Scripts/Runtime/Logs/DebugWindow.cs` | Torben.Runtime | 畫面視窗 (篩選、搜尋、上色、拖曳、縮放) |
| `Scripts/Runtime/StaticObject/CrossObject.cs` | Torben.Runtime | 跨場景物件; DebugWindow 掛在它上面 |

路徑皆相對於 `Assets/[TorbenJuniorUtility]/`。

## 開關

| 開關 | 預設 | 作用 |
|---|---|---|
| `Log.isPrintLog` | `false` | 總開關。關閉時不產生任何訊息, 所有 sink 都收不到 |
| `Log.isDebugLog` | `false` | 只管 `LogLevel.Debug`。關閉時 Debug 訊息不會送到任何 sink |
| `DebugLogs.isGUILog` | `false` | 開啟: 顯示畫面視窗, 並停止輸出到 Console。關閉: 隱藏視窗, 恢復 Console |
| `DebugLogs.isConsoleWithGUI` | `false` | `isGUILog` 開啟時仍同時輸出到 Console |
| `DebugLogs.isUnityLog` | `true` | 收集 Unity 引擎訊息到 `DebugWindow.UnityHistory`。**不受 `isPrintLog` 影響**; 關閉時取消訂閱, 期間的訊息不補記 |

歷史紀錄不受 `isGUILog` 影響: 只要訊息通過前兩個開關就會被記錄, 所以視窗晚點開啟也看得到之前的訊息。

### Unity 引擎訊息

- 指沒有經過 `Torben.Log`、直接寫進 Console 的訊息: 未捕捉的例外、引擎警告、第三方或舊程式直接呼叫的 `UnityEngine.Debug.Log`。
- 以 `Application.logMessageReceivedThreaded` 收集, 可能在任何執行緒觸發。等級對應: `Warning` → Warning; `Error`、`Assert`、`Exception` → Error; 其他 → Normal。分類固定為 `UnityLogCategory.Instance` (標籤 `[Unity]`, 可改 `tagColorHex`)。
- `DebugLogs` 自己輸出到 Console 時以執行緒旗標排除, 所以 `Torben.Log` 的訊息不會重複出現成 `[Unity]`。
- 只存訊息本身, 不存堆疊追蹤 (stack trace)。
- 正式版不想收集時, 在 `BeforeSceneLoad` 之後設 `DebugLogs.isUnityLog = false`; 在這之前產生的引擎訊息已被收集。
- 離開 Play (Editor) 或程式結束時 (`Application.quitting`) 取消訂閱。

## 載入順序

| 階段 | 發生的事 |
|---|---|
| `SubsystemRegistration` | `DebugLogs.Install`: 清空 sink, 加入 Console 與歷史紀錄兩個 sink; 重設 `isGUILog` 與 DebugWindow 的所有設定; `isUnityLog` 恢復開啟並開始收集 Unity 訊息 |
| `BeforeSceneLoad` 之後 | 專案可以修改開關與 DebugWindow 設定 |
| `AfterSceneLoad` | 若 `isGUILog` 已開啟, 此時才建立視窗元件 (掛在 `CrossObject` 上) |
| 之後 | 第一次開啟 `isGUILog` 時建立視窗元件; 之後切換只改 `enabled` |

- 在 `SubsystemRegistration` 之前或同階段做的設定會被重設; 請在 `BeforeSceneLoad` 或之後設定。
- `isPrintLog` 開啟之前呼叫的 Log 不會產生, 也不會補記。要看到載入初期的訊息, 需在更早的階段開啟 `isPrintLog`。
- 每次進入 Play 都會重設 (包含關閉 Domain Reload 時), 不會殘留上一次的 sink 或設定。

## Log API (`Torben.Log`)

| 成員 | 說明 | 配置 |
|---|---|---|
| `Information / Warning / Error(message, category = null)` | 產生訊息並送給每個 sink; `category` 為 null 時使用空分類 | 本身零配置 (開啟堆疊追蹤的等級每次配置); 各 sink 自行決定 |
| `Debug(message, category = null)` | 同上, 等級為 Debug; 帶 `[Conditional]`, 未定義保留條件時整行呼叫被編譯期移除 (見下方「Log.Debug 的編譯期移除」) | 同上; 被移除時零配置 |
| `Format(level, [category,] format, arg1[, arg2[, arg3]])` | 開啟時才以 `string.Format` 組字串; 分類放在等級後面。Debug 等級請用 `Log.Debug` | 關閉時零配置; 開啟時每次呼叫配置 (字串、值型別裝箱) |
| `IsStackTraceEnabled(level)` / `SetStackTraceEnabled(level, bool)` | 該等級是否擷取堆疊追蹤; 預設只有 Error。隨時可改, 只影響之後的訊息 | 零配置 |
| `IsEnabled(level)` | 該等級的訊息目前是否會送到 sink (`isPrintLog`、至少一個 sink、Debug 另需 `isDebugLog`) | 零配置 |
| `AddSink(ILogSink)` | 加入 sink; 已存在或 null 時不做事 | 每次呼叫配置新陣列 |
| `RemoveSink(ILogSink)` | 移除 sink; 不存在時不做事 | 有移除時配置新陣列 |
| `ClearSinks()` | 移除全部 sink | 零配置 |
| `HasSink` | 是否至少有一個 sink | 零配置 |

sink 清單採 copy-on-write: 新增或移除時建立新陣列再整個替換, 寫入時只讀取當下的陣列, 不需加鎖。新增或移除不應在每幀執行。

### 避免關閉時的配置

開關檢查在 `Log` 內部, 參數在呼叫前就已建立。`Log.Information($"hp={hp}")` 或 `"hp=" + hp` 即使 Log 關閉也會組字串。`Information` / `Warning` / `Error` 需要組字串時改用下列其中一種:

```csharp
Log.Format(LogLevel.Normal, "hp={0}", hp);              // 開啟時才格式化
Log.Format(LogLevel.Warning, NetworkCategory.Instance, "retry {0}/{1}", n, max);

if (Log.IsEnabled(LogLevel.Normal))                     // 需要較複雜的組字串時
    Log.Information(BuildReport());

Log.Debug($"hp={hp}");                                  // Debug 等級: 正式版整行被移除, 可以用 $"..."
```

- 參數以泛型傳入, 關閉時不裝箱; 最多 3 個參數, 更多時用 `IsEnabled`。
- 不提供 `Information(string, T)` 這類同名多載: 傳入分類子類別時, 泛型多載會比 `LogCategory` 參數更優先, 分類會被當成格式參數。
- `Format(LogLevel.Debug, ...)` 可以使用, 關閉時一樣零配置, 但不會被 `[Conditional]` 移除 (`[Conditional]` 無法依參數值判斷), 呼叫會留在建置中。Debug 等級請用 `Log.Debug`。
- `Warning` / `Error` 通常在異常分支, 正常運作時不會執行, 一般版的配置可以接受; 每幀會執行的路徑再改用 `Log.Format`。
- `format` 格式錯誤時 `string.Format` 丟出的 `FormatException` 會傳回呼叫端。
- 字串常值 (例如 `Log.Information("ready")`) 不配置, 照原本寫法即可。

### Log.Debug 的編譯期移除

`Log.Debug` 帶有三個 `[System.Diagnostics.Conditional]`: `UNITY_EDITOR`、`DEVELOPMENT_BUILD`、`TORBEN_LOG_DEBUG`。

- 多個 `[Conditional]` 為 OR: 呼叫端編譯時任一 symbol 有定義就保留呼叫, 否則整行 (包含參數運算) 被移除。保留後仍由 `isPrintLog` / `isDebugLog` 決定是否輸出。
- 去留由**呼叫端的編譯**決定, 與 `Log` 所在組件無關。要在實機保留 Debug Log: Build Settings 勾選 Development Build, 或在 Player Settings 的 Scripting Define Symbols 加入 `TORBEN_LOG_DEBUG`。
- 參數中的副作用 (例如 `Log.Debug($"{i++}")`) 會跟著被移除, 參數不得有副作用。
- 函式庫 (Core / Runtime) 內部不得呼叫 `Log.Debug`: 打包成 DLL 時就決定了去留, 使用端無法再控制。
- dotnet 測試專案沒有 `UNITY_EDITOR`, 在 `Library.Tests.csproj` 的 `DefineConstants` 加入 `TORBEN_LOG_DEBUG` 才會保留。

### 呼叫端位置與堆疊追蹤

- 每個公開的寫入方法 (含 `Format`) 最後都有 `LogArgsEnd argsEnd`、`[CallerMemberName]`、`[CallerFilePath]`、`[CallerLineNumber]` 四個選填參數, 由編譯器在呼叫處填入, 不要自己傳。值是編譯期常數, 執行時不配置。
- 呼叫端參數必須宣告在每個公開方法上 (編譯器只填入「被呼叫的方法」的參數), 無法集中到一個方法。
- `LogArgsEnd` 是分隔用的空結構: 沒有它時, `Format(level, "{0}{1}{2}", a, b, "c")` 會被編譯器選成 2 參數版本, 把 `"c"` 當成 `callerMemberName`; 有它之後多傳的參數會變成型別不符, 選到正確的多載或編譯失敗。
- `[CallerFilePath]` 是編譯時的完整路徑, 建置版的執行檔裡會留有建置機器上的路徑字串。
- 堆疊追蹤用 `System.Diagnostics.StackTrace` 擷取, 只存框架; 第一次讀取 `LogStackTrace.Text` 時才轉成文字 (去掉最上方 `Torben.Log` 自己的框架) 並快取。檔名與行號是否存在依平台與建置設定而定 (Editor 有; Mono 建置版、IL2CPP 的行號**未驗證**)。
- Unity Console 點兩下仍會跳到 `DebugLogs.Write`。`[HideInCallstack]` 無法解決 (2026-11-02 查 6000.5.5f1 的 `UnityEditor.CoreModule.dll`): 它是 `UnityEditor.ConsoleWindow` 用 `TypeCache` 收集被標記的方法, 只在 Console 開啟「Strip logging callstack」時從詳細區的堆疊文字去掉這些框架; 屬性本身在各平台的 `UnityEngine.CoreModule` 都存在, 但只在 Editor 的 Console 有作用。`Log` 在 Core 不能引用 `UnityEngine`, 無法標記, 堆疊中仍會留下 `Torben.Log` 的框架。改用畫面視窗的位置連結開啟呼叫端。

### LogEntry

自訂 sink 只需實作 `ILogSink.Write(in LogEntry entry)`, 再用 `Log.AddSink` 加入。

| 欄位 | 說明 |
|---|---|
| `Level`、`Category`、`Message` | 等級、分類、訊息 |
| `Time` | 寫入時間 (`DateTime.UtcNow`, UTC); 以 3 個參數建構時為 `default` |
| `CallerFilePath`、`CallerMemberName`、`CallerLineNumber` | 呼叫端位置; Unity 引擎訊息沒有 (`HasCaller` 為 false) |
| `StackTrace` | `LogStackTrace`; 該等級未開啟擷取時為 null。Unity 引擎訊息存 Unity 提供的堆疊文字 (不受 `SetStackTraceEnabled` 影響, 內容依 Unity 的 Stack Trace Logging 設定) |

## LogCategory

繼承 `LogCategory` 建立自己的分類:

```csharp
public sealed class NetworkCategory : LogCategory
{
    public static readonly NetworkCategory Instance = new NetworkCategory();
    private NetworkCategory() : base("Network") { tagColorHex = "#4FC3F7"; }
}

Log.Warning("Timeout", NetworkCategory.Instance);   // Console / 視窗顯示 "[Network]Timeout"
```

- `Tag`: 預設為 `[IdentifierName]`; 覆寫 `UpdateTag()` 可自訂格式 (參考 `SystemCategory`)。
- `tagColorHex`: **只影響標籤**的顏色。訊息文字的顏色由等級決定, 見下方 `SetLevelColor`。
- `SystemCategory(object value, params string[] tags)`: 標籤為 `[型別名][tag1][tag2]...`。
- 訊息與標籤不跳脫 rich text: `<color>`、`<b>` 等標記會被 Console 與畫面視窗解讀, 可當成格式功能使用。要原樣顯示 `<` 開頭的文字時需自行處理。

## DebugWindow API

所有設定都是靜態的, 視窗建立前就可以設定; 每次進入 Play 時恢復預設值。

| 成員 | 預設 | 說明 |
|---|---|---|
| `History` | — | Log 訊息的歷史紀錄 (`LogHistorySink`, 最多 200 筆); Clear 按鈕只清這份 |
| `UnityHistory` | — | Unity 引擎訊息的歷史紀錄 (最多 200 筆); Clear 按鈕不清 |
| `ShowLogMessages` / `ShowUnityMessages` | `true` | 依來源篩選; 視窗上方 Log / Unity 開關同步此設定。兩個都開即顯示全部 |
| `Size` | 600 × 180 | 視窗大小 (縮放前單位); 最小 80 × 60 |
| `Position` | 右下角 | 視窗左上角位置; 設定後不再自動貼齊右下角, 拖曳也會更新 |
| `ResetPosition()` | — | 恢復右下角 |
| `IsDraggable` | `true` | 是否可拖曳標題列移動視窗 |
| `AutoScroll` | `true` | 有新訊息時捲到最下方; 由關轉開時立即捲到最下方; 視窗的 Auto 開關同步此設定 |
| `Scale` | `0` | 介面縮放倍率; `0` 表示依 `Screen.height / 720` 自動計算 (最小 1 倍), 視窗佔畫面的比例固定 |
| `SearchText` | `""` | 搜尋文字; 比對標籤與訊息, 不分大小寫 |
| `IsLevelVisible(level)` / `SetLevelVisible(level, bool)` | 全部顯示 | 依等級篩選; 視窗上方 N / W / E / D 開關同步此設定 |
| `History.Clear()` | — | 清空 Log 歷史紀錄; 視窗的 Clear 按鈕也是呼叫它 |
| `ShowTime` | `false` | 在訊息下方第二行開頭顯示時間 `(HH:mm:ss.ff)` (當地時間); 視窗的 Time 開關同步此設定 |
| `ShowLocation` | `false` | 在第二行顯示發生位置 `檔案:行號`; Log 訊息為呼叫端, Unity 訊息取堆疊中第一個有原始碼位置的框架; 視窗的 Loc 開關同步此設定 |
| `Collapse` | `false` | 合併相同訊息; 視窗的 Collapse 開關同步此設定 |
| `GetLevelColor(level)` / `SetLevelColor(level, hex)` | Warning `#FFD54F`、Error `#FF6B6B`、Debug `#9E9E9E`、Normal 無 | 訊息文字顏色; null 或空字串表示預設文字色 |

### 視窗操作

- 上方一列: N / W / E / D 等級開關、Log / Unity 來源開關、`Clear`、`X`; 第二列: 搜尋框、`Time`、`Loc`、`Collapse`、`Auto` 開關; 下方為捲動清單。
- 每筆最左側為行號 `[n]`: 該筆的流水號 (`LogHistorySink` 共用, 依寫入順序遞增)。同一筆的號碼固定, 不因舊紀錄被擠掉或篩選而改變; 被篩掉或屬於另一份紀錄的號碼會留空缺。欄寬依目前最大的號碼位數決定 (最少 3 位)。
- 行號右邊為 `+` / `-` 展開按鈕, 展開後以小一級的字顯示堆疊追蹤; 沒有堆疊追蹤時按鈕呈現無法使用。
- 行號、展開按鈕、次數膠囊都以訊息第一行文字的中線垂直置中 (依字型實際行高計算)。
- 列與列之間有 1px 分隔線, 也當作檢查對齊的參考線。
- `Time` / `Loc` 開啟時, 訊息下方多一行小字 (與訊息間多留 2px): `(時間)  路徑:行號`。Editor 中點擊路徑會開啟該檔案 (專案內的檔案用 `AssetDatabase.OpenAsset`, 與 Console 相同); 建置版只顯示。
- `Collapse`: 等級、分類 (同一實例)、內容與呼叫端位置都相同的訊息合併成一列, 顯示在第一次出現的位置並沿用其流水號 (同 Unity Console), 最右側以膠囊標籤顯示次數; 合併多次的列不顯示時間。只合併仍在紀錄中的訊息 (兩份紀錄各最新 200 筆), 被擠掉的不計入次數。
- 兩份紀錄依寫入順序 (`LogHistorySink` 共用的流水號) 合併顯示; 來源、等級、搜尋三種篩選同時套用。
- `Clear` 只清 Log 訊息, Unity 訊息保留 (避免重要的引擎錯誤一起被刪)。Unity 訊息只會因超過 200 筆被擠掉或重新進入 Play 而消失。
- `Clear` 為兩段式確認: 第一次點擊進入清除狀態 (按鈕變黃), 再點一次才清除 (按住時紅色); 在視窗其他位置按下滑鼠即取消。
- `X` 等同 `DebugLogs.isGUILog = false`, 一併恢復 Console 輸出。
- 開啟視窗與拖曳結束時: 寬或高超過畫面時改為畫面該方向的一半; 自訂位置超出畫面時拉回邊緣。

### 可調整的外觀參數

都是 `DebugWindow.cs` 裡的常數或 `CreateStyles()` 中的值, 沒有公開 API; 改程式後重新編譯生效。

| 位置 | 目前值 | 作用 |
|---|---|---|
| `FoldWidth` | `18f` | 展開按鈕寬度; 第二行與堆疊追蹤的縮排也依此計算 |
| `FoldHeight` | `14f` | 展開按鈕高度; 改了仍會自動置中 |
| `BadgeHeight` | `16f` | 次數膠囊高度; 膠囊貼圖與左右圓角半徑依此產生 |
| `DetailOffset` | `2f` | 第二行 (時間、位置) 與訊息之間多留的距離 |
| `separatorColor` | 白色, 透明度 `0.12f` | 列分隔線顏色 |
| `CreateStyles()` 的 `textCenter` | `padding.top + lineHeight / 2` | 三個小元件的置中基準; 整體偏高或偏低時在這裡加減幾 px, 三者一起移動 |
| `CreateStyles()` 的 `detailSize` | 主訊息字型大小 - 2 (最小 8) | 第二行、堆疊追蹤、行號、展開按鈕與膠囊的字型大小 |
| `CreateCapsuleTexture()` 的像素顏色 | `(110, 110, 110)`, alpha `220` | 膠囊底色 |

### 繪製與效能

- 只在 IMGUI 的 `Layout` 事件更新資料, 讓 `Layout` 與 `Repaint` 的控制項數量一致, 避免 `Getting control X's position...` 例外。
- 用 `LogHistorySink.CopyTo(dest, sequences, out version)` 在同一次鎖定內取得資料、流水號與版本號; 兩份紀錄各自只為新進來的幾筆組字串, 舊字串直接搬移。改變等級顏色時才全部重組。
- 篩選與搜尋只在條件或內容改變時重算。合併相同訊息時一併重新分組 (重用同一個 `Dictionary`)。
- 時間、位置、行號與次數字串都在第一次顯示時才組, 之後快取。膠囊標籤的貼圖在建立樣式時產生一次 (16 × 16), 元件銷毀時釋放。
- 展開按鈕與 Time / Loc 開關的變更延到下一個 `Layout` 事件才套用。展開狀態以流水號記錄, 新訊息進來或篩選改變時不會錯位。
- 隱藏時設 `enabled = false`, `OnGUI` 完全不會被呼叫。
- GUILayout 本身每幀會有少量配置, 無法完全消除; 以除錯視窗而言可接受。

### 生命週期

- 視窗元件掛在 `CrossObject` 上, 跟著它跨場景, 也跟著它一起銷毀。
- `CrossObject` 被銷毀後再開啟 `isGUILog`, 會重新建立 `CrossObject` 與視窗元件; 歷史紀錄不受影響。

## 平台

- IMGUI 在所有目標平台都可用, 視窗不限 Editor 或 Development Build。
- 自動縮放依畫面高度計算, 不使用 `Screen.dpi` (實體 DPI 不隨解析度改變, 調低解析度時視窗反而佔更大比例)。不考慮實體尺寸, 小螢幕手機上的字可能偏小。**Android 與 iOS 未驗證**。
- 沒有內建快捷鍵或手勢; 開關由專案呼叫 `DebugLogs.isGUILog`。

## 測試

| 檔案 | 類型 | 內容 |
|---|---|---|
| `dotnet/Library.Tests/LogTests.cs` | dotnet | 多 sink、重複加入、移除、`HasSink`、`isDebugLog`、`isPrintLog`、`IsEnabled`、`Format` (4 個等級、有 / 無分類、關閉時不格式化參數)、`Log.Debug` 的 `[Conditional]` symbol、公開寫入方法只有 10 個、`SystemCategory` 標籤 |
| `dotnet/Library.Tests/LogEntryInfoTests.cs` | dotnet | 呼叫端位置 (含 `Format` 與 3 個字串參數的多載選擇)、UTC 時間、堆疊追蹤預設值與開關、堆疊從呼叫端開始、`LogText` 的時間 / 路徑 / Unity 位置解析 |
| `dotnet/Library.Tests/LogHistorySinkTests.cs` | dotnet | 覆蓋最舊、Version 遞增、`CopyTo` 順序與版本號、`Clear`、容量小於 1 時丟例外、流水號遞增且跨紀錄共用 |
| `Scripts/Tests/PlayMode/DebugWindowTests.cs` | PlayMode | 安裝歷史紀錄、建立 / 隱藏視窗、`isGUILog` 抑制 Console、`isConsoleWithGUI`、Debug 過濾、設定讀寫、右下角預設位置、Unity 訊息收集 / 不重複收自己的 Log / `isUnityLog` 關閉 / 不受 `isPrintLog` 影響、清 Log 不影響 Unity、來源篩選設定、顯示設定 (Time / Loc / Collapse) 讀寫、Log 紀錄帶時間與呼叫端、Unity 訊息帶時間與堆疊位置、開啟所有顯示選項時繪製不出錯 |

執行方式見 [dotnet 測試手冊](dotnet-test.md) 與 [Unity CLI 手冊](unity-cli-pipeline.md)。視窗的實際畫面 (版面、拖曳、搜尋、Log / Unity 合併順序與開關、展開按鈕、第二行字體大小、點擊路徑開檔、合併次數) 需在 Play 時人工確認。
