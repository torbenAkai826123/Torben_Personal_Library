# Log 系統說明

本文說明 `Torben.Log` 與畫面 Log 視窗 (`DebugWindow`) 的架構、開關、API 與已知限制。快速上手見 [Log 快速使用說明](manual/log-quickstart.md)。

## 架構

```text
Log.Information / Warning / Error / Debug   (Core, 純 C#)
        │  isPrintLog、isDebugLog 過濾
        ▼
   sink 清單 (copy-on-write 陣列)
        ├─ DebugLogs         → Unity Console        (Runtime)
        └─ LogHistorySink    → Log 歷史紀錄 (容量可設定) (Core) ── DebugWindow.History
                                   ▲
                              DebugWindow 依流水號合併兩份紀錄並繪製 (Runtime, IMGUI)
                                   ▼
Unity 引擎訊息 ─ Application.logMessageReceivedThreaded (isUnityLog 開啟時)
        └─ DebugLogs.OnUnityLog → Unity 歷史紀錄 (容量可設定) ── DebugWindow.UnityHistory
                                  (排除 DebugLogs 自己輸出到 Console 的訊息)
```

啟動時的初始值由設定檔 `LogSettings` 提供, 見「設定檔」。

| 檔案 | 組件 | 責任 |
|---|---|---|
| `Scripts/Core/Logs/Log.cs` | Torben.Core | 對外入口; 總開關與 Debug 開關; 管理 sink 清單; sink 例外隔離 (`SinkErrorHandler`) |
| `Scripts/Core/Logs/LogModels.cs` | Torben.Core | `LogLevel`、`ILogSink`、`LogEntry`、`LogArgsEnd` |
| `Scripts/Core/Logs/LogStackTrace.cs` | Torben.Core | 堆疊追蹤 (擷取時只存框架, 讀取時才轉文字) |
| `Scripts/Core/Logs/LogText.cs` | Torben.Core | 顯示用文字轉換: 時間、短路徑、解析 Unity 堆疊位置、顯示位置、複製用文字 (`FormatForCopy`)、去除 rich text |
| `Scripts/Core/Logs/LogCategory.cs` | Torben.Core | 分類標籤 (`Tag`) 與標籤顏色 (`tagColorHex`) |
| `Scripts/Core/Logs/LogHistorySink.cs` | Torben.Core | 固定容量環狀緩衝區, 保留最新的 N 筆 |
| `Scripts/Core/Logs/LogOptions.cs` | Torben.Core | Log 的初始設定 (`LogOptions`) 與等級開關 (`LogLevelSwitch`) |
| `Scripts/Runtime/Logs/DebugLogs.cs` | Torben.Runtime | Console 輸出; 啟動時安裝 sink; `isGUILog` 開關; 收集 Unity 引擎訊息 |
| `Scripts/Runtime/Logs/UnityLogCategory.cs` | Torben.Runtime | Unity 引擎訊息的分類 (`[Unity]`) |
| `Scripts/Runtime/Logs/DebugWindow.cs` | Torben.Runtime | 畫面視窗 (篩選、搜尋、上色、拖曳、縮放) |
| `Scripts/Runtime/Logs/DebugLogsOptions.cs` | Torben.Runtime | DebugLogs 的初始設定 |
| `Scripts/Runtime/Logs/DevelopmentAlertOptions.cs` | Torben.Runtime | Development Alert 的等級 (`DevelopmentAlertLevel`) 與設定 |
| `Scripts/Runtime/Logs/DebugWindowOptions.cs` | Torben.Runtime | DebugWindow 的初始設定、`DebugWindowButtons`、`LogLevelColor`; 容量建議上限 `MaxRecommendedCapacity` 的唯一定義處 |
| `Scripts/Runtime/Logs/LogSettings.cs` | Torben.Runtime | 設定檔 (`ScriptableObject`); `ResourcesPath` 為預設檔路徑的唯一定義處 |
| `Resources/TorbenTools/LogSettings.Default.asset` | — | 預設檔 |
| `Scripts/Editor/Logs/*.cs` | Torben.Editor | Project Settings 頁面、選單、狀態檢查、寫入操作、define 字串處理 |
| `Scripts/Runtime/StaticObject/CrossObject.cs` | Torben.Runtime | 跨場景物件; DebugWindow 掛在它上面 |

路徑皆相對於 `Assets/[TorbenJuniorUtility]/`。

## 開關

下表的「預設」為設定檔欄位初始值的現況, 以 `LogSettings` 各欄位的初始值為準; 實際啟動值由設定檔決定。

| 開關 | 預設 | 作用 |
|---|---|---|
| `Log.isPrintLog` | `false` | 總開關。關閉時不產生任何訊息, 所有 sink 都收不到 |
| `Log.isDebugLog` | `false` | 只管 `LogLevel.Debug`。關閉時 Debug 訊息不會送到任何 sink |
| `DebugLogs.isGUILog` | `false` | 開啟: 顯示畫面視窗, 並停止輸出到 Console。關閉: 隱藏視窗, 恢復 Console |
| `DebugLogs.isConsoleWithGUI` | `false` | `isGUILog` 開啟時仍同時輸出到 Console |
| `DebugLogs.isUnityLog` | `true` | 收集 Unity 引擎訊息到 `DebugWindow.UnityHistory`。**不受 `isPrintLog` 影響**; 關閉時取消訂閱, 期間的訊息不補記 |

歷史紀錄不受 `isGUILog` 影響: 只要訊息通過前兩個開關就會被記錄, 所以視窗晚點開啟也看得到之前的訊息。

### Unity 引擎訊息

- 指沒有經過 `Torben.Log`、直接寫進 Console 的訊息: 未捕捉的例外、引擎警告、第三方或舊程式直接呼叫的 `UnityEngine.Debug.Log`。
- 以 `Application.logMessageReceivedThreaded` 收集, 可能在任何執行緒觸發。等級對應: `Warning` → Warning; `Error`、`Assert`、`Exception` → Error; 其他 → Normal。分類固定為 `UnityLogCategory.Instance` (標籤 `[Unity]`, 可改 `tagColorHex`)。
- `DebugLogs` 自己輸出到 Console 時以執行緒旗標排除, 所以 `Torben.Log` 的訊息不會重複出現成 `[Unity]`。
- 只存訊息本身, 不存堆疊追蹤 (stack trace)。
- 正式版不想收集時, 在 `BeforeSceneLoad` 之後設 `DebugLogs.isUnityLog = false`; 在這之前產生的引擎訊息已被收集。
- 離開 Play (Editor) 或程式結束時 (`Application.quitting`) 取消訂閱。

## 載入順序

| 階段 | 發生的事 |
|---|---|
| `SubsystemRegistration` | `DebugLogs.Install`: 以 `Resources.Load` 讀取設定檔; 清空 sink (一併清除 sink 錯誤的回報記錄) 並設定 `Log.SinkErrorHandler`, 加入 Console 與歷史紀錄兩個 sink (容量依設定檔); 重設 `isGUILog` 與 DebugWindow 的所有設定; 開始收集 Unity 訊息; 依序套用設定檔的 `log`、`window`、`output`; 最後依 Development Alert 提醒設定問題 (見「設定檔」) |
| `BeforeSceneLoad` 之後 | 專案可以修改開關與 DebugWindow 設定 |
| `AfterSceneLoad` | 若 `isGUILog` 已開啟 (含設定檔的 `openWindowOnStart`), 此時才建立視窗元件 (掛在 `CrossObject` 上) |
| 之後 | 第一次開啟 `isGUILog` 時建立視窗元件; 之後切換只改 `enabled` |

- 在 `SubsystemRegistration` 之前或同階段做的設定會被重設; 請在 `BeforeSceneLoad` 或之後設定。
- `isPrintLog` 開啟之前呼叫的 Log 不會產生, 也不會補記。要看到載入初期的訊息, 需在更早的階段開啟 `isPrintLog`。
- 每次進入 Play 都會重設 (包含關閉 Domain Reload 時), 不會殘留上一次的 sink 或設定。`Log.isPrintLog`、`isDebugLog`、堆疊追蹤開關與 `DebugLogs.isConsoleWithGUI` 也會恢復為設定檔的值。
- 2026-11-03 實測 (Unity 6000.5.5f1, Editor): `SubsystemRegistration` 階段的 `Resources.Load` 讀得到預設檔 (PlayMode 測試 `Startup_LoadsDefaultSettingsFromResources`), 因此安裝維持在此階段, 自訂 sink 的加入時機不變。建置版未驗證。

## 設定檔

`LogSettings` (`ScriptableObject`) 只保存**啟動時的初始值**。執行中的目前狀態仍是 `Log` / `DebugLogs` / `DebugWindow` 的靜態欄位, 熱路徑不讀取設定檔; 在 Play 中修改設定檔要到下次進入 Play 才生效。

### 三層來源

| 順序 | 來源 | 說明 |
|---|---|---|
| 1 | 覆蓋檔 | 預設檔的 `projectOverride` 指向的檔案, 屬於遊戲專案, 位置由使用者選擇。**整份取代**預設檔, 不做逐欄位覆蓋; 覆蓋檔自己的 `projectOverride` 不使用 (不支援串接) |
| 2 | 預設檔 | `Assets/[TorbenJuniorUtility]/Resources/TorbenTools/LogSettings.Default.asset`, 隨函式庫提交 |
| 3 | 欄位初始值 | 找不到預設檔時用 `ScriptableObject.CreateInstance<LogSettings>()` 的值, 並在 Console 警告一次 (直接用 `UnityEngine.Debug.LogWarning`, 不經過 `Log`)。不拋例外, 行為與加入設定檔之前相同 |

- 預設值只有一份來源: `LogOptions` / `DebugLogsOptions` / `DebugWindowOptions` 的欄位初始值。`DebugWindow` 每次進入 Play 時也以 `DebugWindowOptions` 的初始值重設。
- **更新函式庫時不要覆蓋 `LogSettings.Default.asset`**: 覆蓋後 `projectOverride` 的連結會遺失。遺失時狀態檢查會提示「可能是更新庫時連結遺失」, 可按「Link」重新連結。
- 容量 (`window.logCapacity` / `unityCapacity`) 只在啟動時生效, 小於 1 時以 1 計 (不拋例外); 大於 `DebugWindowOptions.MaxRecommendedCapacity` (10000, `LogSettingsChecker.MaxRecommendedCapacity` 引用同一個值) 時狀態檢查與 Development Alert 會提醒。執行中不能修改容量。
- 等級相關欄位 (堆疊追蹤、等級顏色、等級顯示) 以「等級 + 值」的陣列儲存, Inspector 會顯示等級名稱。沒有列出的等級: 堆疊追蹤視為關閉、顏色為預設文字色、顯示視為開啟; 未定義的等級值會被忽略。

### Development Alert

啟動時 (`DebugLogs.Install`) 檢查生效中的設定檔, 有問題時依 `developmentAlert.level` 用 Unity 的 Log 提醒。Development Build 遇到 Error 會自動顯示開發主控台; 此時已開始收集 Unity 訊息, 所以畫面視窗 (`DebugWindow.UnityHistory`) 也收得到 (設定檔的 `isUnityLog` 關閉時不收集)。

| 等級 | 輸出 |
|---|---|
| `Off` | 不輸出 |
| `Warning` (預設) | `UnityEngine.Debug.LogWarning` |
| `Error` | `UnityEngine.Debug.LogError` |

| 檢查項目 | 條件 |
|---|---|
| 找不到設定檔 | 讀不到等級, 一律以 `Warning` 輸出原本的「LogSettings not found」訊息 (內容不變) |
| 容量超出範圍 | `window.logCapacity` 或 `unityCapacity` 小於 1, 或大於 `DebugWindowOptions.MaxRecommendedCapacity` (與 Editor 狀態檢查相同的條件與常數) |
| 等級陣列重複 | `log.stackTrace` 或 `window.levelColors` 中同一等級出現多次 (只有最上面那筆生效) |

- 不經過 `Log`, 直接呼叫 `UnityEngine.Debug`。多個問題合併成**一則**訊息 (標題一行, 每個問題一行), 每次 Install 最多一則。
- 欄位為 `LogSettings` 的 `[SerializeField, HideInInspector] developmentAlert`, 以 `LogSettings.DevelopmentAlert` 讀取。**只在 Project Settings 頁面編輯**, Inspector 不顯示; 編輯對象為生效中的檔案 (有覆蓋檔時為覆蓋檔), 與 Save Current State 相同。它不是執行中的狀態, Save Current State 不寫入。
- Editor 中進入 Play 時, 狀態檢查的摘要 (見下方) 與 Development Alert 可能各提醒一次容量問題。

### Editor 工具

不會在載入或匯入期間自動建立檔案, 一律由使用者按按鈕或選單建立。

| 位置 | 功能 |
|---|---|
| Project Settings > TorbenTools > Log | 上方顯示狀態檢查的警告與修正按鈕; 預設檔不存在時只顯示「Create Default Settings File」。存在時依區塊顯示: **Files** (預設檔、`projectOverride` 連結、生效的檔案; 右側 Create Project Override、Save Current State、Edit in Inspector 按鈕)、**Runtime Check** (`Development Alert` 等級, 唯一在此頁編輯的欄位; 以 `SerializedObject` 寫入生效中的檔案, 支援 Undo)、**Build** (Keep Debug Log in Builds 開關與 Standalone / Android / iOS、Build Profile 的狀態)、**Log / Output / Window** (生效檔案內容的唯讀表格)。Development Alert 以外的設定內容不在此頁編輯, 按 Edit in Inspector 到設定檔的 Inspector 修改; 頁面每秒重新檢查一次 |
| 設定檔的 Inspector | `LogSettingsInspector`: Log / Output / Window 為 Unity 預設的折疊欄位, 區塊內 bool 在上、其他欄位在下 (依欄位宣告順序)。`projectOverride` 只在預設檔顯示。等級顏色以 `LogLevelColorDrawer` 顯示: 等級 + 自訂開關 + Color picker (關閉時為預設文字顏色, 存空字串; 開啟時存 `#RRGGBB`, 透明時 `#RRGGBBAA`)。右上角 Save Current State 只在 Play 中、且此檔為生效的檔案時可用。以 `SerializedObject` 編輯, Cmd / Ctrl+Z 可復原 |
| `TorbenTools/Log GUI Window` | 切換 `DebugLogs.isGUILog`; 只在 Play 中可用, 開啟時打勾 |
| `TorbenTools/Log/Settings...` | 開啟上述設定頁 |
| `TorbenTools/Log/Keep Debug Log in Builds` | 在目前建置平台的 Player Settings 加入 / 移除 `TORBEN_LOG_DEBUG` (建置版保留 `Log.Debug`); 打勾表示已定義。變更後 Unity 會重新編譯 |
| `TorbenTools/Log/Save Current State as Settings` | 只在 Play 中可用; 讀取目前狀態, 確認目標路徑後寫入目前生效的檔案 (有覆蓋檔寫覆蓋檔, 否則寫預設檔), 支援 Undo |
| `TorbenTools/Log/Create Project Override` | 選擇位置, 複製預設檔內容建立覆蓋檔並連結。位置在 `Assets/[TorbenJuniorUtility]/` 底下時警告 (覆蓋檔應屬於遊戲專案), 可選擇繼續 |
| `TorbenTools/Log/Check Settings` | 執行狀態檢查, 結果輸出到 Console |

狀態檢查 (`LogSettingsChecker`) 的項目: 1. 預設檔是否存在 (警告, 附「Create」); 2. 有其他 `LogSettings` 但預設檔沒有指向它 (警告, 附「Link」); 另外, 預設檔的 `projectOverride` 為 Missing (原本有連結, 但檔案被刪除; 建置版會悄悄改用預設檔) 時警告「projectOverride is Missing」, 附「Clear」(`LogSettingsChecker.IsOverrideMissing`); 3. 覆蓋檔自己也設定了 `projectOverride` (警告, 附「Clear」); 4. 生效檔案的容量小於 1 (錯誤) 或大於上限 (警告); 5. Standalone / Android / iOS 的 `TORBEN_LOG_DEBUG` (資訊); 6. 目前 Build Profile 自己的 define 是否有 `TORBEN_LOG_DEBUG` (資訊, 以 `BuildProfile.GetActiveBuildProfile().scriptingDefines` 讀取; 沒有使用 Build Profile 時不列出)。**Build Profile 的 define 不受 Keep Debug Log in Builds 管理**, 需在 Build Profile 視窗自行設定。

進入 Play 時, 若 1 以外的項目有警告以上, 會在 Console 輸出一次摘要 (找不到預設檔時已由 `DebugLogs.Install` 警告, 不重複輸出)。

## Log API (`Torben.Log`)

| 成員 | 說明 | 配置 |
|---|---|---|
| `Information / Warning / Error(message, category = null)` | 產生訊息並送給每個 sink; `category` 為 null 時使用空分類 | 本身零配置 (開啟堆疊追蹤的等級每次配置); 各 sink 自行決定 |
| `Debug(message, category = null)` | 同上, 等級為 Debug; 帶 `[Conditional]`, 未定義保留條件時整行呼叫被編譯期移除 (見下方「Log.Debug 的編譯期移除」) | 同上; 被移除時零配置 |
| `Format(level, [category,] format, arg1[, arg2[, arg3]])` | 開啟時才以 `string.Format` 組字串; 分類放在等級後面。Debug 等級請用 `Log.Debug` | 關閉時零配置; 開啟時每次呼叫配置 (字串、值型別裝箱) |
| `IsStackTraceEnabled(level)` / `SetStackTraceEnabled(level, bool)` | 該等級是否擷取堆疊追蹤; 預設只有 Error。隨時可改, 只影響之後的訊息 | 零配置 |
| `IsEnabled(level)` | 該等級的訊息目前是否會送到 sink (`isPrintLog`、至少一個 sink、Debug 另需 `isDebugLog`) | 零配置 |
| `AddSink(ILogSink)` | 加入 sink; 已存在或 null 時不做事 | 每次呼叫配置新陣列 |
| `RemoveSink(ILogSink)` | 移除 sink; 不存在時不做事 | 有移除時配置新陣列 |
| `ClearSinks()` | 移除全部 sink; 一併清除 sink 錯誤的回報記錄 | 零配置 |
| `SinkErrorHandler` | sink 的 `Write` 拋例外時呼叫 (見下方「sink 例外隔離」); null 時不回報 | 只在例外發生時配置 |
| `HasSink` | 是否至少有一個 sink | 零配置 |

sink 清單採 copy-on-write: 新增或移除時建立新陣列再整個替換, 寫入時只讀取當下的陣列, 不需加鎖。新增或移除不應在每幀執行。

### sink 例外隔離

`Log` 依序同步呼叫各 sink 的 `Write`, 每次呼叫各自以 `try / catch` 包住:

- 任一 sink 拋例外時, 後面的 sink 照常收到該筆訊息; 例外**不會**傳回呼叫 `Log.Xxx` 的地方。
- 例外交給 `Log.SinkErrorHandler` (`System.Action<ILogSink, System.Exception>`)。Runtime 的 `DebugLogs.Install` 設定為以 `UnityEngine.Debug.LogException` 輸出一則, 訊息第一行為 `[Log] Sink '型別全名' threw an exception while writing ...`, 後面接原例外 (含堆疊)。不包成 `InnerException`, 因為 Unity 會改以最內層例外的訊息顯示, sink 名稱就看不到。
- 每個 sink 實例 (以參考比對) 只回報第一次失敗; `RemoveSink` 該 sink 或 `ClearSinks` 時清除記錄, 重新加入後再失敗會再回報。`SinkErrorHandler` 為 null 時不回報, 也不記錄。記錄用的清單在第一次回報時才建立, 以 sink 清單的鎖保護。
- 失敗的 sink **不會**被自動移除 (自動移除會讓問題更難追查)。
- 防止遞迴: 回報期間以 `[ThreadStatic]` 旗標標記, 同一執行緒上的 `Log` 寫入直接略過 (例如處理器內呼叫 `Log.Error`)。`SinkErrorHandler` 自己拋的例外會被捕捉並忽略。其他執行緒的寫入不受影響。
- 效能: 無例外時的寫入路徑沒有新增配置 (dotnet 以 `GC.GetAllocatedBytesForCurrentThread` 確認為 0)。

### 避免關閉時的配置

開關檢查在 `Log` 內部, 參數在呼叫前就已建立。`Log.Information($"hp={hp}")` 或 `"hp=" + hp` 即使 Log 關閉也會組字串。`Information` / `Warning` / `Error` 需要組字串時改用下列其中一種:

```csharp
Log.Format(LogLevel.Normal, "hp={0}", hp);              // 開啟時才格式化
Log.Format(LogLevel.Warning, NetworkCategory.Instance, "retry {0}/{1}", n, max);

if (Log.IsEnabled(LogLevel.Normal))                     // 需要較複雜的組字串時
    Log.Information(BuildReport());

Log.Debug($"hp={hp}");                                  // Debug 等級: 正式版整行被移除, 可以用 $"..."
```

- 參數以泛型傳入, 關閉時不裝箱; 最多 3 個參數, 更多時用 `IsEnabled`。
- 不提供 `Information(string, T)` 這類同名多載: 傳入分類子類別時, 泛型多載會比 `LogCategory` 參數更優先, 分類會被當成格式參數。
- `Format(LogLevel.Debug, ...)` 可以使用, 關閉時一樣零配置, 但不會被 `[Conditional]` 移除 (`[Conditional]` 無法依參數值判斷), 呼叫會留在建置中。Debug 等級請用 `Log.Debug`。
- `Warning` / `Error` 通常在異常分支, 正常運作時不會執行, 一般版的配置可以接受; 每幀會執行的路徑再改用 `Log.Format`。
- `format` 格式錯誤時 `string.Format` 丟出的 `FormatException` 會傳回呼叫端。
- 字串常值 (例如 `Log.Information("ready")`) 不配置, 照原本寫法即可。

### Log.Debug 的編譯期移除

`Log.Debug` 帶有三個 `[System.Diagnostics.Conditional]`: `UNITY_EDITOR`、`DEVELOPMENT_BUILD`、`TORBEN_LOG_DEBUG`。

- 多個 `[Conditional]` 為 OR: 呼叫端編譯時任一 symbol 有定義就保留呼叫, 否則整行 (包含參數運算) 被移除。保留後仍由 `isPrintLog` / `isDebugLog` 決定是否輸出。
- 去留由**呼叫端的編譯**決定, 與 `Log` 所在組件無關。要在實機保留 Debug Log: Build Settings 勾選 Development Build, 或在 Player Settings 的 Scripting Define Symbols 加入 `TORBEN_LOG_DEBUG`。
- 參數中的副作用 (例如 `Log.Debug($"{i++}")`) 會跟著被移除, 參數不得有副作用。
- 函式庫 (Core / Runtime) 內部不得呼叫 `Log.Debug`: 打包成 DLL 時就決定了去留, 使用端無法再控制。
- dotnet 測試專案沒有 `UNITY_EDITOR`, 在 `Library.Tests.csproj` 的 `DefineConstants` 加入 `TORBEN_LOG_DEBUG` 才會保留。

### 呼叫端位置與堆疊追蹤

- 每個公開的寫入方法 (含 `Format`) 最後都有 `LogArgsEnd argsEnd`、`[CallerMemberName]`、`[CallerFilePath]`、`[CallerLineNumber]` 四個選填參數, 由編譯器在呼叫處填入, 不要自己傳。值是編譯期常數, 執行時不配置。
- 呼叫端參數必須宣告在每個公開方法上 (編譯器只填入「被呼叫的方法」的參數), 無法集中到一個方法。
- `LogArgsEnd` 是分隔用的空結構: 沒有它時, `Format(level, "{0}{1}{2}", a, b, "c")` 會被編譯器選成 2 參數版本, 把 `"c"` 當成 `callerMemberName`; 有它之後多傳的參數會變成型別不符, 選到正確的多載或編譯失敗。
- `[CallerFilePath]` 是編譯時的完整路徑, 建置版的執行檔裡會留有建置機器上的路徑字串。
- 堆疊追蹤用 `System.Diagnostics.StackTrace` 擷取, 只存框架; 第一次讀取 `LogStackTrace.Text` 時才轉成文字 (去掉最上方 `Torben.Log` 自己的框架, 再逐行清理, 見下方「堆疊文字清理」) 並快取。檔名與行號是否存在依平台與建置設定而定: Editor 有; IL2CPP 需在 Player Settings 把 IL2CPP Stacktrace Information 設為 Method Name, File Name, and Line Number (2026-11-03 於 macOS IL2CPP 建置確認); Mono 建置版**未驗證**。
- 堆疊文字清理 (`LogStackTrace.CleanFrames`): 逐行處理, 每行只套用第一條符合的規則, 保留行首縮排與原本的換行 (`\n` / `\r\n`)。不使用 `System.Text.RegularExpressions` (IL2CPP 會增加程式碼大小), 以字串搜尋判斷。`FromText` (Unity 提供的堆疊) 也經過同樣的處理, 但 Unity 格式不符合規則, 內容不變。

  | # | 條件 (行尾) | 處理 |
  |---|---|---|
  | A | ` [0x十六進位] in 路徑:數字` (路徑不在角括號內) | 去掉 ` [0x...]`, 路徑改為 `LogText.ToDisplayPath(路徑, null)` (從 `Assets/` 或 `Packages/` 開始, 都沒有只留檔名) |
  | B | ` <0x十六進位 + 0x十六進位> 數字 in <十六進位>:數字` | 從 ` <0x` 開始到行尾全部去掉 |
  | C | ` [0x十六進位] in <十六進位>:數字` | 從 ` [0x` 開始到行尾全部去掉 |
  | — | 其他 (Unity 的 `(at Assets/...:68)`、沒有尾段的行、角括號內不是十六進位 (例如 `<filename unknown>`)、無法辨識的格式) | 原樣保留 |

  - 原則: 角括號內是十六進位 (MVID) 時, 視為沒有原始碼位置, 行號一律視為佔位 (IL2CPP 開啟行號時, 沒有原始碼的框架 (例如委派的 `Invoke`) 為 `[0x00000] in <MVID>:1`)。
  - Unity 的 Mono 類別庫 (Editor 與 IL2CPP 共用, 6000.5.5f1 的 `mscorlib.dll` 格式字串為 `" in {0}:{1} "`) 每行行號後多一個空白: 判斷前先略過行尾空白, 改寫的行一併去掉, 原樣保留的行不動。
  - 解析從行尾往前找: 行號為最後一個 `:` 之後的數字 (Windows 路徑本身含 `:`); 路徑為最後一個 ` in ` 與該 `:` 之間的內容 (可含空白)。
  - 不改編譯器產生的名稱 (例如 `<>c__DisplayClass9_0`), 也不隱藏 `UnityEngine.` 框架。
  - 例: `  at CallLog.LogError (System.String message) [0x0001c] in /Users/.../Assets/X/CallLog.cs:68` → `  at CallLog.LogError (System.String message) in Assets/X/CallLog.cs:68`; `... <0x00000 + 0xffffffff> 0 in <000...0>:0` 與 `... [0x00000] in <000...0>:1` → 只留方法。
- Unity Console 點兩下仍會跳到 `DebugLogs.Write`。`[HideInCallstack]` 無法解決 (2026-11-02 查 6000.5.5f1 的 `UnityEditor.CoreModule.dll`): 它是 `UnityEditor.ConsoleWindow` 用 `TypeCache` 收集被標記的方法, 只在 Console 開啟「Strip logging callstack」時從詳細區的堆疊文字去掉這些框架; 屬性本身在各平台的 `UnityEngine.CoreModule` 都存在, 但只在 Editor 的 Console 有作用。`Log` 在 Core 不能引用 `UnityEngine`, 無法標記, 堆疊中仍會留下 `Torben.Log` 的框架。改用畫面視窗的位置連結開啟呼叫端。

### LogEntry

自訂 sink 只需實作 `ILogSink.Write(in LogEntry entry)`, 再用 `Log.AddSink` 加入。

| 欄位 | 說明 |
|---|---|
| `Level`、`Category`、`Message` | 等級、分類、訊息 |
| `Time` | 寫入時間 (`DateTime.UtcNow`, UTC); 以 3 個參數建構時為 `default` |
| `CallerFilePath`、`CallerMemberName`、`CallerLineNumber` | 呼叫端位置; Unity 引擎訊息沒有 (`HasCaller` 為 false) |
| `StackTrace` | `LogStackTrace`; 該等級未開啟擷取時為 null。Unity 引擎訊息存 Unity 提供的堆疊文字 (不受 `SetStackTraceEnabled` 影響, 內容依 Unity 的 Stack Trace Logging 設定) |

## LogCategory

繼承 `LogCategory` 建立自己的分類:

```csharp
public sealed class NetworkCategory : LogCategory
{
    public static readonly NetworkCategory Instance = new NetworkCategory();
    private NetworkCategory() : base("Network") { tagColorHex = "#4FC3F7"; }
}

Log.Warning("Timeout", NetworkCategory.Instance);   // Console / 視窗顯示 "[Network]Timeout"
```

- `Tag`: 預設為 `[IdentifierName]`; 覆寫 `UpdateTag()` 可自訂格式 (參考 `SystemCategory`)。
- `tagColorHex`: **只影響標籤**的顏色。訊息文字的顏色由等級決定, 見下方 `SetLevelColor`。
- `SystemCategory(object value, params string[] tags)`: 標籤為 `[型別名][tag1][tag2]...`。
- 訊息與標籤不跳脫 rich text: `<color>`、`<b>` 等標記會被 Console 與畫面視窗解讀, 可當成格式功能使用。要原樣顯示 `<` 開頭的文字時需自行處理。

## DebugWindow API

所有設定都是靜態的, 視窗建立前就可以設定; 每次進入 Play 時恢復為設定檔的值。下表「預設」為 `DebugWindowOptions` 欄位初始值的現況, 以原始碼為準。

| 成員 | 預設 | 說明 |
|---|---|---|
| `History` | — | Log 訊息的歷史紀錄 (`LogHistorySink`, 最多 `LogCapacity` 筆); Clear 按鈕只清這份 |
| `UnityHistory` | — | Unity 引擎訊息的歷史紀錄 (最多 `UnityCapacity` 筆); Clear 按鈕不清 |
| `LogCapacity` / `UnityCapacity` | 200 / 200 | 兩份紀錄的容量, 唯讀; 由設定檔在啟動時決定 |
| `ShowLogMessages` / `ShowUnityMessages` | `true` | 依來源篩選; 視窗上方的來源按鈕同步此設定, 點擊輪替 Unity (只顯示 Unity) → Unity Off (只顯示 Log) → Both (全部)。兩者都關時按鈕顯示 None, 點擊回到 Both |
| `Size` | 600 × 180 | 視窗大小 (縮放前單位); 最小 80 × 60 |
| `Position` | 右下角 | 視窗左上角位置; 設定後不再自動貼齊右下角, 拖曳也會更新 |
| `ResetPosition()` | — | 恢復右下角 |
| `UsesDefaultPosition` | `true` | 目前是否使用右下角預設位置 (唯讀) |
| `IsDraggable` | `true` | 是否可拖曳標題列移動視窗 |
| `AutoScroll` | `true` | 有新訊息時捲到最下方; 由關轉開時立即捲到最下方; 視窗的 Auto 開關同步此設定 |
| `Scale` | `0` | 介面縮放倍率; `0` 表示依 `Screen.height / 720` 自動計算 (最小 1 倍), 視窗佔畫面的比例固定 |
| `SearchText` | `""` | 搜尋文字; 比對標籤與訊息, 不分大小寫 |
| `IsLevelVisible(level)` / `SetLevelVisible(level, bool)` | 全部顯示 | 依等級篩選; 視窗上方 N / W / E / D 開關同步此設定 |
| `History.Clear()` | — | 清空 Log 歷史紀錄; 視窗的 Clear 按鈕也是呼叫它 |
| `ShowTime` | `false` | 在訊息下方第二行開頭顯示時間 `(HH:mm:ss.ff)` (當地時間); 視窗的 Time 開關同步此設定 |
| `ShowLocation` | `false` | 在第二行顯示發生位置 `檔案:行號`; Log 訊息為呼叫端, Unity 訊息取堆疊中第一個有原始碼位置的框架; 視窗的 Loc 開關同步此設定 |
| `Collapse` | `false` | 合併相同訊息; 視窗的 Collapse 開關同步此設定 |
| `GetLevelColor(level)` / `SetLevelColor(level, hex)` | Warning `#FFD54F`、Error `#FF6B6B`、Debug `#9E9E9E`、Normal 無 | 訊息文字顏色; null 或空字串表示預設文字色 |
| `VisibleButtons` | `All` | 顯示哪些按鈕 (`DebugWindowButtons`: Levels、Source、Clear、Close、Search、Time、Location、Collapse、Copy)。`Copy` 同時控制每一列的複製按鈕與工具列的 `Copy`。**舊設定檔**: 存成特定組合 (非 `All`, 即不是 `-1`) 的 `visibleButtons` 沒有 `Copy` 位元 (`1 << 8`), 複製按鈕會被隱藏; 需要時在 Inspector 勾選 Copy。隱藏的只是按鈕, 功能仍可透過靜態屬性控制; 隱藏後不留空位, 第一列全部隱藏時整列不畫, 沒有搜尋框時第二列的開關靠右。`Auto` 開關不在旗標內, 一律顯示。隱藏 `Close` 時觸控裝置上無法用按鈕關閉視窗 |

### 視窗操作

- 上方一列: N / W / E / D 等級開關、Log / Unity 來源開關、`Copy`、`Clear`、`X`; 第二列: 搜尋框、`Time`、`Loc`、`Collapse`、`Auto` 方形核取方塊 (自繪貼圖, 開啟時藍底白勾, 滑過時變亮; 點擊方框或文字都會切換); 下方為捲動清單。
- 每筆最左側為行號 `[n]`: 該筆的流水號 (`LogHistorySink` 共用, 依寫入順序遞增)。同一筆的號碼固定, 不因舊紀錄被擠掉或篩選而改變; 被篩掉或屬於另一份紀錄的號碼會留空缺。欄寬依目前最大的號碼位數決定 (最少 3 位)。
- 行號右邊為 `+` / `-` 展開按鈕, 展開後以小一級的字顯示堆疊追蹤; 沒有堆疊追蹤時按鈕呈現無法使用。
- 展開按鈕右邊為 `C` 複製按鈕 (位置與尺寸同展開按鈕), 複製該列; 工具列的 `Copy` 複製目前看得到的全部內容。見下方「複製」。
- 行號、展開按鈕、次數膠囊都以訊息第一行文字的中線垂直置中 (依字型實際行高計算)。
- 列與列之間有 1px 分隔線, 也當作檢查對齊的參考線。
- `Time` / `Loc` 開啟時, 訊息下方多一行小字 (與訊息間多留 2px): `(時間)  路徑:行號`。Editor 中點擊路徑會開啟該檔案 (專案內的檔案用 `AssetDatabase.OpenAsset`, 與 Console 相同); 建置版只顯示。
- `Collapse`: 等級、分類 (同一實例)、內容與呼叫端位置都相同的訊息合併成一列, 顯示在第一次出現的位置並沿用其流水號 (同 Unity Console), 最右側以膠囊標籤顯示次數; 合併多次的列不顯示時間。只合併仍在紀錄中的訊息 (兩份紀錄各自容量內的最新紀錄), 被擠掉的不計入次數。
- 兩份紀錄依寫入順序 (`LogHistorySink` 共用的流水號) 合併顯示; 來源、等級、搜尋三種篩選同時套用。
- `Clear` 只清 Log 訊息, Unity 訊息保留 (避免重要的引擎錯誤一起被刪)。Unity 訊息只會因超過容量被擠掉或重新進入 Play 而消失。
- `Clear` 為兩段式確認: 第一次點擊進入清除狀態 (按鈕變黃), 再點一次才清除 (按住時紅色); 在視窗其他位置按下滑鼠即取消。
- `X` 等同 `DebugLogs.isGUILog = false`, 一併恢復 Console 輸出。
- 開啟視窗與拖曳結束時: 寬或高超過畫面時改為畫面該方向的一半; 自訂位置超出畫面時拉回邊緣。

### 複製

以 `UnityEngine.GUIUtility.systemCopyBuffer` 寫入系統剪貼簿 (Runtime API, 建置版也可用)。

- 每一列的 `C`: 複製該列; 合併 (Collapse) 時為第一次出現的那筆, 並附上次數。
- 工具列的 `Copy`: 複製**目前篩選後看得到的全部內容** (套用等級、來源、搜尋與 Collapse), 依顯示順序, 每筆之間空一行。展開狀態不影響內容。
- 按下後該按鈕約 1 秒 (`CopiedFeedbackSeconds`, 以 `Time.realtimeSinceStartup` 判斷) 顯示已複製狀態: 底色變綠, 工具列按鈕文字改為 `Copied`。不另開提示。狀態在 `Layout` 事件決定, 避免同一影格的 `Layout` / `Repaint` 版面不一致。
- 文字由 Core 的 `LogText.FormatForCopy(in LogEntry entry, int count, string projectRoot)` 組成 (每次呼叫配置, 只在按下按鈕時發生)。Editor 中傳入視窗取得的專案根目錄 (`Application.dataPath` 的上一層), 建置版傳 `null`:

```text
[Error] [Tag] 訊息內容 (x3)
(19:47:27.20) Assets/Foo/CallLog.cs:68
堆疊文字 (StackTrace.Text, 已清理的版本)
```

| 項目 | 規則 |
|---|---|
| 第一行 | `[等級] ` + 分類 Tag (空白則省略, 後接一個空白) + 訊息; `count` 大於 1 時行尾加 ` (x次數)` |
| 第二行 | `(時間) 路徑:行號`, 格式同視窗第二行 (`LogText.FormatTime`、`ToDisplayPath`; 位置取法同 `LogText.TryGetLocation`)。**不受** 視窗 Time / Loc 開關影響, 合併多次時也輸出第一次出現的時間; 沒有資料的部分省略, 兩者都沒有就省略整行 |
| 堆疊 | 有堆疊時輸出 `StackTrace.Text`, 不受展開狀態影響 |
| rich text | 分類與訊息以 `LogText.StripRichText` 去掉 Unity rich text 標籤: `<b>` `</b>` `<i>` `</i>` `<color=...>` `</color>` `<size=...>` `</size>` `<material=...>` `</material>` `<quad .../>`。只去掉這些已知標籤 (區分大小寫), 其他 `<...>` (例如 `List<int>`、`<OnGUIFunc>`、`<0x...>`) 保留; 未閉合或巢狀的 `<` 不拋例外, 原樣保留。堆疊文字不處理 |
| 換行 | 一律為 `\n` (堆疊文字中的 `\r\n` 也轉為 `\n`) |

### 可調整的外觀參數

都是 `DebugWindow.cs` 裡的常數或 `CreateStyles()` 中的值, 沒有公開 API; 改程式後重新編譯生效。

| 位置 | 目前值 | 作用 |
|---|---|---|
| `FoldWidth` | `18f` | 展開按鈕寬度; 第二行與堆疊追蹤的縮排也依此計算 |
| `FoldHeight` | `14f` | 展開按鈕高度; 改了仍會自動置中 |
| `BadgeHeight` | `16f` | 次數膠囊高度; 膠囊貼圖與左右圓角半徑依此產生 |
| `CheckSize` | `14f` | 第二列核取方塊的方框大小; 貼圖依此產生 |
| `CreateCheckTexture()` 的像素顏色 | 邊框 `(150, 150, 150)`、底 `(30, 30, 30, 200)`; 開啟時 `(74, 144, 226)` 與白勾 | 核取方塊外觀 |
| `DetailOffset` | `2f` | 第二行 (時間、位置) 與訊息之間多留的距離 |
| `separatorColor` | 白色, 透明度 `0.12f` | 列分隔線顏色 |
| `CopiedFeedbackSeconds` | `1f` | 按下複製後顯示已複製狀態的秒數 |
| `copiedColor` | `(0.45, 0.85, 0.45)` | 已複製狀態的按鈕底色 |
| `CreateStyles()` 的 `textCenter` | `padding.top + lineHeight / 2` | 三個小元件的置中基準; 整體偏高或偏低時在這裡加減幾 px, 三者一起移動 |
| `CreateStyles()` 的 `detailSize` | 主訊息字型大小 - 2 (最小 8) | 第二行、堆疊追蹤、行號、展開按鈕與膠囊的字型大小 |
| `CreateCapsuleTexture()` 的像素顏色 | `(110, 110, 110)`, alpha `220` | 膠囊底色 |

### 繪製與效能

- 只在 IMGUI 的 `Layout` 事件更新資料, 讓 `Layout` 與 `Repaint` 的控制項數量一致, 避免 `Getting control X's position...` 例外。
- 用 `LogHistorySink.CopyTo(dest, sequences, out version)` 在同一次鎖定內取得資料、流水號與版本號; 兩份紀錄各自只為新進來的幾筆組字串, 舊字串直接搬移。改變等級顏色時才全部重組。
- 篩選與搜尋只在條件或內容改變時重算。合併相同訊息時一併重新分組 (重用同一個 `Dictionary`)。
- 時間、位置、行號與次數字串都在第一次顯示時才組, 之後快取。膠囊標籤的貼圖在建立樣式時產生一次 (16 × 16), 元件銷毀時釋放。
- 展開按鈕與 Time / Loc 開關的變更延到下一個 `Layout` 事件才套用。展開狀態以流水號記錄, 新訊息進來或篩選改變時不會錯位。
- 隱藏時設 `enabled = false`, `OnGUI` 完全不會被呼叫。
- GUILayout 本身每幀會有少量配置, 無法完全消除; 以除錯視窗而言可接受。

### 生命週期

- 視窗元件掛在 `CrossObject` 上, 跟著它跨場景, 也跟著它一起銷毀。
- `CrossObject` 被銷毀後再開啟 `isGUILog`, 會重新建立 `CrossObject` 與視窗元件; 歷史紀錄不受影響。

## 平台

- IMGUI 在所有目標平台都可用, 視窗不限 Editor 或 Development Build。
- 自動縮放依畫面高度計算, 不使用 `Screen.dpi` (實體 DPI 不隨解析度改變, 調低解析度時視窗反而佔更大比例)。不考慮實體尺寸, 小螢幕手機上的字可能偏小。**Android 與 iOS 未驗證**。
- 沒有內建快捷鍵或手勢; 開關由專案呼叫 `DebugLogs.isGUILog`。
- 複製使用 `GUIUtility.systemCopyBuffer`: Editor (PlayMode 測試與人工) 與 macOS IL2CPP 建置 (人工, 2026-11-05) 已確認; Android / iOS 未驗證。

## 測試

| 檔案 | 類型 | 內容 |
|---|---|---|
| `dotnet/Library.Tests/LogTests.cs` | dotnet | 多 sink、重複加入、移除、`HasSink`、`isDebugLog`、`isPrintLog`、`IsEnabled`、`Format` (4 個等級、有 / 無分類、關閉時不格式化參數)、`Log.Debug` 的 `[Conditional]` symbol、公開寫入方法只有 10 個、`SystemCategory` 標籤 |
| `dotnet/Library.Tests/LogEntryInfoTests.cs` | dotnet | 呼叫端位置 (含 `Format` 與 3 個字串參數的多載選擇)、UTC 時間、堆疊追蹤預設值與開關、堆疊從呼叫端開始、`LogText` 的時間 / 路徑 / Unity 位置解析 |
| `dotnet/Library.Tests/LogStackTraceCleanTests.cs` | dotnet | 堆疊文字清理: IL2CPP 兩種格式、路徑含空白 / Windows / `Packages/` / 都沒有、規則 B / C 任意行號、`<filename unknown>` 與 Unity 格式原樣、空字串與換行、`\r\n` 保留、Mono 行尾空白、先去掉 `Torben.Log.` 框架、`FromText` 經過清理 |
| `dotnet/Library.Tests/LogHistorySinkTests.cs` | dotnet | 覆蓋最舊、Version 遞增、`CopyTo` 順序與版本號、`Clear`、容量小於 1 時丟例外、流水號遞增且跨紀錄共用 |
| `dotnet/Library.Tests/LogSinkIsolationTests.cs` | dotnet | sink 例外隔離: 第一個 sink 失敗時第二個仍收到且呼叫端無例外、同一 sink 只回報一次且不被移除、`RemoveSink` / `ClearSinks` 後再回報、不同 sink 各回報一次、處理器為 null、處理器內呼叫 `Log` 不遞迴、處理器拋例外、無例外時寫入不配置 |
| `dotnet/Library.Tests/LogTextCopyTests.cs` | dotnet | `FormatForCopy`: 有 / 無分類、空 Tag、Collapse 次數、rich text、有 / 無時間與位置、Unity 訊息從堆疊取位置、堆疊、保留 `<OnGUIFunc>` / `List<int>`、只用 `\n`; `StripRichText`: 已知標籤、其他角括號保留、無標籤時回傳原字串、巢狀與未閉合不拋例外 |
| `dotnet/Library.Tests/LogOptionsTests.cs` | dotnet | `LogOptions` 欄位初始值、`Apply` / `Capture` 往返、堆疊追蹤開關對應、未定義 / 缺少 / 重複等級與 null 時不拋例外 |
| `Scripts/Tests/PlayMode/LogSettingsInstallTests.cs` | PlayMode | 啟動時從 Resources 讀到預設檔; 沒有設定檔時與修改前相同且只警告一次; 套用預設檔; 覆蓋檔整份取代; `openWindowOnStart`; `DebugWindowOptions.Capture`; 容量 5 與 7 時兩份紀錄、合併順序、來源 / 搜尋篩選與 Collapse 正確; 隱藏按鈕後繪製不出錯; Development Alert (沒有問題 / Off 無輸出、Warning / Error 各一則、剛好在上限不提醒、多個問題合併一則、找不到設定檔為 Warning、`UnityHistory` 收得到、`isUnityLog` 關閉時不收集); sink 例外以 `LogException` 回報一次且帶型別名稱 |
| `Scripts/Tests/EditMode/ScriptingDefineUtilityTests.cs` | EditMode | define 字串的判斷 / 新增 / 移除 (null、空字串、前後空白、重複、大小寫) |
| `Scripts/Tests/EditMode/LogLevelColorDrawerTests.cs` | EditMode | Color picker 與色碼互轉 (不透明 / 透明、空字串、無法解析、預設顏色往返) |
| `Scripts/Tests/EditMode/LogSettingsCheckerTests.cs` | EditMode | 狀態檢查 1～4 (暫存 asset, TearDown 刪除)、Link 修正、預設檔路徑與 Resources 讀到同一個檔案、函式庫資料夾判斷; Development Alert 頁面編輯生效中的檔案 (有 / 無覆蓋檔)、Undo、欄位帶 `HideInInspector`; 覆蓋檔 Missing 判斷 (刪除暫存 asset); 容量上限常數與 Runtime 共用 |
| `Scripts/Tests/PlayMode/DebugWindowTests.cs` | PlayMode | 安裝歷史紀錄、建立 / 隱藏視窗、`isGUILog` 抑制 Console、`isConsoleWithGUI`、Debug 過濾、設定讀寫、右下角預設位置、Unity 訊息收集 / 不重複收自己的 Log / `isUnityLog` 關閉 / 不受 `isPrintLog` 影響、清 Log 不影響 Unity、來源篩選設定、顯示設定 (Time / Loc / Collapse) 讀寫、Log 紀錄帶時間與呼叫端、Unity 訊息帶時間與堆疊位置、開啟所有顯示選項時繪製不出錯; 複製單筆 (rich text 去除、時間與位置) / 全部 (等級、來源、搜尋、Collapse 篩選與顯示順序) / Error 帶堆疊後 `systemCopyBuffer` 內容正確; `Copy` 旗標關閉時兩種按鈕都不畫 |

執行方式見 [dotnet 測試手冊](dotnet-test.md) 與 [Unity CLI 手冊](unity-cli-pipeline.md)。視窗的實際畫面 (版面、拖曳、搜尋、Log / Unity 合併順序與開關、展開按鈕、第二行字體大小、點擊路徑開檔、合併次數) 需在 Play 時人工確認。複製按鈕的已複製回饋與貼到文字編輯器的內容也需人工確認。設定頁的顯示與 Undo (含 Development Alert)、選單勾選狀態、`Log GUI Window` 切換、Create Project Override 流程、Save Current State 的對話框也需人工確認。
