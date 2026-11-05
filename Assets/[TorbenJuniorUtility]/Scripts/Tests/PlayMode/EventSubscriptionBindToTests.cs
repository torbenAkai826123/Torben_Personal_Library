using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Torben.Events;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EventSubscriptionBindToTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in created)
            if (obj != null) Object.Destroy(obj);
        created.Clear();
    }

    private EventProbeSubscriber CreateSubscriber(string name)
    {
        var obj = new GameObject(name);
        created.Add(obj);
        return obj.AddComponent<EventProbeSubscriber>();
    }

    [UnityTest]
    public IEnumerator BindTo_DestroyGameObject_DisposesSubscription()
    {
        var channel = new EventChannel<ProbeEvent>("Probe");
        var subscriber = CreateSubscriber("Bound");
        var subscription = channel.Subscribe(subscriber.OnProbe).BindTo(subscriber);
        Assert.AreEqual(1, channel.SubscriberCount);

        Object.Destroy(subscriber.gameObject);
        yield return null;

        Assert.IsFalse(subscription.IsActive);
        Assert.AreEqual(0, channel.SubscriberCount);
        channel.Publish(new ProbeEvent(1));     // 已取消, 不會呼叫已銷毀的物件
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator BindTo_SameGameObjectManyTimes_SharesOneBinder_AllDisposed()
    {
        var first = new EventChannel<ProbeEvent>("First");
        var second = new EventChannel<ProbeEvent>("Second");
        var subscriber = CreateSubscriber("Shared");
        var other = subscriber.gameObject.AddComponent<EventProbeSubscriber>();
        var a = first.Subscribe(subscriber.OnProbe).BindTo(subscriber);
        var b = second.Subscribe(subscriber.OnProbe).BindTo(subscriber);
        var c = first.Subscribe(other.OnProbe).BindTo(other);   // 同一 GameObject 的另一個元件

        Assert.AreEqual(1, subscriber.GetComponents<EventSubscriptionBinder>().Length);
        Assert.AreEqual(3, subscriber.GetComponent<EventSubscriptionBinder>().Count);

        Object.Destroy(subscriber.gameObject);
        yield return null;

        Assert.IsFalse(a.IsActive || b.IsActive || c.IsActive);
        Assert.AreEqual(0, first.SubscriberCount);
        Assert.AreEqual(0, second.SubscriberCount);
    }

    [Test]
    public void BindTo_NullOwner_DisposesAndThrowsArgumentNull()
    {
        var channel = new EventChannel<ProbeEvent>();
        var subscription = channel.Subscribe((in ProbeEvent e) => { });

        Assert.Throws<System.ArgumentNullException>(() => subscription.BindTo(null));
        Assert.IsFalse(subscription.IsActive);
        Assert.AreEqual(0, channel.SubscriberCount);
    }

    [UnityTest]
    public IEnumerator BindTo_DestroyedOwner_DisposesAndThrowsInvalidOperation()
    {
        var channel = new EventChannel<ProbeEvent>();
        var subscriber = CreateSubscriber("Destroyed");
        Object.Destroy(subscriber.gameObject);
        yield return null;

        var subscription = channel.Subscribe((in ProbeEvent e) => { });
        Assert.Throws<System.InvalidOperationException>(() => subscription.BindTo(subscriber));
        Assert.IsFalse(subscription.IsActive);
        Assert.AreEqual(0, channel.SubscriberCount);
    }

    [Test]
    public void BindTo_NullSubscription_ThrowsArgumentNull()
    {
        var subscriber = CreateSubscriber("Owner");
        Assert.Throws<System.ArgumentNullException>(() => ((EventSubscription)null).BindTo(subscriber));
    }

    [UnityTest]
    public IEnumerator UnboundDestroyedSubscriber_OthersStillReceive_ReportedOnceInConsole()
    {
        Assert.IsNotNull(EventErrors.Handler, "EventErrors.Handler should be installed on load.");
        var channel = new EventChannel<ProbeEvent>("Leaky");
        var leaked = CreateSubscriber("Leaked");
        var alive = CreateSubscriber("Alive");
        channel.Subscribe(leaked.OnProbe);                      // 忘記 BindTo
        channel.Subscribe(alive.OnProbe).BindTo(alive);

        Object.Destroy(leaked.gameObject);
        yield return null;

        LogAssert.Expect(LogType.Exception, new Regex(@"\[Event\] Channel 'Leaky'"));
        channel.Publish(new ProbeEvent(1));
        channel.Publish(new ProbeEvent(2));                     // 同一訂閱再次失敗不再回報

        Assert.AreEqual(2, alive.ReceivedCount);
        Assert.AreEqual(2, channel.SubscriberCount);
        LogAssert.NoUnexpectedReceived();
    }
}
