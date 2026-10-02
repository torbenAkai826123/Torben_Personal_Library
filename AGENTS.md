# Agent 優先讀取（`[TorbenJuniorUtility]`）

## 專案地圖

- **專案根**：`Assets/[TorbenJuniorUtility]/`（下文「專案根」均指此路徑）
- 主要內容：專案根（底下含 `Scripts/` 等）
- 慣例：遊戲／工具程式優先放在專案根下的 `Scripts/`（以既有目錄名為準，不存在才新建），不要散落在 `Assets/` 根層或其他套件目錄
- 先讀：專案根內 README／docs（若有）；產生的說明預設放倉庫根的 `./docs/`
- 請求收件匣：`docs/inbox/`；格式與處理方式見 [`docs/inbox/README.md`](docs/inbox/README.md)
- 通常不改：`Packages/`、第三方 Asset、名稱含 `backup` 的目錄（除非任務點名）
- 命名原則：`.\Assets\[TorbenJuniorUtility]\NamingRule.md` 若有建立原則檔案，優先以該檔案原則優先，沒有則依同類型最小必要原則命名。

## 專案檔案索引

- 以索引協助檔案定位或盤點前，先在倉庫根目錄執行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Update-ProjectIndex.ps1`，再讀 `project_index/work-queue.json` 與 `project_index/content.json`。
- 索引範圍、排除項目、GUID／asmdef／asmref 欄位及工作佇列規則見 [`INDEX_WORKFLOW.md`](INDEX_WORKFLOW.md)。
- 索引是定位與快取提示，不限制為完成當前任務閱讀檔案；依任務需要可直接檢視未變更或未列入工作佇列的來源檔案。不得把「未變更」解讀為「不得閱讀」。
- 更新器每次都重新列舉核准來源範圍並計算指紋；可偵測 Unity Editor 關閉期間手動新增、修改、刪除及搬移的檔案。不得掃描或手動修改 Unity 產生的 `Library/` 等快取。
- 新檔案／修改檔案的工作項目會一直保留，直到 `analysis-cache.json` 有相同來源指紋、分析上下文指紋與分析版本的結果。記錄分析結果使用 `tools/Set-ProjectFileAnalysis.ps1`。

## 協作原則（必做）

通案原則；可複製到其他專案。

1. **以可驗證目標驅動**  
   先從請求與上下文整理目標、可觀察的完成判準及硬性限制，再選擇必要步驟。目標或判準無法驗證時，先釐清，不自行編造。會改玩法／資料／架構／可觀察行為時，先一句話說明目的與應避免的結果；純問答或明確機械修改可省略。術語以對應 Spec 為準；無 Spec 時以本檔與現有 README 為準。

2. **在已授權範圍內解決阻礙**  
   檔案或實測否證前提、指定做法行不通時，附證據說明並判斷能否在原目標與限制內修正。能用已設定且可用的工具修正、結果可驗證時直接處理並回報；已知可用工具都無法完成時停下來，回報阻礙與所需條件，不為達標而搜尋、安裝或改用環境中未設定的工具、套件或服務。需要改變目標、明確指定的做法、不可逆取捨或超出授權範圍時，請使用者決定。不要把尚未查證的猜測當作事實。

3. **能資料／參數達成時，先指旋鈕**  
   若同類效果可用 Inspector／Prefab／關卡或資料設定達成，先指出欄位位置與建議值（必要時附預期現象），非必要不改程式邏輯。使用者已要求代改且範圍明確、工具可用時直接執行；範圍不明且會實質改變結果時才詢問。

4. **行為要申報；意外當缺陷**  
   交付時列出本次新增或改變的可觀察行為（限實際改到的，以及已知會連帶影響的；不確定就寫不確定，勿窮舉）。未申報卻出現的行為變更，一經發現視為缺陷：回報並修正或補同意；不以「當初沒預見」免責。不得為達目的附帶未要求功能。

5. **範圍明講，完成後驗證**  
   不用「最小改動」代替清單。實作前列預計改動的檔案或欄位、禁止範圍與完成判準；執行中若發現達標必需的同範圍檔案，可說明原因後一併處理，不必逐檔請示。涉及額外功能、被禁止的檔案、不可逆操作或不同目標時先詢問。診斷或檢視本身不構成改檔授權；交付時用測試、檔案差異或實際操作驗證判準，未能驗證要明說。

## Goal driven 請求

- `docs/inbox/` 是人工提交請求的入口，不代表自動監看或自動執行。讀取請求後，依本檔規則判斷目標、授權範圍與完成判準。請求檔的處理中、封存與未完成標記，依 [`docs/inbox/README.md`](docs/inbox/README.md)。
- 請求至少寫明「要達成什麼」及「如何確認完成」；檔案清單、建議做法與限制可補充，但建議做法不應取代目標。參考 [`docs/goal-driven-example.md`](docs/goal-driven-example.md)。
- 能從現有資料推得的次要細節可採合理假設並說明；缺少資訊會實質改變結論或行動方向時，先釐清。完成後報告結果、驗證證據、實際改動與已知限制。

## Unity Editor 批次操作規則

- 用語：本專案的「Unity CLI」專指 Unity 官方的 `unity` 命令列工具，操作方式見 [`docs/unity-cli-pipeline.md`](docs/unity-cli-pipeline.md)；用它時僅以已知設定確認其安裝、登入與 Unity Pipeline 連線，缺少其中任何一項就回報，不自行搜尋或安裝。本節其餘規則針對以 `Unity.exe` 命令列參數 (`-batchmode` 等) 直接啟動 Editor 的批次操作；請求寫「batch mode」或「Unity.exe」時用此方式。
- 執行前讀 [`docs/unity-batchmode.md`](docs/unity-batchmode.md)；依 `ProjectSettings/ProjectVersion.txt` 核對 Editor 版本，確認 `Unity.exe` 實際路徑、專案根路徑、Git 原有變更及同專案是否已在 Editor 開啟。不可讓同一專案同時在 Editor 與 batch mode 執行；不要擅自關閉使用者的 Editor。
- 命令列操作仍受本檔的目標與檔案範圍限制。已授權範圍內的路徑、編譯或測試阻礙可自行排除並回報；需要改變目標、關閉使用中的 Editor、修改禁止範圍或做不可逆操作時才詢問。
- 以 `-projectPath` 指向倉庫根目錄；記錄退出碼、Editor 日誌及測試結果。執行前後比較檔案差異，區分原有變更與本次變更。Unity 產生的 `Library/` 等暫存內容不得手動修改或提交；對意外變更不可逕自覆蓋、重置或當成本次成果。
- 授權失敗時依手冊保留日誌並判斷是否有可排除的環境原因；只在條件確實改變且取得必要執行權限時，用同一已設定工具重試一次。產物已寫出不代表成功：退出碼非零、程序崩潰或測試結果缺失時，該次執行視為失敗；產物須另行驗證，未驗證前不得宣稱完成。
- 自訂 `-executeMethod` 必須先有符合範圍的 `Editor` 目錄靜態方法；不可假設方法或測試已存在。執行 `-runTests` 時不要加 `-quit`，以免測試提前結束。

## 注意

- 未要求時：不要主動新增說明文件、不要重構範圍外程式、不要升級 Unity／套件。
- 不要改：`Library/`、`Temp/`、`Logs/`、`Obj/`、名稱含 `backup` 的目錄，以及第三方 Asset／`Packages/`（除非任務點名）。
- Unity：新增／移動資源時一併處理對應 `.meta`；不要無故刪改無關 `.meta`。
- 產生的說明預設只放倉庫根 `./docs/`；不要在專案根或 `Assets/` 到處開 markdown。

## 書寫慣例

- 註解中文：半形標點 + 空格（避免混用全形標點）。 
- 委派點：Runtime 中需在 Inspector 設定的事件，優先用 `UnityEvent`（含泛型）＋ lambda；Core 與熱路徑使用 `System.Action`／`System.Func` 或自訂 delegate。
- 必須用 `System` 時寫完整名，勿單獨 `using System;`（Core 不受此限）。

## 函式庫架構

- Core：純 C#，位於 `Scripts/Core/`，不得引用 `UnityEngine`／`UnityEditor`；以 `dotnet test` 測試（見 `docs/dotnet-test.md`）。
- Runtime：只放必須使用 Unity API 的部分，盡量精簡；主要以 PlayMode 測試。
- Editor：Editor 專用工具。
- 新功能先判斷能否放進 Core，不行才放 Runtime。
- dotnet 測試只驗證邏輯正確性；GC 與效能數字以 Unity Performance Testing 為準。
- 依賴方向：Samples 可引用函式庫；函式庫 (含其測試組件) 不得引用 Samples。
- 函式庫的 PlayMode 測試不得整份包在 `#if UNITY_EDITOR` 中，以保留在 Android 實機執行的可能性；範例測試不受此限。

## 平台

- 目標平台：PC + Android；Android 使用 IL2CPP（AOT）。
- 避免執行期反射、`System.Reflection.Emit`、`dynamic`；必要的反射需加 `[Preserve]` 或 `link.xml`。

## GC 與效能

- 每幀路徑上的 API 以零配置為目標；載入期或一次性 API 允許配置。
- 公開 API 的 XML 註解需標明配置行為：零配置／僅初始化時配置／每次呼叫配置。XML 註解不屬於「不主動新增說明文件」的限制。
- 優先使用內建方案（例如預先設定容量並重複使用的 `List<T>`）。只有同時符合以下條件才自製資料結構：
  1. 位於每幀路徑。
  2. Performance Testing 量測顯示內建方案確實造成配置或明顯開銷。
  3. 自製方案的實作與測試成本可接受。
- 無法判斷是否值得時，列出量測數據與取捨交給使用者決定，不自行選擇。
- Performance Testing 預設只記錄數據；只有標記為熱路徑的 API 才設為通過／失敗門檻。

## 測試

- 改到 Core：執行 `dotnet test`，不受 Editor 是否開啟影響。
- 改到 Runtime／Editor：跑 EditMode + PlayMode。依下列順序選擇工具，前一項無法執行才換下一項，不必事先詢問；換工具時回報前一項失敗的原因：
  1. Editor 開著且 `unity status` 為 `ready`：用 Unity CLI 讓開著的 Editor 執行測試 (`unity command run_tests`)。
  2. Editor 關閉：用 Unity CLI 的 `unity test`。
  3. Unity CLI 無法執行 (未安裝、連不上、指令失敗等)：改用 `Unity.exe` batch mode (見「Unity Editor 批次操作規則」)。此時若 Editor 開著，才提醒使用者關閉 Editor。
- 新增公開 API 時一併新增測試。
- 回報時附上實際執行的指令與結果摘要；沒有執行或無法執行時要明說，不可推測為通過。

## 命令列

- 路徑含 `[TorbenJuniorUtility]`，命令中的路徑一律加引號，避免被 shell 當成萬用字元。
