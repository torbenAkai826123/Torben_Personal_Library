# Log 快速使用說明

> 本文是給人看的入門說明；完整行為與 API 以 [Log 系統說明](../log-system.md) 為準。

一句話：**用 `Log.Xxx()` 寫訊息，用開關決定要不要輸出、輸出到 Console 還是畫面視窗**。

## 1. 打開總開關

`Log.isPrintLog` 預設是關的，不打開的話什麼都不會出現。

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void SetupLog()
{
    Torben.Log.isPrintLog = true;    // 總開關
    Torben.Log.isDebugLog = true;    // 需要看 Log.Debug 時才開
}
```

要在 `BeforeSceneLoad` 或更晚的時間設定，太早設定會在啟動時被重設。

## 2. 寫訊息

```csharp
Log.Information("載入完成");
Log.Warning("找不到存檔，使用預設值");
Log.Error("連線失敗");
Log.Debug("hp=" + hp);               // 只有 isDebugLog 開啟時才會輸出
```

## 3. 加上分類標籤 (可省略)

```csharp
public sealed class NetworkCategory : LogCategory
{
    public static readonly NetworkCategory Instance = new NetworkCategory();
    private NetworkCategory() : base("Network") { tagColorHex = "#4FC3F7"; }
}

Log.Warning("Timeout", NetworkCategory.Instance);   // 顯示為 [Network]Timeout
```

`tagColorHex` 只改標籤顏色；訊息顏色由等級決定 (Warning 黃、Error 紅、Debug 灰)。

## 4. 改用畫面視窗

系統錯誤訊息太多、蓋掉自己的訊息時，把訊息改顯示在遊戲畫面上：

```csharp
DebugLogs.isGUILog = true;           // 開啟視窗，Console 不再輸出
DebugLogs.isConsoleWithGUI = true;   // 想兩邊都看時再開
```

做成按鈕切換的例子：

```csharp
AddButton("GUI Status " + (DebugLogs.isGUILog ? "ON" : "OFF"),
    () => { DebugLogs.isGUILog = !DebugLogs.isGUILog; });
```

視窗開啟前的訊息也看得到 (最多保留最近 200 筆)。

視窗上可以：

- 用 **N / W / E / D** 切換顯示 Normal、Warning、Error、Debug。
- 在搜尋框輸入文字篩選 (不分大小寫)。
- 拖曳標題列移動視窗。

## 5. 調整視窗 (可省略)

```csharp
DebugWindow.Size = new Vector2(600, 180);
DebugWindow.Position = new Vector2(10, 10);   // DebugWindow.ResetPosition() 回到右下角
DebugWindow.IsDraggable = false;
DebugWindow.SetLevelVisible(LogLevel.Debug, false);
DebugWindow.SetLevelColor(LogLevel.Normal, "#FFFFFF");
DebugWindow.SearchText = "Network";
DebugWindow.Scale = 2f;                        // 0 = 依螢幕 DPI 自動
```

## 常見問題

| 狀況 | 原因 |
|---|---|
| 什麼都沒輸出 | `Log.isPrintLog` 沒開 |
| `Log.Debug` 沒輸出 | `Log.isDebugLog` 沒開 |
| Console 突然沒訊息 | `DebugLogs.isGUILog` 開著，訊息改到畫面視窗了 |
| 啟動最初幾行沒出現 | 那時 `isPrintLog` 還沒打開，這些訊息不會補記 |
| 設定好像沒生效 | 設定得太早被重設，改在 `BeforeSceneLoad` 之後設定 |
