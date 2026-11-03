using System;
using NUnit.Framework;
using Torben;

// LogEntry 的時間、呼叫端位置與堆疊追蹤
[TestFixture]
public class LogEntryInfoTests
{
    private CaptureSink _sink;
    private bool savedPrintLog;
    private bool[] savedStackTrace;

    [SetUp]
    public void SetUp()
    {
        _sink = new CaptureSink();
        Log.ClearSinks();
        Log.AddSink(_sink);
        savedPrintLog = Log.isPrintLog;
        Log.isPrintLog = true;
        var levels = (LogLevel[])Enum.GetValues(typeof(LogLevel));
        savedStackTrace = new bool[levels.Length];
        foreach (var level in levels) savedStackTrace[(int)level] = Log.IsStackTraceEnabled(level);
    }

    [TearDown]
    public void TearDown()
    {
        Log.ClearSinks();
        Log.isPrintLog = savedPrintLog;
        Log.isDebugLog = false;
        for (int i = 0; i < savedStackTrace.Length; i++) Log.SetStackTraceEnabled((LogLevel)i, savedStackTrace[i]);
    }

    [Test]
    public void Write_FillsCallerInfo()
    {
        Log.Information("hello");

        var entry = _sink.Entries[0];
        Assert.That(entry.CallerMemberName, Is.EqualTo(nameof(Write_FillsCallerInfo)));
        Assert.That(entry.CallerFilePath, Does.EndWith("LogEntryInfoTests.cs"));
        Assert.That(entry.CallerLineNumber, Is.GreaterThan(0));
        Assert.That(entry.HasCaller, Is.True);
    }

    [Test]
    public void Format_FillsCallerInfo()
    {
        Log.Format(LogLevel.Warning, TestCatlog.Instance, "{0}-{1}", 1, 2);

        Assert.That(_sink.Entries[0].Message, Is.EqualTo("1-2"));
        Assert.That(_sink.Entries[0].CallerMemberName, Is.EqualTo(nameof(Format_FillsCallerInfo)));
    }

    [Test]
    public void Format_ThreeStringArgs_BindToFormatArguments()
    {
        // 沒有 LogArgsEnd 時, 第 3 個字串參數會被編譯器綁到 2 參數版本的 callerMemberName
        Log.Format(LogLevel.Normal, "{0}{1}{2}", "a", "b", "c");

        Assert.That(_sink.Entries[0].Message, Is.EqualTo("abc"));
        Assert.That(_sink.Entries[0].CallerMemberName, Is.EqualTo(nameof(Format_ThreeStringArgs_BindToFormatArguments)));
    }

    [Test]
    public void Write_SetsUtcTime()
    {
        var before = DateTime.UtcNow;
        Log.Information("time");
        var after = DateTime.UtcNow;

        var time = _sink.Entries[0].Time;
        Assert.That(time.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(time, Is.InRange(before, after));
    }

    [Test]
    public void ThreeArgumentConstructor_HasNoExtraInfo()
    {
        var entry = new LogEntry(LogLevel.Normal, TestCatlog.Instance, "x");

        Assert.That(entry.Time, Is.EqualTo(default(DateTime)));
        Assert.That(entry.HasCaller, Is.False);
        Assert.That(entry.StackTrace, Is.Null);
    }

    [Test]
    public void StackTrace_DefaultOnlyError()
    {
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Error), Is.True);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Normal), Is.False);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Warning), Is.False);
        Assert.That(Log.IsStackTraceEnabled(LogLevel.Debug), Is.False);
    }

    [Test]
    public void StackTrace_CapturedOnlyForEnabledLevels()
    {
        Log.SetStackTraceEnabled(LogLevel.Error, true);
        Log.SetStackTraceEnabled(LogLevel.Normal, false);

        Log.Information("info");
        Log.Error("error");

        Assert.That(_sink.Entries[0].StackTrace, Is.Null);
        Assert.That(_sink.Entries[1].StackTrace, Is.Not.Null);
    }

    [Test]
    public void StackTrace_SetEnabled_AppliesToLaterEntries()
    {
        Log.SetStackTraceEnabled(LogLevel.Warning, true);
        Log.Warning("on");
        Log.SetStackTraceEnabled(LogLevel.Warning, false);
        Log.Warning("off");

        Assert.That(_sink.Entries[0].StackTrace, Is.Not.Null);
        Assert.That(_sink.Entries[1].StackTrace, Is.Null);
    }

    [Test]
    public void StackTrace_TextStartsAtCaller()
    {
        Log.SetStackTraceEnabled(LogLevel.Error, true);
        Log.Format(LogLevel.Error, "{0}", 1);

        string text = _sink.Entries[0].StackTrace.Text;
        string firstLine = text.Split('\n')[0];
        Assert.That(firstLine, Does.Contain(nameof(StackTrace_TextStartsAtCaller)));
        Assert.That(text, Does.Not.Contain("Torben.Log."));
        Assert.That(_sink.Entries[0].StackTrace.Text, Is.SameAs(text), "第二次讀取應使用快取");
    }

    [Test]
    public void StackTraceFromText_EmptyIsNull()
    {
        Assert.That(LogStackTrace.FromText(null), Is.Null);
        Assert.That(LogStackTrace.FromText("  \n"), Is.Null);
        Assert.That(LogStackTrace.FromText("A:B ()\n").Text, Is.EqualTo("A:B ()"));
    }
}

[TestFixture]
public class LogTextTests
{
    [Test]
    public void FormatTime_DefaultIsEmpty()
    {
        Assert.That(LogText.FormatTime(default), Is.EqualTo(string.Empty));
    }

    [Test]
    public void FormatTime_UsesLocalTime()
    {
        var utc = new DateTime(2026, 11, 2, 10, 0, 1, 230, DateTimeKind.Utc);
        string expected = utc.ToLocalTime().ToString("HH:mm:ss.ff", System.Globalization.CultureInfo.InvariantCulture);

        Assert.That(LogText.FormatTime(utc), Is.EqualTo(expected));
        Assert.That(LogText.FormatTime(utc), Does.Match(@"^\d\d:\d\d:\d\d\.\d\d$"));
    }

    [TestCase("/Users/me/Proj/Assets/Scripts/Foo.cs", "/Users/me/Proj", "Assets/Scripts/Foo.cs")]
    [TestCase("/Users/me/Proj/Assets/Scripts/Foo.cs", "/Users/me/Proj/", "Assets/Scripts/Foo.cs")]
    [TestCase(@"C:\Work\Proj\Assets\Foo.cs", @"C:\Work\Proj", "Assets/Foo.cs")]
    [TestCase(@"C:\Work\Proj\Assets\Foo.cs", null, "Assets/Foo.cs")]
    [TestCase("/build/Proj/Packages/com.x/Foo.cs", null, "Packages/com.x/Foo.cs")]
    [TestCase("Assets/Foo.cs", "/Users/me/Proj", "Assets/Foo.cs")]
    [TestCase("/somewhere/else/Foo.cs", "/Users/me/Proj", "Foo.cs")]
    [TestCase("", null, "")]
    [TestCase(null, null, "")]
    public void ToDisplayPath(string path, string root, string expected)
    {
        Assert.That(LogText.ToDisplayPath(path, root), Is.EqualTo(expected));
    }

    [Test]
    public void TryParseUnityLocation_SkipsFramesWithoutSource()
    {
        string stack = "UnityEngine.Debug:Log (object)\n"
                     + "Game.Boot:Start () (at <00000000000000000000000000000000>:0)\n"
                     + "Game.Player:Update () (at Assets/Game/Player.cs:42)\n"
                     + "Game.Other:Run () (at Assets/Game/Other.cs:7)\n";

        Assert.That(LogText.TryParseUnityLocation(stack, out string path, out int line), Is.True);
        Assert.That(path, Is.EqualTo("Assets/Game/Player.cs"));
        Assert.That(line, Is.EqualTo(42));
    }

    [Test]
    public void TryParseUnityLocation_AbsoluteWindowsPath()
    {
        Assert.That(LogText.TryParseUnityLocation("A:B () (at C:/Pkg/Foo.cs:3)", out string path, out int line), Is.True);
        Assert.That(path, Is.EqualTo("C:/Pkg/Foo.cs"));
        Assert.That(line, Is.EqualTo(3));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("UnityEngine.Debug:Log (object)\n")]
    [TestCase("A:B () (at <abc>:0)")]
    public void TryParseUnityLocation_NotFound(string stack)
    {
        Assert.That(LogText.TryParseUnityLocation(stack, out string path, out int line), Is.False);
        Assert.That(path, Is.Null);
        Assert.That(line, Is.EqualTo(0));
    }
}
