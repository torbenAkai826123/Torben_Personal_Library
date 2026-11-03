using NUnit.Framework;
using Torben.Editor;

public sealed class ScriptingDefineUtilityTests
{
    private const string Symbol = ScriptingDefineUtility.LogDebugSymbol;

    [TestCase(null, ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase("A;B", ExpectedResult = false)]
    [TestCase("TORBEN_LOG_DEBUG", ExpectedResult = true)]
    [TestCase("A; TORBEN_LOG_DEBUG ;B", ExpectedResult = true)]
    [TestCase("A;TORBEN_LOG_DEBUG_X", ExpectedResult = false)]
    [TestCase("torben_log_debug", ExpectedResult = false)]
    public bool Contains_FindsTrimmedSymbol(string defines) => ScriptingDefineUtility.Contains(defines, Symbol);

    [TestCase(null, ExpectedResult = "TORBEN_LOG_DEBUG")]
    [TestCase("", ExpectedResult = "TORBEN_LOG_DEBUG")]
    [TestCase("A", ExpectedResult = "A;TORBEN_LOG_DEBUG")]
    [TestCase(" A ; ;B; ", ExpectedResult = "A;B;TORBEN_LOG_DEBUG")]
    [TestCase("A;TORBEN_LOG_DEBUG;B", ExpectedResult = "A;TORBEN_LOG_DEBUG;B")]
    [TestCase("A;A;TORBEN_LOG_DEBUG;TORBEN_LOG_DEBUG", ExpectedResult = "A;TORBEN_LOG_DEBUG")]
    public string Add_NormalizesAndDoesNotDuplicate(string defines) => ScriptingDefineUtility.Add(defines, Symbol);

    [TestCase(null, ExpectedResult = "")]
    [TestCase("", ExpectedResult = "")]
    [TestCase("TORBEN_LOG_DEBUG", ExpectedResult = "")]
    [TestCase("A; TORBEN_LOG_DEBUG ;B", ExpectedResult = "A;B")]
    [TestCase("TORBEN_LOG_DEBUG;A;TORBEN_LOG_DEBUG", ExpectedResult = "A")]
    [TestCase("A;B;A", ExpectedResult = "A;B")]
    public string Remove_RemovesEveryOccurrence(string defines) => ScriptingDefineUtility.Remove(defines, Symbol);

    [Test]
    public void Add_EmptySymbol_OnlyNormalizes()
    {
        Assert.That(ScriptingDefineUtility.Add(" A;;A ", "  "), Is.EqualTo("A"));
        Assert.That(ScriptingDefineUtility.Contains("A", " "), Is.False);
    }

    [Test]
    public void Set_AddsOrRemoves()
    {
        Assert.That(ScriptingDefineUtility.Set("A", Symbol, true), Is.EqualTo("A;TORBEN_LOG_DEBUG"));
        Assert.That(ScriptingDefineUtility.Set("A;TORBEN_LOG_DEBUG", Symbol, false), Is.EqualTo("A"));
    }
}
