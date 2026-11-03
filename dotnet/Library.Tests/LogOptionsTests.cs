using System;
using NUnit.Framework;
using Torben;

[TestFixture]
public class LogOptionsTests
{
    private LogOptions saved;

    [SetUp]
    public void SetUp() => saved = LogOptions.Capture();

    [TearDown]
    public void TearDown() => saved.Apply();

    [Test]
    public void Defaults_MatchLogDefaultBehavior()
    {
        var options = new LogOptions();

        Assert.That(options.isPrintLog, Is.False);
        Assert.That(options.isDebugLog, Is.False);
        foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            Assert.That(LogLevelSwitch.Find(options.stackTrace, level, false), Is.EqualTo(level == LogLevel.Error), level.ToString());
    }

    [Test]
    public void Apply_ThenCapture_RoundTrips()
    {
        var options = new LogOptions
        {
            isPrintLog = true,
            isDebugLog = true,
            stackTrace = new[]
            {
                new LogLevelSwitch(LogLevel.Normal, true),
                new LogLevelSwitch(LogLevel.Warning, false),
                new LogLevelSwitch(LogLevel.Error, false),
                new LogLevelSwitch(LogLevel.Debug, true),
            },
        };

        options.Apply();
        var captured = LogOptions.Capture();

        Assert.That(captured.isPrintLog, Is.True);
        Assert.That(captured.isDebugLog, Is.True);
        Assert.That(captured.stackTrace, Is.EqualTo(options.stackTrace));
    }

    [Test]
    public void Apply_MapsEachLevelToStackTraceSwitch()
    {
        var options = new LogOptions
        {
            stackTrace = new[] { new LogLevelSwitch(LogLevel.Warning, true) },
        };

        options.Apply();

        Assert.That(Log.IsStackTraceEnabled(LogLevel.Warning), Is.True);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Normal), Is.False);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Error), Is.False);   // 沒有列出的等級視為關閉
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Debug), Is.False);
    }

    [Test]
    public void Capture_ListsEveryLevelInEnumOrder()
    {
        var captured = LogOptions.Capture();
        var levels = (LogLevel[])Enum.GetValues(typeof(LogLevel));

        Assert.That(captured.stackTrace.Length, Is.EqualTo(levels.Length));
        for (int i = 0; i < levels.Length; i++)
            Assert.That(captured.stackTrace[i].level, Is.EqualTo(levels[i]));
    }

    // 模擬 LogLevel 增減等級後的舊資料: 有未定義的等級值、缺少等級、重複、null
    [Test]
    public void Apply_UndefinedMissingOrNullLevels_DoesNotThrow()
    {
        var options = new LogOptions
        {
            stackTrace = new[]
            {
                new LogLevelSwitch((LogLevel)99, true),
                new LogLevelSwitch((LogLevel)(-1), true),
                new LogLevelSwitch(LogLevel.Error, true),
                new LogLevelSwitch(LogLevel.Error, false),
            },
        };

        Assert.DoesNotThrow(options.Apply);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Error), Is.True);    // 重複時以第一筆為準
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Normal), Is.False);

        options.stackTrace = null;
        Assert.DoesNotThrow(options.Apply);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Error), Is.False);
    }
}
