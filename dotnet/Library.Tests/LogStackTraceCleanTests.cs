using NUnit.Framework;
using Torben;

// LogStackTrace 轉文字時的逐行清理 (IL / 原生偏移、路徑縮短)
[TestFixture]
public class LogStackTraceCleanTests
{
    private const string Il2CppWithLines =
        "  at CallLog.LogError (System.String message) [0x0001c] in /Users/torbenstudio/Exclude/Github.com/Torben_Personal_Library/Assets/Exceptions/LogTest/CallLog.cs:68\n"
      + "  at CallLog+<>c__DisplayClass9_0.<OnGUIFunc>b__5 () [0x00000] in /Users/torbenstudio/Exclude/Github.com/Torben_Personal_Library/Assets/Exceptions/LogTest/CallLog.cs:98\n"
      + "  at CallLog.OnGUIFunc (System.Int32 id) [0x0010a] in /Users/torbenstudio/Exclude/Github.com/Torben_Personal_Library/Assets/Exceptions/LogTest/CallLog.cs:98\n"
      + "  at UnityEngine.GUI.CallWindowDelegate (UnityEngine.GUI+WindowFunction func, System.Int32 id, ...) [0x00061] in /Users/bokken/build/output/unity/unity/Modules/IMGUI/GUI.cs:1847";

    private const string Il2CppWithLinesCleaned =
        "  at CallLog.LogError (System.String message) in Assets/Exceptions/LogTest/CallLog.cs:68\n"
      + "  at CallLog+<>c__DisplayClass9_0.<OnGUIFunc>b__5 () in Assets/Exceptions/LogTest/CallLog.cs:98\n"
      + "  at CallLog.OnGUIFunc (System.Int32 id) in Assets/Exceptions/LogTest/CallLog.cs:98\n"
      + "  at UnityEngine.GUI.CallWindowDelegate (UnityEngine.GUI+WindowFunction func, System.Int32 id, ...) in GUI.cs:1847";

    [Test]
    public void Il2CppWithLines_RemovesOffsetAndShortensPath()
    {
        Assert.That(LogStackTrace.CleanFrames(Il2CppWithLines), Is.EqualTo(Il2CppWithLinesCleaned));
    }

    [Test]
    public void Il2CppMethodNameOnly_RemovesTail()
    {
        string input = "  at CallLog.LogError (System.String message) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";
        Assert.That(LogStackTrace.CleanFrames(input), Is.EqualTo("  at CallLog.LogError (System.String message)"));
    }

    // 規則 A 的路徑變化
    [TestCase("  at A.B () [0x00012] in /Users/a b/Assets/X.cs:5", "  at A.B () in Assets/X.cs:5")]
    [TestCase(@"  at A.B () [0x00012] in C:\proj\Assets\X.cs:5", "  at A.B () in Assets/X.cs:5")]
    [TestCase("  at A.B () [0x00012] in /build/proj/Packages/com.x/Y.cs:7", "  at A.B () in Packages/com.x/Y.cs:7")]
    [TestCase("  at A.B () [0x00012] in /some/where/Z.cs:9", "  at A.B () in Z.cs:9")]
    public void RuleA_PathVariants(string input, string expected)
    {
        Assert.That(LogStackTrace.CleanFrames(input), Is.EqualTo(expected));
    }

    // 角括號內是 MVID 時沒有原始碼位置, 行號只是佔位
    [TestCase("  at A.B () <0x00012 + 0x00034> 0 in <a1b2c3d4e5f60718293a4b5c6d7e8f90>:0")]
    [TestCase("  at A.B () <0x00012 + 0x00034> 0 in <a1b2c3d4e5f60718293a4b5c6d7e8f90>:68")]
    public void RuleB_AnyMvidAnyLine_RemovesTail(string input)
    {
        Assert.That(LogStackTrace.CleanFrames(input), Is.EqualTo("  at A.B ()"));
    }

    // IL2CPP 有行號的建置中, 沒有原始碼的框架 (例如委派的 Invoke)
    [TestCase("  at System.Action.Invoke () [0x00000] in <00000000000000000000000000000000>:1")]
    [TestCase("  at System.Action.Invoke () [0x00000] in <00000000000000000000000000000000>:0 ")]
    public void RuleC_IlOffsetWithMvid_RemovesTail(string input)
    {
        Assert.That(LogStackTrace.CleanFrames(input), Is.EqualTo("  at System.Action.Invoke ()"));
    }

    // 不符合任何規則時原樣保留, 且回傳同一個字串 (不配置)
    [TestCase("  at X () in <filename unknown>:0")]
    [TestCase("  at X () [0x00000] in <filename unknown>:0")]
    [TestCase("CallLog:LogError (string) (at Assets/CallLog.cs:68)")]
    [TestCase("  at A.B ()")]
    [TestCase("  at A.B () in /Users/me/Assets/X.cs:5")]
    [TestCase("")]
    [TestCase("\n")]
    [TestCase("\r\n\r\n")]
    public void Unmatched_KeepsOriginal(string input)
    {
        Assert.That(LogStackTrace.CleanFrames(input), Is.SameAs(input));
    }

    [Test]
    public void MixedLines_LastLineWithoutNewline()
    {
        string input = "  at A.B () [0x1] in /p/Assets/X.cs:5\n  at C.D ()\n  at E.F () <0x1 + 0x2> 0 in <ab>:0";
        Assert.That(LogStackTrace.CleanFrames(input),
            Is.EqualTo("  at A.B () in Assets/X.cs:5\n  at C.D ()\n  at E.F ()"));
    }

    [Test]
    public void CrLf_IsPreserved()
    {
        string input = "  at A.B () [0x1] in /p/Assets/X.cs:5\r\n  at E.F () <0x1 + 0x2> 0 in <ab>:0\r\n  at G.H ()\r\n";
        Assert.That(LogStackTrace.CleanFrames(input),
            Is.EqualTo("  at A.B () in Assets/X.cs:5\r\n  at E.F ()\r\n  at G.H ()\r\n"));
    }

    // Mono 類別庫的實際格式: 每行行號後有一個空白 (最後一行會被 TrimEnd 去掉)
    [Test]
    public void MonoTrailingSpace_IsCleanedAndRemoved()
    {
        string input = "  at A.B () [0x0001c] in /p/Assets/X.cs:68 \n"
                     + "  at C.D () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0 \n"
                     + "  at E.F () [0x00061] in /build/GUI.cs:1847";
        Assert.That(LogStackTrace.CleanFrames(input),
            Is.EqualTo("  at A.B () in Assets/X.cs:68\n  at C.D ()\n  at E.F () in GUI.cs:1847"));
    }

    [Test]
    public void TrailingSpace_UnmatchedLineKeepsOriginal()
    {
        string input = "  at A.B () \n  at C.D () in <abc>:12 ";
        Assert.That(LogStackTrace.CleanFrames(input), Is.SameAs(input));
    }

    [Test]
    public void LogFramesStrippedBeforeCleaning()
    {
        string input = "  at Torben.Log.Error (System.String message) [0x00001] in /p/Assets/Log.cs:10\n"
                     + "  at Torben.Log.SinkLog () [0x00002] in /p/Assets/Log.cs:20\n"
                     + "  at Game.Player.Hit () [0x00003] in /p/Assets/Game/Player.cs:30";
        Assert.That(LogStackTrace.ToDisplayText(input), Is.EqualTo("  at Game.Player.Hit () in Assets/Game/Player.cs:30"));
    }

    [Test]
    public void FromText_UnityFormat_Unchanged()
    {
        string unity = "UnityEngine.Debug:LogError (object)\nCallLog:LogError (string) (at Assets/CallLog.cs:68)\nGame.Boot:Start () (at <00000000000000000000000000000000>:0)";
        Assert.That(LogStackTrace.FromText(unity + "\n").Text, Is.EqualTo(unity));
    }

    [Test]
    public void FromText_Il2CppFormat_Cleaned()
    {
        Assert.That(LogStackTrace.FromText(Il2CppWithLines + "\n").Text, Is.EqualTo(Il2CppWithLinesCleaned));
    }
}
