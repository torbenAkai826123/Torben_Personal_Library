using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Torben;
using Torben.Editor;
using UnityEditor;
using UnityEngine;

public sealed class LogSettingsCheckerTests
{
    // 暫存 asset 的資料夾; TearDown 時整個刪除
    private const string TempFolder = LogSettingsAssets.LibraryRoot + "/TempLogSettingsTests";

    [SetUp]
    public void SetUp()
    {
        if (!AssetDatabase.IsValidFolder(TempFolder))
            AssetDatabase.CreateFolder(LogSettingsAssets.LibraryRoot, "TempLogSettingsTests");
    }

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(TempFolder);
    }

    private static LogSettings CreateTemp(string name)
    {
        var settings = ScriptableObject.CreateInstance<LogSettings>();
        AssetDatabase.CreateAsset(settings, $"{TempFolder}/{name}.asset");
        return settings;
    }

    private static List<LogSettingsIssue> Problems(List<LogSettingsIssue> issues)
        => issues.Where(issue => issue.Level != LogSettingsIssueLevel.Info).ToList();

    [Test]
    public void Check1_DefaultMissing_WarnsWithCreateFix()
    {
        var issues = LogSettingsChecker.CheckAssets(null, new List<LogSettings>());

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Level, Is.EqualTo(LogSettingsIssueLevel.Warning));
        Assert.That(issues[0].Fix, Is.Not.Null);
    }

    [Test]
    public void Check_DefaultOnly_NoProblem()
    {
        var defaultSettings = CreateTemp("Default");

        var issues = LogSettingsChecker.CheckAssets(defaultSettings, new[] { defaultSettings });

        Assert.That(Problems(issues), Is.Empty);
    }

    [Test]
    public void Check2_UnlinkedOtherSettings_WarnsAndLinkFixes()
    {
        var defaultSettings = CreateTemp("Default");
        var other = CreateTemp("Other");
        var all = new[] { defaultSettings, other };

        var issues = Problems(LogSettingsChecker.CheckAssets(defaultSettings, all));

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Level, Is.EqualTo(LogSettingsIssueLevel.Warning));
        Assert.That(issues[0].Message, Does.Contain("Other.asset"));

        issues[0].Fix();

        Assert.That(defaultSettings.projectOverride, Is.SameAs(other));
        Assert.That(Problems(LogSettingsChecker.CheckAssets(defaultSettings, all)), Is.Empty);
    }

    [Test]
    public void Check3_ChainedOverride_Warns()
    {
        var defaultSettings = CreateTemp("Default");
        var projectOverride = CreateTemp("Override");
        var chained = CreateTemp("Chained");
        defaultSettings.projectOverride = projectOverride;
        projectOverride.projectOverride = chained;

        var issues = Problems(LogSettingsChecker.CheckAssets(defaultSettings, new[] { defaultSettings, projectOverride }));

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Message, Does.Contain("chained"));
        Assert.That(defaultSettings.ResolveEffective(), Is.SameAs(projectOverride));   // 不遞迴
    }

    [TestCase(0, LogSettingsIssueLevel.Error)]
    [TestCase(-5, LogSettingsIssueLevel.Error)]
    [TestCase(LogSettingsChecker.MaxRecommendedCapacity + 1, LogSettingsIssueLevel.Warning)]
    public void Check4_Capacity_OutOfRange(int capacity, LogSettingsIssueLevel expected)
    {
        var defaultSettings = CreateTemp("Default");
        defaultSettings.window.unityCapacity = capacity;

        var issues = Problems(LogSettingsChecker.CheckAssets(defaultSettings, new[] { defaultSettings }));

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Level, Is.EqualTo(expected));
        Assert.That(issues[0].Message, Does.Contain("unityCapacity"));
    }

    [Test]
    public void Check4_Capacity_ChecksEffectiveOverride()
    {
        var defaultSettings = CreateTemp("Default");
        var projectOverride = CreateTemp("Override");
        defaultSettings.projectOverride = projectOverride;
        defaultSettings.window.logCapacity = 0;      // 不生效的檔案不檢查
        projectOverride.window.logCapacity = 0;

        var issues = Problems(LogSettingsChecker.CheckAssets(defaultSettings, new[] { defaultSettings, projectOverride }));

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Message, Does.Contain("Override.asset"));
    }

    [Test]
    public void DefaultAssetPath_IsLibraryResourcesPath()
    {
        Assert.That(LogSettingsAssets.DefaultAssetPath,
            Is.EqualTo(LogSettingsAssets.LibraryRoot + "/Resources/" + LogSettings.ResourcesPath + ".asset"));
        var atPath = AssetDatabase.LoadAssetAtPath<LogSettings>(LogSettingsAssets.DefaultAssetPath);
        Assert.That(atPath, Is.Not.Null, "Default settings asset should exist (committed with the library).");
        Assert.That(Resources.Load<LogSettings>(LogSettings.ResourcesPath), Is.SameAs(atPath));
    }

    [Test]
    public void IsInLibrary_DetectsLibraryFolder()
    {
        Assert.That(LogSettingsAssets.IsInLibrary(LogSettingsAssets.LibraryRoot + "/Settings/A.asset"), Is.True);
        Assert.That(LogSettingsAssets.IsInLibrary("Assets/Game/A.asset"), Is.False);
        Assert.That(LogSettingsAssets.IsInLibrary(LogSettingsAssets.LibraryRoot + "Other/A.asset"), Is.False);
    }
    // ---- Development Alert (Project Settings 頁面) ----

    private static DevelopmentAlertLevel AlertLevelOf(SerializedObject serialized)
        => (DevelopmentAlertLevel)serialized.FindProperty(LogSettingsAssets.DevelopmentAlertLevelPath).enumValueIndex;

    [Test]
    public void DevelopmentAlert_PageEditsEffectiveFile_WithOverride()
    {
        var defaultSettings = CreateTemp("Default");
        var projectOverride = CreateTemp("Override");
        defaultSettings.projectOverride = projectOverride;

        using (var serialized = LogSettingsAssets.CreateDevelopmentAlertObject(defaultSettings))
        {
            Assert.That(serialized.targetObject, Is.SameAs(projectOverride));
            Assert.That(AlertLevelOf(serialized), Is.EqualTo(DevelopmentAlertLevel.Warning), "預設值為 Warning");
            serialized.FindProperty(LogSettingsAssets.DevelopmentAlertLevelPath).enumValueIndex = (int)DevelopmentAlertLevel.Error;
            serialized.ApplyModifiedProperties();
        }

        Assert.That(projectOverride.DevelopmentAlert.level, Is.EqualTo(DevelopmentAlertLevel.Error));
        Assert.That(defaultSettings.DevelopmentAlert.level, Is.EqualTo(DevelopmentAlertLevel.Warning));
    }

    [Test]
    public void DevelopmentAlert_PageEditsDefaultFile_WithoutOverride()
    {
        var defaultSettings = CreateTemp("Default");

        using (var serialized = LogSettingsAssets.CreateDevelopmentAlertObject(defaultSettings))
            Assert.That(serialized.targetObject, Is.SameAs(defaultSettings));
    }

    [Test]
    public void DevelopmentAlert_EditSupportsUndo()
    {
        var defaultSettings = CreateTemp("Default");
        using (var serialized = LogSettingsAssets.CreateDevelopmentAlertObject(defaultSettings))
        {
            serialized.FindProperty(LogSettingsAssets.DevelopmentAlertLevelPath).enumValueIndex = (int)DevelopmentAlertLevel.Off;
            serialized.ApplyModifiedProperties();
        }
        Assert.That(defaultSettings.DevelopmentAlert.level, Is.EqualTo(DevelopmentAlertLevel.Off));

        Undo.PerformUndo();

        Assert.That(defaultSettings.DevelopmentAlert.level, Is.EqualTo(DevelopmentAlertLevel.Warning));
    }

    // 預設 Inspector 以 HideInInspector 隱藏; 自訂的 LogSettingsInspector 只畫 projectOverride / log / output / window
    [Test]
    public void DevelopmentAlert_HiddenInInspector()
    {
        var field = typeof(LogSettings).GetField("developmentAlert",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        Assert.That(System.Attribute.IsDefined(field, typeof(HideInInspector)), Is.True);
        Assert.That(System.Attribute.IsDefined(field, typeof(SerializeField)), Is.True);
    }

    // ---- 覆蓋檔遺失 (Missing) ----

    [Test]
    public void OverrideMissing_DeletedFile_Warns()
    {
        var defaultSettings = CreateTemp("Default");
        var projectOverride = CreateTemp("Override");
        LogSettingsAssets.LinkOverride(defaultSettings, projectOverride);
        Assert.That(LogSettingsChecker.IsOverrideMissing(defaultSettings), Is.False);

        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(projectOverride));

        Assert.That(LogSettingsChecker.IsOverrideMissing(defaultSettings), Is.True);
        var problems = Problems(LogSettingsChecker.CheckAssets(defaultSettings, new List<LogSettings> { defaultSettings }));
        Assert.That(problems.Count, Is.EqualTo(1));
        Assert.That(problems[0].Level, Is.EqualTo(LogSettingsIssueLevel.Warning));
        Assert.That(problems[0].Message, Does.Contain("Missing"));
        Assert.That(defaultSettings.ResolveEffective(), Is.SameAs(defaultSettings), "建置版改用預設檔");
    }

    [Test]
    public void OverrideMissing_NoLink_False()
    {
        var defaultSettings = CreateTemp("Default");
        Assert.That(LogSettingsChecker.IsOverrideMissing(defaultSettings), Is.False);
    }

    [Test]
    public void MaxRecommendedCapacity_SharedWithRuntime()
        => Assert.That(LogSettingsChecker.MaxRecommendedCapacity, Is.EqualTo(DebugWindowOptions.MaxRecommendedCapacity));
}
