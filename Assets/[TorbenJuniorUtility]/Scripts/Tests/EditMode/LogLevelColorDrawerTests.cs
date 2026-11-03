using NUnit.Framework;
using Torben.Editor;
using UnityEngine;

public sealed class LogLevelColorDrawerTests
{
    [Test]
    public void ToHex_OpaqueIsRgb_TranslucentIsRgba()
    {
        Assert.That(LogLevelColorDrawer.ToHex(new Color32(0xFF, 0x6B, 0x6B, 0xFF)), Is.EqualTo("#FF6B6B"));
        Assert.That(LogLevelColorDrawer.ToHex(new Color32(0x9E, 0x9E, 0x9E, 0x80)), Is.EqualTo("#9E9E9E80"));
    }

    [TestCase("#FFD54F", true)]
    [TestCase("#FFD54F80", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("not a color", false)]
    public void TryParse_HandlesHexAndEmpty(string hex, bool expected)
    {
        Assert.That(LogLevelColorDrawer.TryParse(hex, out _), Is.EqualTo(expected));
    }

    [Test]
    public void ToHex_RoundTripsDefaultColors()
    {
        foreach (var hex in new[] { "#FFD54F", "#FF6B6B", "#9E9E9E" })
        {
            Assert.That(LogLevelColorDrawer.TryParse(hex, out var color), Is.True);
            Assert.That(LogLevelColorDrawer.ToHex(color), Is.EqualTo(hex));
        }
    }
}
