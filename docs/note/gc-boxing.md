# 會產生 GC 的裝箱與字串配置

整理 C# 中常見、容易被忽略的記憶體配置。本專案的 C# 版本為 9.0 (Unity 6000.5、`dotnet/` 專案的 `LangVersion` 皆為 9.0)，下文以此為準。

> 本筆記內容是 C# 語言與執行環境的一般行為，尚未在本專案以 Profiler 或 Performance Testing 量測。標「未驗證」的項目在 Unity (Mono／IL2CPP) 上的實際行為可能不同，需要時以量測為準。

## 先分清兩件事

| | 裝箱 (boxing) | 新字串配置 |
|---|---|---|
| 是什麼 | 值型別 (`int`、`float`、`struct`、`enum`) 被當成 `object` 或介面使用時，在 heap 上包一份複本 | 執行期組出一個新的 `string` |
| 何時發生 | 只在「值型別 → `object`／介面」時 | 只要字串不是常值，不管有沒有裝箱都會發生 |
| 例子 | `object o = hp;` | `"名稱 " + name` |

兩者常一起出現，但不是同一件事。例如 `$"名稱 {name}"` (name 是 `string`) 沒有裝箱，但仍會配置新字串。

## 會裝箱的情境

| 情境 | 範例 | 避免方式 |
|---|---|---|
| 值型別指派給 `object` | `object o = 5;` | 保留原型別，或改用泛型 |
| 傳給參數型別為 `object` 的方法 | `string.Format("{0}", hp)` | 用泛型方法延後處理 (例如 `Log.DebugFormat`) |
| `params object[]` | `Foo(params object[] args)` 傳入 `int` | 提供固定數量的泛型多載；陣列本身也會配置 |
| 字串插值內含值型別 | `$"血量 {hp}"` | 見下方「字串插值」 |
| 值型別轉成介面 | `IComparable c = 5;`、把 `struct` 傳給參數型別為介面的方法 | 改用泛型約束 `void Foo<T>(T x) where T : IComparable<T>` |
| `struct` 呼叫沒有覆寫的 `ToString`／`Equals`／`GetHashCode` | 自訂 `struct` 沒覆寫就呼叫 `Equals` | 覆寫這些方法，並實作 `IEquatable<T>` |
| `enum` 呼叫 `ToString()` | `state.ToString()` | 會配置字串；熱路徑上改用預先建好的對照表 |
| 非泛型集合 | `ArrayList`、`Hashtable` | 改用 `List<T>`、`Dictionary<TKey, TValue>` |
| 透過介面 `foreach` | `IEnumerable<int> e = list; foreach (var x in e)` | 直接對 `List<T>` 做 `foreach`，列舉器是 `struct`，不會裝箱 |
| `Enum.HasFlag` | `flags.HasFlag(Flag.A)` | 未驗證：較舊的執行環境會裝箱。熱路徑上可改寫成 `(flags & Flag.A) != 0` |
| 以 `enum` 為鍵的 `Dictionary` | `Dictionary<State, int>` | 未驗證：較舊的 Unity Mono 版本比較鍵時會裝箱。有疑慮時可傳入自訂的 `IEqualityComparer<State>` |

## 會配置新字串的情境

- 字串串接：`"a" + b`
- 字串插值：`$"..."`
- `string.Format(...)`
- 任何 `ToString()`，包括 `int.ToString()` (不會裝箱，但會配置字串)
- `Substring`、`Replace`、`Split`、`ToUpper` 等回傳新字串的方法

字串常值 (`"固定文字"`) 不會配置，編譯時就已經存在。

## 字串插值 `$"..."` 在 C# 9 的實際行為

編譯器會依內容改寫成其他呼叫：

| 寫法 | 改寫結果 | 裝箱 | 新字串 |
|---|---|---|---|
| `$"名稱 {name}"` (只含 `string`) | `string.Concat("名稱 ", name)` | 否 | 是 |
| `$"血量 {hp}"` (含值型別) | `string.Format("血量 {0}", (object)hp)` | 是 | 是 |

C# 10 起有 interpolated string handler，可以讓 `$"..."` 避免裝箱，甚至在 log 關閉時完全不組字串；目前專案的 C# 版本無法使用。

## 搭配本專案 Log 系統的寫法

問題在於 C# 會**先算好所有引數才進入方法**，所以 `Log.Debug($"血量 {hp}")` 在 log 關閉時，裝箱與組字串仍然已經發生。

| 情境 | 寫法 |
|---|---|
| 不在每幀路徑上 | 直接用 `$"..."`，好讀優先 |
| 每幀路徑，參數 3 個以內 | `Log.DebugFormat("血量 {0}", hp)`：參數以泛型傳入，關閉時零配置 |
| 每幀路徑，參數超過 3 個或格式複雜 | `if (Log.IsEnabled(LogLevel.Debug)) Log.Debug($"...");` |
| 固定字串 | `Log.Debug("固定文字")`：常值不配置 |

`XxxFormat` 在 log 開啟時仍會裝箱並組字串 (內部呼叫 `string.Format`)；這是要輸出 log 本來就得付的成本。

## 判斷原則

- 只有**每幀路徑**需要在意。載入期或一次性的配置只發生一次，對 GC 壓力很小。
- 每幀配置的問題在於累積：每幀幾十 bytes，乘上每秒 60 幀，很快就會觸發 GC，造成卡頓。
- 不確定時用 Unity Profiler 的 GC Alloc 欄位或 Performance Testing 確認，不要只憑推測改寫。

## 不是裝箱、但同樣會配置的常見情況

- Lambda 捕捉外部變數 (closure)：每次建立都會配置。
- LINQ：大多會配置列舉器或中間集合。
- `new` 出來的 class 實例、陣列，以及容量不足而擴充的 `List<T>`。
