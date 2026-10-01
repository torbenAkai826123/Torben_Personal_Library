# Unity CLI (`unity`) 與 Unity Pipeline 操作手冊

本手冊說明 Unity 官方的獨立命令列工具 `unity`，以及讓它控制 Editor 的 `com.unity.pipeline` 套件。它和 [`unity-batchmode.md`](unity-batchmode.md) 介紹的 `Unity.exe` 命令列參數是不同的工具。

> Unity CLI 與 Pipeline 套件目前都是實驗版 (CLI `1.0.0-beta`、套件 `0.x-exp`)，指令與旗標可能隨版本變動。以本機 `unity <command> --help` 的輸出為準。

## 和 `Unity.exe` 命令列的差異

| | `Unity.exe` 命令列 ([`unity-batchmode.md`](unity-batchmode.md)) | Unity CLI `unity` (本手冊) |
|---|---|---|
| 形式 | Editor 本體加 `-batchmode` 等參數 | 獨立程式，由 Unity Hub 自動安裝 |
| Editor 開著時 | 不能對同一專案執行 | 透過 Pipeline 套件連線，直接操作開著的 Editor |
| Editor 關閉時 | 自行組參數執行 batch mode | `unity test`、`unity run`、`unity build` 代為啟動 batch mode |
| 輸出 | 只有 Editor 日誌 | `--format json` 結構化結果與固定退出碼 |
| Editor 路徑 | 需手動指定 `Unity.exe` | 依 `ProjectVersion.txt` 自動找已安裝的 Editor |

## 本機現況 (2026-10-01 驗證)

| 項目 | 狀態 | 確認方式 |
|---|---|---|
| CLI | `1.0.0-beta.11`，以 MSIX 安裝 (`UnityTechnologies.UnityCLI`)，位置 `%LOCALAPPDATA%\Microsoft\WindowsApps\unity.exe` | `unity version --format json`、`unity diagnose update` |
| CLI 健康檢查 | 全部通過 (原有的安裝腳本版重複副本已移除) | `unity doctor --format json` |
| 登入 | 已登入，憑證存在 Windows 認證管理員 (`credentialSource: keyring`)。若回報未登入 (退出碼 3)，由使用者執行 `unity auth login` | `unity auth status --format json` |
| Editor | `6000.5.5f1` 已安裝，與專案版本相符 | `unity editors -i` |
| Pipeline 套件 | `0.8.0-exp.1` 已在 `Packages/manifest.json` | `unity pipeline list` |
| EditMode 測試 | `unity test` 成功，8/8 通過，退出碼 0 | 見下方「Editor 關閉時」 |
| 連線開著的 Editor | **未驗證** (驗證時 Editor 關閉) | `unity status` |

## 執行前檢查

在倉庫根目錄的 PowerShell 執行。這些指令只讀取狀態，不會改動專案。

```powershell
# 避免分頁器與橫幅干擾輸出
$env:UNITY_NO_PAGER = '1'
$env:UNITY_NO_BANNER = '1'

unity --version        # CLI 是否可用
unity auth status      # 是否已登入
unity pipeline list    # 專案的 Pipeline 套件狀態，含 Safe Mode 欄位
unity status           # 已連線的 Editor (Port、專案、版本、PID、狀態)
git status --short     # 記下原有變更，執行後比對
```

- `unity status` 有列出本專案且狀態為 `ready` → 照「Editor 開著時」操作。
- `unity status` 沒有列出 → 先排除下方「連不上的常見原因」，再照「Editor 關閉時」操作。
- 注意：`unity pipeline list` 不接受 `--project-path`，它會列出所有已知專案。

## Editor 開著時：操作執行中的 Editor

前提：專案已裝 Pipeline 套件，Editor 已開啟且編譯成功。

```powershell
unity status                                  # 確認狀態為 ready
unity command                                 # 列出此 Editor 提供的指令
unity command --query scene                   # 以關鍵字篩選指令
unity list                                    # 列出指令與參數結構 (schema)
unity command <指令名稱> [參數...]             # 執行一個指令
unity command eval '<C# 程式碼>'              # 若 Editor 提供 eval，可執行任意 C#
unity recompile                               # 重新編譯並回報編譯錯誤；--strict 連警告也算失敗
unity command run_tests [參數...]             # 在開著的 Editor 執行測試；test_status 查進度 (參數以 unity list 為準)
```

- **指令名稱由 Editor 端定義**：先用 `unity command` 或 `unity list` 查，不要猜名稱。
- **有多個 Editor 開著時**，一律加 `--project-path "<倉庫根目錄>"`。不加時，CLI 會依目前目錄判斷目標。
- **Editor 開著時不要直接改 `.unity`、`.prefab`、`.asset` 的 YAML**：Editor 不會即時套用，還可能被 Editor 的記憶體狀態覆蓋。改用 `unity command` 操作，再存檔。
- 長時間的指令可加 `--timeout <秒>` (預設 30 秒)，或用 `--detach` 轉為背景工作，再用 `unity job` 查詢。

### 連不上的常見原因

1. **Safe Mode**：專案有 C# 編譯錯誤時，Editor 會進入 Safe Mode，Pipeline 套件不會載入，`unity command`、`unity status`、`unity recompile` 都連不上 (`recompile` 會以退出碼 7 結束)。用 `unity pipeline list` 的 `Safe Mode` 欄位確認；修正編譯錯誤並重開 Editor 後再試。
2. **沙箱**：AI 工具的 shell 若在受限沙箱中執行，可能看不到實際開著的 Editor。`unity status` 沒有結果時，不能直接斷定 Editor 已關閉，應先詢問使用者。
3. `unity pipeline list` 的 `Running` 欄位可能殘留舊狀態 (本機驗證時 Editor 已關，仍顯示 `true`，但 PID 空白、`Server Reachable` 為 `false`)。以 `unity status` 與 `Server Reachable` 為準。

## Editor 關閉時：batch mode 執行

和 `Unity.exe` 相同的限制：同一專案不可同時在 Editor 與 batch mode 執行。

### 執行測試 (已驗證)

```powershell
$resultPath = Join-Path $env:TEMP 'TorbenJuniorUtility-editmode-results.xml'
unity test . --mode EditMode --output $resultPath --timeout 600 --non-interactive --format json
$exitCode = $LASTEXITCODE
```

- `--output` 指向系統暫存目錄，不要寫進 `Assets/`。不指定時預設在目前目錄產生 `test-results.xml`。
- 判斷結果：退出碼 `0` 為全部通過；`8` 為測試有失敗 (不要重試)；`6` 或 `7` 為未產生結果 (編譯錯誤、授權、當機或逾時)，可排除原因後重試。
- 和 `Unity.exe` 一樣，仍要檢查結果 XML 的 `total` 與 `failed="0"`，確認預期的測試真的有執行。
- 其他常用選項：`--filter <名稱>`、`--mode PlayMode`、`--report-format junit`、`--rerun-failed`。完整清單見 `unity test --help`。

### 其他 batch 指令

```powershell
# 執行 Editor 腳本的靜態方法 (等同 Unity.exe 的 -executeMethod)
unity run . --log-file "$env:TEMP\unity-run.log" -- -executeMethod Namespace.Class.Method

# 建置；先用 --list-targets 查可用平台
unity build . --list-targets
```

`--` 之後的參數會直接轉給 Editor，寫法與 `unity run --help` 的範例相同；但 `unity run` 與 `unity build` 尚未在本機實際執行過。

## 輸出與退出碼

- 自動化一律加 `--format json`。結果寫在 stdout，以 `success` 欄位與退出碼判斷，錯誤代碼在 `errors[0].code`；不要解析 stderr。
- 輸出導向管線或檔案時，預設格式會變成 TSV。

| 退出碼 | 意義 |
|---|---|
| 0 | 成功 |
| 1 | 一般錯誤 |
| 2 | 參數錯誤 |
| 3 | 驗證失敗 (未登入或登入過期) |
| 4 | 缺少必要設定 |
| 6 | 主要操作失敗；`unity test` 表示沒有產生測試結果 |
| 7 | 服務或執行中的 Editor 連不上，可重試 |
| 8 | 僅 `unity test`：測試有執行且有失敗 |

## 需先取得使用者同意的指令

下列指令會改變環境、專案或使用者的工作狀態，依 `AGENTS.md`，未經同意不要執行：

| 指令 | 影響 |
|---|---|
| `unity close` | 關閉 Editor，**不會存檔**，`--force` 也一樣 |
| `unity pipeline install` / `upgrade` | 修改 `Packages/manifest.json` |
| `unity self-update`、`unity install`、`unity uninstall`、`unity install-modules` | 更新 CLI 或安裝、移除 Editor |
| `unity auth login` / `logout` | 需要使用者在瀏覽器登入，或會登出使用者 |
| `unity mcp configure`、`unity skill install` | 修改 AI 工具的設定檔 |
| `unity vcs` 底下會寫入的子指令 (`setup`、`sync`、`switch`、`hooks` 等) | 改動 Git 倉庫或遠端 |

## 已知注意事項

- **batch mode 可能改到 `ProjectSettings/`**：2026-10-01 執行 `unity test` 後，當時安裝的 `com.unity.ai.inference` 套件 (之後已移除) 自動從 `ProjectSettings/ProjectSettings.asset` 的 `scriptingDefineSymbols` 移除了 `SENTIS_ANALYTICS_ENABLED`。執行後務必用 `git status` 比對；這類變更不是任務成果，應回報使用者決定保留或還原。- CLI 每次執行會送出一筆匿名使用量資料；設定 `UNITY_NO_CRASH_REPORT` 只會關閉當機回報。
- `unity skill show` 會印出 CLI 內建、給 AI 代理用的完整操作指南，版本更新後可用它核對本手冊。

## 參考

- [Introduction to the Unity CLI](https://docs.unity.com/en-us/unity-cli/unity-cli)
- [Use the Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [Unity CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Install Pipeline package](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)
- [Replace MCP Server with Unity CLI](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)
