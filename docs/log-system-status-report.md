# Log 系統現況報告 (給設計用 AI)

- 對象: 負責後續設計 Log 系統的 AI。假設讀者看不到原始碼, 本文已附上必要的型別定義。
- 基準: 提交 `5207f04` (2026-11-03), 分支 `main`。含 2026-11-02 的問題修正 (第 7 節)、Unity 引擎訊息收集, 2026-11-03 的時間 / 呼叫端位置 / 堆疊追蹤與視窗顯示 (第 7.1 節), 以及同日的寫入方法精簡 (第 7.2 節) 與設定檔 / Editor 工具 (第 7.3 節, 提交 `23a6a4a`)。
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
| 關閉時零配置的格式化 API | 完成 | Core `Log.IsEnabled`、`Log.Format` |
| Debug 等級編譯期移除 | 完成 | Core `Log.Debug` 的 `[Conditional]` |
| 總開關、Debug 等級開關 | 完成 | Core `Log.isPrintLog` / `isDebugLog` |
| 多 sink (copy-on-write) | 完成 | Core `Log.AddSink` 等 |
| 分類標籤 + 標籤顏色 | 完成 | Core `LogCategory` |
| 歷史紀錄 (環狀緩衝區, 含 Clear) | 完成 | Core `LogHistorySink` |
| 時間戳記 (UTC) | 完成 | Core `LogEntry.Time` |
| 呼叫端位置 (檔案、方法、行號) | 完成 | Core `LogEntry.Caller*`, `Log` 各方法的 Caller 參數 |
| 堆疊追蹤 (依等級開關, 預設 Error) | 完成 | Core `LogStackTrace`、`Log.SetStackTraceEnabled` |
| Unity Console 輸出 | 完成 | Runtime `DebugLogs` |
| 啟動時自動安裝 sink | 完成 | Runtime `DebugLogs.Install` |
| 設定檔 (預設檔 + 遊戲專案覆蓋檔, 啟動時套用一次) | 完成 | Core `LogOptions`; Runtime `LogSettings`、`DebugLogsOptions`、`DebugWindowOptions` |
| 歷史紀錄容量可設定 (Log / Unity 各自) | 完成 (只在啟動時生效) | `DebugWindowOptions.logCapacity` / `unityCapacity` |
| 畫面視窗 (IMGUI) | 完成 | Runtime `DebugWindow` |
| ├ 等級篩選 (N/W/E/D)、搜尋 | 完成 | |
| ├ 行號、展開按鈕 (堆疊追蹤)、列分隔線 | 完成 | |
| ├ 第二行時間 / 位置 (Time / Loc 開關), Editor 點擊位置開檔 | 完成 | |
| ├ 合併相同訊息 (Collapse, 膠囊次數標籤) | 完成 | |
| ├ 等級文字顏色 | 完成 | |
| ├ 拖曳、超出畫面時拉回、尺寸過大時縮為半個畫面 | 完成 | |
| ├ 依畫面高度自動縮放 | 完成 (行動平台未驗證) | |
| ├ Clear (兩段式確認)、Close 按鈕 | 完成 | |
| ├ 自動捲到最下方 | 完成 | |
| └ 隱藏個別按鈕 | 完成 | `DebugWindow.VisibleButtons` |
| Unity 引擎訊息收集 (獨立開關 `isUnityLog`, 預設開) | 完成 | Runtime `DebugLogs`、`UnityLogCategory` |
| ├ 視窗 Log / Unity 來源篩選 | 完成 | `DebugWindow.ShowLogMessages` / `ShowUnityMessages` |
| └ Clear 只清 Log, 保留 Unity 訊息 | 完成 | |
| 跨場景宿主 | 完成 | Runtime `CrossObject` |
| Console 點兩下跳到呼叫端 | **未解決** (`HideInCallstack` 不適用, 見 7.1) | — |
| Editor 工具 (Project Settings 頁面、`TorbenTools/` 選單、狀態檢查) | 完成 (畫面與操作待人工確認) | Editor `Scripts/Editor/Logs/` |
| Android / iOS 實機 | **未驗證** | — |

---

## 3. 架構與資料流

```text
呼叫端: Log.Information / Warning / Error / Debug(message, category)
        或 Log.Format(level, [category,] format, arg1..arg3)  ← 先檢查 IsEnabled, 開啟才 string.Format
        (Log.Debug 在呼叫端未定義 UNITY_EDITOR / DEVELOPMENT_BUILD / TORBEN_LOG_DEBUG 時整行被編譯期移除)
        │
        │  (1)(2) IsEnabled(level) 為 false → 直接 return
        │         (isPrintLog 關閉、沒有 sink, 或 Debug 等級但 isDebugLog 關閉)
        │  (3) category == null → 改用 LogCategoryEmpty.None
        │  (4) 該等級開啟堆疊追蹤 → LogStackTrace.Capture() (配置; 只存框架)
        │  (5) 建立 LogEntry (readonly struct): 時間 DateTime.UtcNow + 呼叫端位置 (編譯期常數)
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
Unity 引擎訊息 → Application.logMessageReceivedThreaded → DebugLogs.OnUnityLog (存時間 + Unity 的堆疊文字)
                (isUnityLog 開啟時; 不受 isPrintLog 影響; 排除 DebugLogs 自己輸出到 Console 的訊息)
```

組件 (asmdef):

| 組件 | 引用 | 含有的 Log 檔案 |
|---|---|---|
| `Torben.Core` (`noEngineReferences: true`) | 無 | `Log.cs`、`LogModels.cs`、`LogCategory.cs`、`LogHistorySink.cs`、`LogStackTrace.cs`、`LogText.cs` |
| `Torben.Runtime` | `Torben.Core` | `DebugLogs.cs`、`DebugWindow.cs`、`UnityLogCategory.cs`、`StaticObject/CrossObject.cs` |
| `Library.Tests.PlayMode` | Core、Runtime | `DebugWindowTests.cs` |
| dotnet `Library.Tests` | 專案 `dotnet/Torben.Core` (編譯 `Scripts/Core/**/*.cs`; 測試組件看不到 `internal`) | `LogTests.cs`、`LogHistorySinkTests.cs`、`LogEntryInfoTests.cs` |

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
    public readonly System.DateTime Time;   // UTC; 3 參數建構子為 default
    public readonly string CallerFilePath;  // 編譯期完整路徑; Unity 訊息為 null
    public readonly string CallerMemberName;
    public readonly int CallerLineNumber;
    public readonly LogStackTrace StackTrace; // 未擷取時 null
    public bool HasCaller { get; }          // CallerFilePath 非空

    public LogEntry(LogLevel level, LogCategory category, string message);   // 其餘欄位為預設值
    public LogEntry(LogLevel level, LogCategory category, string message, System.DateTime time,
        string callerFilePath, string callerMemberName, int callerLineNumber, LogStackTrace stackTrace);
}

public readonly struct LogArgsEnd { }   // 見 4.6, 呼叫端不要傳
```

- 約 64 bytes (原本約 24)。兩份 200 筆的紀錄合計約 25 KB。
- **沒有** 影格編號、執行緒 ID、`UnityEngine.Object` context。

### 4.3.1 `LogStackTrace`

```csharp
public sealed class LogStackTrace
{
    public static LogStackTrace Capture();             // new System.Diagnostics.StackTrace(1, true); 每次配置
    public static LogStackTrace FromText(string text); // null / 空白回傳 null; 用於 Unity 提供的堆疊文字
    public string Text { get; }                        // 第一次讀取才轉文字並快取; 去掉最上方 "Torben.Log." 的框架後逐行清理
}
```

- 擷取與轉文字分開: `Log` 呼叫時只存框架, 視窗展開時才轉文字。
- 檔名與行號依平台: Editor 有; IL2CPP 堆疊行號已驗證, 需開啟 File Name and Line Number (Player Settings 的 IL2CPP Stacktrace Information); Mono 建置版**未驗證**。
- 轉文字時逐行清理 (`CleanFrames`, `FromText` 也套用): 去掉 IL 偏移 ` [0x...]` 並把路徑縮短成 `LogText.ToDisplayPath(路徑, null)`; 角括號內是 MVID 時視為沒有原始碼位置, 去掉 ` <0x... + 0x...> 數字 in <MVID>:數字` 與 ` [0x...] in <MVID>:數字` 尾段; Mono 類別庫每行行號後的空白會先略過; 其他格式原樣保留。規則見 `docs/log-system.md`「堆疊文字清理」。

### 4.3.2 `LogText` (顯示用文字轉換, 結果應由呼叫端快取)

```csharp
public static class LogText
{
    public static string FormatTime(System.DateTime utcTime);              // 當地時間 "HH:mm:ss.ff"; default → ""
    public static string ToDisplayPath(string path, string projectRoot);   // 去掉 projectRoot; 否則從 "Assets/" 或 "Packages/" 開始; 都沒有只留檔名
    public static bool TryParseUnityLocation(string stackTrace, out string path, out int line); // 第一個 "(at 路徑:行號)", 略過 "(at <...>:0)"
}
```

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
    public static bool IsStackTraceEnabled(LogLevel level);              // 預設只有 Error
    public static void SetStackTraceEnabled(LogLevel level, bool isEnabled); // 隨時可改, 只影響之後的訊息

    // 以下每個公開寫入方法的最後都有 4 個選填參數 (下文以 "..." 表示):
    //   LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "",
    //   [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0
    public static void Information(string message, LogCategory category = default, ...);
    public static void Warning(string message, LogCategory category = default, ...);
    public static void Error(string message, LogCategory category = default, ...);
    [Conditional("UNITY_EDITOR")] [Conditional("DEVELOPMENT_BUILD")] [Conditional("TORBEN_LOG_DEBUG")]
    public static void Debug(string message, LogCategory category = default, ...);

    // 6 個多載: 有 / 無 category 各 1~3 個泛型參數
    public static void Format<T1>(LogLevel level, string format, T1 arg1, ...);
    public static void Format<T1, T2>(LogLevel level, string format, T1 arg1, T2 arg2, ...);
    public static void Format<T1, T2, T3>(LogLevel level, string format, T1 arg1, T2 arg2, T3 arg3, ...);
    public static void Format<T1>(LogLevel level, LogCategory category, string format, T1 arg1, ...);
    // ... 帶 category 的 2、3 參數版本同理
}
```

- 公開寫入方法共 10 個: `Information` / `Warning` / `Error` / `Debug` 各 1 個 + `Format` 6 個 (dotnet 測試以反射鎖定數量)。
- `Format` 關閉時零配置 (泛型參數不裝箱、不呼叫 `ToString`); 開啟時每次呼叫配置。`Format(LogLevel.Debug, ...)` 不會被 `[Conditional]` 移除, Debug 等級請用 `Log.Debug`。
- `Log.Debug` 的去留由呼叫端的編譯決定: 遊戲程式勾選 Development Build 或定義 `TORBEN_LOG_DEBUG` 可保留; 函式庫內部不得呼叫 (打包 DLL 時就決定了)。參數中的副作用會跟著被移除。
- 刻意不用 `Information(string, T)` 同名多載: 傳入分類子類別時泛型多載會勝出, 分類會被當成格式參數。
- 字串插值 `$"..."` 與 `+` 串接仍會在呼叫前配置; 要嘛改用 `Format`, 要嘛先檢查 `IsEnabled`; `Log.Debug` 在正式版整行移除, 可直接用 `$"..."`。
- Caller 參數由編譯器在呼叫處填入, 是編譯期常數, 不配置。編譯器只填「被呼叫的方法」的參數, 所以 10 個公開寫入方法都要各自宣告, 無法集中。
- `LogArgsEnd` 的用途: 沒有它時 `Format(level, "{0}{1}{2}", a, b, "c")` 會被多載解析選成 2 參數版本 (選填參數都要補預設值時, 比較型別特定度, `string` 勝過 `T3`), `"c"` 變成 `callerMemberName` 且不報錯。有測試覆蓋。
- 寫入時的成本: 開啟時每筆多一次 `DateTime.UtcNow`; 開啟堆疊追蹤的等級每筆多一次 `StackTrace` 擷取 (配置)。
- `[CallerFilePath]` 會把建置機器的完整路徑字串留在執行檔裡。

---

## 5. Runtime 元件

### 5.1 `DebugLogs` (Console sink + 安裝)

| 成員 | 預設 | 說明 |
|---|---|---|
| `static bool isGUILog` | `false` | 開: 顯示視窗並停止 Console 輸出; 關: 隱藏視窗並恢復 Console |
| `static bool isConsoleWithGUI` | `false` | `isGUILog` 開啟時仍輸出到 Console |
| `static bool isUnityLog` | `true` | 收集 Unity 引擎訊息到 `DebugWindow.UnityHistory`; 不受 `isPrintLog` 影響; 設定時訂閱 / 取消訂閱 `Application.logMessageReceivedThreaded` |
| `Write(in LogEntry)` | — | 組 `tag + message` (標籤有色時包 `<color>`), 依等級呼叫 `Debug.Log` / `LogWarning` / `LogError`。`Debug` 等級走 `Debug.Log`。輸出期間設 `[ThreadStatic]` 旗標, 讓 Unity 訊息收集略過自己 |

Unity 訊息的等級對應: `Warning` → Warning; `Error` / `Assert` / `Exception` → Error; 其他 → Normal。分類為 `UnityLogCategory.Instance` (public, 標籤 `[Unity]`)。存 `condition`、`DateTime.UtcNow` 與 `LogStackTrace.FromText(stackTrace)`; 堆疊不受 `Log.SetStackTraceEnabled` 影響 (字串本來就由 Unity 配置), 內容依 Unity 的 Stack Trace Logging 設定。沒有呼叫端位置。

Console 輸出 (`Write`) 未改變: 只輸出 `tag + message`, Console 點兩下仍跳到 `DebugLogs.Write`。`Application.quitting` 時取消訂閱。

安裝 (`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`): `Resources.Load<LogSettings>(LogSettings.ResourcesPath)` 讀取預設檔 (有 `projectOverride` 時改用覆蓋檔, 找不到時用 `CreateInstance` 的欄位初始值並以 `UnityEngine.Debug.LogWarning` 警告一次), 重設 `isGUILog`、`Log.ClearSinks()`、加入 `new DebugLogs()` 與 `DebugWindow.ResetStatics(logCapacity, unityCapacity)` 回傳的歷史紀錄, 再依序套用 `log`、`window`、`output`。此階段讀得到 Resources (2026-11-03 Editor 實測), 安裝維持在 `SubsystemRegistration`。測試用的 `internal Install(LogSettings)` 以 `InternalsVisibleTo` 開放給 `Library.Tests.PlayMode`。進入 Play 都會重設 (含關閉 Domain Reload)。**注意**: 會清掉使用者在更早階段加入的自訂 sink, 自訂 sink 必須在 `BeforeSceneLoad` 之後加入。

### 5.2 `DebugWindow` (畫面視窗, `MonoBehaviour`)

- 兩份歷史紀錄: `History` (Log) 與 `UnityHistory` (Unity), 容量由設定檔決定 (`LogCapacity` / `UnityCapacity`, 欄位初始值各 200)。`ShowLogMessages` / `ShowUnityMessages` 依來源篩選 (預設都開)。
- 常數: `WindowId 0x4C4F47`、邊距 `10`、標題列高 `18`、參考高度 `720`。
- 靜態設定 (視窗建立前就能設定, 進入 Play 時以 `DebugWindowOptions` 欄位初始值重設後套用設定檔): `VisibleButtons`、`UsesDefaultPosition`、`History`、`Size` (預設 600×180, 最小 80×60)、`Position` / `ResetPosition()`、`IsDraggable`、`AutoScroll`、`Scale` (0 = `max(1, Screen.height / 720)`)、`SearchText`、`IsLevelVisible` / `SetLevelVisible`、`GetLevelColor` / `SetLevelColor` (預設 Warning `#FFD54F`、Error `#FF6B6B`、Debug `#9E9E9E`)、`ShowTime` / `ShowLocation` / `Collapse` (預設皆 `false`)。
- 內部快取: 每份紀錄一個 `Source` (`Snapshot`、`Sequences`、`Lines` 長度為該紀錄的容量), `visible[logCapacity + unityCapacity]` (篩選後索引, Unity 的索引加 `logCapacity` 區分)。陣列在元件建立或歷史紀錄換新時配置 (`BindHistories`), 不在每幀配置。以 `Version` / `filterVersion` / `styleVersion` 三個版本號決定是否重組字串或重算篩選; 只在 `EventType.Layout` 更新資料。
- 介面: 上方一列 N/W/E/D 開關 + Log/Unity 開關 + `Clear` + `X`; 第二列搜尋框 + `Time` / `Loc` / `Collapse` / `Auto` 方形核取方塊 (`CheckBox`: 執行期產生 14×14 方框貼圖, 開啟時打勾, 元件銷毀時釋放); 捲動清單。
- 每一列 (`DrawRow`):
  - 第一行: 行號 `[n]` (不合併時在清單中的順序, 套用篩選後) + `+` / `-` 展開按鈕 (沒有堆疊追蹤時 `GUI.enabled = false`) + 訊息 + 合併次數膠囊 (次數 > 1 才顯示, 最右側)。行號、按鈕、膠囊以訊息第一行文字中線置中 (`lineStyle.padding.top + lineHeight / 2`)。
  - 第二行 (`Time` 或 `Loc` 開啟時, 與訊息間多 2px, 字比主訊息小 2 點): `(HH:mm:ss.ff)` + `路徑:行號`。Log 訊息用呼叫端位置; Unity 訊息用 `LogText.TryParseUnityLocation`。Editor 中位置是按鈕, 以 `AssetDatabase.OpenAsset(asset, line)` 開啟, 找不到資源時改用 `InternalEditorUtility.OpenFileAtLineExternal`; 建置版只顯示。專案根 (`Application.dataPath` 的上一層) 只在 Editor 取得。
  - 展開時: 小一級字顯示 `StackTrace.Text`。
  - 列與列之間 1px 分隔線 (白色, 透明度 0.12), 兼作對齊參考線。
- 合併 (`Collapse`): 以 `GroupKey` (等級、分類實例、訊息、呼叫端檔案與行號) 分組, 用重用的 `Dictionary<GroupKey, int>` 與 `previousOccurrence[]` 串起同組。顯示在第一次出現的位置並沿用其行號 (同 Unity Console); 合併多次的列不顯示時間; 展開顯示第一次出現的堆疊。只合併仍在紀錄中的訊息, 被擠掉的不計入次數。
- 展開狀態以流水號存在 `HashSet<long>`; 合併時同組任一次被展開就算展開。按鈕點擊與 Time / Loc 變更都延到下一個 `Layout` 事件套用。
- 時間、位置、行號、次數字串第一次顯示時才組並快取 (`Source.Times` / `Locations` 隨快照一起搬移)。膠囊貼圖 (16×16, `HideAndDontSave`) 在建立樣式時產生一次, `OnDestroy` 釋放。
- 外觀常數: `FoldWidth 18`、`FoldHeight 14`、`BadgeHeight 16`、`DetailOffset 2`、`separatorColor`; 詳見 `docs/log-system.md`「可調整的外觀參數」。
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
| dotnet `LogTests` + `LogHistorySinkTests` + `LogEntryInfoTests` | 61 (58 → 61, 7.2 新增 3 個) | **通過** (2026-11-03 `dotnet test dotnet/Library.Tests/Library.Tests.csproj`, 整體 111/111) |
| dotnet `LogStackTraceCleanTests` | 25 | **通過** (2026-11-03 `dotnet test dotnet/TorbenJunior.sln`, Library.Tests 141/141、Samples.Tests 8/8) |
| dotnet `LogOptionsTests` | 5 | **通過** (2026-11-03 `dotnet test dotnet/TorbenJunior.sln`, Library.Tests 116/116、Samples.Tests 8/8) |
| PlayMode `DebugWindowTests` + `LogSettingsInstallTests` | 22 + 8 | **通過** (2026-11-03 `unity command test run --mode playmode --async_tests true`, 全部 PlayMode 37/37) |
| EditMode `ScriptingDefineUtilityTests` + `LogSettingsCheckerTests` + `LogLevelColorDrawerTests` | 38 | **通過** (2026-11-03 `unity command test run --mode editor`, 全部 EditMode 46/46) |

dotnet `LogTests` 另含 `Format` 4 個等級 / 有無分類 / 關閉時不格式化、`Log.Debug` 的 3 個 `ConditionalAttribute`、公開寫入方法只有 10 個 (反射, 只在測試中使用)。測試專案在 `DefineConstants` 加入 `TORBEN_LOG_DEBUG`, 否則測試中的 `Log.Debug` 會被移除。

dotnet `LogEntryInfoTests` 涵蓋: 呼叫端位置 (含 `Format` 與 3 個字串參數的多載選擇)、UTC 時間、3 參數建構子無額外資訊、堆疊追蹤預設值與開關、堆疊從呼叫端開始且快取、`FromText`、`LogText` 的時間 / 路徑 / Unity 位置解析。`LogStackTraceCleanTests` 涵蓋堆疊文字清理規則 (見 4.3.1)。

PlayMode 涵蓋: 安裝歷史紀錄、建立 / 隱藏視窗、`isGUILog` 抑制 Console、`isConsoleWithGUI`、Debug 過濾、設定讀寫、右下角預設位置、過大視窗縮為半個畫面、超出畫面位置拉回、自動縮放、Unity 訊息收集 (等級與分類)、不重複收自己的 Log、`isUnityLog` 關閉、不受 `isPrintLog` 影響、清 Log 不影響 Unity、來源篩選設定、Time / Loc / Collapse 設定、Log 紀錄帶時間與呼叫端、Unity 訊息帶時間與堆疊位置、開啟所有顯示選項時繪製不出錯。

人工確認 (使用者於 Play 中, 2026-11-02 ~ 11-03): Log / Unity 開關與合併順序、Clear 保留 Unity 訊息、第二行字體大小、Editor 點擊位置開到正確行、展開按鈕、合併次數、行號、膠囊位置、置中對齊、分隔線 — 皆確認可接受。

仍未測試: 拖曳手感、搜尋操作、Clear 兩段式按鈕的自動化測試; 背景執行緒產生的 Unity 訊息。

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

### 7.1 2026-11-03 新增 (提交 `5207f04`)

| 項目 | 處理 |
|---|---|
| 時間戳記 | `LogEntry.Time` = `DateTime.UtcNow`; 顯示時轉當地時間。不記錄遊戲時間 (`Time.time`) |
| 呼叫端位置 | `[CallerFilePath]` / `[CallerMemberName]` / `[CallerLineNumber]` 加到所有公開寫入方法; 以 `LogArgsEnd` 防止多傳參數被綁成呼叫端資訊 |
| 堆疊追蹤 | 新類別 `LogStackTrace`; `Log.SetStackTraceEnabled` 依等級控制, 預設 Error。決定不做成「建構後不可改」: 設定只影響之後的訊息, 已寫入的 `LogEntry` 本來就不可變 |
| Unity 訊息 | 加存時間與 Unity 的堆疊文字 |
| 視窗 | 行號、展開按鈕、Time / Loc 第二行、Editor 點擊開檔、Collapse 與膠囊次數、置中對齊、分隔線 (見 5.2) |
| `HideInCallstack` | **不採用**。查 6000.5.5f1 的 `UnityEditor.CoreModule.dll`: `ConsoleWindow.InitializeHideInCallstackMethodsCache` 以 `TypeCache.GetMethodsWithAttribute<HideInCallstackAttribute>()` 收集方法名, 只在 Console 開啟「Strip logging callstack」時從詳細區堆疊文字去掉 (`StacktraceWithHyperlinks` → `StripCallstack`)。屬性在各平台的 `UnityEngine.CoreModule` 都存在, 但只在 Editor Console 有作用; 對點兩下跳轉是否有效**未查證**。`Log` 在 Core 無法標記, 所以 Console 點兩下仍跳到 `DebugLogs.Write`; 目前以視窗的位置連結替代 |
| 文件 | `docs/log-system.md` 補 API、視窗操作、可調整外觀參數表 |

### 7.2 2026-11-03 寫入方法精簡 (28 → 10, 破壞性變更)

| 項目 | 處理 |
|---|---|
| `XxxFormat` | 刪除 `InformationFormat` / `WarningFormat` / `ErrorFormat` / `DebugFormat` 共 24 個多載 |
| `Log.Format` | 新增 6 個多載, 以 `LogLevel` 為第一個參數; 行為同原本的 `XxxFormat` (關閉時零配置), 保留 `LogArgsEnd` |
| `Log.Debug` | 加上 `[Conditional("UNITY_EDITOR")]`、`[Conditional("DEVELOPMENT_BUILD")]`、`[Conditional("TORBEN_LOG_DEBUG")]` |
| 判斷依據 | 只有 Log **關閉時**的配置需要處理; Debug 給開發者用, 正式版以編譯期移除; Warning / Error 多在異常分支, 一般版的配置可接受 |
| 呼叫端遷移 | `Log.XxxFormat(...)` → `Log.Format(LogLevel.Xxx, ...)`; PlayMode 測試 1 處、dotnet 測試 5 處。Core / Runtime 原始碼沒有 `Log.Debug(` 呼叫 |
| 測試 | dotnet 測試專案加 `TORBEN_LOG_DEBUG`; 新增 `Format` 4 個等級、`Conditional` symbol、10 個寫入方法的測試 |
| 規則 | `AGENTS.md` 新增「Log 使用規則」 |

### 7.3 2026-11-03 設定檔與 Editor 工具 (提交 `23a6a4a`)

| 項目 | 處理 |
|---|---|
| 設定檔 | 新增 `LogSettings` (`ScriptableObject`) 與三組 `[Serializable]` 設定 (`LogOptions` 在 Core, `DebugLogsOptions` / `DebugWindowOptions` 在 Runtime)。只在 `Install` 套用一次, 熱路徑不讀取。覆蓋檔整份取代, 不支援串接 |
| 預設值來源 | 欄位初始值為唯一來源; `DebugWindow.ResetStatics` 改為套用 `new DebugWindowOptions()`。XML 註解中寫死的預設數值改為「預設值見 ... 欄位初始值」 |
| 容量 | 常數 200 移除; `visible` 等陣列的偏移量改為 `logCapacity`, 長度為兩個容量總和 |
| 按鈕 | 新增 `DebugWindowButtons` 旗標與 `DebugWindow.VisibleButtons`; `Auto` 開關不在需求列出的旗標內, 一律顯示 |
| 新公開 API | `DebugWindow.LogCapacity` / `UnityCapacity` / `UsesDefaultPosition` / `VisibleButtons`、`LogLevelSwitch`、`LogLevelColor`、Editor 的 `ScriptingDefineUtility` / `LogSettingsChecker` / `LogSettingsAssets` |
| 行為變更 | 每次進入 Play 時 `Log.isPrintLog` / `isDebugLog` / 堆疊追蹤開關與 `DebugLogs.isConsoleWithGUI` 也恢復為設定檔的值 (原本只有關閉 Domain Reload 時會保留上次的值) |
| Editor 工具 | Project Settings `Project/TorbenTools/Log` (檔案、建置與唯讀設定表格)、設定檔的 Inspector (預設折疊欄位 + Save Current State, 等級顏色用 Color picker)、`TorbenTools/` 選單、狀態檢查 6 項、進入 Play 時的摘要; `Torben.Editor` 加入 `Torben.Runtime` 引用; 新增 `Library.Tests.EditMode` 測試組件 |
| 測試隔離 | `DebugWindowTests` 與 `LogSettingsInstallTests` 在 SetUp 以 `CreateInstance<LogSettings>()` 重新安裝, TearDown 恢復啟動時的設定檔; 專案的覆蓋檔 (例如開啟 `openWindowOnStart`) 不影響測試結果 |
| Build Profile | `BuildProfile.GetActiveBuildProfile().scriptingDefines` 可讀到目前 profile 的 define (編譯通過; 有啟用 profile 時的顯示未人工確認)。Keep Debug Log in Builds 只改 Player Settings, 不管理 profile 的 define |

## 8. 目前沒有的能力 (設計時可考慮的缺口)

以下只列出現況缺少的部分, 不代表都需要做:

- **編譯期移除**: 只有 `Log.Debug` 有 `[Conditional]` (7.2); `Information` / `Warning` / `Error` / `Format` 在正式建置仍保留 (關閉時不輸出)。
- **Entry 資訊**: 時間、呼叫端位置、堆疊追蹤已完成 (7.1)。仍沒有影格編號、遊戲時間 (需由 Runtime 注入)、執行緒 ID、context 物件。
- **Console 點兩下**: 仍跳到 `DebugLogs.Write`, 不是呼叫端。可能做法 (未實作、未驗證): Console 訊息加 `<a href="..." line="...">` 超連結; 或 Editor 以 `[OnOpenAsset]` 攔截後依呼叫端轉址 (需讀 Console 內部狀態, 較脆弱)。
- **視窗互動**: 沒有點擊整列反白 / 選取 (已評估: 需記錄選取狀態與整列點擊判斷, 比分隔線複雜, 未做); 沒有複製單筆內容。
- **等級**: 沒有 Fatal / Exception, 沒有「最低等級」篩選 (enum 順序也不支援)。
- **分類篩選**: 視窗只能依等級、來源 (Log / Unity) 與文字搜尋, 不能依自訂分類開關; `Log` 也沒有依分類過濾。
- **sink 錯誤隔離 / 非同步**: sink 例外會往上拋; 沒有背景執行緒或佇列。
- **持久化**: 沒有寫檔 sink、沒有匯出或複製功能 (行動平台寫檔涉及路徑與權限, 屬平台風險)。
- **視窗開關**: 沒有快捷鍵或手勢, 需專案自行呼叫 `DebugLogs.isGUILog`。
- **容量**: 只能在啟動時由設定檔決定, 執行中不能修改。
- **相同訊息合併**: 只在顯示層合併, 儲存仍是每筆一份, 不是儲存端減量。
- **設定檔切換**: 不能依建置設定檔切換不同 `LogSettings`; 不支援逐欄位覆蓋。

---

## 9. 給設計者的提醒

- 新的資料處理 (篩選、格式化、緩衝) 優先放 Core 並以 dotnet 測試; 只有繪製、Unity Console、場景物件放 Runtime。
- 每次呼叫 `Log.Xxx` 是熱路徑候選: 新增欄位到 `LogEntry` (例如時間) 時, 留意取值成本與 Core 不能用 `UnityEngine.Time`。
- 變更 `LogLevel` 數值或順序會影響 `DebugWindow` 的陣列索引與按鈕文字。
- 變更 `DebugLogs.Install` 的清空行為會影響「關閉 Domain Reload 時不殘留 sink」的保證。
- 行動平台 (Android / iOS) 的視窗縮放與觸控操作尚未驗證, 設計若依賴這些平台, 需安排實機驗證。
