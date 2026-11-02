# Log 系統說明

本文說明 `Torben.Log` 與畫面 Log 視窗 (`DebugWindow`) 的架構、開關、API 與已知限制。快速上手見 [Log 快速使用說明](manual/log-quickstart.md)。

## 架構

```text
Log.Information / Warning / Error / Debug   (Core, 純 C#)
        │  isPrintLog、isDebugLog 過濾
        ▼
   sink 清單 (copy-on-write 陣列)
        ├─ DebugLogs         → Unity Console        (Runtime)
        └─ LogHistorySink    → 最近 200 筆歷史紀錄  (Core)
                                   ▲
                              DebugWindow 讀取並繪製 (Runtime, IMGUI)
```

| 檔案 | 組件 | 責任 |
|---|---|---|
| `Scripts/Core/Logs/Log.cs` | Torben.Core | 對外入口; 總開關與 Debug 開關; 管理 sink 清單 |
| `Scripts/Core/Logs/LogModels.cs` | Torben.Core | `LogLevel`、`ILogSink`、`LogEntry` |
| `Scripts/Core/Logs/LogCategory.cs` | Torben.Core | 分類標籤 (`Tag`) 與標籤顏色 (`tagColorHex`) |
| `Scripts/Core/Logs/LogHistorySink.cs` | Torben.Core | 固定容量環狀緩衝區, 保留最新的 N 筆 |
| `Scripts/Runtime/Logs/DebugLogs.cs` | Torben.Runtime | Console 輸出; 啟動時安裝 sink; `isGUILog` 開關 |
| `Scripts/Runtime/Logs/DebugWindow.cs` | Torben.Runtime | 畫面視窗 (篩選、搜尋、上色、拖曳、縮放) |
| `Scripts/Runtime/StaticObject/CrossObject.cs` | Torben.Runtime | 跨場景物件; DebugWindow 掛在它上面 |

路徑皆相對於 `Assets/[TorbenJuniorUtility]/`。

## 開關

| 開關 | 預設 | 作用 |
|---|---|---|
| `Log.isPrintLog` | `false` | 總開關。關閉時不產生任何訊息, 所有 sink 都收不到 |
| `Log.isDebugLog` | `false` | 只管 `LogLevel.Debug`。關閉時 Debug 訊息不會送到任何 sink |
| `DebugLogs.isGUILog` | `false` | 開啟: 顯示畫面視窗, 並停止輸出到 Console。關閉: 隱藏視窗, 恢復 Console |
| `DebugLogs.isConsoleWithGUI` | `false` | `isGUILog` 開啟時仍同時輸出到 Console |

歷史紀錄不受 `isGUILog` 影響: 只要訊息通過前兩個開關就會被記錄, 所以視窗晚點開啟也看得到之前的訊息。

## 載入順序

| 階段 | 發生的事 |
|---|---|
| `SubsystemRegistration` | `DebugLogs.Install`: 清空 sink, 加入 Console 與歷史紀錄兩個 sink; 重設 `isGUILog` 與 DebugWindow 的所有設定 |
| `BeforeSceneLoad` 之後 | 專案可以修改開關與 DebugWindow 設定 |
| `AfterSceneLoad` | 若 `isGUILog` 已開啟, 此時才建立視窗元件 (掛在 `CrossObject` 上) |
| 之後 | 第一次開啟 `isGUILog` 時建立視窗元件; 之後切換只改 `enabled` |

- 在 `SubsystemRegistration` 之前或同階段做的設定會被重設; 請在 `BeforeSceneLoad` 或之後設定。
- `isPrintLog` 開啟之前呼叫的 Log 不會產生, 也不會補記。要看到載入初期的訊息, 需在更早的階段開啟 `isPrintLog`。
- 每次進入 Play 都會重設 (包含關閉 Domain Reload 時), 不會殘留上一次的 sink 或設定。

## Log API (`Torben.Log`)

| 成員 | 說明 | 配置 |
|---|---|---|
| `Information / Warning / Error / Debug(message, category = null)` | 產生訊息並送給每個 sink; `category` 為 null 時使用空分類 | 本身零配置; 各 sink 自行決定 |
| `AddSink(ILogSink)` | 加入 sink; 已存在或 null 時不做事 | 每次呼叫配置新陣列 |
| `RemoveSink(ILogSink)` | 移除 sink; 不存在時不做事 | 有移除時配置新陣列 |
| `ClearSinks()` | 移除全部 sink | 零配置 |
| `HasSink` | 是否至少有一個 sink | 零配置 |

sink 清單採 copy-on-write: 新增或移除時建立新陣列再整個替換, 寫入時只讀取當下的陣列, 不需加鎖。新增或移除不應在每幀執行。

自訂 sink 只需實作 `ILogSink.Write(in LogEntry entry)`, 再用 `Log.AddSink` 加入。`LogEntry` 包含 `Level`、`Category`、`Message`。

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

## DebugWindow API

所有設定都是靜態的, 視窗建立前就可以設定; 每次進入 Play 時恢復預設值。

| 成員 | 預設 | 說明 |
|---|---|---|
| `History` | — | 歷史紀錄 (`LogHistorySink`, 最多 200 筆) |
| `Size` | 600 × 180 | 視窗大小 (縮放前單位); 最小 80 × 60 |
| `Position` | 右下角 | 視窗左上角位置; 設定後不再自動貼齊右下角, 拖曳也會更新 |
| `ResetPosition()` | — | 恢復右下角 |
| `IsDraggable` | `true` | 是否可拖曳標題列移動視窗 |
| `AutoScroll` | `true` | 有新訊息時捲到最下方 |
| `Scale` | `0` | 介面縮放倍率; `0` 表示依 `Screen.height / 720` 自動計算 (最小 1 倍), 視窗佔畫面的比例固定 |
| `SearchText` | `""` | 搜尋文字; 比對標籤與訊息, 不分大小寫 |
| `IsLevelVisible(level)` / `SetLevelVisible(level, bool)` | 全部顯示 | 依等級篩選; 視窗上方 N / W / E / D 開關同步此設定 |
| `GetLevelColor(level)` / `SetLevelColor(level, hex)` | Warning `#FFD54F`、Error `#FF6B6B`、Debug `#9E9E9E`、Normal 無 | 訊息文字顏色; null 或空字串表示預設文字色 |

### 繪製與效能

- 只在 IMGUI 的 `Layout` 事件更新資料, 讓 `Layout` 與 `Repaint` 的控制項數量一致, 避免 `Getting control X's position...` 例外。
- 用 `LogHistorySink.CopyTo(dest, out version)` 在同一次鎖定內取得資料與版本號; 有新訊息時只為新進來的幾筆組字串, 舊字串直接搬移。改變等級顏色時才全部重組。
- 篩選與搜尋只在條件或內容改變時重算。
- 隱藏時設 `enabled = false`, `OnGUI` 完全不會被呼叫。
- GUILayout 本身每幀會有少量配置, 無法完全消除; 以除錯視窗而言可接受。

### 生命週期

- 視窗元件掛在 `CrossObject` 上, 跟著它跨場景, 也跟著它一起銷毀。
- `CrossObject` 被銷毀後再開啟 `isGUILog`, 會重新建立 `CrossObject` 與視窗元件; 歷史紀錄不受影響。

## 平台

- IMGUI 在所有目標平台都可用, 視窗不限 Editor 或 Development Build。
- 自動縮放依畫面高度計算, 不使用 `Screen.dpi` (實體 DPI 不隨解析度改變, 調低解析度時視窗反而佔更大比例)。不考慮實體尺寸, 小螢幕手機上的字可能偏小。**Android 與 iOS 未驗證**。
- 沒有內建快捷鍵或手勢; 開關由專案呼叫 `DebugLogs.isGUILog`。

## 測試

| 檔案 | 類型 | 內容 |
|---|---|---|
| `dotnet/Library.Tests/LogTests.cs` | dotnet | 多 sink、重複加入、移除、`HasSink`、`isDebugLog`、`isPrintLog` |
| `dotnet/Library.Tests/LogHistorySinkTests.cs` | dotnet | 覆蓋最舊、Version 遞增、`CopyTo` 順序與版本號 |
| `Scripts/Tests/PlayMode/DebugWindowTests.cs` | PlayMode | 安裝歷史紀錄、建立 / 隱藏視窗、`isGUILog` 抑制 Console、`isConsoleWithGUI`、Debug 過濾、設定讀寫、右下角預設位置 |

執行方式見 [dotnet 測試手冊](dotnet-test.md) 與 [Unity CLI 手冊](unity-cli-pipeline.md)。視窗的實際畫面 (版面、拖曳、搜尋) 需在 Play 時人工確認。
