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
        settings.window.visibleButtons = DebugWindowButtons.Clear | DebugWindowButtons.Search;

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
        Assert.That(DebugWindow.VisibleButtons, Is.EqualTo(DebugWindowButtons.Clear | DebugWindowButtons.Search));
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
    public void StartWithWindow_OpensWindow()
    {
        var settings = CreateSettings();
        settings.output.startWithWindow = true;

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
            DebugWindowButtons.Time,    // 第一列不畫, 核取方塊那一列成為第一列 (靠右)
            DebugWindowButtons.All & ~(DebugWindowButtons.Levels | DebugWindowButtons.Source | DebugWindowButtons.Copy | DebugWindowButtons.Clear),
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
    // ---- Development Alert ----

    private const string AlertPattern = "Development Alert";

    private static bool HasMessage(LogHistorySink history, string part)
        => System.Array.Exists(Entries(history), entry => entry.Message != null && entry.Message.Contains(part));

    [Test]
    public void DevelopmentAlert_NoProblem_NoOutput()
    {
        var settings = CreateSettings();
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Error;

        DebugLogs.Install(settings);

        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.False);
    }

    [Test]
    public void DevelopmentAlert_Off_NoOutput()
    {
        var settings = CreateSettings();
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Off;
        settings.window.logCapacity = 0;

        DebugLogs.Install(settings);

        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.False);
    }

    [Test]
    public void DevelopmentAlert_Warning_OneWarning()
    {
        var settings = CreateSettings();
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Warning;
        settings.window.logCapacity = 0;

        DebugLogs.Install(settings);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, "\n- window.logCapacity is 0"), Is.True);
        Assert.That(DebugWindow.LogCapacity, Is.EqualTo(1), "容量小於 1 時維持現況: 以 1 計");
    }

    [Test]
    public void DevelopmentAlert_Error_OneError()
    {
        var settings = CreateSettings();
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Error;
        settings.window.unityCapacity = DebugWindowOptions.MaxRecommendedCapacity + 1;
        // Error 需在發出前標明為預期中的錯誤 (正規表示式不跨行比對, 內容由 UnityHistory 確認)
        LogAssert.Expect(LogType.Error, new Regex(AlertPattern));

        DebugLogs.Install(settings);

        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, "window.unityCapacity is 10001"), Is.True);
    }

    [Test]
    public void DevelopmentAlert_AtRecommendedCapacity_NoOutput()
    {
        var settings = CreateSettings();
        settings.window.logCapacity = DebugWindowOptions.MaxRecommendedCapacity;

        DebugLogs.Install(settings);

        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.False);
    }

    // 多個問題合併為一則: 容量 (兩項) + 堆疊追蹤重複 + 等級顏色重複
    [Test]
    public void DevelopmentAlert_MultipleProblems_MergedIntoOneMessage()
    {
        var settings = CreateSettings();
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Error;
        settings.window.logCapacity = 0;
        settings.window.unityCapacity = -5;
        settings.log.stackTrace = new[]
        {
            new LogLevelSwitch(LogLevel.Error, true),
            new LogLevelSwitch(LogLevel.Error, false),
            new LogLevelSwitch(LogLevel.Error, false),
        };
        settings.window.levelColors = new[]
        {
            new LogLevelColor(LogLevel.Warning, "#FFFFFF"),
            new LogLevelColor(LogLevel.Warning, "#000000"),
        };
        LogAssert.Expect(LogType.Error, new Regex(AlertPattern));

        DebugLogs.Install(settings);

        LogAssert.NoUnexpectedReceived();
        var alert = System.Array.Find(Entries(DebugWindow.UnityHistory), entry => entry.Message.Contains(AlertPattern)).Message;
        Assert.That(alert, Does.Contain("window.logCapacity is 0"));
        Assert.That(alert, Does.Contain("window.unityCapacity is -5"));
        Assert.That(alert, Does.Contain("log.stackTrace lists Error more than once"));
        Assert.That(alert, Does.Contain("window.levelColors lists Warning more than once"));
        Assert.That(alert.Split('\n').Length, Is.EqualTo(5), "標題一行 + 四個問題各一行 (同一等級重複三次只算一項)");
    }

    // 找不到設定檔: 讀不到等級, 一律以 Warning 提醒, 訊息內容不變
    [Test]
    public void DevelopmentAlert_NoSettings_Warning()
    {
        DebugLogs.Install(null);

        LogAssert.Expect(LogType.Warning, new Regex("LogSettings not found"));
        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, "LogSettings not found"), Is.True);
    }

    // 發出時已開始收集 Unity 訊息: GUI 視窗的 UnityHistory 收得到
    [Test]
    public void DevelopmentAlert_RecordedInUnityHistory()
    {
        var settings = CreateSettings();
        settings.window.logCapacity = 0;

        DebugLogs.Install(settings);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.True);
    }

    // isUnityLog 關閉的設定檔: 提醒仍輸出到 Console, 但不收集 (關閉期間的訊息不補記)
    [Test]
    public void DevelopmentAlert_UnityLogOff_NotRecorded()
    {
        var settings = CreateSettings();
        settings.window.logCapacity = 0;
        settings.output.isUnityLog = false;

        DebugLogs.Install(settings);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.False);
    }

    // ---- Development Alert: 正式版仍開著的開發設定 (B 類) ----

    private const string ReleaseHeader = "Development settings enabled in a non-development build:";
    private const string ReleaseLineHead = "Development log settings left on in a non-development build: ";

    private LogSettings CreateAllDevelopmentSettings()
    {
        var settings = CreateSettings();
        settings.output.startWithWindow = true;
        settings.log.isPrintLog = true;
        settings.log.isDebugLog = true;
        return settings;
    }

    private static string FindAlert() => System.Array.Find(Entries(DebugWindow.UnityHistory), entry => entry.Message.Contains(AlertPattern)).Message;

    [Test]
    public void ReleaseProblems_DebugBuild_NoItems()
    {
        var problems = new List<string>();

        DebugLogs.CollectReleaseProblems(CreateAllDevelopmentSettings(), true, true, problems);

        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void ReleaseProblems_AllOff_NoItems()
    {
        var problems = new List<string>();

        DebugLogs.CollectReleaseProblems(CreateSettings(), false, false, problems);

        Assert.That(problems, Is.Empty);
    }

    [TestCase("output.startWithWindow")]
    [TestCase("log.isPrintLog")]
    [TestCase("log.isDebugLog")]
    [TestCase("TORBEN_LOG_DEBUG")]
    public void ReleaseProblems_EachItemAlone(string item)
    {
        var settings = CreateSettings();
        settings.output.startWithWindow = item == "output.startWithWindow";
        settings.log.isPrintLog = item == "log.isPrintLog";
        settings.log.isDebugLog = item == "log.isDebugLog";
        var problems = new List<string>();

        DebugLogs.CollectReleaseProblems(settings, false, item == "TORBEN_LOG_DEBUG", problems);

        Assert.That(problems.Count, Is.EqualTo(1));
        Assert.That(problems[0], Does.StartWith(item));
    }

    [Test]
    public void ReleaseProblems_AllOn_AllItemsInOrder()
    {
        var problems = new List<string>();

        DebugLogs.CollectReleaseProblems(CreateAllDevelopmentSettings(), false, true, problems);

        Assert.That(problems.Count, Is.EqualTo(4));
        Assert.That(problems[0], Is.EqualTo("output.startWithWindow"));
        Assert.That(problems[1], Is.EqualTo("log.isPrintLog"));
        Assert.That(problems[2], Is.EqualTo("log.isDebugLog"));
        Assert.That(problems[3], Does.StartWith("TORBEN_LOG_DEBUG"));
    }

    // 找不到設定檔: 前三項依欄位初始值 (皆為 false), TORBEN_LOG_DEBUG 照常判斷
    [Test]
    public void ReleaseProblems_NullSettings_UsesFieldDefaults()
    {
        var problems = new List<string>();

        DebugLogs.CollectReleaseProblems(null, false, true, problems);

        Assert.That(problems.Count, Is.EqualTo(1));
        Assert.That(problems[0], Does.StartWith("TORBEN_LOG_DEBUG"));
    }

    // A 類與 B 類同時存在 (Warning): 合併為一則; A 類維持多行, B 類只佔一行
    [Test]
    public void ReleaseAlert_WithSettingsProblems_MergedIntoOneMessage()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.window.logCapacity = 0;

        DebugLogs.Install(settings, false, false);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        LogAssert.NoUnexpectedReceived();
        var alerts = System.Array.FindAll(Entries(DebugWindow.UnityHistory), entry => entry.Message.Contains(AlertPattern));
        Assert.That(alerts.Length, Is.EqualTo(1));
        var lines = alerts[0].Message.Split('\n');
        Assert.That(lines[1], Does.StartWith("- window.logCapacity is 0"));
        Assert.That(lines[2], Is.EqualTo(ReleaseLineHead + "log.isPrintLog (if intentional, set Development Alert to Off in " + settings.name + ")."));
        Assert.That(lines.Length, Is.EqualTo(3));
    }

    // 只有 B 類 (Warning): 整則訊息只有一行, 列出所有沒關的項目
    [Test]
    public void ReleaseAlert_WarningOnlyReleaseProblems_SingleLine()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.log.isDebugLog = true;

        DebugLogs.Install(settings, false, true);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        LogAssert.NoUnexpectedReceived();
        string alert = FindAlert();
        Assert.That(alert, Does.Not.Contain("\n"));
        Assert.That(alert, Does.StartWith("[Log] " + ReleaseLineHead + "log.isPrintLog, log.isDebugLog, TORBEN_LOG_DEBUG"));
    }

    // A 類與 B 類同時存在 (Error): 維持多行清單
    [Test]
    public void ReleaseAlert_ErrorWithSettingsProblems_KeepsMultiLine()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.window.logCapacity = 0;
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Error;
        LogAssert.Expect(LogType.Error, new Regex(AlertPattern));

        DebugLogs.Install(settings, false, false);

        LogAssert.NoUnexpectedReceived();
        var lines = FindAlert().Split('\n');
        Assert.That(lines[1], Does.StartWith("- window.logCapacity is 0"));
        Assert.That(lines[2], Is.EqualTo(ReleaseHeader));
        Assert.That(lines[3], Is.EqualTo("- log.isPrintLog"));
        Assert.That(lines[4], Does.StartWith("If this is intentional, set Development Alert to Off"));
        Assert.That(lines.Length, Is.EqualTo(5));
    }

    // 只有 B 類: 只有 B 段 (標題下一行就是 B 段標題)
    [Test]
    public void ReleaseAlert_OnlyReleaseProblems_OnlyReleaseSection()
    {
        var settings = CreateSettings();
        settings.log.isDebugLog = true;
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Error;
        LogAssert.Expect(LogType.Error, new Regex(AlertPattern));

        DebugLogs.Install(settings, false, true);

        LogAssert.NoUnexpectedReceived();
        var lines = FindAlert().Split('\n');
        Assert.That(lines[1], Is.EqualTo(ReleaseHeader));
        Assert.That(lines[2], Is.EqualTo("- log.isDebugLog"));
        Assert.That(lines[3], Does.StartWith("- TORBEN_LOG_DEBUG"));
        Assert.That(lines[4], Does.StartWith("If this is intentional"));
        Assert.That(lines.Length, Is.EqualTo(5));
    }

    [Test]
    public void ReleaseAlert_Off_NoOutput()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.window.logCapacity = 0;
        settings.DevelopmentAlert.level = DevelopmentAlertLevel.Off;

        DebugLogs.Install(settings, false, true);

        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.False);
    }

    // 開發版 (含 Editor) 不檢查: 四項都開著也沒有輸出
    [Test]
    public void ReleaseAlert_DebugBuild_NoOutput()
    {
        var settings = CreateSettings();
        settings.log.isPrintLog = true;
        settings.log.isDebugLog = true;

        DebugLogs.Install(settings, true, true);

        LogAssert.NoUnexpectedReceived();
        Assert.That(HasMessage(DebugWindow.UnityHistory, AlertPattern), Is.False);
    }

    // 訊息包含生效中設定檔的名稱: 預設檔
    [Test]
    public void ReleaseAlert_NamesDefaultFile()
    {
        var settings = CreateSettings();
        settings.name = "LogSettings.Default";
        settings.log.isPrintLog = true;

        DebugLogs.Install(settings, false, false);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        Assert.That(FindAlert(), Does.EndWith("set Development Alert to Off in LogSettings.Default)."));
    }

    // 訊息包含生效中設定檔的名稱: 覆蓋檔 (檢查的是覆蓋檔的值與等級)
    [Test]
    public void ReleaseAlert_NamesOverrideFile()
    {
        var defaultSettings = CreateSettings();
        defaultSettings.name = "LogSettings.Default";
        defaultSettings.DevelopmentAlert.level = DevelopmentAlertLevel.Off;
        var projectOverride = CreateSettings();
        projectOverride.name = "GameLogOverride";
        projectOverride.log.isPrintLog = true;
        defaultSettings.projectOverride = projectOverride;

        DebugLogs.Install(defaultSettings, false, false);

        LogAssert.Expect(LogType.Warning, new Regex(AlertPattern));
        Assert.That(FindAlert(), Does.EndWith("set Development Alert to Off in GameLogOverride)."));
    }

    // 找不到設定檔: 以一行併入「LogSettings not found」那一則 (一律 Warning)
    [Test]
    public void ReleaseAlert_NoSettings_MergedIntoNotFoundWarning()
    {
        DebugLogs.Install(null, false, true);

        LogAssert.Expect(LogType.Warning, new Regex("LogSettings not found"));
        LogAssert.NoUnexpectedReceived();
        var report = System.Array.Find(Entries(DebugWindow.UnityHistory), entry => entry.Message.Contains("LogSettings not found")).Message;
        var lines = report.Split('\n');
        Assert.That(lines.Length, Is.EqualTo(2));
        Assert.That(lines[1], Does.StartWith(ReleaseLineHead + "TORBEN_LOG_DEBUG"));
        Assert.That(lines[1], Does.EndWith("(if intentional, create the settings file and set Development Alert to Off)."));
    }

    // sink 例外隔離: Install 設定 SinkErrorHandler, 以 LogException 輸出 (含 sink 型別名稱), 只有第一次
    private sealed class ThrowingSink : ILogSink
    {
        public void Write(in LogEntry entry) => throw new System.InvalidOperationException("sink failed");
    }

    [Test]
    public void SinkError_ReportedOnceWithTypeName_OtherSinksStillWrite()
    {
        Log.isPrintLog = true;
        Log.AddSink(new ThrowingSink());
        // 預期訊息依出現順序登記: Console sink 先輸出, 接著 ThrowingSink 失敗的回報 (只有第一次)
        LogAssert.Expect(LogType.Log, "first");
        LogAssert.Expect(LogType.Exception, new Regex("Sink 'LogSettingsInstallTests\\+ThrowingSink'"));
        LogAssert.Expect(LogType.Log, "second");

        Assert.DoesNotThrow(() => Log.Information("first"));
        Log.Information("second");
        LogAssert.NoUnexpectedReceived();

        // 訊息第一行帶 sink 型別名稱, 後面附原例外; 畫面視窗也收得到
        var report = System.Array.Find(Entries(DebugWindow.UnityHistory), entry => entry.Message.Contains("ThrowingSink"));
        Assert.That(report.Message, Does.Contain("InvalidOperationException: sink failed"));

        var messages = System.Array.ConvertAll(Entries(DebugWindow.History), entry => entry.Message);
        Assert.That(messages, Is.EqualTo(new[] { "first", "second" }));
    }
}
