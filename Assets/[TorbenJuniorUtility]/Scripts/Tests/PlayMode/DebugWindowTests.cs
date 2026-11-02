using System.Collections;
using NUnit.Framework;
using Torben;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DebugWindowTests
{
    private bool savedPrintLog;
    private bool savedDebugLog;

    [SetUp]
    public void SetUp()
    {
        savedPrintLog = Log.isPrintLog;
        savedDebugLog = Log.isDebugLog;
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
        DebugWindow.ResetPosition();
        DebugWindow.SearchText = string.Empty;
        foreach (LogLevel level in System.Enum.GetValues(typeof(LogLevel)))
            DebugWindow.SetLevelVisible(level, true);
        if (CrossObject.IsReady) Object.Destroy(CrossObject.Instance.gameObject);
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
}
