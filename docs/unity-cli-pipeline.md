# Unity CLI (`unity`) 與 Unity Pipeline 操作手冊

本手冊說明 Unity 官方的獨立命令列工具 `unity`，以及讓它控制 Editor 的 `com.unity.pipeline` 套件。它和 [`unity-batchmode.md`](unity-batchmode.md) 介紹的 Editor 執行檔命令列參數是不同的工具。

> Unity CLI 與 Pipeline 套件目前都是實驗版 (CLI `1.0.0-beta`、套件 `0.x-exp`)，指令與旗標可能隨版本變動。以本機 `unity <command> --help` 的輸出為準。

## 和 Editor 執行檔命令列的差異

| | Editor 執行檔命令列 ([`unity-batchmode.md`](unity-batchmode.md)) | Unity CLI `unity` (本手冊) |
|---|---|---|
| 形式 | Editor 本體加 `-batchmode` 等參數 | 獨立程式 |
| Editor 開著時 | 不能對同一專案執行 | 透過 Pipeline 套件連線，直接操作開著的 Editor |
| Editor 關閉時 | 自行組參數執行 batch mode | `unity test`、`unity run`、`unity build` 代為啟動 batch mode |
| 輸出 | 只有 Editor 日誌 | `--format json` 結構化結果與固定退出碼 |
| Editor 路徑 | 需手動指定 (macOS 為 `Unity.app/Contents/MacOS/Unity`) | 依 `ProjectVersion.txt` 自動找已安裝的 Editor |

## 本機現況 (macOS，2026-10-30 驗證)

| 項目 | 狀態 | 確認方式 |
|---|---|---|
| CLI | `1.0.0-beta.15` (darwin arm64)，以 Homebrew 安裝，位置 `/opt/homebrew/bin/unity` | `unity version --format json` |
| CLI 健康檢查 | 無失敗項目 (PATH 與憑證存放的檢查顯示為略過) | `unity doctor --format json` |
| 登入 | 已登入，憑證存在 macOS 鑰匙圈 (`credentialSource: keyring`)。若回報未登入 (退出碼 3)，由使用者執行 `unity auth login` | `unity auth status --format json` |
| 授權 | Unity Personal 已啟用。登入不等於啟用授權，每台機器要各自啟用；沒有授權時 Editor 會以結束碼 198 停止 (`EDITOR_LICENSING_FAILURE`)，由使用者執行 `unity license activate --personal --accept-eula` | `unity license status` |
| Editor | `6000.5.5f1` (arm64) 已安裝，與專案版本相符，位置 `/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app`；已裝模組：Android (含 SDK、NDK、OpenJDK)、Mac Standalone。**沒有 iOS 與 Windows 建置模組** | `unity editors -i` |
| Pipeline 套件 | `0.8.0-exp.1` 已在 `Packages/manifest.json`，CLI 回報有新版可升級 (未升級) | `unity pipeline editors` |
| 連線開著的 Editor | 已驗證：`unity status` 列出本專案，狀態 `ready` (Editor 由使用者以 `unity open .` 開啟) | `unity status` |
| EditMode 測試 (開著的 Editor) | 已驗證：8/8 通過 | `unity command run_tests --mode editor` |
| PlayMode 測試 (開著的 Editor) | 已驗證：7/7 通過 (需非同步執行，見下方) | `unity command run_tests --mode playmode --async_tests true` |
| `unity test` (batch mode) | **失敗**：授權啟用後，首次匯入時 Editor 在 Burst 編譯器中以 SIGBUS 當機，未產生結果 (退出碼 6)。原因未確認，推測是 AI 工具的 shell 沙箱擋住 Burst 產生的原生程式碼；Windows 上曾於 2026-10-01 跑出 8/8 通過 | 見下方「Editor 關閉時」 |

## 執行前檢查

在倉庫根目錄執行。這些指令只讀取狀態，不會改動專案。

```bash
# 避免分頁器與橫幅干擾輸出
export UNITY_NO_PAGER=1 UNITY_NO_BANNER=1

unity --version          # CLI 是否可用
unity auth status        # 是否已登入
unity license status     # 是否有啟用中的授權 (batch mode 需要)
unity pipeline editors   # 各專案的 Pipeline 套件狀態，含 Safe Mode 欄位
unity status             # 已連線的 Editor (Port、專案、版本、PID、狀態)
git status --short       # 記下原有變更，執行後比對
```

- `unity status` 有列出本專案且狀態為 `ready` → 照「Editor 開著時」操作。
- `unity status` 沒有列出 → 先排除下方「連不上的常見原因」，再照「Editor 關閉時」操作。
- 注意：舊版的 `unity pipeline list` 在 `1.0.0-beta.15` 已改名為 `unity pipeline editors`。它會列出所有已知專案，不只本專案。

## Editor 開著時：操作執行中的 Editor

前提：專案已裝 Pipeline 套件，Editor 已開啟且編譯成功。

```bash
unity status                                  # 確認狀態為 ready
unity command                                 # 列出此 Editor 提供的指令
unity command --query scene                   # 以關鍵字篩選指令
unity command --query run_tests --detail full --format json   # 查指令的參數結構 (schema)
unity commands --grep <關鍵字>                 # 搜尋 CLI 本身的指令 (不需連線 Editor)
unity command <指令名稱> [--參數 值...]         # 執行一個指令
unity command eval '<C# 程式碼>'              # 若 Editor 提供 eval，可執行任意 C#
unity recompile                               # 重新編譯並回報編譯錯誤；--strict 連警告也算失敗
```

- **指令名稱由 Editor 端定義**：先用 `unity command` 或 `unity command --query <關鍵字>` 查，不要猜名稱。舊版的 `unity list` 在 `1.0.0-beta.15` 已移除，參數結構改用 `--query` 加 `--detail full` 查詢。
- **有多個 Editor 開著時**，一律加 `--project-path "<倉庫根目錄>"`。不加時，CLI 會依目前目錄判斷目標。
- **Editor 開著時不要直接改 `.unity`、`.prefab`、`.asset` 的 YAML**：Editor 不會即時套用，還可能被 Editor 的記憶體狀態覆蓋。改用 `unity command` 操作，再存檔。
- 長時間的指令可加 `--timeout <秒>` (預設值依指令而定；連線 Player 或舊版 Pipeline 時為 30 秒)，或用 `--detach` 轉為背景工作，再用 `unity job` 查詢。

### 在開著的 Editor 執行測試 (macOS 上已驗證)

`run_tests` 的參數 (`0.8.0-exp.1`)：`--mode` 為 `all`、`editor`、`playmode` (預設 `all`)；`--filter` 與 `--filter_type` (`testName`、`assembly`、`category`) 篩選測試；`--timeout` 預設 300 秒；`--async_tests true` 改為非同步執行。

```bash
# EditMode：可同步執行，直接回傳結果
unity command run_tests --mode editor --timeout 600 --format json

# PlayMode：必須非同步執行，再查詢結果
unity command run_tests --mode playmode --async_tests true --format json
unity command test_status --format json   # status 從 running 變成 completed 後讀 summary
```

- **PlayMode 不能同步執行**：進入 Play Mode 會重新載入 domain，HTTP 請求會斷掉。同步執行時，CLI 外層仍回報 `success: true` 且退出碼為 0，但 `data.result.success` 為 `false`、`Total` 為 0，錯誤訊息在 `data.result.error`。
- 因此判斷 `run_tests` 結果時，不能只看退出碼與外層 `success`，要檢查 `data.result.success` 與 `Summary` (非同步時為 `test_status` 的 `summary`) 的 `Total`、`Failed`。

### 連不上的常見原因

1. **Safe Mode**：專案有 C# 編譯錯誤時，Editor 會進入 Safe Mode，Pipeline 套件不會載入，`unity command`、`unity status`、`unity recompile` 都連不上 (`recompile` 會以退出碼 7 結束)。用 `unity pipeline editors` 的 `safeMode` 欄位確認；修正編譯錯誤並重開 Editor 後再試。
2. **沙箱**：AI 工具的 shell 若在受限沙箱中執行，可能看不到實際開著的 Editor。`unity status` 沒有結果時，不能直接斷定 Editor 已關閉，應先詢問使用者。
3. `unity pipeline editors` 的 `isRunning` 欄位可能殘留舊狀態 (Windows 與 macOS 驗證時都出現過：`isRunning` 為 `true`，但 `pid` 空白、`serverReachable` 為 `false`)。以 `unity status` 與 `serverReachable` 為準。

## Editor 關閉時：batch mode 執行

和 Editor 執行檔命令列相同的限制：同一專案不可同時在 Editor 與 batch mode 執行。

### 執行測試 (Windows 上已驗證；macOS 上首次匯入時當機，見「已知注意事項」)

```bash
resultPath="${TMPDIR:-/tmp}/TorbenJuniorUtility-editmode-results.xml"
unity test . --mode EditMode --output "$resultPath" --timeout 600 --non-interactive --format json
exitCode=$?
```

- `--output` 指向系統暫存目錄，不要寫進 `Assets/`。不指定時預設在目前目錄產生 `test-results.xml`。
- 判斷結果：退出碼 `0` 為全部通過；`8` 為測試有失敗 (不要重試)；`6` 或 `7` 為未產生結果 (編譯錯誤、授權、當機或逾時)，可排除原因後重試。
- 和 Editor 執行檔命令列一樣，仍要檢查結果 XML 的 `total` 與 `failed="0"`，確認預期的測試真的有執行。
- 其他常用選項：`--filter <名稱>`、`--mode PlayMode`、`--report-format junit`、`--rerun-failed`。完整清單見 `unity test --help`。

### 其他 batch 指令

```bash
# 執行 Editor 腳本的靜態方法 (等同 Editor 執行檔的 -executeMethod)
unity run . --log-file "${TMPDIR:-/tmp}/unity-run.log" -- -executeMethod Namespace.Class.Method

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

- **batch mode 可能改到 `ProjectSettings/`**：2026-10-01 執行 `unity test` 後，當時安裝的 `com.unity.ai.inference` 套件 (之後已移除) 自動從 `ProjectSettings/ProjectSettings.asset` 的 `scriptingDefineSymbols` 移除了 `SENTIS_ANALYTICS_ENABLED`。執行後務必用 `git status` 比對；這類變更不是任務成果，應回報使用者決定保留或還原。
- **macOS 上 batch mode 首次匯入當機**：2026-10-30 在 AI 工具的 shell (macOS 沙箱) 中執行 `unity test`，Editor 匯入 shader 時於 Burst 編譯器 (`Burst.Compiler.IL.Server.LibraryCompiler:CompileMethodGroupImpl`) 以 SIGBUS 當機，退出碼 6。由使用者在一般終端機以 `unity open .` 開啟並完成匯入後，改用開著的 Editor 執行測試即正常。沙箱是否為原因尚未驗證。
- **macOS 上開啟專案會產生換行符號差異**：`.gitattributes` 設定 `* text=auto eol=crlf`，但 macOS 上的 Unity 存檔時寫 LF，被重新存檔的資產會出現在 `git status`，`git diff` 卻沒有內容差異。這類檔案屬於 `AGENTS.md` 的「不用提交」：`git add` 後不會產生變更，`git status` 的標記也會消失。
- **動態字型會在顯示文字時自動補字**：TextMeshPro 字型資源若為動態模式 (`m_AtlasPopulationMode: 1`)，Editor 或 PlayMode 測試顯示文字時會把缺少的字與圖集寫回 `SDF.asset`；開著的場景若用到該字型，Clear Dynamic Data 後也會立刻補回。這是預期雜訊，依 `AGENTS.md` 歸為「不獨立提交」，只有異常時才回報。
- **日誌含存取權杖**：`unity test` 寫入 `~/Library/Logs/Unity/Editor.log` 的啟動參數含 `-accessToken`。分享日誌前先移除該行。
- CLI 每次執行會送出一筆匿名使用量資料；設定 `UNITY_NO_CRASH_REPORT` 只會關閉當機回報，`UNITY_NO_CLI_INVOKED_TELEMETRY` 可關閉使用量 ping。
- `unity skill show` 會印出 CLI 內建、給 AI 代理用的完整操作指南，版本更新後可用它核對本手冊。

## 參考

- [Introduction to the Unity CLI](https://docs.unity.com/en-us/unity-cli/unity-cli)
- [Use the Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [Unity CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Install Pipeline package](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)
- [Replace MCP Server with Unity CLI](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)
