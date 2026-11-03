# Log API 修改需求: 精簡寫入方法 (28 → 10)

- 對象: 負責實作的 AI。前提是讀者已讀過 `docs/log-system-status-report.md` (現況報告, 基準提交 `5207f04`) 與 `docs/log-system.md`。
- 目的: 減少 `Log` 的公開寫入方法數量, 同時保留「Log 關閉時可以零配置」的能力。
- 性質: **破壞性變更** (刪除 24 個公開方法, 新增 6 個)。

---

## 1. 背景與判斷依據

28 個方法的組成是 `4 個等級 × (1 個一般版 + 6 個 XxxFormat)`, 其中有兩個成因:

| 成因 | 影響 |
|---|---|
| GC: Log 關閉時 `$"..."` 仍會在進入方法前組好字串並配置 | 每個等級多出 6 個 `XxxFormat` 多載 |
| `[CallerFilePath]` 等屬性只由編譯器填入「被直接呼叫的方法」 | 每個公開方法都要各自宣告 4 個 Caller 參數, 無法集中 |

討論後的結論:

- Log **開啟時** 的配置不是問題 (`UnityEngine.Debug.Log` 本身的成本更高), 只有 **關閉時** 的配置需要處理。
- `Debug` 等級只給開發者除錯用, 開啟時配置可以接受; 正式版用編譯期移除即可, 不需要 Format 版本。
- `Information` / `Warning` / `Error` 保留一般版方便臨時使用; 需要零配置時, 呼叫端明確改用單一入口 `Log.Format`, 以呼叫方式表達「這裡有意減少配置」。
- `Warning` / `Error` 通常位於異常分支, 正常運作時不會執行到, 一般版的配置可以接受。

---

## 2. 修改內容

### 2.1 `Log.Debug` 加上 `[Conditional]`

```csharp
[System.Diagnostics.Conditional("UNITY_EDITOR")]
[System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
[System.Diagnostics.Conditional("TORBEN_LOG_DEBUG")]
public static void Debug(string message, LogCategory category = default,
    LogArgsEnd argsEnd = default,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0);
```

- 多個 `[Conditional]` 為 OR: 呼叫端編譯時任一 symbol 有定義就保留呼叫, 否則整行 (包含參數運算) 被移除。
- 保留後仍由執行期的 `isPrintLog` / `isDebugLog` 決定是否輸出, 行為不變。
- `[Conditional]` 由 **呼叫端的編譯** 決定, 與 `Log` 所在組件無關。因此:
  - 遊戲程式由 Unity 編譯, 勾選 Development Build 或在 Scripting Define Symbols 加入 `TORBEN_LOG_DEBUG` 即可保留。
  - 函式庫內部若呼叫 `Log.Debug`, 打包成 DLL 時就決定了去留。**函式庫內部不得使用 `Log.Debug`** (目前應該沒有使用處, 請確認)。
- 參數中的副作用 (例如 `Log.Debug($"{i++}")`) 會跟著被移除, 需寫進 XML 註解與文件。

### 2.2 刪除全部 `XxxFormat` (24 個)

刪除 `InformationFormat`、`WarningFormat`、`ErrorFormat`、`DebugFormat` 的所有多載。

### 2.3 新增 `Log.Format` (6 個多載)

```csharp
public static void Format<T1>(LogLevel level, string format, T1 arg1, ...);
public static void Format<T1, T2>(LogLevel level, string format, T1 arg1, T2 arg2, ...);
public static void Format<T1, T2, T3>(LogLevel level, string format, T1 arg1, T2 arg2, T3 arg3, ...);
public static void Format<T1>(LogLevel level, LogCategory category, string format, T1 arg1, ...);
public static void Format<T1, T2>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2, ...);
public static void Format<T1, T2, T3>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3, ...);
// "..." = LogArgsEnd argsEnd = default, [CallerMemberName] ..., [CallerFilePath] ..., [CallerLineNumber] ...
```

- 行為與原本的 `XxxFormat` 相同: 先檢查 `IsEnabled(level)`, 關閉時直接 return, 不裝箱、不呼叫 `ToString`、不配置; 開啟時才 `string.Format`。
- **保留 `LogArgsEnd`**: 「泛型參數後接選填 Caller 參數」的結構不變, `Format(level, "{0}{1}{2}", a, b, "c")` 仍可能被解析成 2 參數版本並把 `"c"` 綁到 `callerMemberName`。
- `Format(LogLevel.Debug, ...)` 可以使用, 但 **不會** 被 `[Conditional]` 移除 (`[Conditional]` 無法依參數值判斷)。關閉時一樣零配置, 只是呼叫留在建置中。XML 註解與文件註明「Debug 請用 `Log.Debug`」。
- XML 註解標明配置行為: 關閉時零配置; 開啟時每次呼叫配置。

### 2.4 保留不變

- `Log.Information` / `Warning` / `Error` (一般版, 無 `[Conditional]`)。
- `Log.IsEnabled`、`isPrintLog`、`isDebugLog`、sink 相關 API、堆疊追蹤相關 API。
- `LogEntry`、`LogHistorySink`、Runtime 端 (`DebugLogs`、`DebugWindow`) 不需修改, 除非有呼叫被刪除的方法。

### 2.5 修改後的公開寫入方法

| 方法 | 數量 | `[Conditional]` | 關閉時配置 |
|---|---|---|---|
| `Information` / `Warning` / `Error` | 3 | 無 | 呼叫端用 `$"..."` 時會配置; 字串常值不配置 |
| `Debug` | 1 | 有 | 正式版整行移除 |
| `Format` | 6 | 無 | 零配置 |
| **合計** | **10** | | |

---

## 3. 測試

### 3.1 dotnet 測試專案

- `dotnet/Library.Tests` 不會定義 `UNITY_EDITOR`, 測試中的 `Log.Debug` 會被移除。**在 `Library.Tests.csproj` 的 `DefineConstants` 加入 `TORBEN_LOG_DEBUG`** (保留原有常數)。
- 把原本針對 `XxxFormat` 的測試改寫成 `Log.Format`, 涵蓋:
  - 4 個等級各至少一次 (含 `LogLevel.Debug` 在 `isDebugLog` 關閉時不輸出)。
  - 有 / 無 category 的版本。
  - 關閉時不呼叫參數的 `ToString` (沿用原測試手法)。
  - 3 個字串參數時的多載選擇 (`LogArgsEnd` 防誤綁), 以及呼叫端位置正確。
- 新增: 以反射確認 `Log.Debug` 帶有 3 個 `ConditionalAttribute`, symbol 分別為 `UNITY_EDITOR`、`DEVELOPMENT_BUILD`、`TORBEN_LOG_DEBUG` (反射只用在測試, 不違反 AOT 規則)。
- 新增: 以反射確認 `Log` 的公開寫入方法只剩上表 10 個 (防止日後又加回多載)。

### 3.2 PlayMode 測試

- Editor 中 `UNITY_EDITOR` 有定義, `Log.Debug` 會保留, 現有 Debug 過濾測試應該不受影響。
- 若有呼叫被刪除方法的地方, 改用 `Log.Format`。

---

## 4. 文件與規則

### 4.1 `docs/log-system.md`

- 移除 `XxxFormat` 說明, 加入 `Log.Format` 與用法範例。
- 加入 `Log.Debug` 的 `[Conditional]` 說明: 保留條件、如何在實機保留 Debug Log (Development Build 或 `TORBEN_LOG_DEBUG`)、參數副作用會被移除、函式庫內部不得使用。
- 更新 4.6 / 7 節中關於 28 個方法的敘述。

### 4.2 `AGENTS.md` 新增 Log 使用規則

```text
Log 使用規則:
- 有動態值 ($"..."、+ 串接) 的 Information / Warning / Error, 一律用 Log.Format。
- 固定字串常值直接用 Log.Information / Warning / Error (常值不配置)。
- Debug 等級用 Log.Debug, 可以使用 $"..." (正式版會被編譯期移除)。不要用 Log.Format(LogLevel.Debug, ...)。
- 函式庫 (Core / Runtime) 內部不得呼叫 Log.Debug。
- Log.Debug 的參數不得有副作用。
```

### 4.3 現況報告

更新 `docs/log-system-status-report.md` 的第 2 節表格、第 4.6 節 API、第 6 節測試數量, 並在第 7 節新增本次修改紀錄。

---

## 5. 呼叫端遷移

| 原本 | 改為 |
|---|---|
| `Log.InformationFormat("{0}", a)` | `Log.Format(LogLevel.Normal, "{0}", a)` |
| `Log.WarningFormat(cat, "{0}", a)` | `Log.Format(LogLevel.Warning, cat, "{0}", a)` |
| `Log.ErrorFormat("{0}{1}", a, b)` | `Log.Format(LogLevel.Error, "{0}{1}", a, b)` |
| `Log.DebugFormat("{0}", a)` | `Log.Debug($"{a}")` |

搜尋整個倉庫 (含 Samples、測試、文件) 的 `InformationFormat|WarningFormat|ErrorFormat|DebugFormat` 並全部替換。

---

## 6. 完成標準

全部符合才算完成:

1. `dotnet test dotnet/Library.Tests/Library.Tests.csproj` 全部通過。
2. Unity PlayMode 與 EditMode 測試全部通過。
3. 倉庫中搜尋 `InformationFormat|WarningFormat|ErrorFormat|DebugFormat` 沒有結果 (修改紀錄中的歷史說明除外)。
4. 3.1 的兩個反射測試存在且通過。
5. Core / Runtime 原始碼中沒有 `Log.Debug(` 呼叫。
6. `docs/log-system.md`、`AGENTS.md`、現況報告已依第 4 節更新。
7. 回報: 實際修改的檔案清單、測試數量變化、遇到但未處理的問題。

---

## 7. 不在本次範圍

- C# 10 插值字串 handler (專案目前為 C# 9, 不採用)。
- Android 實機 Profiler 量測 (之後另外進行, 用來確認正式版關閉 Log 時每幀的 GC Alloc)。
- Console 點兩下跳到呼叫端、`LogLevel` 順序等其他缺口。
