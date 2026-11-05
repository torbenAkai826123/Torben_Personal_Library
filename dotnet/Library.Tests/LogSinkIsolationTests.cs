using System.Collections.Generic;
using NUnit.Framework;
using Torben;

// 每次 Write 都拋例外的 sink
public sealed class ThrowingSink : ILogSink
{
    public int WriteCount;
    public void Write(in LogEntry entry)
    {
        WriteCount++;
        throw new System.InvalidOperationException("sink failed");
    }
}

// 不配置的 sink, 用來量測寫入路徑本身的配置
public sealed class CountingSink : ILogSink
{
    public int Count;
    public void Write(in LogEntry entry) => Count++;
}

[TestFixture]
public class LogSinkIsolationTests
{
    private bool savedPrintLog;
    private readonly List<(ILogSink sink, System.Exception exception)> reports = new();

    [SetUp]
    public void SetUp()
    {
        savedPrintLog = Log.isPrintLog;
        Log.isPrintLog = true;
        Log.ClearSinks();
        reports.Clear();
        Log.SinkErrorHandler = (sink, exception) => reports.Add((sink, exception));
    }

    [TearDown]
    public void TearDown()
    {
        Log.SinkErrorHandler = null;
        Log.ClearSinks();
        Log.isPrintLog = savedPrintLog;
        Log.SetStackTraceEnabled(LogLevel.Error, true);
    }

    [Test]
    public void FirstSinkThrows_SecondSinkStillReceives_CallerGetsNoException()
    {
        var throwing = new ThrowingSink();
        var capture = new CaptureSink();
        Log.AddSink(throwing);
        Log.AddSink(capture);

        Assert.DoesNotThrow(() => Log.Information("hello"));

        Assert.That(capture.Entries.Count, Is.EqualTo(1));
        Assert.That(capture.Entries[0].Message, Is.EqualTo("hello"));
        Assert.That(reports.Count, Is.EqualTo(1));
        Assert.That(reports[0].sink, Is.SameAs(throwing));
        Assert.That(reports[0].exception, Is.TypeOf<System.InvalidOperationException>());
    }

    [Test]
    public void SameSinkFailsRepeatedly_ReportedOnce_AndSinkIsKept()
    {
        var throwing = new ThrowingSink();
        Log.AddSink(throwing);

        Log.Information("a");
        Log.Information("b");
        Log.Information("c");

        Assert.That(reports.Count, Is.EqualTo(1));
        Assert.That(throwing.WriteCount, Is.EqualTo(3), "失敗的 sink 不會被移除");
        Assert.That(Log.HasSink, Is.True);
    }

    [Test]
    public void RemoveAndReAdd_ReportsAgain()
    {
        var throwing = new ThrowingSink();
        Log.AddSink(throwing);
        Log.Information("a");
        Log.RemoveSink(throwing);
        Log.AddSink(throwing);

        Log.Information("b");

        Assert.That(reports.Count, Is.EqualTo(2));
    }

    [Test]
    public void ClearSinksAndReAdd_ReportsAgain()
    {
        var throwing = new ThrowingSink();
        Log.AddSink(throwing);
        Log.Information("a");
        Log.ClearSinks();
        Log.AddSink(throwing);

        Log.Information("b");

        Assert.That(reports.Count, Is.EqualTo(2));
    }

    [Test]
    public void DifferentSinks_EachReportedOnce()
    {
        var first = new ThrowingSink();
        var second = new ThrowingSink();
        Log.AddSink(first);
        Log.AddSink(second);

        Log.Information("a");
        Log.Information("b");

        Assert.That(reports.Count, Is.EqualTo(2));
        Assert.That(reports[0].sink, Is.SameAs(first));
        Assert.That(reports[1].sink, Is.SameAs(second));
    }

    [Test]
    public void HandlerNull_DoesNotThrow()
    {
        Log.SinkErrorHandler = null;
        var capture = new CaptureSink();
        Log.AddSink(new ThrowingSink());
        Log.AddSink(capture);

        Assert.DoesNotThrow(() => Log.Error("x"));
        Assert.That(capture.Entries.Count, Is.EqualTo(1));
    }

    [Test]
    public void HandlerCallsLog_WriteIsSkipped_NoRecursion()
    {
        var capture = new CaptureSink();
        var throwing = new ThrowingSink();
        Log.AddSink(capture);
        Log.AddSink(throwing);
        int handlerCalls = 0;
        Log.SinkErrorHandler = (sink, exception) =>
        {
            handlerCalls++;
            Log.Error("from handler");
        };

        Log.Information("original");

        Assert.That(handlerCalls, Is.EqualTo(1));
        Assert.That(throwing.WriteCount, Is.EqualTo(1), "回報期間的 Log 寫入被略過");
        Assert.That(capture.Entries.ConvertAll(entry => entry.Message), Is.EqualTo(new[] { "original" }));

        // 回報結束後恢復正常寫入
        Log.Information("after");
        Assert.That(capture.Entries.Count, Is.EqualTo(2));
    }

    [Test]
    public void HandlerThrows_CallerGetsNoException()
    {
        Log.SinkErrorHandler = (sink, exception) => throw new System.InvalidOperationException("handler failed");
        Log.AddSink(new ThrowingSink());

        Assert.DoesNotThrow(() => Log.Information("x"));
        // 回報旗標已復原: 之後的寫入照常送到 sink
        var capture = new CaptureSink();
        Log.AddSink(capture);
        Log.Information("y");
        Assert.That(capture.Entries.Count, Is.EqualTo(1));
    }

    // 無例外時寫入路徑不配置 (堆疊追蹤關閉; LogEntry 為 struct, 訊息為常值)
    [Test]
    public void NoException_WriteDoesNotAllocate()
    {
        Log.SetStackTraceEnabled(LogLevel.Error, false);
        var sink = new CountingSink();
        Log.AddSink(sink);
        for (int i = 0; i < 100; i++)
        {
            Log.Information("warm up");
            Log.Error("warm up");
        }

        long before = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            Log.Information("message");
            Log.Error("message");
        }
        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(sink.Count, Is.EqualTo(2200));
        Assert.That(allocated, Is.EqualTo(0));
    }
}
