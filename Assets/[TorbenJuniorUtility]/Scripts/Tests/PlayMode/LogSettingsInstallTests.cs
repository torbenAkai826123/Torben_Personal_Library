using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Torben;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LogSettingsInstallTests
{
    private readonly List<LogSettings> created = new List<LogSettings>();
    private bool savedPrintLog;
    private bool savedDebugLog;

    [SetUp]
    public void SetUp()
    {
        savedPrintLog = Log.isPrintLog;
        savedDebugLog = Log.isDebugLog;
        DebugLogs.Install(CreateSettings());    // 以欄位初始值開始, 不受專案設定檔 (含覆蓋檔) 影響
    }

    [TearDown]
    public void TearDown()
    {
        DebugLogs.isGUILog = false;
        if (CrossObject.IsReady) Object.Destroy(CrossObject.Instance.gameObject);
        DebugLogs.Install(DebugLogs.StartupSettings);   // 恢復進入 Play 時的安裝結果, 不影響其他測試
        foreach (var settings in created) Object.Destroy(settings);
        created.Clear();
        Log.isPrintLog = savedPrintLog;
        Log.isDebugLog = savedDebugLog;
    }

    private LogSettings CreateSettings()
    {
        var settings = ScriptableObject.CreateInstance<LogSettings>();
        created.Add(settings);
        return settings;
    }

    private static LogEntry[] Entries(LogHistorySink history)
    {
        var entries = new LogEntry[1000];
        int count = history.CopyTo(entries);
        System.Array.Resize(ref entries, count);
        return entries;
    }

    private static string[] VisibleMessages(DebugWindow window)
    {
        var messages = new string[window.VisibleRowCount];
        for (int row = 0; row < messages.Length; row++) messages[row] = window.GetVisibleEntry(row).Message;
        return messages;
    }

    // 4.1 待驗證項目: SubsystemRegistration 階段的 Resources.Load 讀得到預設檔
    [Test]
    public void Startup_LoadsDefaultSettingsFromResources()
    {
        var expected = Resources.Load<LogSettings>(LogSettings.ResourcesPath);

        Assert.That(expected, Is.Not.Null);
        Assert.That(DebugLogs.StartupSettings, Is.SameAs(expected));
    }

    // 沒有設定檔: 行為與加入設定檔之前相同 (數值為修改前寫死的預設值), 且只警告一次
    [Test]
    public void NoSettings_BehavesAsBeforeAndWarnsOnce()
    {
        Log.isPrintLog = true;
        Log.isDebugLog = true;
        DebugWindow.ShowTime = true;

        DebugLogs.Install(null);

        LogAssert.Expect(LogType.Warning, new Regex("LogSettings not found"));
        LogAssert.NoUnexpectedReceived();
        Assert.That(Log.isPrintLog, Is.False);
        Assert.That(Log.isDebugLog, Is.False);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Error), Is.True);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Normal), Is.False);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Warning), Is.False);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Debug), Is.False);
        Assert.That(Log.HasSink, Is.True);
        Assert.That(DebugLogs.isGUILog, Is.False);
        Assert.That(DebugLogs.isConsoleWithGUI, Is.False);
        Assert.That(DebugLogs.isUnityLog, Is.True);
        Assert.That(DebugWindow.LogCapacity, Is.EqualTo(200));
        Assert.That(DebugWindow.UnityCapacity, Is.EqualTo(200));
        Assert.That(DebugWindow.Size, Is.EqualTo(new Vector2(600f, 180f)));
        Assert.That(DebugWindow.UsesDefaultPosition, Is.True);
        Assert.That(DebugWindow.Scale, Is.EqualTo(0f));
        Assert.That(DebugWindow.IsDraggable, Is.True);
        Assert.That(DebugWindow.AutoScroll, Is.True);
        Assert.That(DebugWindow.SearchText, Is.Empty);
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Normal), Is.Null);
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Warning), Is.EqualTo("#FFD54F"));
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Error), Is.EqualTo("#FF6B6B"));
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Debug), Is.EqualTo("#9E9E9E"));
        foreach (LogLevel level in System.Enum.GetValues(typeof(LogLevel)))
            Assert.That(DebugWindow.IsLevelVisible(level), Is.True);
        Assert.That(DebugWindow.ShowLogMessages, Is.True);
        Assert.That(DebugWindow.ShowUnityMessages, Is.True);
        Assert.That(DebugWindow.ShowTime, Is.False);
        Assert.That(DebugWindow.ShowLocation, Is.False);
        Assert.That(DebugWindow.Collapse, Is.False);
        Assert.That(DebugWindow.VisibleButtons, Is.EqualTo(DebugWindowButtons.All));
    }

    [Test]
    public void DefaultSettings_Applied()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.log.stackTrace = new[] { new LogLevelSwitch(LogLevel.Warning, true) };
        settings.output.isConsoleWithGUI = true;
        settings.output.isUnityLog = false;
        settings.window.logCapacity = 3;
        settings.window.size = new Vector2(320f, 120f);
        settings.window.useDefaultPosition = false;
        settings.window.position = new Vector2(7f, 8f);
        settings.window.levelColors = new[] { new LogLevelColor(LogLevel.Normal, "#00FF00") };
        settings.window.visibleLevels = new[] { new LogLevelSwitch(LogLevel.Debug, false) };
        settings.window.showTime = true;
        settings.window.visibleButtons = DebugWindowButtons.Clear | DebugWindowButtons.Close;

        DebugLogs.Install(settings);

        LogAssert.NoUnexpectedReceived();
        Assert.That(Log.isPrintLog, Is.True);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Warning), Is.True);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Error), Is.False);
        Assert.That(DebugLogs.isConsoleWithGUI, Is.True);
        Assert.That(DebugLogs.isUnityLog, Is.False);
        Assert.That(DebugWindow.LogCapacity, Is.EqualTo(3));
        Assert.That(DebugWindow.UnityCapacity, Is.EqualTo(200));
        Assert.That(DebugWindow.Size, Is.EqualTo(new Vector2(320f, 120f)));
        Assert.That(DebugWindow.UsesDefaultPosition, Is.False);
        Assert.That(DebugWindow.Position, Is.EqualTo(new Vector2(7f, 8f)));
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Normal), Is.EqualTo("#00FF00"));
        Assert.That(DebugWindow.GetLevelColor(LogLevel.Error), Is.Null);       // 沒有列出的等級使用預設文字顏色
        Assert.That(DebugWindow.IsLevelVisible(LogLevel.Debug), Is.False);
        Assert.That(DebugWindow.IsLevelVisible(LogLevel.Normal), Is.True);     // 沒有列出的等級視為顯示
        Assert.That(DebugWindow.ShowTime, Is.True);
        Assert.That(DebugWindow.VisibleButtons, Is.EqualTo(DebugWindowButtons.Clear | DebugWindowButtons.Close));
    }

    [Test]
    public void ProjectOverride_ReplacesDefaultEntirely()
    {
        var defaultSettings = CreateSettings();
        var projectOverride = CreateSettings();
        defaultSettings.projectOverride = projectOverride;
        defaultSettings.window.collapse = true;
        defaultSettings.window.logCapacity = 9;
        projectOverride.window.logCapacity = 4;
        projectOverride.window.showLocation = true;

        DebugLogs.Install(defaultSettings);

        Assert.That(DebugWindow.LogCapacity, Is.EqualTo(4));
        Assert.That(DebugWindow.ShowLocation, Is.True);
        Assert.That(DebugWindow.Collapse, Is.False);    // 整份替換, 不做逐欄位覆蓋
    }

    [Test]
    public void OpenWindowOnStart_OpensWindow()
    {
        var settings = CreateSettings();
        settings.output.openWindowOnStart = true;

        DebugLogs.Install(settings);

        Assert.That(DebugLogs.isGUILog, Is.True);
        Assert.That(CrossObject.Instance.GetComponent<DebugWindow>().enabled, Is.True);
    }

    [Test]
    public void Capture_ReflectsCurrentState()
    {
        var settings = CreateSettings();
        settings.window.logCapacity = 6;
        DebugLogs.Install(settings);
        DebugWindow.Position = new Vector2(3f, 4f);
        DebugWindow.SetLevelVisible(LogLevel.Warning, false);
        DebugWindow.VisibleButtons = DebugWindowButtons.Search;

        var window = DebugWindowOptions.Capture();

        Assert.That(window.logCapacity, Is.EqualTo(6));
        Assert.That(window.useDefaultPosition, Is.False);
        Assert.That(window.position, Is.EqualTo(new Vector2(3f, 4f)));
        Assert.That(LogLevelSwitch.Find(window.visibleLevels, LogLevel.Warning, true), Is.False);
        Assert.That(window.visibleButtons, Is.EqualTo(DebugWindowButtons.Search));
        Assert.That(LogLevelColor.Find(window.levelColors, LogLevel.Error), Is.EqualTo("#FF6B6B"));
    }

    // 兩份紀錄容量不同時, 各自保留最新的 N 筆, 合併順序、來源篩選與合併相同訊息都正確 (visible 的偏移量依 Log 容量)
    [UnityTest]
    public IEnumerator DifferentCapacities_MergeAndFilterCorrectly()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.window.logCapacity = 5;
        settings.window.unityCapacity = 7;
        DebugLogs.Install(settings);
        DebugLogs.isGUILog = true;  // 視窗開啟時 Log 不輸出到 Console

        for (int i = 0; i < 10; i++)
        {
            Log.Format(LogLevel.Normal, "log {0}", i);
            Debug.Log($"unity {i}");
        }

        Assert.That(Entries(DebugWindow.History).Length, Is.EqualTo(5));
        Assert.That(Entries(DebugWindow.UnityHistory).Length, Is.EqualTo(7));
        yield return null;
        yield return null;     // 確保至少經過一次 Layout
        var window = CrossObject.Instance.GetComponent<DebugWindow>();
        Assert.That(VisibleMessages(window), Is.EqualTo(new[]
        {
            "unity 3", "unity 4", "log 5", "unity 5", "log 6", "unity 6",
            "log 7", "unity 7", "log 8", "unity 8", "log 9", "unity 9",
        }));

        DebugWindow.ShowUnityMessages = false;
        yield return null;
        yield return null;     // 確保至少經過一次 Layout
        Assert.That(VisibleMessages(window), Is.EqualTo(new[] { "log 5", "log 6", "log 7", "log 8", "log 9" }));

        DebugWindow.ShowUnityMessages = true;
        DebugWindow.ShowLogMessages = false;
        DebugWindow.SearchText = "unity 9";
        yield return null;
        yield return null;     // 確保至少經過一次 Layout
        Assert.That(VisibleMessages(window), Is.EqualTo(new[] { "unity 9" }));

        DebugWindow.SearchText = string.Empty;
        DebugWindow.ShowLogMessages = true;
        DebugWindow.Collapse = true;
        for (int i = 0; i < 3; i++) Log.Information("dup");
        yield return null;
        yield return null;     // 確保至少經過一次 Layout
        // Log 紀錄只剩 log 8、log 9 與 3 筆 dup; dup 合併成一列
        Assert.That(VisibleMessages(window), Is.EqualTo(new[]
        {
            "unity 3", "unity 4", "unity 5", "unity 6", "unity 7", "log 8", "unity 8", "log 9", "unity 9", "dup",
        }));
    }

    // 隱藏按鈕 (含全部隱藏) 後繪製不出錯; OnGUI 出錯時 Unity 會記錄錯誤, 測試就會失敗
    [UnityTest]
    public IEnumerator VisibleButtons_HiddenButtons_DrawWithoutError()
    {
        Log.isPrintLog = true;
        DebugLogs.isGUILog = true;
        Log.Error("with stack");
        var cases = new[]
        {
            DebugWindowButtons.None,
            DebugWindowButtons.Levels,
            DebugWindowButtons.Search | DebugWindowButtons.Clear,
            DebugWindowButtons.All & ~DebugWindowButtons.Close,
            DebugWindowButtons.All,
        };
        foreach (var buttons in cases)
        {
            DebugWindow.VisibleButtons = buttons;
            yield return null;
            yield return null;
        }

        LogAssert.NoUnexpectedReceived();
        Assert.That(CrossObject.Instance.GetComponent<DebugWindow>().enabled, Is.True);
    }
}
