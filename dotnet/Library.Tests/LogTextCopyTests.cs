using NUnit.Framework;
using Torben;

// 標籤含 rich text 的分類
public sealed class RichTagCategory : LogCategory
{
    public static readonly RichTagCategory Instance = new RichTagCategory();
    private RichTagCategory() : base("<b>Rich</b>") { }
}

[TestFixture]
public class LogTextCopyTests
{
    private static readonly System.DateTime Time = new System.DateTime(2026, 11, 5, 11, 47, 27, 200, System.DateTimeKind.Utc);
    private const string Root = "/Users/me/Project";

    private static LogEntry Entry(LogLevel level, LogCategory category, string message,
        System.DateTime time = default, string file = null, int line = 0, LogStackTrace stack = null)
        => new LogEntry(level, category, message, time, file, file == null ? null : "Member", line, stack);

    // ---- 第一行 ----

    [Test]
    public void FirstLine_WithoutCategory()
    {
        var entry = Entry(LogLevel.Error, null, "message");
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Error] message"));
    }

    [Test]
    public void FirstLine_EmptyTag_Omitted()
    {
        var entry = Entry(LogLevel.Normal, LogCategoryEmpty.None, "message");
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Normal] message"));
    }

    [Test]
    public void FirstLine_WithCategory()
    {
        var entry = Entry(LogLevel.Warning, TestCatlog.Instance, "message");
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Warning] [Test] message"));
    }

    [Test]
    public void FirstLine_CollapseCount()
    {
        var entry = Entry(LogLevel.Normal, TestCatlog.Instance, "message");
        Assert.That(LogText.FormatForCopy(entry, 3, null), Is.EqualTo("[Normal] [Test] message (x3)"));
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Normal] [Test] message"));
    }

    [Test]
    public void FirstLine_RichTextRemovedFromTagAndMessage()
    {
        var entry = Entry(LogLevel.Normal, RichTagCategory.Instance, "<color=#FF0000>red</color> and <b>bold</b>");
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Normal] [Rich] red and bold"));
    }

    // ---- 第二行 ----

    [Test]
    public void SecondLine_TimeAndLocation()
    {
        var entry = Entry(LogLevel.Error, null, "m", Time, Root + "/Assets/Foo.cs", 68);
        string expected = "[Error] m\n(" + LogText.FormatTime(Time) + ") Assets/Foo.cs:68";
        Assert.That(LogText.FormatForCopy(entry, 1, Root), Is.EqualTo(expected));
    }

    [Test]
    public void SecondLine_TimeOnly()
    {
        var entry = Entry(LogLevel.Normal, null, "m", Time);
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Normal] m\n(" + LogText.FormatTime(Time) + ")"));
    }

    [Test]
    public void SecondLine_LocationOnly()
    {
        var entry = Entry(LogLevel.Normal, null, "m", default, Root + "/Assets/Foo.cs", 3);
        Assert.That(LogText.FormatForCopy(entry, 1, Root), Is.EqualTo("[Normal] m\nAssets/Foo.cs:3"));
    }

    [Test]
    public void SecondLine_NoTimeNoLocation_Omitted()
    {
        var entry = Entry(LogLevel.Normal, null, "m");
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Normal] m"));
    }

    [Test]
    public void SecondLine_UnityMessage_LocationFromStackTrace()
    {
        var stack = LogStackTrace.FromText("Foo:Bar () (at Assets/Foo.cs:12)");
        var entry = Entry(LogLevel.Normal, null, "m", default, null, 0, stack);
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Normal] m\nAssets/Foo.cs:12\nFoo:Bar () (at Assets/Foo.cs:12)"));
    }

    // ---- 堆疊 ----

    [Test]
    public void StackTrace_AppendedAfterDetails()
    {
        var stack = LogStackTrace.FromText("A.B () (at <00000000000000000000000000000000>:0)\nC.D ()");
        var entry = Entry(LogLevel.Error, null, "m", default, Root + "/Assets/Foo.cs", 1, stack);
        Assert.That(LogText.FormatForCopy(entry, 1, Root), Is.EqualTo("[Error] m\nAssets/Foo.cs:1\n" + stack.Text));
    }

    [Test]
    public void StackTrace_WithoutDetails()
    {
        var stack = LogStackTrace.FromText("C.D ()");
        var entry = Entry(LogLevel.Error, null, "m", default, null, 0, stack);
        Assert.That(LogText.FormatForCopy(entry, 1, null), Is.EqualTo("[Error] m\nC.D ()"));
    }

    [Test]
    public void StackTrace_KeepsAngleBrackets()
    {
        var stack = LogStackTrace.FromText("UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&) <OnGUIFunc>b__0 List<int>");
        var entry = Entry(LogLevel.Error, null, "m", default, null, 0, stack);
        Assert.That(LogText.FormatForCopy(entry, 1, null), Does.Contain("<OnGUIFunc>").And.Contain("List<int>"));
    }

    [Test]
    public void NewLines_AreLineFeedOnly()
    {
        var stack = LogStackTrace.FromText("A.B ()\r\nC.D ()");
        var entry = Entry(LogLevel.Error, null, "m", Time, Root + "/Assets/Foo.cs", 1, stack);
        Assert.That(LogText.FormatForCopy(entry, 1, Root), Does.Not.Contain("\r"));
    }

    // ---- rich text ----

    [TestCase("<b>a</b><i>b</i>", "ab")]
    [TestCase("<color=#FF6B6B>a</color>", "a")]
    [TestCase("<color=red>a</color>", "a")]
    [TestCase("<size=20>a</size>", "a")]
    [TestCase("<material=2>a</material>", "a")]
    [TestCase("a<quad material=1 size=20 x=0.1 y=0.1 width=0.5 height=0.5/>b", "ab")]
    [TestCase("a<quad/>b", "ab")]
    [TestCase("<b><color=#FFF>nested</color></b>", "nested")]
    public void StripRichText_RemovesKnownTags(string input, string expected)
        => Assert.That(LogText.StripRichText(input), Is.EqualTo(expected));

    [TestCase("List<int>")]
    [TestCase("Dictionary<string, List<int>>")]
    [TestCase("<OnGUIFunc>b__0")]
    [TestCase("(at <0x00012>:0)")]
    [TestCase("<color>")]
    [TestCase("<B>upper</B>")]
    [TestCase("<quad>")]
    [TestCase("a < b > c")]
    public void StripRichText_KeepsOtherBrackets(string input)
        => Assert.That(LogText.StripRichText(input), Is.EqualTo(input));

    [Test]
    public void StripRichText_NoTag_ReturnsSameInstance()
    {
        const string text = "plain text";
        Assert.That(LogText.StripRichText(text), Is.SameAs(text));
    }

    [TestCase("<b>unclosed", "unclosed")]
    [TestCase("<b", "<b")]
    [TestCase("text <", "text <")]
    [TestCase("<<b>>", "<>")]
    [TestCase("<b<i>>", "<b>")]
    [TestCase("<color=", "<color=")]
    [TestCase(">", ">")]
    [TestCase("<>", "<>")]
    public void StripRichText_MalformedDoesNotThrow(string input, string expected)
        => Assert.That(LogText.StripRichText(input), Is.EqualTo(expected));

    [Test]
    public void StripRichText_Null_ReturnsEmpty()
        => Assert.That(LogText.StripRichText(null), Is.EqualTo(string.Empty));
}
