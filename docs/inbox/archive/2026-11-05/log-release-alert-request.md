# Development Alert 補充需求: 正式版仍開啟的開發設定

- 對象: 負責實作的 AI。前提是 `log-v1-finalize-request.md` 的 Development Alert 已完成。
- 目的: 正式版 (非 Development Build) 啟動時, 若開發用的 Log 設定仍開著, 依 Development Alert 等級發出提醒, 協助發現「建置前忘了關」的情況。
- 原則: **只提醒, 不改任何設定、不擋任何訊息。** 故意在正式版開著時, 由使用者把該設定檔的 Development Alert 設為 Off 表示「已知」。

---

## 1. 名詞

| 名稱 | 意思 |
|---|---|
| A 類 (現有) | 設定本身有問題: 找不到設定檔、容量超出範圍、等級陣列重複 |
| B 類 (本次新增) | 設定沒有錯, 但屬於開發用途, 不應該帶到正式版 |

---

## 2. B 類檢查

### 2.1 執行條件

只在 `!UnityEngine.Debug.isDebugBuild` 時檢查。Editor 與 Development Build 中 `isDebugBuild` 為 true, 不檢查, 不會產生任何輸出。

### 2.2 檢查項目

依 Install 時套用的 **生效中設定** 判斷:

| 項目 | 觸發條件 |
|---|---|
| 啟動時開啟視窗 | `output.openWindowOnStart` 為 true |
| Log 輸出 | `log.isPrintLog` 為 true |
| Debug 等級 | `log.isDebugLog` 為 true |
| 保留 Debug Log | `TORBEN_LOG_DEBUG` 有定義 (Runtime 中以 `#if TORBEN_LOG_DEBUG` 判斷) |

- `TORBEN_LOG_DEBUG` 的判斷反映的是 **Runtime 組件編譯時** 的狀態。目前函式庫以原始碼形式由遊戲專案編譯, 結果正確; 日後打包成 DLL 時只反映打包當下。在 XML 註解與文件中註明。
- 找不到設定檔時, 前三項依欄位初始值判斷 (預設皆為 false, 應該不會觸發), 第四項照常判斷; 等級沿用現有規則 (一律 Warning)。

### 2.3 輸出

- 與 A 類共用 Development Alert 等級, 並 **合併為同一則訊息** (每次 Install 最多一則, 現有規則不變)。
- 訊息分兩段, 只輸出有內容的段落。B 類段落結尾附上處理方式:

```text
[Log] LogSettings problems (Development Alert; ...):
- (A 類項目, 若有)
Development settings enabled in a non-development build:
- isPrintLog
- openWindowOnStart
If this is intentional, set Development Alert to Off in <生效中設定檔名稱>.
```

- 實際文字與現有訊息風格一致即可, 但需包含: B 類項目清單、「若為刻意設定, 將 Development Alert 設為 Off」, 以及生效中設定檔的名稱 (預設檔或覆蓋檔)。
- 輸出時機沿用現有規則 (在開始收集 Unity 訊息之後), 讓 `openWindowOnStart` 開著時, 提醒會出現在 GUI 視窗中。

### 2.4 可測試性

`Debug.isDebugBuild` 在 Editor 與測試中永遠為 true, `#if` 也無法在測試中切換。因此檢查邏輯拆成可注入參數的 internal 方法, 例如:

```csharp
internal static void CollectReleaseProblems(LogSettings settings, bool isDebugBuild, bool isDebugSymbolDefined, List<string> problems);
```

Install 以 `Debug.isDebugBuild` 與 `#if` 的結果呼叫它; 測試直接傳入參數。

---

## 3. Editor

- Project Settings 頁面的 Development Alert 欄位下方加一行說明: 「Off 同時關閉設定問題與正式版開發設定的提醒」。
- 不新增其他 Editor 功能。

---

## 4. 測試

| 範圍 | 測試 |
|---|---|
| EditMode 或 PlayMode | `isDebugBuild = true` 時, 四項都開著也不產生 B 類項目 |
| 同上 | `isDebugBuild = false` 時, 四項各自單獨觸發, 以及全部同時觸發 |
| 同上 | `isDebugBuild = false` 且四項皆關閉時, 沒有 B 類項目 |
| PlayMode | A 類與 B 類同時存在時合併為一則, 兩段都在; 只有 B 類時只有 B 段 |
| PlayMode | Development Alert 為 Off 時沒有輸出 |
| PlayMode | 訊息包含生效中設定檔的名稱 (預設檔與覆蓋檔各一次) |

---

## 5. 文件

- `docs/log-system.md`: Development Alert 章節補上 B 類的條件、項目、`TORBEN_LOG_DEBUG` 在 DLL 下的限制, 以及「刻意在正式版開啟時設為 Off」。
- 現況報告: 「刻意不做」清單中的「建置前檢查」補註「改以執行時 Development Alert (正式版開發設定提醒) 處理」。

---

## 6. 完成標準

1. 所有測試通過, 第 4 節的測試都存在。
2. Editor Play 與 Development Build 中, 開發設定開著時沒有任何 B 類輸出 (人工確認)。
3. macOS IL2CPP **非 Development Build**, 設定 `isPrintLog` 與 `openWindowOnStart` 為 true: 啟動後 GUI 視窗的 Unity 訊息中出現提醒, 內容列出兩項 (人工確認)。
4. 同上, 將 Development Alert 設為 Off 後重新建置: 沒有提醒 (人工確認)。
5. 第 5 節文件已更新。
6. 回報修改的檔案清單與測試數量變化。
