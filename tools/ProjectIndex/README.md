# ProjectIndex 製作指令

本檔是索引工具的規格，給要修改或重新實作此工具的人 (或 AI) 參考。日常的使用時機與查詢方式見倉庫根的 [`INDEX_WORKFLOW.md`](../../INDEX_WORKFLOW.md)。

## 平台現況

| 實作 | 位置 | 需求 | 驗證 |
|---|---|---|---|
| 跨平台 (C# 單檔程式) | `ProjectIndex.cs` | .NET 10 SDK | macOS 已驗證 (2026-10-30)；Windows 未實測 |

dotnet 是本專案執行 Core 測試的必備工具，所以優先使用這個跨平台版本。只有在某個系統上無法用 dotnet 執行時，才依本規格另做該系統的版本，放在 `ProjectIndex/{OS}/`，並在上表新增一列。

## 執行方式

```bash
dotnet tools/ProjectIndex/ProjectIndex.cs <command> [--option value] [--flag]
```

- 第一次執行或修改本檔後要重新編譯 (約 9 秒)，之後約 0.3 秒。編譯產物在 dotnet 的使用者快取，不寫入倉庫。
- 預設專案根是本檔往上兩層的目錄；`--project-root <path>` 可指定其他專案根 (例如在複本上測試)。
- 成功時退出碼為 0；參數錯誤或驗證失敗時在 stderr 輸出 `error: <訊息>`，退出碼為 1。
- 參數名稱不分大小寫。

| 命令 | 參數 | 行為 |
|---|---|---|
| `update` | 無 | 重新掃描來源根，寫入 `project_index/` 的 6 個 JSON 檔；stdout 輸出兩行摘要，警告寫到 stderr (`WARNING: ...`) |
| `find` | `--path`、`--assembly`、`--guid` (至少一個)、`--include-meta`、`--detail`、`--limit 1..10000` (預設 50) | 只讀查詢，不寫入任何檔案 |
| `set-analysis` | `--path`、`--source-hash`、`--context-hash`、`--analysis-json` (皆必填) | 指紋與目前來源相符時才寫入 `analysis-cache.json` |
| `set-status` | `--path`、`--status`、`--reason` (選填) | 寫入 `states.json`，並在 `status-history.jsonl` 附加一行 |

## 掃描範圍

- 遞迴來源根：`Assets/[TorbenJuniorUtility]/` (`unity-library-assets`)、`ProjectSettings/` (`unity-project-settings`)、`dotnet/` (`dotnet-source-and-projects`)、`docs/` (`project-documentation`)、`tools/` (`project-tools`)。
- 單檔來源根：根目錄的 `AGENTS.md`、`CLAUDE.md`、`README.md`、`INDEX_WORKFLOW.md`、`.gitignore` (`project-guidance`)，以及 `Packages/manifest.json`、`Packages/packages-lock.json` (`unity-package-manifest`)。不存在的根直接略過。
- 包含隱藏檔。路徑一律用相對於專案根、以 `/` 分隔的形式，比對時不分大小寫。
- 排除規則 (任一路徑段符合即排除)：
  - 名稱含 `backup`，或為 `archive`／`_archive` (不分大小寫)。
  - 名稱為 `.git`、`.vs`、`.DS_Store`、`PackageCache`、`Obj`、`Build`、`Builds`、`bin`、`obj`、`node_modules`、`project_index`。
  - 第一層名稱為 `Library`、`Temp`、`Logs`、`UserSettings` (Unity 只在專案根產生這些資料夾)。
  - 路徑以 `docs/inbox`、`docs/archive`、`docs/ignore` 開頭。

## 輸出格式

全部寫在 `project_index/`，`schemaVersion` 為 2，`analysisVersion` 為 1。JSON 以 UTF-8 (無 BOM)、縮排輸出，先寫暫存檔再取代原檔。時間用 ISO 8601 UTC (`DateTime.ToString("o")`)。`files` 物件的鍵依路徑排序 (不分大小寫)。

| 檔案 | 主要欄位 |
|---|---|
| `manifest.json` | `schemaVersion`、`generatedAt`、`projectRoot: "."`、`roots[{path, name}]` |
| `content.json` | `scope` (同 `roots`)、`files{路徑: 檔案項目}`、`assemblies{asmdef／asmref 路徑: 組件資訊}` |
| `changes.json` | `added`、`modified`、`removed`、`moved[{guid, from, to, contentChanged}]`、`unchangedCount`、`indexedCount`、`unreviewedCount`、`warnings` |
| `work-queue.json` | `contentReview[{path, reason, sourceHash, contextHash, kind, assetGuid, assembly}]`、`locationChanges` (同 `moved`)、`removed`、`unreviewedCount` |
| `analysis-cache.json` | `files{路徑: {sourceHash, contextHash, analysisVersion, analyzedAt, analysis}}` |
| `states.json` | `updatedAt`、`files{路徑: {status, updatedAt, reason}}` |
| `status-history.jsonl` | 每行 `{changedAt, path, from, to, reason, contentRead: false}` |

檔案項目的欄位：

- `sha256`：檔案內容的 SHA-256，小寫十六進位。
- `sizeBytes`、`modifiedAt`：檔案大小與最後寫入時間 (UTC)。
- `kind`：依副檔名分類。`.cs` 為 `csharp`；`.asmdef` 為 `assembly-definition`；`.asmref` 為 `assembly-reference`；`.meta` 為 `unity-meta`；Unity 序列化資產 (`.unity`、`.prefab`、`.asset` 等) 為 `unity-serialized-asset`；二進位檔 (圖片、字型、音訊、模型、壓縮檔等) 為 `binary-asset`；`.csproj`／`.sln`／`.props`／`.targets` 為 `dotnet-project`；`.md`／`.mdc` 為 `documentation`；`.ps1` 為 `powershell`；其他為 `configuration-or-data`。完整清單以 `ProjectIndex.cs` 為準。
- `reviewable`：不是 `.meta`、也不是二進位檔時為 `true`。
- `assetGuid`：Unity 資產根內，同名 `.meta` 的 `guid:` 值；沒有時為 `null`。
- `unityGuid`：一般檔案同 `assetGuid`；`.meta` 檔則是它所描述資產的 GUID。
- `assembly`：只用於 Unity 資產根內的檔案。從檔案所在目錄往上找最近的 asmdef／asmref，記錄 `{name, kind: "asmdef"|"asmref", path, guid, references}`；找不到時為 `null`，不推算成 `Assembly-CSharp`。asmref 的 `GUID:<guid>` 要對應到 asmdef 的名稱，無法解析時名稱為 `<unresolved>` 並記一筆警告。
- `contextHash`：把 `{kind, assembly, assetGuid}` 序列化成精簡 JSON 後取 SHA-256。這個值只需要在同一實作中保持穩定；換實作後舊快取失效是可接受的。

## 更新規則

1. 一個 GUID 被多個檔案使用時，記錄警告 `Duplicate Unity GUID; move detection disabled for <guid>`，該 GUID 不參與搬移偵測。
2. 搬移：同一個 `assetGuid` 在前一次和這一次索引的路徑不同時，視為搬移，不計入新增與刪除。有效的分析快取要複製到新路徑，人工狀態也移到新路徑。
3. 有效快取：`sourceHash` 等於 `sha256`、`contextHash` 相符、`analysisVersion` 為 1，而且 `analysis` 不是 `null`。
4. `contentReview` 收錄沒有有效快取的可分析檔案，`reason` 依序判斷：前次沒有這個路徑時，是搬移就填 `moved`，否則填 `added`；內容改變填 `modified`；組件歸屬改變填 `context-changed`；其餘填 `uncached`。這份清單不是待辦事項。
5. `states.json` 的 `updatedAt` 沿用前一次的值，只有 `set-status` 會更新它。

## 寫入命令的檢查

- `--path` 必須是專案相對路徑，不可以 `/` 開頭，也不可含 `.` 或 `..` 路徑段，而且要存在於目前的索引中 (不分大小寫)。
- `set-analysis`：條目必須是 `reviewable`，傳入的兩個指紋要和索引相符，而且來源檔目前的 SHA-256 也要相符，否則拒絕寫入。`--analysis-json` 必須是 JSON 值，不能是 `null`。
- `set-status`：`--status` 只接受 `unclassified`、`draft`、`approved`、`withdrawn`、`deprecated` (不分大小寫，儲存時用小寫)。不讀取來源內容。

## 驗證情境

修改或重新實作後，在倉庫複本上 (用 `--project-root` 指定) 至少確認下列情境：

1. 初次 `update` 收錄的檔案，和 `git ls-files` 在掃描範圍內列出的檔案一致；`.DS_Store` 不在索引中，`Scripts/Core/Logs/` 有收錄。
2. `find` 的路徑、組件 (含 asmref 的 `(asmref)` 標記)、資料夾 GUID 三種查詢，以及超過 `--limit` 時的提示。
3. 不給條件、GUID 格式錯誤、狀態值錯誤時，退出碼為 1。
4. `set-analysis` 與 `set-status` 寫入後再 `update`，快取仍然有效。
5. 連同 `.meta` 一起搬移檔案後，`moved` 有記錄，快取與狀態都跟著到新路徑。
6. 來源被修改後，用舊指紋執行 `set-analysis` 會被拒絕。
7. 複製 `.meta` 造成重複 GUID 時，`update` 和相關的 `find` 都會輸出警告。
