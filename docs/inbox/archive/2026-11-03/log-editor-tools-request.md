# Log 設定檔與 Editor 工具實作需求

- 對象: 負責實作的 AI。前提是讀者已讀過 `docs/log-system-status-report.md` (現況報告)、`docs/log-system.md`, 以及 `log-api-change-request.md` (Log API 精簡, 含 `Log.Debug` 的 `[Conditional]`)。
- 依賴: 第 5.2 節的「Keep Debug Log in Builds」依賴 API 精簡需求中的 `TORBEN_LOG_DEBUG`。建議先完成 API 精簡需求, 若同時進行需確認 symbol 名稱一致。
- 目的: 讓 Log 系統的初始設定可以存在 ScriptableObject, 並提供 Editor 工具編輯、檢查與快速操作。

---

## 1. 設計原則 (實作時不得違反)

| 原則 | 說明 |
|---|---|
| SO 只是初始值 | 執行中的「目前狀態」仍是現有的靜態欄位。SO 只在 `DebugLogs.Install()` 時套用一次。`Log` 的熱路徑不得讀 SO |
| 預設值只有一份來源 | 真正的預設值是 C# **欄位初始值**。找不到任何設定檔時, 用 `ScriptableObject.CreateInstance<LogSettings>()` 取得的值必須與目前行為完全一致 |
| 註解不寫死數值 | XML 註解不得寫預設數值 (例如「預設 600 x 180」), 改寫「預設值見 `LogSettings` 欄位初始值」。現有註解若有寫死數值, 一併修改 |
| 庫的檔案只在庫的資料夾內 | 所有新檔案都放在 `Assets/[TorbenJuniorUtility]/` 底下。遊戲專案只會多出一個由使用者自行選擇位置的覆蓋設定檔 |
| 不自動產生檔案 | Editor 不得在 `[InitializeOnLoad]` 或匯入期間自動建立 asset。一律由使用者透過按鈕或選單建立 |
| 沒有設定檔也能運作 | 找不到設定檔時只輸出一次警告, 不得拋出例外, 也不得改變現有行為 |
| 分層 | Core 不得引用 `UnityEngine` / `UnityEditor`; Runtime 不得引用 `UnityEditor` (必要時用 `#if UNITY_EDITOR`, 但優先放到 Editor 組件) |

---

## 2. 檔案結構

```text
Assets/[TorbenJuniorUtility]/
├─ Scripts/Core/Log/LogOptions.cs                     新增
├─ Scripts/Runtime/Log/DebugLogsOptions.cs            新增
├─ Scripts/Runtime/Log/DebugWindowOptions.cs          新增 (含 DebugWindowButtons)
├─ Scripts/Runtime/Log/LogSettings.cs                 新增
├─ Scripts/Runtime/DebugLogs.cs                       修改 (Install)
├─ Scripts/Runtime/DebugWindow.cs                     修改 (容量、按鈕旗標)
├─ Scripts/Editor/Log/LogSettingsProvider.cs          新增
├─ Scripts/Editor/Log/LogMenu.cs                      新增
├─ Scripts/Editor/Log/LogSettingsChecker.cs           新增
├─ Scripts/Editor/Log/ScriptingDefineUtility.cs       新增 (define 字串處理, 可測試)
└─ Resources/TorbenTools/LogSettings.Default.asset    新增 (由 Editor 選單建立後提交)
```

- 若 Editor 組件 (`asmdef`, 只含 Editor 平台, 引用 Core 與 Runtime) 尚不存在, 新增 `Torben.Editor`。先確認專案中是否已有, 不要重複建立。
- 實際路徑以專案現有慣例為準 (例如 `Scripts/Runtime/` 底下是否已有 `Log/` 子資料夾); 有差異時跟隨現況並在回報中說明。

---

## 3. 資料結構

### 3.1 `LogOptions` (Core, `[System.Serializable]`)

| 欄位 | 對應 |
|---|---|
| `isPrintLog` | `Log.isPrintLog` |
| `isDebugLog` | `Log.isDebugLog` |
| 各等級堆疊追蹤開關 | `Log.SetStackTraceEnabled(level, ...)`, 預設只有 Error 開啟 |

- 方法: `void Apply()` (寫入 `Log`)、`static LogOptions Capture()` (從 `Log` 讀取目前狀態)。
- 堆疊追蹤開關的儲存方式由實作決定, 但 Inspector 必須顯示等級名稱, 且 `LogLevel` 增加等級時 `Apply` 不得超出範圍或拋例外 (有測試)。

### 3.2 `DebugLogsOptions` (Runtime, `[System.Serializable]`)

| 欄位 | 對應 |
|---|---|
| `openWindowOnStart` | 啟動時設定 `DebugLogs.isGUILog` (預設 `false`) |
| `isConsoleWithGUI` | `DebugLogs.isConsoleWithGUI` |
| `isUnityLog` | `DebugLogs.isUnityLog` |

### 3.3 `DebugWindowOptions` (Runtime, `[System.Serializable]`)

| 欄位 | 對應 | 備註 |
|---|---|---|
| `logCapacity` | `History` 容量 (原常數 200) | 最小 1; 只在 Install 時生效 |
| `unityCapacity` | `UnityHistory` 容量 (原常數 200) | 同上 |
| `size` | `DebugWindow.Size` | 沿用現有最小值限制 |
| `useDefaultPosition` / `position` | `Position` / `ResetPosition()` | `useDefaultPosition` 為 true 時用右下角預設位置 |
| `scale` | `Scale` | 0 = 自動 |
| `isDraggable`、`autoScroll` | 同名屬性 | |
| `levelColors` | `SetLevelColor` | 每個等級一個顏色, Inspector 顯示等級名稱 |
| `visibleLevels` | `SetLevelVisible` | |
| `showLogMessages` / `showUnityMessages` | 同名屬性 | |
| `showTime` / `showLocation` / `collapse` | 同名屬性 | |
| `visibleButtons` | 新增, 見下 | 預設全部顯示 |

```csharp
[System.Flags]
public enum DebugWindowButtons
{
    None = 0,
    Levels = 1 << 0,   // N/W/E/D
    Source = 1 << 1,   // Log / Unity
    Clear = 1 << 2,
    Close = 1 << 3,
    Search = 1 << 4,
    Time = 1 << 5,
    Location = 1 << 6,
    Collapse = 1 << 7,
    All = ~0,
}
```

- 隱藏的只是按鈕, 功能仍可透過靜態屬性控制。
- `DebugWindow` 新增對應的靜態屬性 `VisibleButtons`, 繪製時檢查。隱藏後的版面不得留下空位。
- XML 註解註明: 隱藏 `Close` 時, 觸控裝置上無法用按鈕關閉視窗。

### 3.4 `LogSettings` (Runtime, `ScriptableObject`)

```csharp
public sealed class LogSettings : UnityEngine.ScriptableObject
{
    public const string ResourcesPath = "TorbenTools/LogSettings.Default"; // 路徑唯一定義處
    public LogSettings projectOverride;   // 只在預設檔中有意義
    public LogOptions log;
    public DebugLogsOptions output;
    public DebugWindowOptions window;
    public void Apply();                  // 套用三組設定
    public LogSettings ResolveEffective(); // projectOverride 非 null 回傳它, 否則回傳自己; 不遞迴
}
```

- 覆蓋方式為 **整份替換**, 不做逐欄位覆蓋。
- 覆蓋檔本身的 `projectOverride` 不使用 (不支援串接), 由狀態檢查警告。
- 欄位名稱可依專案命名慣例調整, 但 `ResourcesPath` 必須是唯一定義處。

---

## 4. Runtime 修改

### 4.1 `DebugLogs.Install()` 流程

```text
1. 重設靜態欄位 (現有行為)
2. settings = Resources.Load<LogSettings>(LogSettings.ResourcesPath)
   ├─ 找到 → effective = settings.ResolveEffective()
   └─ 找不到 → effective = CreateInstance<LogSettings>(), 輸出警告 (每次進入 Play 最多一次)
3. effective.Apply()
4. 依 window.logCapacity / unityCapacity 建立 History / UnityHistory, 加入 sink
5. 若 output.openWindowOnStart, 開啟視窗 (沿用現有時機: AfterSceneLoad 之前不建立 GameObject)
```

- **待驗證**: 在 `SubsystemRegistration` 階段是否能使用 `Resources.Load`。若不行, 把第 2～5 步移到 `BeforeSceneLoad`, 並確認自訂 sink 的加入時機說明 (現況報告 5.1) 是否需要更新。在回報中寫明實際採用哪個階段。
- 警告訊息本身不得經過 `Log` (避免順序問題), 直接用 `UnityEngine.Debug.LogWarning`。

### 4.2 `DebugWindow` 容量相關

- 現況以常數 200 建立 `Snapshot` / `Sequences` / `Lines` 等陣列, 且 `visible[400]` 以「Unity 索引 + 200」區分來源。**這個偏移量必須改為 `logCapacity`**, 陣列長度改為兩個容量的總和。
- 這些陣列只在 Install (或首次建立) 時配置, 不得在每幀配置。
- 搜尋所有寫死 200 / 400 的地方並改為依容量計算。

---

## 5. Editor 工具

### 5.1 Project Settings 頁面 (`LogSettingsProvider`)

- 路徑: `Project/TorbenTools/Log`。
- 上方: 狀態檢查結果 (見 5.4), 有問題時以警告框顯示, 並附上修正按鈕。
- 預設檔不存在時: 只顯示「建立預設設定檔」按鈕, 不自動建立。
- 預設檔存在時: 顯示目前生效的檔案 (預設或覆蓋), 並繪製其內容 (`SerializedObject`, 支援 Undo)。
- 顯示「Create Project Override」與「Keep Debug Log in Builds」(含各平台狀態)。

### 5.2 選單 (`LogMenu`)

```text
TorbenTools/
├─ Log GUI Window                      切換 DebugLogs.isGUILog; 只在 Play 中可用; 開啟時打勾
└─ Log/
   ├─ Settings...                      SettingsService.OpenProjectSettings("Project/TorbenTools/Log")
   ├─ Keep Debug Log in Builds         切換目前建置平台的 TORBEN_LOG_DEBUG; 打勾表示已定義
   ├─ Save Current State as Settings   只在 Play 中可用, 見 5.3
   ├─ Create Project Override          見 5.3
   └─ Check Settings                   執行 5.4, 結果輸出到 Console
```

- `TorbenTools/Log GUI Window` 與 `TorbenTools/Log/` 同層, 用 `priority` 讓 `Log GUI Window` 排在前面。
- 勾選狀態在 validate 函式中以 `Menu.SetChecked` 設定。
- 未來其他系統沿用 `TorbenTools/{系統名稱}/` 慣例, 常用開關可放在 `TorbenTools/` 同層。寫進 `AGENTS.md`。

### 5.3 寫入操作

| 操作 | 行為 |
|---|---|
| **建立預設設定檔** | 在 `Assets/[TorbenJuniorUtility]/Resources/` + `LogSettings.ResourcesPath` + `.asset` 建立 (資料夾不存在就建立)。內容為欄位初始值 |
| **Create Project Override** | `EditorUtility.SaveFilePanelInProject` 讓使用者選位置 → 複製目前預設檔的內容建立新 asset → 設定預設檔的 `projectOverride` → `SetDirty` + `SaveAssets`, 支援 Undo。選擇的位置在 `Assets/[TorbenJuniorUtility]/` 底下時警告 (覆蓋檔應屬於遊戲專案) 但允許繼續 |
| **Save Current State as Settings** | 以 `LogOptions.Capture()` 及各靜態屬性讀取目前狀態, 寫入 **目前生效的檔案** (有覆蓋寫覆蓋檔, 否則寫預設檔)。寫入前用對話框顯示目標路徑並確認。`Undo.RecordObject` + `SetDirty` |
| **Keep Debug Log in Builds** | 以 `ScriptingDefineUtility` 新增 / 移除 symbol, 透過 `PlayerSettings.Get/SetScriptingDefineSymbols(NamedBuildTarget, ...)` 套用到目前建置平台 |

- `ScriptingDefineUtility` 的字串處理 (新增、移除、判斷是否存在; 處理分號、空白、重複) 寫成純函式, 不呼叫 `PlayerSettings`, 以便測試。

### 5.4 狀態檢查 (`LogSettingsChecker`)

回傳結果清單 (等級: 正常 / 警告 / 錯誤 + 訊息 + 可選的修正動作), 供設定頁與選單共用。檢查項目:

| # | 項目 | 等級 |
|---|---|---|
| 1 | 預設檔是否存在於 `Resources.Load` 能讀到的路徑 | 不存在: 警告 (附「建立」) |
| 2 | 是否有其他 `LogSettings` asset 存在 (`AssetDatabase.FindAssets("t:LogSettings")`), 但預設檔的 `projectOverride` 沒有指向它 | 警告: 「可能是更新庫時連結遺失」(附「連結」) |
| 3 | `projectOverride` 指向的 asset 自己也設定了 `projectOverride` | 警告: 不支援串接 |
| 4 | 容量小於 1 或明顯過大 (上限由實作決定並寫進文件) | 錯誤 / 警告 |
| 5 | 各平台 (Standalone / Android / iOS) 的 `TORBEN_LOG_DEBUG` 狀態 | 資訊 |
| 6 | Unity 6 Build Profile 自己的 define | 若 API 可取得, 列出目前 profile 是否定義 `TORBEN_LOG_DEBUG`; 若無法取得, 在文件註明「Build Profile 的 define 不受此按鈕管理」 |

- Play 開始時若有警告以上的項目, 輸出一次摘要到 Console (Editor 專用, 用 `EditorApplication.playModeStateChanged`)。

---

## 6. 測試

| 範圍 | 測試 |
|---|---|
| dotnet | `LogOptions.Apply` / `Capture` 往返一致; 堆疊追蹤開關對應正確; 等級數量變動時不拋例外 |
| PlayMode | 沒有設定檔時行為與現況相同且只警告一次; 有預設檔時套用; 有覆蓋檔時使用覆蓋檔; 兩個容量不同 (例如 5 與 7) 時兩份紀錄都正確、`visible` 偏移正確、合併與篩選正常; `VisibleButtons` 隱藏後繪製不出錯 |
| EditMode | `ScriptingDefineUtility` 的新增 / 移除 / 判斷 (含空字串、重複、前後空白); 狀態檢查 #1～#4 (用暫存 asset, 測試後刪除); 建立預設檔的路徑正確 |
| 人工確認 | 設定頁顯示與 Undo、選單勾選狀態、`Log GUI Window` 切換、Create Project Override 流程、Save Current State 的對話框 |

測試建立的暫存 asset 必須在 TearDown 中刪除, 不得留在專案中。

---

## 7. 文件

- `docs/log-system.md`: 新增「設定檔」章節 (載入順序、預設 / 覆蓋 / 欄位初始值三層、更新庫時不要覆蓋 `LogSettings.Default.asset`)、Editor 工具與選單說明。
- `AGENTS.md`: 新增「XML 註解不寫死預設數值」規則與 `TorbenTools/{系統名稱}/` 選單慣例。
- 現況報告: 更新第 2 節表格、第 5 節 Runtime 元件、第 8 節缺口 (移除「Editor 工具: 無」「設定方式: 全部是靜態欄位」「容量: 無法設定」), 並在第 7 節新增修改紀錄。

---

## 8. 完成標準

全部符合才算完成:

1. dotnet、PlayMode、EditMode 測試全部通過, 且第 6 節列出的測試都存在。
2. 刪除 `LogSettings.Default.asset` 後進入 Play: 行為與修改前相同, Console 只出現一次警告。
3. 程式中 `"TorbenTools/LogSettings.Default"` 只出現在 `LogSettings.ResourcesPath`。
4. `DebugWindow` 中不再有寫死的 `200` / `400` 容量或偏移量。
5. Core 組件沒有引用 `UnityEngine` / `UnityEditor`; Runtime 組件沒有引用 `UnityEditor`。
6. XML 註解中沒有寫死的預設數值 (搜尋「預設」附近的數字並逐一確認)。
7. `LogSettings.Default.asset` 已建立於 `Assets/[TorbenJuniorUtility]/Resources/TorbenTools/` 並提交。
8. 第 7 節文件已更新。
9. 回報: 修改的檔案清單、Install 實際採用的階段 (4.1 待驗證項目)、Build Profile define 能否偵測 (5.4 #6)、遇到但未處理的問題。

---

## 9. 不在本次範圍

- Console 點兩下跳到呼叫端 (`[OnOpenAsset]` 攔截)。
- 依建置設定檔切換不同 `LogSettings`。
- 執行中修改容量。
- 逐欄位覆蓋。
