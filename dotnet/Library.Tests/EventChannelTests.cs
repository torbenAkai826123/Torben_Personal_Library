using System.Collections.Generic;
using NUnit.Framework;
using Torben;
using Torben.Events;

public readonly struct TestEvent
{
    public readonly int Value;
    public TestEvent(int value) => Value = value;
}

public readonly struct OtherTestEvent
{
    public readonly int Value;
    public OtherTestEvent(int value) => Value = value;
}

// 記錄追查訊息用的 sink
public sealed class EventTraceSink : ILogSink
{
    public readonly List<string> Messages = new List<string>();
    public void Write(in LogEntry entry) => Messages.Add(entry.Message);
}

// 不配置的訂閱者, 用來量測 Publish 本身的配置
public sealed class CountingSubscriber
{
    public int Count;
    public int Sum;
    public void OnEvent(in TestEvent e)
    {
        Count++;
        Sum += e.Value;
    }
}

[TestFixture]
public class EventChannelTests
{
    private readonly List<(string channel, System.Exception exception)> reports = new();

    [SetUp]
    public void SetUp()
    {
        reports.Clear();
        EventErrors.Handler = (channel, exception) => reports.Add((channel, exception));
    }

    [TearDown]
    public void TearDown()
    {
        EventErrors.Handler = null;
    }

    // ---- 基本 ----

    [Test]
    public void Publish_AllSubscribersReceive()
    {
        var channel = new EventChannel<TestEvent>();
        int a = 0, b = 0, c = 0;
        channel.Subscribe((in TestEvent e) => a++);
        channel.Subscribe((in TestEvent e) => b++);
        channel.Subscribe((in TestEvent e) => c++);

        channel.Publish(new TestEvent(1));

        Assert.That((a, b, c), Is.EqualTo((1, 1, 1)));
    }

    [Test]
    public void Publish_CallsInSubscriptionOrder()
    {
        var channel = new EventChannel<TestEvent>();
        var order = new List<string>();
        channel.Subscribe((in TestEvent e) => order.Add("first"));
        channel.Subscribe((in TestEvent e) => order.Add("second"));
        channel.Subscribe((in TestEvent e) => order.Add("third"));

        channel.Publish(default);

        Assert.That(order, Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void Publish_WithoutSubscribers_DoesNothing()
    {
        var channel = new EventChannel<TestEvent>();
        Assert.DoesNotThrow(() => channel.Publish(new TestEvent(1)));
        Assert.That(channel.SubscriberCount, Is.EqualTo(0));
        Assert.That(reports, Is.Empty);
    }

    [Test]
    public void Publish_PassesEventContent()
    {
        var channel = new EventChannel<TestEvent>();
        int received = 0;
        channel.Subscribe((in TestEvent e) => received = e.Value);

        channel.Publish(new TestEvent(42));

        Assert.That(received, Is.EqualTo(42));
    }

    [Test]
    public void DebugName_DefaultsToTypeName()
    {
        Assert.That(new EventChannel<TestEvent>().DebugName, Is.EqualTo("TestEvent"));
        Assert.That(new EventChannel<TestEvent>("DoorOpened").DebugName, Is.EqualTo("DoorOpened"));
    }

    [Test]
    public void Subscribe_Null_Throws()
    {
        var channel = new EventChannel<TestEvent>();
        Assert.Throws<System.ArgumentNullException>(() => channel.Subscribe(null));
        Assert.That(channel.SubscriberCount, Is.EqualTo(0));
    }

    // ---- 取消 ----

    [Test]
    public void Dispose_StopsReceiving()
    {
        var channel = new EventChannel<TestEvent>();
        int count = 0;
        var subscription = channel.Subscribe((in TestEvent e) => count++);
        channel.Publish(default);

        subscription.Dispose();
        channel.Publish(default);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Dispose_Twice_DoesNothing()
    {
        var channel = new EventChannel<TestEvent>();
        var first = channel.Subscribe((in TestEvent e) => { });
        channel.Subscribe((in TestEvent e) => { });

        first.Dispose();
        Assert.DoesNotThrow(() => first.Dispose());
        Assert.That(channel.SubscriberCount, Is.EqualTo(1));
    }

    [Test]
    public void IsActive_And_SubscriberCount_TrackSubscriptions()
    {
        var channel = new EventChannel<TestEvent>();
        var a = channel.Subscribe((in TestEvent e) => { });
        var b = channel.Subscribe((in TestEvent e) => { });
        Assert.That(channel.SubscriberCount, Is.EqualTo(2));
        Assert.That(a.IsActive && b.IsActive, Is.True);

        a.Dispose();
        Assert.That(a.IsActive, Is.False);
        Assert.That(b.IsActive, Is.True);
        Assert.That(channel.SubscriberCount, Is.EqualTo(1));

        b.Dispose();
        Assert.That(channel.SubscriberCount, Is.EqualTo(0));
    }

    // ---- 發布中變更 ----

    [Test]
    public void SubscribeDuringPublish_NotCalledThisTime()
    {
        var channel = new EventChannel<TestEvent>();
        int late = 0;
        bool added = false;
        channel.Subscribe((in TestEvent e) =>
        {
            if (added) return;
            added = true;
            channel.Subscribe((in TestEvent e2) => late++);
        });

        channel.Publish(default);
        Assert.That(late, Is.EqualTo(0));
        Assert.That(channel.SubscriberCount, Is.EqualTo(2));

        channel.Publish(default);
        Assert.That(late, Is.EqualTo(1));
    }

    [Test]
    public void DisposeDuringPublish_NotYetCalledSubscriber_IsSkipped()
    {
        var channel = new EventChannel<TestEvent>();
        EventSubscription second = null;
        int secondCount = 0, thirdCount = 0;
        channel.Subscribe((in TestEvent e) => second.Dispose());
        second = channel.Subscribe((in TestEvent e) => secondCount++);
        channel.Subscribe((in TestEvent e) => thirdCount++);

        channel.Publish(default);

        Assert.That(secondCount, Is.EqualTo(0));
        Assert.That(thirdCount, Is.EqualTo(1));
        Assert.That(channel.SubscriberCount, Is.EqualTo(2));
    }

    [Test]
    public void DisposeSelfDuringPublish_RestStillReceive()
    {
        var channel = new EventChannel<TestEvent>();
        EventSubscription self = null;
        int selfCount = 0, otherCount = 0;
        self = channel.Subscribe((in TestEvent e) =>
        {
            selfCount++;
            self.Dispose();
        });
        channel.Subscribe((in TestEvent e) => otherCount++);

        channel.Publish(default);
        channel.Publish(default);

        Assert.That(selfCount, Is.EqualTo(1));
        Assert.That(otherCount, Is.EqualTo(2));
        Assert.That(self.IsActive, Is.False);
    }

    // ---- 例外 ----

    [Test]
    public void FirstSubscriberThrows_SecondStillReceives_PublisherGetsNoException()
    {
        var channel = new EventChannel<TestEvent>("Damaged");
        int second = 0;
        channel.Subscribe((in TestEvent e) => throw new System.InvalidOperationException("boom"));
        channel.Subscribe((in TestEvent e) => second++);

        Assert.DoesNotThrow(() => channel.Publish(default));

        Assert.That(second, Is.EqualTo(1));
        Assert.That(reports.Count, Is.EqualTo(1));
        Assert.That(reports[0].channel, Is.EqualTo("Damaged"));
        Assert.That(reports[0].exception.Message, Is.EqualTo("boom"));
    }

    [Test]
    public void SameSubscriptionFailingRepeatedly_ReportedOnce()
    {
        var channel = new EventChannel<TestEvent>();
        int calls = 0;
        channel.Subscribe((in TestEvent e) =>
        {
            calls++;
            throw new System.InvalidOperationException("again");
        });

        channel.Publish(default);
        channel.Publish(default);
        channel.Publish(default);

        Assert.That(calls, Is.EqualTo(3));
        Assert.That(reports.Count, Is.EqualTo(1));
    }

    [Test]
    public void DifferentFailingSubscriptions_EachReportedOnce()
    {
        var channel = new EventChannel<TestEvent>();
        channel.Subscribe((in TestEvent e) => throw new System.InvalidOperationException("a"));
        channel.Subscribe((in TestEvent e) => throw new System.InvalidOperationException("b"));

        channel.Publish(default);
        channel.Publish(default);

        Assert.That(reports.Count, Is.EqualTo(2));
    }

    [Test]
    public void NullHandler_DoesNotThrow()
    {
        EventErrors.Handler = null;
        var channel = new EventChannel<TestEvent>();
        int second = 0;
        channel.Subscribe((in TestEvent e) => throw new System.InvalidOperationException());
        channel.Subscribe((in TestEvent e) => second++);

        Assert.DoesNotThrow(() => channel.Publish(default));
        Assert.That(second, Is.EqualTo(1));
    }

    [Test]
    public void HandlerThrows_NotPassedToPublisher()
    {
        EventErrors.Handler = (channel, exception) => throw new System.InvalidOperationException("handler failed");
        var channel = new EventChannel<TestEvent>();
        int second = 0;
        channel.Subscribe((in TestEvent e) => throw new System.InvalidOperationException());
        channel.Subscribe((in TestEvent e) => second++);

        Assert.DoesNotThrow(() => channel.Publish(default));
        Assert.That(second, Is.EqualTo(1));
    }

    // ---- 巢狀 ----

    [Test]
    public void PublishOtherEventInCallback_RunsSynchronously()
    {
        var damaged = new EventChannel<TestEvent>();
        var died = new EventChannel<OtherTestEvent>();
        var order = new List<string>();
        damaged.Subscribe((in TestEvent e) =>
        {
            order.Add("damaged");
            died.Publish(new OtherTestEvent(e.Value));
            order.Add("damaged done");
        });
        died.Subscribe((in OtherTestEvent e) => order.Add("died " + e.Value));

        damaged.Publish(new TestEvent(7));

        Assert.That(order, Is.EqualTo(new[] { "damaged", "died 7", "damaged done" }));
        Assert.That(reports, Is.Empty);
    }

    [Test]
    public void PublishSameEventInCallback_WithStopCondition_Works()
    {
        var channel = new EventChannel<TestEvent>();
        var values = new List<int>();
        channel.Subscribe((in TestEvent e) =>
        {
            values.Add(e.Value);
            if (e.Value < 3) channel.Publish(new TestEvent(e.Value + 1));
        });

        channel.Publish(new TestEvent(1));

        Assert.That(values, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(reports, Is.Empty);
    }

    [Test]
    public void InfiniteLoop_StopsAtMaxDepth_AndReportsOnce()
    {
        var ping = new EventChannel<TestEvent>("Ping");
        var pong = new EventChannel<OtherTestEvent>("Pong");
        int pingCalls = 0, pongCalls = 0;
        ping.Subscribe((in TestEvent e) => { pingCalls++; pong.Publish(default); });
        pong.Subscribe((in OtherTestEvent e) => { pongCalls++; ping.Publish(default); });

        Assert.DoesNotThrow(() => ping.Publish(default));

        // 深度 1 到 MaxPublishDepth 的 Publish 都有執行, 兩個頻道交替
        Assert.That(pingCalls + pongCalls, Is.EqualTo(EventChannel.MaxPublishDepth));
        Assert.That(reports.Count, Is.EqualTo(1));
        Assert.That(reports[0].exception, Is.TypeOf<System.InvalidOperationException>());
        StringAssert.Contains(reports[0].channel, reports[0].exception.Message);
        StringAssert.Contains("infinite loop", reports[0].exception.Message);

        // 同一頻道再次超過深度不再回報; 深度計數已歸零, 一般發布照常
        ping.Publish(default);
        Assert.That(reports.Count, Is.EqualTo(1));
        Assert.That(pingCalls + pongCalls, Is.EqualTo(EventChannel.MaxPublishDepth * 2));
    }

    [Test]
    public void DepthCounter_RestoredAfterPublish()
    {
        var channel = new EventChannel<TestEvent>();
        int nestedCalls = 0;
        // 先跑一次會深入到上限的循環, 再確認單層發布仍可執行
        channel.Subscribe((in TestEvent e) => { nestedCalls++; channel.Publish(default); });
        channel.Publish(default);
        Assert.That(nestedCalls, Is.EqualTo(EventChannel.MaxPublishDepth));

        var other = new EventChannel<OtherTestEvent>();
        int otherCalls = 0;
        other.Subscribe((in OtherTestEvent e) => otherCalls++);
        other.Publish(default);
        Assert.That(otherCalls, Is.EqualTo(1));
    }

    // ---- 群組 ----

    [Test]
    public void Group_Dispose_CancelsAll()
    {
        var channel = new EventChannel<TestEvent>();
        int count = 0;
        var group = new EventSubscriptionGroup();
        group.Add(channel.Subscribe((in TestEvent e) => count++));
        group.Add(channel.Subscribe((in TestEvent e) => count++));
        Assert.That(group.Count, Is.EqualTo(2));

        group.Dispose();
        channel.Publish(default);

        Assert.That(count, Is.EqualTo(0));
        Assert.That(channel.SubscriberCount, Is.EqualTo(0));
        Assert.That(group.Count, Is.EqualTo(0));
    }

    [Test]
    public void Group_AddAfterDispose_CancelsImmediately()
    {
        var channel = new EventChannel<TestEvent>();
        var group = new EventSubscriptionGroup();
        group.Dispose();

        var subscription = channel.Subscribe((in TestEvent e) => { });
        group.Add(subscription);

        Assert.That(subscription.IsActive, Is.False);
        Assert.That(channel.SubscriberCount, Is.EqualTo(0));
        Assert.That(group.Count, Is.EqualTo(0));
    }

    [Test]
    public void Group_AddNull_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new EventSubscriptionGroup().Add(null));
    }

    // ---- 同一 callback ----

    [Test]
    public void SameCallbackTwice_CalledTwice_EachCancellable()
    {
        var channel = new EventChannel<TestEvent>();
        var subscriber = new CountingSubscriber();
        EventCallback<TestEvent> callback = subscriber.OnEvent;
        var first = channel.Subscribe(callback);
        var second = channel.Subscribe(callback);

        channel.Publish(default);
        Assert.That(subscriber.Count, Is.EqualTo(2));

        first.Dispose();
        channel.Publish(default);
        Assert.That(subscriber.Count, Is.EqualTo(3));
        Assert.That(second.IsActive, Is.True);

        second.Dispose();
        channel.Publish(default);
        Assert.That(subscriber.Count, Is.EqualTo(3));
    }

    // ---- 追查 ----

    [Test]
    public void Trace_Enabled_WritesThroughLog()
    {
        bool savedPrintLog = Log.isPrintLog;
        var sink = new EventTraceSink();
        try
        {
            Log.isPrintLog = true;
            Log.ClearSinks();
            Log.AddSink(sink);
            var channel = new EventChannel<TestEvent>("DoorOpened") { IsTraceEnabled = true };
            channel.Subscribe((in TestEvent e) => { });
            channel.Subscribe((in TestEvent e) => { });

            channel.Publish(default);

            Assert.That(sink.Messages, Is.EqualTo(new[] { "[Event] DoorOpened -> 2 subscriber(s)" }));
        }
        finally
        {
            Log.ClearSinks();
            Log.isPrintLog = savedPrintLog;
        }
    }

    [Test]
    public void Trace_Disabled_WritesNothing()
    {
        bool savedPrintLog = Log.isPrintLog;
        var sink = new EventTraceSink();
        try
        {
            Log.isPrintLog = true;
            Log.ClearSinks();
            Log.AddSink(sink);
            var channel = new EventChannel<TestEvent>("DoorOpened");
            channel.Subscribe((in TestEvent e) => { });

            channel.Publish(default);

            Assert.That(sink.Messages, Is.Empty);
        }
        finally
        {
            Log.ClearSinks();
            Log.isPrintLog = savedPrintLog;
        }
    }

    // ---- 配置 ----

    [TestCase(false)]
    [TestCase(true)]
    public void Publish_WithSubscribers_LogOff_DoesNotAllocate(bool traceEnabled)
    {
        bool savedPrintLog = Log.isPrintLog;
        try
        {
            Log.isPrintLog = false;
            var channel = new EventChannel<TestEvent>("Alloc") { IsTraceEnabled = traceEnabled };
            var subscriber = new CountingSubscriber();
            channel.Subscribe(subscriber.OnEvent);
            channel.Subscribe(subscriber.OnEvent);
            var e = new TestEvent(1);

            for (int i = 0; i < 100; i++) channel.Publish(in e);    // 暖機 (JIT、ThreadStatic 初始化)
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) channel.Publish(in e);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(subscriber.Count, Is.EqualTo(2 * 10100));
        }
        finally
        {
            Log.isPrintLog = savedPrintLog;
        }
    }
}
