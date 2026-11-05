using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Torben.Editor
{
    public enum LogSettingsIssueLevel
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>狀態檢查的一個結果; Fix 不為 null 時可用 FixLabel 的按鈕修正.</summary>
    public sealed class LogSettingsIssue
    {
        public readonly LogSettingsIssueLevel Level;
        public readonly string Message;
        public readonly string FixLabel;
        public readonly System.Action Fix;

        public LogSettingsIssue(LogSettingsIssueLevel level, string message, string fixLabel = null, System.Action fix = null)
        {
            Level = level;
            Message = message;
            FixLabel = fixLabel;
            Fix = fix;
        }

        public override string ToString() => $"[{Level}] {Message}";
    }

    /// <summary>
    /// LogSettings 的狀態檢查, 供 Project Settings 頁面、選單與進入 Play 時的摘要共用.
    /// </summary>
    public static class LogSettingsChecker
    {
        /// <summary>容量超過此值時警告. 數值定義在 DebugWindowOptions.MaxRecommendedCapacity (與執行時的 Development Alert 共用).</summary>
        public const int MaxRecommendedCapacity = DebugWindowOptions.MaxRecommendedCapacity;

        private static readonly (string name, NamedBuildTarget target)[] platforms =
        {
            ("Standalone", NamedBuildTarget.Standalone),
            ("Android", NamedBuildTarget.Android),
            ("iOS", NamedBuildTarget.iOS),
        };

        /// <summary>檢查專案中的設定檔與各平台的 define (項目 1～6).</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static List<LogSettingsIssue> Run()
        {
            var issues = CheckAssets(Resources.Load<LogSettings>(LogSettings.ResourcesPath), FindAllSettings());
            CheckDefines(issues);
            return issues;
        }

        /// <summary>
        /// 檢查設定檔 (項目 1～4). defaultSettings 為 Resources 讀到的預設檔 (可為 null), allSettings 為專案中所有 LogSettings.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static List<LogSettingsIssue> CheckAssets(LogSettings defaultSettings, IReadOnlyList<LogSettings> allSettings)
        {
            var issues = new List<LogSettingsIssue>();

            // 1. 預設檔
            if (defaultSettings == null)
            {
                issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Warning,
                    $"Default settings not found at Resources/{LogSettings.ResourcesPath}. Default values are used at runtime.",
                    "Create", () => LogSettingsAssets.CreateDefault()));
                return issues;
            }

            // 2. 有其他設定檔, 但預設檔沒有指向它
            if (allSettings != null)
            {
                foreach (var settings in allSettings)
                {
                    if (settings == null || settings == defaultSettings || settings == defaultSettings.projectOverride) continue;
                    var target = settings;
                    issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Warning,
                        $"'{AssetPathOf(settings)}' is not linked as projectOverride. The link may have been lost when the library was updated.",
                        "Link", () => LogSettingsAssets.LinkOverride(defaultSettings, target)));
                }
            }

            // 覆蓋檔遺失 (項目 3 之前的附加檢查): 原本有連結, 但檔案被刪除 (Missing). 建置版會改用預設檔
            if (IsOverrideMissing(defaultSettings))
            {
                var target = defaultSettings;
                issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Warning,
                    $"'{AssetPathOf(defaultSettings)}' projectOverride is Missing (the linked file was deleted). The default file is used at runtime.",
                    "Clear", () => LogSettingsAssets.ClearOverride(target)));
            }

            // 3. 不支援串接
            var projectOverride = defaultSettings.projectOverride;
            if (projectOverride != null && projectOverride.projectOverride != null)
            {
                issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Warning,
                    $"'{AssetPathOf(projectOverride)}' has its own projectOverride, which is ignored (chained overrides are not supported).",
                    "Clear", () => LogSettingsAssets.ClearOverride(projectOverride)));
            }

            // 4. 容量 (只檢查實際生效的檔案)
            var effective = defaultSettings.ResolveEffective();
            if (effective.window != null)
            {
                CheckCapacity(issues, effective, "logCapacity", effective.window.logCapacity);
                CheckCapacity(issues, effective, "unityCapacity", effective.window.unityCapacity);
            }
            return issues;
        }

        /// <summary>settings 的 projectOverride 是否為 Missing (有連結但檔案已不存在); 沒有連結時為 false.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static bool IsOverrideMissing(LogSettings settings)
        {
            using (var serialized = new SerializedObject(settings))
            {
                var property = serialized.FindProperty(nameof(LogSettings.projectOverride));
                return property.objectReferenceValue == null && property.objectReferenceEntityIdValue != EntityId.None;
            }
        }

        /// <summary>結果中是否有警告或錯誤.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool HasProblem(IReadOnlyList<LogSettingsIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Level != LogSettingsIssueLevel.Info) return true;
            return false;
        }

        /// <summary>專案中所有 LogSettings asset.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static List<LogSettings> FindAllSettings()
        {
            var list = new List<LogSettings>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LogSettings)))
            {
                var settings = AssetDatabase.LoadAssetAtPath<LogSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null) list.Add(settings);
            }
            return list;
        }

        /// <summary>某個平台的 Player Settings 是否定義 TORBEN_LOG_DEBUG.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static bool IsLogDebugDefined(NamedBuildTarget target)
            => ScriptingDefineUtility.Contains(PlayerSettings.GetScriptingDefineSymbols(target), ScriptingDefineUtility.LogDebugSymbol);

        private static void CheckCapacity(List<LogSettingsIssue> issues, LogSettings settings, string field, int capacity)
        {
            if (capacity < 1)
                issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Error,
                    $"'{AssetPathOf(settings)}' window.{field} is {capacity}; it must be at least 1 (1 is used at runtime)."));
            else if (capacity > MaxRecommendedCapacity)
                issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Warning,
                    $"'{AssetPathOf(settings)}' window.{field} is {capacity}; values above {MaxRecommendedCapacity} may slow down the window."));
        }

        // 5. 各平台的 define; 6. 目前 Build Profile 的 define
        private static void CheckDefines(List<LogSettingsIssue> issues)
        {
            var parts = new List<string>();
            foreach (var (name, target) in platforms)
                parts.Add($"{name}: {(IsLogDebugDefined(target) ? "defined" : "not defined")}");
            issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Info,
                $"{ScriptingDefineUtility.LogDebugSymbol} in Player Settings - {string.Join(", ", parts)}."));

            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if (profile == null) return;
            bool inProfile = System.Array.IndexOf(profile.scriptingDefines ?? System.Array.Empty<string>(), ScriptingDefineUtility.LogDebugSymbol) >= 0;
            issues.Add(new LogSettingsIssue(LogSettingsIssueLevel.Info,
                $"Active Build Profile '{profile.name}': {ScriptingDefineUtility.LogDebugSymbol} {(inProfile ? "defined" : "not defined")} in its own defines (not managed by Keep Debug Log in Builds)."));
        }

        private static string AssetPathOf(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? asset.name : path;
        }
    }

    // 進入 Play 時, 有警告以上的項目就輸出一次摘要
    [InitializeOnLoad]
    internal static class LogSettingsPlayModeCheck
    {
        static LogSettingsPlayModeCheck()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            var defaultSettings = Resources.Load<LogSettings>(LogSettings.ResourcesPath);
            if (defaultSettings == null) return;    // 找不到預設檔時 DebugLogs.Install 已警告, 不重複輸出
            var issues = LogSettingsChecker.CheckAssets(defaultSettings, LogSettingsChecker.FindAllSettings());
            if (!LogSettingsChecker.HasProblem(issues)) return;
            var text = new System.Text.StringBuilder("[Log] LogSettings check found problems (Project Settings > TorbenTools > Log):");
            foreach (var issue in issues)
                if (issue.Level != LogSettingsIssueLevel.Info) text.Append('\n').Append(issue);
            Debug.LogWarning(text.ToString());
        }
    }
}
