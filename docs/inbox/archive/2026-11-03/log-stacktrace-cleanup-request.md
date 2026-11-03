# 堆疊追蹤文字清理需求

- 對象: 負責實作的 AI。前提是讀者已讀過 `docs/log-system.md` 與現況報告中 `LogStackTrace`、`LogText` 的說明。
- 目的: `LogStackTrace.Text` 轉文字時, 去掉畫面上沒有用的資訊, 並把路徑縮短成與視窗第二行相同的格式。

---

## 1. 實際格式 (macOS IL2CPP 建置)

### 1.1 有行號 (Player Settings 的 IL2CPP Stacktrace Information = Method Name, File Name, and Line Number)

```text
  at CallLog.LogError (System.String message) [0x0001c] in /Users/torbenstudio/Exclude/Github.com/Torben_Personal_Library/Assets/Exceptions/LogTest/CallLog.cs:68
  at CallLog+<>c__DisplayClass9_0.<OnGUIFunc>b__5 () [0x00000] in /Users/torbenstudio/Exclude/Github.com/Torben_Personal_Library/Assets/Exceptions/LogTest/CallLog.cs:98
  at CallLog.OnGUIFunc (System.Int32 id) [0x0010a] in /Users/torbenstudio/Exclude/Github.com/Torben_Personal_Library/Assets/Exceptions/LogTest/CallLog.cs:98
  at UnityEngine.GUI.CallWindowDelegate (UnityEngine.GUI+WindowFunction func, System.Int32 id, ...) [0x00061] in /Users/bokken/build/output/unity/unity/Modules/IMGUI/GUI.cs:1847
```

### 1.2 沒有行號 (Stacktrace Information = Method Name)

```text
  at CallLog.LogError (System.String message) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
```

- `[0x0001c]`: IL 偏移。已有行號時, 畫面上沒有用途。
- `<0x... + 0x...>`: 原生偏移 + IL 偏移; `0 in <32 碼十六進位>:0`: 組件 MVID 與行號, 全 0 表示沒有資訊。
- Editor (Mono) 的格式應該與 1.1 相同或相近 (未驗證), 規則需同時適用。

## 2. 期望結果

1.1 變成:

```text
  at CallLog.LogError (System.String message) in Assets/Exceptions/LogTest/CallLog.cs:68
  at CallLog+<>c__DisplayClass9_0.<OnGUIFunc>b__5 () in Assets/Exceptions/LogTest/CallLog.cs:98
  at CallLog.OnGUIFunc (System.Int32 id) in Assets/Exceptions/LogTest/CallLog.cs:98
  at UnityEngine.GUI.CallWindowDelegate (UnityEngine.GUI+WindowFunction func, System.Int32 id, ...) in GUI.cs:1847
```

1.2 變成:

```text
  at CallLog.LogError (System.String message)
```

## 3. 規則

逐行處理, 每行只套用第一條符合的規則:

| # | 條件 (行尾) | 處理 |
|---|---|---|
| A | ` [0x十六進位] in 路徑:數字` (路徑不在角括號內) | 去掉 ` [0x...]`; 路徑改為 `LogText.ToDisplayPath(路徑, null)`; 結果為 `方法 in 顯示路徑:行號` |
| B | ` <0x十六進位 + 0x十六進位> 數字 in <十六進位>:0` | 從 ` <0x` 開始到行尾全部去掉 |
| — | 其他 (包含 Unity 提供的堆疊文字 `(at Assets/...:68)`、沒有尾段的行、無法辨識的格式) | 原樣保留 |

細節:

- 解析位置: 從行尾往前找。行號為最後一個 `:` 之後的數字 (Windows 路徑 `C:\...` 本身含 `:`, 不能從前面找); 路徑為最後一個 ` in ` 與該 `:` 之間的內容 (路徑可能含空白)。
- 規則 A 的 `ToDisplayPath` 第二個參數 (專案根目錄) 傳 `null`, 沿用其「從 `Assets/` 或 `Packages/` 開始, 都沒有就只留檔名」的行為。不要在 Core 取得專案根目錄。
- 規則 A 遇到 `in <...>:數字` (角括號內不是路徑) 時不套用 A; 行號為 0 時改套用 B 的判斷, 行號不為 0 時原樣保留。
- 保留行首縮排與原本的換行字元 (`\n` 或 `\r\n`)。
- 現有的「去掉最上方 `Torben.Log.` 框架」行為不變, 且在清理之前執行。
- 不改編譯器產生的名稱 (例如 `<>c__DisplayClass9_0`), 也不隱藏 `UnityEngine.` 框架。
- 不使用 `System.Text.RegularExpressions` (IL2CPP 建置會增加程式碼大小), 以字串搜尋手動判斷。
- 只在第一次讀取 `Text` 時執行並快取 (現有行為), 可以配置; XML 註解需標明配置行為。
- `LogStackTrace.FromText` (Unity 提供的堆疊) 的內容同樣經過這個處理, 但因格式不符, 結果應與原本相同 (有測試確認)。

## 4. 測試 (dotnet)

| 輸入 | 期望 |
|---|---|
| 1.1 的四行 | 2 的四行 |
| 1.2 的一行 | 2 的一行 |
| 規則 A, 路徑含空白 (`/Users/a b/Assets/X.cs:5`) | `... in Assets/X.cs:5` |
| 規則 A, Windows 路徑 (`C:\proj\Assets\X.cs:5`) | 依 `ToDisplayPath` 現有行為 (若不支援反斜線, 在回報中說明, 不在本次修改 `ToDisplayPath`) |
| 規則 A, 路徑在 `Packages/` 下 | 從 `Packages/` 開始 |
| 規則 A, 路徑沒有 `Assets/` 也沒有 `Packages/` | 只留檔名 |
| `<0x00012 + 0x00034> 0 in <a1b2…>:0` | 去掉尾段 (規則 B) |
| `<0x00012 + 0x00034> 0 in <a1b2…>:68` | 原樣 |
| Unity 格式 `CallLog:LogError (string) (at Assets/CallLog.cs:68)` | 原樣 |
| 沒有尾段的行 | 原樣 |
| 空字串、只有換行、最後一行沒有換行 | 不拋例外, 結果正確 |
| `\r\n` 換行 | 保留 `\r\n` |
| 開頭是 `Torben.Log.` 框架 | 先去掉該框架, 其餘行再清理 |

## 5. 完成標準

1. dotnet 測試全部通過, 第 4 節的案例都存在。
2. Core 沒有引用 `System.Text.RegularExpressions`。
3. macOS IL2CPP 建置 (兩種 Stacktrace Information 設定各一次) 中展開 Error 的堆疊, 顯示為第 2 節的格式 (人工確認)。
4. Editor 中展開 Error 的堆疊, 確認格式並在回報中附上一段實際輸出 (確認 1 節「Editor 格式未驗證」的部分)。
5. `docs/log-system.md` 補上這些規則; 現況報告把「IL2CPP 堆疊行號: 未驗證」改為「已驗證, 需開啟 File Name and Line Number」。
6. 回報修改的檔案與測試數量變化。
