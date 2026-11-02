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
}
