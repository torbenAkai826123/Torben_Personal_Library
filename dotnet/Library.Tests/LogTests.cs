using System.Collections.Generic;
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
        Log.Sink = _sink;
        // 如果關閉就不會有輸出，會出現錯誤。
        if (!Torben.Log.isPrintLog)
        {
            printState = Torben.Log.isPrintLog;
            Torben.Log.isPrintLog = true;
        }
    }

    [TearDown]
    public void TearDown() => Log.Sink = null;   // 靜態狀態一定要清掉，否則測試之間會互相影響

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
}
