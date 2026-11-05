using System.Collections;
using NUnit.Framework;
using Torben;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DebugWindowTests
{
    private bool savedPrintLog;
    private bool savedDebugLog;
    private LogSettings defaults;

    [SetUp]
    public void SetUp()
    {
        savedPrintLog = Log.isPrintLog;
        savedDebugLog = Log.isDebugLog;
        // 以欄位初始值開始, 不受專案設定檔 (含覆蓋檔) 影響
        defaults = ScriptableObject.CreateInstance<LogSettings>();
        DebugLogs.Install(defaults);
        Log.isPrintLog = true;
        Log.isDebugLog = false;
    }

    [TearDown]
    public void TearDown()
    {
        DebugLogs.isGUILog = false;
        DebugLogs.isConsoleWithGUI = false;
        DebugLogs.isUnityLog = true;
        DebugWindow.ShowLogMessages = true;
        DebugWindow.ShowUnityMessages = true;
        DebugWindow.ShowTime = false;
        DebugWindow.ShowLocation = false;
        DebugWindow.Collapse = false;
        DebugWindow.AutoScroll = true;
        DebugWindow.Minimized = false;
        DebugWindow.VisibleButtons = DebugWindowButtons.All;
        DebugWindow.ResetPosition();
        DebugWindow.SearchText = string.Empty;
        foreach (LogLevel level in System.Enum.GetValues(typeof(LogLevel)))
            DebugWindow.SetLevelVisible(level, true);
        if (CrossObject.IsReady) Object.Destroy(CrossObject.Instance.gameObject);
        DebugLogs.Install(DebugLogs.StartupSettings);   // 恢復進入 Play 時的安裝結果
        Object.Destroy(defaults);
        Log.isPrintLog = savedPrintLog;
        Log.isDebugLog = savedDebugLog;
    }

    [Test]
    public void Install_AddsHistorySink()
    {
        int before = DebugWindow.History.Version;

        Log.Information("history");
        LogAssert.Expect(LogType.Log, "history");

        Assert.That(DebugWindow.History.Version, Is.EqualTo(before + 1));
    }

    [UnityTest]
    public IEnumerator GUILogOn_CreatesEnabledWindowOnCrossObject()
    {
        DebugLogs.isGUILog = true;
        yield return null;

        var window = CrossObject.Instance.GetComponent<DebugWindow>();
        Assert.That(window, Is.Not.Null);
        Assert.That(window.enabled, Is.True);

        DebugLogs.isGUILog = false;
        Assert.That(window.enabled, Is.False);
    }

    [Test]
    public void GUILogOn_SuppressesConsoleButKeepsHistory()
    {
        DebugLogs.isGUILog = true;
        int before = DebugWindow.History.Version;

        Log.Error("gui only");

        LogAssert.NoUnexpectedReceived();
        Assert.That(DebugWindow.History.Version, Is.EqualTo(before + 1));
    }

    [Test]
    public void GUILogOn_ConsoleWithGUI_AlsoWritesConsole()
    {
        DebugLogs.isGUILog = true;
        DebugLogs.isConsoleWithGUI = true;

        Log.Warning("both");

        LogAssert.Expect(LogType.Warning, "both");
    }

    [Test]
    public void DebugLevel_Disabled_NotRecorded()
    {
        int before = DebugWindow.History.Version;

        Log.Debug("hidden");

        Assert.That(DebugWindow.History.Version, Is.EqualTo(before));
    }

    [Test]
    public void Settings_RoundTrip()
    {
        var savedSize = DebugWindow.Size;
        DebugWindow.Size = new Vector2(300f, 150f);
        DebugWindow.Position = new Vector2(5f, 6f);
        DebugWindow.SetLevelVisible(LogLevel.Warning, false);
        DebugWindow.SetLevelColor(LogLevel.Normal, "#00FF00");
        DebugWindow.SearchText = "abc";

        Assert.That(DebugWindow.Size, Is.EqualTo(new Vector2(300f, 150f)));
        Assert.That(DebugWindow.Position, Is.EqualTo(new Vector2(5f, 6f)));
        Assert.That(DebugWindow.IsLevelVisible(LogLevel.Warning), Is.False);
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Normal), Is.EqualTo("#00FF00"));
        Assert.That(DebugWindow.SearchText, Is.EqualTo("abc"));

        DebugWindow.Size = savedSize;
        DebugWindow.SetLevelColor(LogLevel.Normal, null);
    }

    [Test]
    public void ResetPosition_ReturnsToBottomRight()
    {
        DebugWindow.Position = new Vector2(5f, 6f);

        DebugWindow.ResetPosition();

        var size = DebugWindow.Size;
        Assert.That(DebugWindow.Position.x + size.x, Is.GreaterThan(5f + size.x));
    }

    [Test]
    public void Open_OversizedWindow_ShrinksToHalfScreen()
    {
        var savedSize = DebugWindow.Size;
        float s = DebugWindow.Scale = 1f;
        DebugWindow.Size = new Vector2(Screen.width * 2f, Screen.height * 2f);

        DebugLogs.isGUILog = true;

        Assert.That(DebugWindow.Size, Is.EqualTo(new Vector2(Screen.width / s * 0.5f, Screen.height / s * 0.5f)));
        DebugWindow.Size = savedSize;
        DebugWindow.Scale = 0f;
    }

    [Test]
    public void Open_OffscreenPosition_ClampedToEdge()
    {
        DebugWindow.Scale = 1f;
        DebugWindow.Position = new Vector2(Screen.width + 100f, -50f);

        DebugLogs.isGUILog = true;

        var size = DebugWindow.Size;    // 畫面太小時開啟會縮小, 開啟後再取
        Assert.That(DebugWindow.Position, Is.EqualTo(new Vector2(Screen.width - size.x, 0f)));
        DebugWindow.Scale = 0f;
    }

    [Test]
    public void AutoScale_FollowsScreenHeight()
    {
        var savedSize = DebugWindow.Size;
        DebugWindow.Scale = 0f;
        DebugWindow.Size = new Vector2(Screen.width * 2f, Screen.height * 2f);

        DebugLogs.isGUILog = true;

        // 開啟時過大的視窗縮成畫面的一半, 畫面大小以自動倍率換算
        float s = Mathf.Max(1f, Screen.height / 720f);
        Assert.That(DebugWindow.Size.x, Is.EqualTo(Screen.width / s * 0.5f).Within(0.01f));
        Assert.That(DebugWindow.Size.y, Is.EqualTo(Screen.height / s * 0.5f).Within(0.01f));
        DebugWindow.Size = savedSize;
    }

    // 取出最新一筆紀錄
    private static LogEntry Latest(LogHistorySink history)
    {
        var entries = new LogEntry[200];
        int count = history.CopyTo(entries);
        return entries[count - 1];
    }

    [Test]
    public void UnityLog_DebugLog_RecordedInUnityHistory()
    {
        int before = DebugWindow.UnityHistory.Version;
        int logBefore = DebugWindow.History.Version;

        Debug.LogWarning("engine warning");
        LogAssert.Expect(LogType.Warning, "engine warning");

        Assert.That(DebugWindow.UnityHistory.Version, Is.EqualTo(before + 1));
        Assert.That(DebugWindow.History.Version, Is.EqualTo(logBefore));
        var entry = Latest(DebugWindow.UnityHistory);
        Assert.That(entry.Message, Is.EqualTo("engine warning"));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry.Category, Is.SameAs(UnityLogCategory.Instance));
        Assert.That(entry.Category.Tag, Is.EqualTo("[Unity]"));
    }

    [Test]
    public void UnityLog_OwnLogToConsole_NotRecordedAsUnity()
    {
        int before = DebugWindow.UnityHistory.Version;

        Log.Information("mine");
        LogAssert.Expect(LogType.Log, "mine");

        Assert.That(DebugWindow.UnityHistory.Version, Is.EqualTo(before));
    }

    [Test]
    public void UnityLog_Disabled_NotRecorded()
    {
        DebugLogs.isUnityLog = false;
        int before = DebugWindow.UnityHistory.Version;

        Debug.Log("ignored");
        LogAssert.Expect(LogType.Log, "ignored");

        Assert.That(DebugWindow.UnityHistory.Version, Is.EqualTo(before));
    }

    [Test]
    public void UnityLog_IndependentOfPrintLog()
    {
        Log.isPrintLog = false;
        int before = DebugWindow.UnityHistory.Version;

        Debug.Log("engine");
        LogAssert.Expect(LogType.Log, "engine");

        Assert.That(DebugWindow.UnityHistory.Version, Is.EqualTo(before + 1));
    }

    [Test]
    public void HistoryClear_KeepsUnityHistory()
    {
        Debug.Log("keep");
        LogAssert.Expect(LogType.Log, "keep");
        int unityVersion = DebugWindow.UnityHistory.Version;

        DebugWindow.History.Clear();

        Assert.That(DebugWindow.UnityHistory.Version, Is.EqualTo(unityVersion));
        Assert.That(Latest(DebugWindow.UnityHistory).Message, Is.EqualTo("keep"));
    }

    [Test]
    public void SourceFilters_DefaultOnAndRoundTrip()
    {
        Assert.That(DebugWindow.ShowLogMessages, Is.True);
        Assert.That(DebugWindow.ShowUnityMessages, Is.True);

        DebugWindow.ShowLogMessages = false;
        DebugWindow.ShowUnityMessages = false;

        Assert.That(DebugWindow.ShowLogMessages, Is.False);
        Assert.That(DebugWindow.ShowUnityMessages, Is.False);
    }

    [Test]
    public void DisplaySettings_DefaultOffAndRoundTrip()
    {
        Assert.That(DebugWindow.ShowTime, Is.False);
        Assert.That(DebugWindow.ShowLocation, Is.False);
        Assert.That(DebugWindow.Collapse, Is.False);

        DebugWindow.ShowTime = true;
        DebugWindow.ShowLocation = true;
        DebugWindow.Collapse = true;

        Assert.That(DebugWindow.ShowTime, Is.True);
        Assert.That(DebugWindow.ShowLocation, Is.True);
        Assert.That(DebugWindow.Collapse, Is.True);
    }

    [Test]
    public void AutoScroll_DefaultOnAndRoundTrip()
    {
        Assert.That(DebugWindow.AutoScroll, Is.True);

        DebugWindow.AutoScroll = false;

        Assert.That(DebugWindow.AutoScroll, Is.False);
    }

    [Test]
    public void Log_HistoryHasTimeAndCaller()
    {
        Log.Information("caller");
        LogAssert.Expect(LogType.Log, "caller");

        var entry = Latest(DebugWindow.History);
        Assert.That(entry.Time, Is.Not.EqualTo(default(System.DateTime)));
        Assert.That(entry.CallerMemberName, Is.EqualTo(nameof(Log_HistoryHasTimeAndCaller)));
        Assert.That(entry.CallerFilePath, Does.EndWith("DebugWindowTests.cs"));
    }

    [Test]
    public void UnityLog_RecordsTimeAndStackTrace()
    {
        Debug.LogWarning("with stack");
        LogAssert.Expect(LogType.Warning, "with stack");

        var entry = Latest(DebugWindow.UnityHistory);
        Assert.That(entry.Time, Is.Not.EqualTo(default(System.DateTime)));
        Assert.That(entry.StackTrace, Is.Not.Null);
        Assert.That(LogText.TryParseUnityLocation(entry.StackTrace.Text, out string path, out int line), Is.True);
        Assert.That(path, Does.EndWith("DebugWindowTests.cs"));
        Assert.That(line, Is.GreaterThan(0));
    }

    // 開啟所有顯示選項並畫幾個影格; OnGUI 出錯時 Unity 會記錄錯誤, 測試就會失敗
    [UnityTest]
    public IEnumerator Window_DrawsWithTimeLocationAndCollapse()
    {
        DebugWindow.ShowTime = true;
        DebugWindow.ShowLocation = true;
        DebugWindow.Collapse = true;
        DebugLogs.isGUILog = true;
        for (int i = 0; i < 3; i++) Log.Information("repeat");
        Log.Error("error with stack");
        Debug.Log("engine message");
        LogAssert.Expect(LogType.Log, "engine message");
        yield return null;
        yield return null;

        DebugWindow.Collapse = false;
        yield return null;
        yield return null;

        Assert.That(CrossObject.Instance.GetComponent<DebugWindow>().enabled, Is.True);
    }

    // 超過容量後舊紀錄被擠掉, 行號 (流水號) 位數增加; 切換自動捲動時繪製不出錯
    [UnityTest]
    public IEnumerator Window_DrawsAfterOverflowAndAutoScrollToggle()
    {
        DebugLogs.isGUILog = true;
        yield return null;

        for (int i = 0; i < 250; i++) Log.Format(LogLevel.Normal, "overflow {0}", i);
        DebugWindow.AutoScroll = false;
        yield return null;
        yield return null;

        Log.Information("after");
        DebugWindow.AutoScroll = true;
        yield return null;
        yield return null;

        Assert.That(CrossObject.Instance.GetComponent<DebugWindow>().enabled, Is.True);
    }
    // ---- 複製 ----

    private static DebugWindow OpenedWindow() => CrossObject.Instance.GetComponent<DebugWindow>();

    [UnityTest]
    public IEnumerator Copy_SingleRow_WritesFormattedTextToClipboard()
    {
        DebugLogs.isGUILog = true;
        Log.Information("first");
        Log.Warning("<b>second</b>");
        yield return null;
        yield return null;

        var window = OpenedWindow();
        GUIUtility.systemCopyBuffer = string.Empty;
        string text = window.CopyRow(1);

        Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(text));
        var lines = text.Split('\n');
        Assert.That(lines[0], Is.EqualTo("[Warning] second"), "rich text 標籤去除");
        Assert.That(lines[1], Does.StartWith("(").And.Contains(") ").And.EndWith("DebugWindowTests.cs:" + window.GetVisibleEntry(1).CallerLineNumber));
        Assert.That(lines.Length, Is.EqualTo(2), "Warning 預設不擷取堆疊");
    }

    [UnityTest]
    public IEnumerator Copy_All_FollowsFiltersSearchAndCollapse()
    {
        DebugLogs.isGUILog = true;
        Log.Information("apple");
        Log.Information("banana");
        Log.Warning("apple pie");
        for (int i = 0; i < 3; i++) Log.Information("apple");
        Debug.Log("unity apple");
        DebugWindow.SearchText = "apple";
        DebugWindow.SetLevelVisible(LogLevel.Warning, false);
        DebugWindow.ShowUnityMessages = false;
        DebugWindow.Collapse = true;
        yield return null;
        yield return null;

        string text = OpenedWindow().CopyAll();

        Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(text));
        var items = text.Split(new[] { "\n\n" }, System.StringSplitOptions.None);
        // banana (搜尋)、Warning (等級)、Unity (來源) 被篩掉; 合併比對呼叫端行號, 兩處呼叫各成一組
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0], Does.StartWith("[Normal] apple\n"));
        Assert.That(items[1], Does.StartWith("[Normal] apple (x3)\n"));

        DebugWindow.Collapse = false;
        DebugWindow.ShowUnityMessages = true;
        yield return null;
        yield return null;
        items = OpenedWindow().CopyAll().Split(new[] { "\n\n" }, System.StringSplitOptions.None);
        Assert.That(items.Length, Is.EqualTo(5));
        Assert.That(items[4], Does.StartWith("[Normal] [Unity] unity apple"), "依顯示順序");
    }

    [UnityTest]
    public IEnumerator Copy_ErrorRow_IncludesStackTrace()
    {
        DebugLogs.isGUILog = true;
        Log.Error("broken");
        yield return null;
        yield return null;

        var window = OpenedWindow();
        string text = window.CopyRow(0);

        Assert.That(text, Does.StartWith("[Error] broken\n"));
        Assert.That(text, Does.EndWith("\n" + window.GetVisibleEntry(0).StackTrace.Text));
    }

    [UnityTest]
    public IEnumerator Copy_ButtonsFollowCopyFlag()
    {
        DebugLogs.isGUILog = true;
        Log.Information("one");
        Log.Information("two");
        yield return null;
        yield return null;
        Assert.That(OpenedWindow().DrawnCopyButtons, Is.EqualTo(3), "每列一個 + 工具列一個");

        DebugWindow.VisibleButtons = DebugWindowButtons.All & ~DebugWindowButtons.Copy;
        yield return null;
        yield return null;
        Assert.That(OpenedWindow().DrawnCopyButtons, Is.EqualTo(0));
    }

    // 每列的複製按鈕: 沒有堆疊也沒有第二行時取代展開按鈕 (上方 Copy_ButtonsFollowCopyFlag);
    // 有堆疊時收合不畫, 展開後畫在堆疊第一行; 有第二行 (Time) 時一律畫在第二行
    [UnityTest]
    public IEnumerator Copy_RowButton_FollowsExpandAndSecondLine()
    {
        DebugLogs.isGUILog = true;
        Log.Error("broken");    // Error 預設擷取堆疊
        yield return null;
        yield return null;
        var window = OpenedWindow();
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(1), "收合且沒有第二行: 只有工具列");

        window.RequestToggleRow(0);
        yield return null;
        yield return null;
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(2), "展開後畫在堆疊第一行");

        window.RequestToggleRow(0);
        DebugWindow.ShowTime = true;
        yield return null;
        yield return null;
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(2), "有第二行: 收合也畫");

        window.RequestToggleRow(0);
        yield return null;
        yield return null;
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(2), "有第二行又展開: 只畫一個");
    }

    // ---- 縮小 ----

    [Test]
    public void Minimized_DefaultOffRoundTripAndResetOnInstall()
    {
        Assert.That(DebugWindow.Minimized, Is.False);

        DebugWindow.Minimized = true;
        Assert.That(DebugWindow.Minimized, Is.True);

        DebugLogs.Install(defaults);
        Assert.That(DebugWindow.Minimized, Is.False);
    }

    // 縮小時只畫標題列 (清單與工具列都不畫), 還原後恢復; 繪製不出錯
    [UnityTest]
    public IEnumerator Minimized_HidesContentAndRestores()
    {
        DebugLogs.isGUILog = true;
        Log.Information("one");
        yield return null;
        yield return null;
        var window = OpenedWindow();
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(2));

        DebugWindow.Minimized = true;
        Log.Information("two");
        yield return null;
        yield return null;
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(0));

        DebugWindow.Minimized = false;
        yield return null;
        yield return null;
        Assert.That(window.DrawnCopyButtons, Is.EqualTo(3), "還原後包含縮小期間的新訊息");
    }

    [Test]
    public void Size_ClampedToMinimum()
    {
        var savedSize = DebugWindow.Size;

        DebugWindow.Size = new Vector2(10f, 10f);

        Assert.That(DebugWindow.Size, Is.EqualTo(new Vector2(150f, 60f)));
        DebugWindow.Size = savedSize;
    }

    // 預設位置 (右下角) 縮小: 往下縮, 標題列的底邊與原本視窗的底邊相同
    [Test]
    public void Minimized_DefaultPosition_KeepsBottomEdge()
    {
        DebugWindow.Scale = 1f;
        float bottom = DebugWindow.Position.y + DebugWindow.CurrentHeight;

        DebugWindow.Minimized = true;

        Assert.That(DebugWindow.CurrentHeight, Is.LessThan(DebugWindow.Size.y));
        Assert.That(DebugWindow.Position.y + DebugWindow.CurrentHeight, Is.EqualTo(bottom).Within(0.01f));
        DebugWindow.Scale = 0f;
    }

    // 視窗在上半部: 往上縮 (左上角不動), 還原後位置不變
    [Test]
    public void Minimized_UpperHalf_KeepsTopEdge()
    {
        DebugWindow.Scale = 1f;
        DebugWindow.Position = new Vector2(0f, 0f);

        DebugWindow.Minimized = true;
        Assert.That(DebugWindow.Position, Is.EqualTo(new Vector2(0f, 0f)));

        DebugWindow.Minimized = false;
        Assert.That(DebugWindow.Position, Is.EqualTo(new Vector2(0f, 0f)));
        DebugWindow.Scale = 0f;
    }

    // 視窗在下半部: 往下縮, 還原時往上展開回原位
    [Test]
    public void Minimized_LowerHalf_CollapsesDownAndRestores()
    {
        var savedSize = DebugWindow.Size;
        DebugWindow.Scale = 1f;
        DebugWindow.Size = new Vector2(300f, 150f);
        float top = Screen.height - 150f;
        Assume.That(top, Is.GreaterThan(0f), "畫面太矮");
        DebugWindow.Position = new Vector2(0f, top);

        DebugWindow.Minimized = true;
        Assert.That(DebugWindow.Position.y + DebugWindow.CurrentHeight, Is.EqualTo((float)Screen.height).Within(0.01f));

        DebugWindow.Minimized = false;
        Assert.That(DebugWindow.Position.y, Is.EqualTo(top).Within(0.01f));
        DebugWindow.Size = savedSize;
        DebugWindow.Scale = 0f;
    }

    // 縮小時的邊緣偵測以標題列的高度判斷, 可以貼到畫面底部
    [Test]
    public void Minimized_EdgeClampUsesTitleBarHeight()
    {
        DebugWindow.Scale = 1f;
        DebugWindow.Position = new Vector2(0f, 0f);
        DebugWindow.Minimized = true;
        DebugWindow.Position = new Vector2(0f, Screen.height + 100f);

        DebugLogs.isGUILog = true;

        Assert.That(DebugWindow.Position.y, Is.EqualTo(Screen.height - DebugWindow.CurrentHeight).Within(0.01f));
        DebugWindow.Scale = 0f;
    }

    // 工具列: 寬度不夠時核取方塊移到第二列; 沒有左側按鈕時一律在第一列; 寬度足夠時在第一列
    [UnityTest]
    public IEnumerator Toolbar_CheckBoxesFollowWidth()
    {
        var savedSize = DebugWindow.Size;
        DebugWindow.Scale = 1f;
        DebugWindow.Size = new Vector2(150f, 150f);
        DebugLogs.isGUILog = true;
        yield return null;
        yield return null;
        Assert.That(OpenedWindow().ChecksOnFirstRow, Is.False, "最小寬度放不下");

        DebugWindow.VisibleButtons = DebugWindowButtons.Time | DebugWindowButtons.Collapse | DebugWindowButtons.Search;
        yield return null;
        yield return null;
        Assert.That(OpenedWindow().ChecksOnFirstRow, Is.True, "沒有左側按鈕");

        if (Screen.width >= 900)
        {
            DebugWindow.VisibleButtons = DebugWindowButtons.All;
            DebugWindow.Size = new Vector2(900f, 150f);
            yield return null;
            yield return null;
            Assert.That(OpenedWindow().ChecksOnFirstRow, Is.True, "寬度足夠");
        }
        DebugWindow.Size = savedSize;
        DebugWindow.Scale = 0f;
    }
}
