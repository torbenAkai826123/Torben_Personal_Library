using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Torben;

public sealed class CaptureSink : ILogSink
{
    public readonly List<LogEntry> Entries = new();
    public void Write(in LogEntry entry) => Entries.Add(entry);
}

// 假定這個類別在專案中被命名為 Test。
public sealed class TestCatlog : LogCategory
{
    public static readonly TestCatlog Instance = new TestCatlog(); 
    private TestCatlog() : base("Test") { }
}

// 記錄 ToString 被呼叫的次數, 用來確認關閉時沒有格式化參數
public sealed class CountingArg
{
    public int ToStringCount;
    public override string ToString() { ToStringCount++; return "arg"; }
}

[TestFixture]
public class LogTests
{
    private const string CATEGORY_NAME = "Test";
    private CaptureSink _sink;
    private bool printState = false;

    [SetUp]
    public void SetUp()
    {
        _sink = new CaptureSink();
        Log.ClearSinks();
        Log.AddSink(_sink);
        // 如果關閉就不會有輸出，會出現錯誤。
        if (!Torben.Log.isPrintLog)
        {
            printState = Torben.Log.isPrintLog;
            Torben.Log.isPrintLog = true;
        }
    }

    [TearDown]
    public void TearDown()
    {
        Log.ClearSinks();   // 靜態狀態一定要清掉，否則測試之間會互相影響
        Torben.Log.isDebugLog = false;
    }

    [OneTimeTearDown]
    public void EndDispose()
    {
        Torben.Log.isPrintLog = printState;
    }

    [Test]
    public void Information_WritesNormalEntry()
    {
        //var category = new LogCategory { IdentifierName = "Test" };       // 非抽象類別可直接呼叫的用法。
        var category = TestCatlog.Instance;

        Log.Information("hello", category);

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(_sink.Entries[0].Level, Is.EqualTo(LogLevel.Normal));
        Assert.That(_sink.Entries[0].Message, Is.EqualTo("hello"));
        Assert.That(_sink.Entries[0].Category.Tag, Is.EqualTo("[Test]"));
    }

    [Test]
    public void Information_DefaultCategory_Tag()
    {
        Log.Information("hello"); 
        Assert.That(_sink.Entries[0].Category.Tag, Is.EqualTo(""));
    }

    [Test]
    public void MultipleSinks_AllReceiveEntry()
    {
        var second = new CaptureSink();
        Log.AddSink(second);

        Log.Warning("both");

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(second.Entries, Has.Count.EqualTo(1));
        Assert.That(second.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void AddSink_SameInstanceTwice_ReceivesOnce()
    {
        Log.AddSink(_sink);

        Log.Information("once");

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void RemoveSink_StopsReceiving()
    {
        var second = new CaptureSink();
        Log.AddSink(second);
        Log.RemoveSink(_sink);

        Log.Information("after remove");

        Assert.That(_sink.Entries, Is.Empty);
        Assert.That(second.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void HasSink_ReflectsSinkList()
    {
        Assert.That(Log.HasSink, Is.True);
        Log.ClearSinks();
        Assert.That(Log.HasSink, Is.False);
    }

    [Test]
    public void Debug_DisabledByDefault_NotWritten()
    {
        Torben.Log.isDebugLog = false;

        Log.Debug("hidden");

        Assert.That(_sink.Entries, Is.Empty);
    }

    [Test]
    public void Debug_Enabled_Written()
    {
        Torben.Log.isDebugLog = true;

        Log.Debug("shown");

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(_sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));
    }

    [Test]
    public void PrintLogOff_NothingWritten()
    {
        Torben.Log.isPrintLog = false;
        try
        {
            Log.Error("blocked");
            Assert.That(_sink.Entries, Is.Empty);
        }
        finally
        {
            Torben.Log.isPrintLog = true;
        }
    }
    [Test]
    public void IsEnabled_ReflectsSwitchesAndSinks()
    {
        Assert.That(Log.IsEnabled(LogLevel.Normal), Is.True);
        Assert.That(Log.IsEnabled(LogLevel.Debug), Is.False);

        Torben.Log.isDebugLog = true;
        Assert.That(Log.IsEnabled(LogLevel.Debug), Is.True);

        Log.ClearSinks();
        Assert.That(Log.IsEnabled(LogLevel.Normal), Is.False);
    }

    [Test]
    public void IsEnabled_PrintLogOff_False()
    {
        Torben.Log.isPrintLog = false;
        try
        {
            Assert.That(Log.IsEnabled(LogLevel.Error), Is.False);
        }
        finally
        {
            Torben.Log.isPrintLog = true;
        }
    }

    [Test]
    public void Format_Enabled_WritesFormattedMessage()
    {
        Log.Format(LogLevel.Normal, "a={0} b={1} c={2}", 1, "x", 2.5f);

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(_sink.Entries[0].Level, Is.EqualTo(LogLevel.Normal));
        Assert.That(_sink.Entries[0].Message, Is.EqualTo(string.Format("a={0} b={1} c={2}", 1, "x", 2.5f)));
        Assert.That(_sink.Entries[0].Category.Tag, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Format_WithCategory_UsesCategoryAndLevel()
    {
        Log.Format(LogLevel.Warning, TestCatlog.Instance, "retry {0}/{1}", 1, 3);

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(_sink.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(_sink.Entries[0].Category.Tag, Is.EqualTo("[Test]"));
        Assert.That(_sink.Entries[0].Message, Is.EqualTo("retry 1/3"));
    }

    [Test]
    public void Format_EachLevel_WritesWithThatLevel()
    {
        Torben.Log.isDebugLog = true;

        Log.Format(LogLevel.Normal, "{0}", 1);
        Log.Format(LogLevel.Warning, "{0}", 2);
        Log.Format(LogLevel.Error, TestCatlog.Instance, "{0}", 3);
        Log.Format(LogLevel.Debug, TestCatlog.Instance, "{0}{1}{2}", 4, 5, 6);

        Assert.That(_sink.Entries, Has.Count.EqualTo(4));
        Assert.That(_sink.Entries[0].Level, Is.EqualTo(LogLevel.Normal));
        Assert.That(_sink.Entries[1].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(_sink.Entries[2].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(_sink.Entries[2].Category.Tag, Is.EqualTo("[Test]"));
        Assert.That(_sink.Entries[3].Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(_sink.Entries[3].Message, Is.EqualTo("456"));
    }

    [Test]
    public void Format_DebugDisabled_DoesNotFormatArguments()
    {
        var arg = new CountingArg();

        Log.Format(LogLevel.Debug, "{0}", arg);

        Assert.That(_sink.Entries, Is.Empty);
        Assert.That(arg.ToStringCount, Is.EqualTo(0));
    }

    [Test]
    public void Debug_HasConditionalSymbols()
    {
        var method = typeof(Log).GetMethod(nameof(Log.Debug));
        var symbols = method.GetCustomAttributes(typeof(System.Diagnostics.ConditionalAttribute), false)
            .Cast<System.Diagnostics.ConditionalAttribute>()
            .Select(attribute => attribute.ConditionString);

        Assert.That(symbols, Is.EquivalentTo(new[] { "UNITY_EDITOR", "DEVELOPMENT_BUILD", "TORBEN_LOG_DEBUG" }));
    }

    // 防止日後又加回多載: 公開寫入方法只有 Information / Warning / Error / Debug 各 1 個與 Format 6 個
    [Test]
    public void PublicWriteMethods_OnlyExpectedTen()
    {
        var writeNames = new[] { nameof(Log.Information), nameof(Log.Warning), nameof(Log.Error), nameof(Log.Debug), nameof(Log.Format) };
        var methods = typeof(Log).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => !method.IsSpecialName && method.ReturnType == typeof(void)
                && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(LogArgsEnd)))
            .ToList();

        Assert.That(methods.Select(method => method.Name).Distinct(), Is.EquivalentTo(writeNames));
        Assert.That(methods, Has.Count.EqualTo(10));
        Assert.That(methods.Count(method => method.Name == nameof(Log.Format)), Is.EqualTo(6));
    }

    [Test]
    public void Format_PrintLogOff_DoesNotFormatArguments()
    {
        var arg = new CountingArg();
        Torben.Log.isPrintLog = false;
        try
        {
            Log.Format(LogLevel.Error, TestCatlog.Instance, "{0}", arg);
        }
        finally
        {
            Torben.Log.isPrintLog = true;
        }

        Assert.That(_sink.Entries, Is.Empty);
        Assert.That(arg.ToStringCount, Is.EqualTo(0));
    }

    [Test]
    public void SystemCategory_TagIncludesCustomTags_AndWritesNoLog()
    {
        Torben.Log.isDebugLog = true;

        var category = new SystemCategory("value", "DOL", "Text");

        Assert.That(category.Tag, Is.EqualTo("[String][DOL][Text]"));
        Assert.That(_sink.Entries, Is.Empty);
    }
}
