using NUnit.Framework;
using Torben;

[TestFixture]
public class LogHistorySinkTests
{
    // Category 不影響 sink 行為, 用 null 即可
    private static LogEntry Entry(string message) => new LogEntry(LogLevel.Normal, null, message);

    private static void WriteMessages(LogHistorySink sink, params string[] messages)
    {
        foreach (var message in messages)
            sink.Write(Entry(message));
    }

    private static string[] Messages(LogEntry[] entries, int count)
    {
        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = entries[i].Message;
        return result;
    }

    [Test]
    public void CopyTo_Empty_ReturnsZero()
    {
        var sink = new LogHistorySink(3);
        var destination = new LogEntry[3];

        Assert.That(sink.CopyTo(destination), Is.EqualTo(0));
    }

    [Test]
    public void CopyTo_UnderCapacity_ReturnsOldestToNewest()
    {
        var sink = new LogHistorySink(3);
        WriteMessages(sink, "a", "b");
        var destination = new LogEntry[3];

        int copied = sink.CopyTo(destination);

        Assert.That(copied, Is.EqualTo(2));
        Assert.That(Messages(destination, copied), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Write_OverCapacity_OverwritesOldest()
    {
        var sink = new LogHistorySink(3);
        WriteMessages(sink, "a", "b", "c", "d", "e");
        var destination = new LogEntry[3];

        int copied = sink.CopyTo(destination);

        Assert.That(copied, Is.EqualTo(3));
        Assert.That(Messages(destination, copied), Is.EqualTo(new[] { "c", "d", "e" }));
    }

    [Test]
    public void Write_ExactlyCapacity_KeepsAllInOrder()
    {
        var sink = new LogHistorySink(3);
        WriteMessages(sink, "a", "b", "c");
        var destination = new LogEntry[3];

        int copied = sink.CopyTo(destination);

        Assert.That(Messages(destination, copied), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void CopyTo_SmallerDestination_ReturnsNewestOldestToNewest()
    {
        var sink = new LogHistorySink(5);
        WriteMessages(sink, "a", "b", "c", "d");
        var destination = new LogEntry[2];

        int copied = sink.CopyTo(destination);

        Assert.That(copied, Is.EqualTo(2));
        Assert.That(Messages(destination, copied), Is.EqualTo(new[] { "c", "d" }));
    }

    [Test]
    public void CopyTo_SmallerDestinationAfterWrapAround_ReturnsNewest()
    {
        var sink = new LogHistorySink(3);
        WriteMessages(sink, "a", "b", "c", "d"); // head 已繞回陣列開頭之後
        var destination = new LogEntry[2];

        int copied = sink.CopyTo(destination);

        Assert.That(Messages(destination, copied), Is.EqualTo(new[] { "c", "d" }));
    }

    [Test]
    public void Version_IncrementsOnEveryWrite_IncludingOverwrite()
    {
        var sink = new LogHistorySink(2);
        Assert.That(sink.Version, Is.EqualTo(0));

        for (int i = 1; i <= 5; i++)
        {
            sink.Write(Entry(i.ToString()));
            Assert.That(sink.Version, Is.EqualTo(i));
        }
    }

    [Test]
    public void CopyTo_DoesNotChangeVersion()
    {
        var sink = new LogHistorySink(2);
        WriteMessages(sink, "a");

        sink.CopyTo(new LogEntry[2]);

        Assert.That(sink.Version, Is.EqualTo(1));
    }

    [Test]
    public void CopyTo_WithVersion_ReturnsVersionAtCopy()
    {
        var sink = new LogHistorySink(2);
        WriteMessages(sink, "a", "b", "c");

        int copied = sink.CopyTo(new LogEntry[2], out int version);

        Assert.That(copied, Is.EqualTo(2));
        Assert.That(version, Is.EqualTo(3));
    }
}
