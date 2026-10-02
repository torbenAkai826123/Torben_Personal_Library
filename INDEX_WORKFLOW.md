# Unity 專案增量索引

索引協助定位來源檔案、確認 Unity 結構 (GUID、asmdef／asmref 歸屬) 與重用仍有效的分析結果。它是定位提示，不限制依任務需要開啟其他來源檔案。所有 AI 共用本流程。

## 使用時機

- 已知路徑、單一符號或關鍵字：直接讀檔或搜尋，不要求更新索引。
- 跨目錄盤點、asmdef／asmref 歸屬、依 GUID 追蹤搬移：優先使用索引。
- 索引不是完整的資產參照圖：只記錄每個資產自身的 GUID，不記錄場景、Prefab 等檔案內部引用了哪些 GUID。查找引用仍需搜尋來源檔。

## 更新

用到索引時才更新，不要求每次 session 開始更新：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Update-ProjectIndex.ps1
```

- 同一段查詢工作可重用當次索引；相關來源或組件設定 (asmdef／asmref／`.meta`) 改變後，下一次索引查詢前再更新。
- 更新器每次重新列舉核准的來源根並計算 SHA-256，能偵測 Editor 關閉期間手動新增、修改、刪除及搬移的檔案，不依賴 Editor 是否開啟。
- 執行成本：約 190 個檔案時，本機實測 1.4～7.6 秒 (後者為冷啟動)。它只寫入 `project_index/`，主控台輸出只有摘要兩行。
- `changes.json` 只代表與**前一次掃描**的差異，每次更新都會覆寫。它不是 session 或長期的變更歷史；需要歷史時看 git。

## 查詢

用查詢入口取得任務需要的項目，不要把 `project_index/` 的 JSON 整份讀進 context (`content.json` 約 420 KB、`work-queue.json` 約 95 KB)：

```powershell
.\tools\Find-ProjectIndex.ps1 -Path "Samples/StateMachine/*.cs"
.\tools\Find-ProjectIndex.ps1 -Assembly "Samples.Tests.PlayMode"
.\tools\Find-ProjectIndex.ps1 -Guid fa9d5289e2444204859332fb8a0cc04e
```

從 Bash 等其他 shell 呼叫時，改用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./tools/Find-ProjectIndex.ps1 <參數>`。

- `-Path`：不分大小寫的子字串比對；只有 `*`、`?` 是萬用字元，`[TorbenJuniorUtility]` 的方括號視為一般字元。
- `-Assembly`：組件名稱完全比對 (不分大小寫)；先列出定義該組件的 asmdef／asmref，再列出歸屬檔案，經 asmref 加入者標 `(asmref)`。
- `-Guid`：比對資產自身的 GUID；資料夾以 `folder` 列出。
- 多個條件同時指定時取交集；至少要指定一個條件，不支援列出全部項目。
- 預設每行輸出 `類型<Tab>組件<Tab>路徑`，最多 50 筆，超過時提示剩餘筆數 (`-Limit` 可調整)。預設省略 `.meta` 項目 (`-IncludeMeta` 可加入)。
- `-Detail` 另外輸出 GUID、大小、修改時間、`sha256` 與 `contextHash`。
- 無匹配時輸出 `matches: 0` 與明確訊息；GUID 無匹配時另外提醒場景／Prefab 引用不在索引內。
- 警告只列出與結果相關的項目：已不在磁碟上的檔案 (索引可能過期)，以及更新器記錄的相關警告 (例如無法解析的 asmref、重複 GUID)。
- 查詢入口只讀取索引，不會更新或寫入任何檔案；索引不存在時直接報錯，請先執行更新。

## 輸出檔

位於被 `.gitignore` 排除的 `project_index/`，每個 clone 各自產生：

- `content.json`：來源檔案指紋、類型、Unity GUID、最近的 asmdef／asmref 歸屬及掃描範圍。
- `changes.json`：與前一次掃描相比的新增、修改、刪除、搬移與警告 (每次覆寫)。
- `work-queue.json`：缺少有效分析快取的可分析檔案，以及搬移位置變更。
- `analysis-cache.json`：明確儲存的分析結果。
- `states.json` 與 `status-history.jsonl`：人工狀態與變更紀錄。

## Unity 結構

索引每個資產的 `.meta` GUID，並以 GUID 配對 asmdef 的名稱。`.asmref` 的 `GUID:<guid>` 會解析成對應組件；一般資產記錄最近的父資料夾組件邊界。沒有 asmdef／asmref 的檔案組件為空 (`-`)，不會推算成 `Assembly-CSharp` 等預設組件。搬移資產時，`changes.json` 和 `work-queue.json` 以 GUID 報告舊／新路徑。

## 索引範圍

只遞迴以下專案自有來源：

- `Assets/[TorbenJuniorUtility]/`
- `ProjectSettings/`
- `dotnet/`
- `docs/`（排除收件匣、封存與忽略資料）
- `tools/`
- 根目錄的 `AGENTS.md`、`CLAUDE.md`、`README.md`、`INDEX_WORKFLOW.md`、`.gitignore`
- `Packages/manifest.json` 與 `Packages/packages-lock.json`

任何路徑中含 `backup` 的檔案或資料夾，以及 `Library/`、`Temp/`、`Logs/`、`Obj/`、Build 輸出、`bin/`、`obj/`、`node_modules/`、`.git/`、`PackageCache/` 和封存目錄均排除。

其他 `Assets/` 目錄不在預設範圍，包含第三方資源與 `Assets/Exceptions/`。若日後有需要索引的自有資產根目錄，應先把明確路徑加到更新器的 `$roots` 清單，並確認排除規則涵蓋該路徑中的產生物。

## 分析快取 (選用)

- 分析快取是選用功能，不因完整讀檔或修改檔案就強制記錄。只儲存有重用價值的分析 (例如責任邊界、主要呼叫關係、不明顯的限制)；不要為了清空佇列填入套版摘要。
- 更新器輸出的 `Without valid analysis cache` (JSON 中為 `unreviewedCount`／`contentReview`) 只表示「缺少有效分析快取」，不代表未讀、待辦或未完成工作，不需要人工清空。
- 快取只有在來源 SHA-256、分析上下文指紋及 `analysisVersion` 全部相符時才有效。

儲存時，以 `Find-ProjectIndex.ps1 -Path <檔案> -Detail` 取得 `sha256` 與 `contextHash`：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-ProjectFileAnalysis.ps1 `
  -Path "Assets/[TorbenJuniorUtility]/Scripts/Core/Example.cs" `
  -SourceHash "<sha256>" `
  -ContextHash "<contextHash>" `
  -AnalysisJson '{"summary":"已確認 Example 的責任與主要呼叫關係。"}'
```

工具會比對索引指紋與來源檔目前的 SHA-256。來源在檢視後若已變更，工具會拒絕記錄過時分析；先重新更新索引再取得新指紋。

## 人工狀態

`approved` 等狀態是人工分類，不取代內容分析或 hash 驗證。AI 只能依使用者明確授權代為記錄，不可因測試通過等理由自行核准或變更狀態：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-ProjectFileStatus.ps1 `
  -Path "Assets/[TorbenJuniorUtility]/Scripts/Core/Example.cs" `
  -Status approved `
  -Reason "人工確認"
```

可用狀態為 `unclassified`、`draft`、`approved`、`withdrawn`、`deprecated`。資產透過 GUID 搬移時，狀態隨資產路徑遷移。
