# Unity Editor 批次操作手冊

本手冊針對 Unity Editor 的 `Unity.exe` 命令列介面，不是 Unity 官方獨立的 `unity` CLI。專案版本以 `ProjectSettings/ProjectVersion.txt` 為準，目前是 `6000.5.5f1`。以下命令在 PowerShell 中執行；`Unity.exe` 的安裝位置因電腦而異，不把範例路徑當成已確認的本機路徑。若要用獨立的 Unity CLI 控制 Editor，需另依 [Unity Pipeline 官方指引](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)設定。

## 執行前

1. 從倉庫根目錄執行 `git status --short`，記下原有變更；確認本次允許修改的檔案與完成判準。
2. 在 Unity Hub 找到與專案版本相符的 Editor，將其 `Unity.exe` 絕對路徑填入下方的 `$unityExe`。用 `Test-Path -LiteralPath $unityExe` 與 `& $unityExe -version` 核對，版本不符時不要開啟此專案。
3. 確認同一專案沒有在 Unity Editor 開啟。Unity 不允許該專案同時供 Editor 與 batch mode 使用；若使用者正在編輯，先協調可用時機，不要擅自關閉 Editor 或刪除鎖定檔。
4. 將日誌與測試結果輸出到系統暫存目錄，不寫入 `Assets/`、專案的 `Logs/` 或 `Library/`。若找不到已設定的 `Unity.exe`，直接回報阻礙；若授權驗證失敗，依下方有限重試流程處理。兩者都不能宣稱完成 Unity 驗證。

```powershell
$projectRoot = (Resolve-Path -LiteralPath '.').Path
$unityExe = 'E:\Program Files\Unity\Editors\6000.5.5f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unityExe)) { throw "找不到 Unity.exe: $unityExe" }
& $unityExe -version
```

## 執行測試

專案有對應的 Unity Test Framework 測試時，才執行下列範例。`-testPlatform EditMode` 可按測試類型改為 `PlayMode`；`-testResults` 寫出 XML。執行 `-runTests` 時不要加入 `-quit`，Unity 文件指出它可能使測試提前退出。

```powershell
$runDir = Join-Path $env:TEMP 'TorbenJuniorUtility-editor-batch'
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$logPath = Join-Path $runDir 'editor.log'
$resultPath = Join-Path $runDir 'editmode-results.xml'
$arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'), '-runTests',
    '-testPlatform', 'EditMode', '-testResults', ('"' + $resultPath + '"'),
    '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $unityExe -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
$exitCode = $process.ExitCode
Write-Host "Unity exit code: $exitCode"
```

檢查退出碼、日誌錯誤與結果 XML；單憑程序退出或沒有錯誤輸出，不代表測試通過。測試須確認 XML 中預期測試有執行、`failed=0`，且程序退出碼為 `0`。若 XML 未產生、沒有執行到預期測試或程序非正常退出，將該次結果列為失敗或未驗證。

## 授權失敗與有限重試

若日誌出現 `No valid Unity Editor license found`（本機曾伴隨退出碼 `198`），先保存該次日誌與退出碼，確認實際使用的 Editor 版本、Unity Hub 授權狀態、同專案是否仍開啟，以及命令是否在受限環境執行。`Access token is unavailable` 或 `com.unity.editor.headless` 訊息單獨出現時，不足以判定授權無效；本機成功執行時也曾出現前者。

只有在可指出已改變的條件時才重試：例如使用者已完成 Hub 登入，或已取得權限讓**同一個已設定的** `Unity.exe` 在可存取授權服務的環境執行。必要的權限須先取得；同一條件下不要反覆重跑。重試一次後仍失敗，或現有工具都不能用，就停止並回報日誌位置、退出碼、已確認的原因與尚缺條件。不要自行搜尋、下載、安裝或切換到未設定的 CLI、套件或服務。

## 自訂資源操作

需要 Unity API 修改場景、Prefab 或匯入設定時，可在任務授權範圍內新增位於 `Assets/[TorbenJuniorUtility]/Scripts/Editor/` 的靜態方法，再用 `-executeMethod Namespace.Class.Method` 呼叫。該目錄與方法目前並非既有設施，不得直接套用不存在的方法名稱。一般批次操作可用 `-batchmode -projectPath <專案根> -executeMethod <方法> -logFile <日誌> -quit`；`-executeMethod` 的失敗須藉由例外或非零退出碼傳回，並檢查日誌。不得藉批次執行繞過 `AGENTS.md` 對 `Packages/`、第三方資源及其他禁止範圍的限制。

## 執行後

再次執行 `git status --short` 與目標檔案的差異檢查，對照執行前紀錄。即使場景、圖片或結果檔已寫出，退出碼非零或日誌顯示崩潰時，該次執行仍算失敗；檔案只能作為待驗證產物。若後續用現有工具獨立驗證通過，可交付該產物，但須揭露先前失敗與後續驗證證據。只交付本次授權且經驗證的變更；不要重置使用者原有變更，也不要手動清除 Unity 產生的暫存檔。回報實際命令、Editor 版本、退出碼、日誌／結果位置、完成判準的證據與未驗證項目。

參考：[Unity Editor 命令列參數](https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html)。本專案 `Packages/manifest.json` 指定 `com.unity.test-framework` 版本 `1.7.0`；使用其他測試選項前，須核對該版本文件。
