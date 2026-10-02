# Unity 專案增量索引

索引協助定位來源檔案、檢視變更與重用仍有效的分析結果。它不限制依任務需要開啟其他來源檔案。

## 更新與查詢

每次以索引定位本專案檔案前，在倉庫根目錄執行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Update-ProjectIndex.ps1
```

更新器每次重新列舉明確核准的來源根並計算 SHA-256，因此能偵測 Editor 關閉期間手動新增、編輯、刪除及搬移的檔案。每次完整計算指紋較慢，但不依賴 Editor 是否開啟，也不會漏掉只更動時間戳而內容不同的檔案。

- `project_index/content.json`：來源檔案指紋、類型、Unity GUID、最近的 asmdef／asmref 歸屬及掃描範圍。
- `project_index/changes.json`：上次掃描以來的新增、修改、刪除、搬移與警告。
- `project_index/work-queue.json`：目前尚無有效分析快取的可分析檔案，以及搬移位置變更。這是建議檢視的清單，不是閱讀檔案的授權邊界。
- `project_index/analysis-cache.json`：由工具或使用者明確儲存的分析結果。只有來源 SHA-256、分析上下文指紋及 `analysisVersion` 全部符合，結果才有效。
- `project_index/states.json` 與 `status-history.jsonl`：可選的人工生命週期狀態與變更紀錄。

工作項目會持續出現在 `contentReview`，直到有符合三種指紋的分析結果。掃描不會代替分析，也不會將尚未分析的項目標記完成。依任務需要可閱讀任何未變更檔案或未列入清單的檔案。

## Unity 結構

索引每個資產的 `.meta` GUID，並以 GUID 配對 asmdef 的名稱。`.asmref` 的 `GUID:<guid>` 會解析成對應組件；一般資產記錄最近的父資料夾組件邊界。搬移資產時，`changes.json` 和 `work-queue.json` 以 GUID 報告舊／新路徑。只有來源或組件上下文指紋改變，才需要重新分析。

## 索引範圍

只遞迴以下專案自有來源：

- `Assets/[TorbenJuniorUtility]/`
- `ProjectSettings/`
- `dotnet/`
- `docs/`（排除收件匣、封存與忽略資料）
- `tools/`
- 根目錄的 `AGENTS.md`、`CLAUDE.md`、`README.md`、`INDEX_WORKFLOW.md`、`.gitignore`
- `Packages/manifest.json` 與 `Packages/packages-lock.json`

任何路徑中含 `backup` 的檔案或資料夾，以及 `Library/`、`Temp/`、`Logs/`、`Obj/`、Build 輸出、`bin/`、`obj/`、`node_modules/`、`.git/`、`PackageCache/` 和封存目錄均排除。索引輸出位於被 `.gitignore` 排除的 `project_index/`。

其他 `Assets/` 目錄不在預設範圍，包含第三方資源。若日後有需要索引的自有資產根目錄，應先把明確路徑加到更新器的 `$roots` 清單，並確認排除規則涵蓋該路徑中的產生物。

## 登錄分析結果

檢視工作項目與目前內容後，使用佇列中的兩個指紋儲存分析結果：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-ProjectFileAnalysis.ps1 `
  -Path "Assets/[TorbenJuniorUtility]/Scripts/Core/Example.cs" `
  -SourceHash "<work-queue sourceHash>" `
  -ContextHash "<work-queue contextHash>" `
  -AnalysisJson '{"summary":"已確認 Example 的責任與主要呼叫關係。"}'
```

工具會比對索引指紋與來源檔目前的 SHA-256。來源在檢視後若已變更，工具會拒絕記錄過時分析；先重新更新並檢視新佇列。

## 狀態

狀態只記錄人工分類，不取代內容分析或 hash 驗證：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-ProjectFileStatus.ps1 `
  -Path "Assets/[TorbenJuniorUtility]/Scripts/Core/Example.cs" `
  -Status approved `
  -Reason "人工確認"
```

可用狀態為 `unclassified`、`draft`、`approved`、`withdrawn`、`deprecated`。資產透過 GUID 搬移時，狀態隨資產路徑遷移。
