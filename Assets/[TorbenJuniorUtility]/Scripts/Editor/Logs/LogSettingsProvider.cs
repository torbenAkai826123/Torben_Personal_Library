using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Torben.Editor
{
    // Project Settings > TorbenTools > Log
    // 每類一個 Header 區塊; 同一列中開關在左、按鈕在右. 設定內容只以表格顯示, 編輯在設定檔的 Inspector 進行;
    // 例外: Development Alert 只在此頁編輯 (Inspector 不顯示)
    internal sealed class LogSettingsProvider : SettingsProvider
    {
        public const string SettingsPath = "Project/TorbenTools/Log";

        private static readonly (string name, NamedBuildTarget target)[] platforms =
        {
            ("Standalone", NamedBuildTarget.Standalone),
            ("Android", NamedBuildTarget.Android),
            ("iOS", NamedBuildTarget.iOS),
        };
        private static readonly LogLevel[] levels = (LogLevel[])System.Enum.GetValues(typeof(LogLevel));

        private List<LogSettingsIssue> issues;
        private LogSettings defaultSettings;
        private Vector2 scroll;
        private GUIStyle rowStyle;
        private SerializedObject alertObject;   // Development Alert 的編輯對象 (生效中的檔案)

        private LogSettingsProvider() : base(SettingsPath, SettingsScope.Project) { }

        [SettingsProvider]
        private static SettingsProvider Create() => new LogSettingsProvider
        {
            keywords = new[] { "Log", "DebugWindow", "TORBEN_LOG_DEBUG", "Override" },
        };

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement) => Refresh();

        // Inspector 修改設定檔、刪除檔案或 Undo 後, 表格與檢查結果跟著更新. 檢查會搜尋資源, 最多每秒一次
        private double nextRefreshTime;

        public override void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextRefreshTime) return;
            nextRefreshTime = EditorApplication.timeSinceStartup + 1.0;
            Refresh();
            Repaint();
        }

        private void Refresh()
        {
            issues = LogSettingsChecker.Run();
            defaultSettings = LogSettingsAssets.LoadDefault();
        }

        public override void OnGUI(string searchContext)
        {
            if (issues == null) Refresh();
            rowStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true };
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawIssues();

            if (defaultSettings == null)
            {
                Header("Files");
                EditorGUILayout.HelpBox($"Default settings file does not exist. Default values are used at runtime.\n{LogSettingsAssets.DefaultAssetPath}", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Create Default Settings File", GUILayout.ExpandWidth(false)))
                    {
                        LogSettingsAssets.CreateDefault();
                        Refresh();
                    }
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            var effective = defaultSettings.ResolveEffective();
            DrawFiles(effective);
            DrawDevelopmentAlert(effective);
            DrawBuild();
            DrawTables(effective);
            EditorGUILayout.EndScrollView();
        }

        private void DrawIssues()
        {
            foreach (var issue in issues)
            {
                if (issue.Level == LogSettingsIssueLevel.Info) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(issue.Message, issue.Level == LogSettingsIssueLevel.Error ? MessageType.Error : MessageType.Warning);
                if (issue.Fix != null && GUILayout.Button(issue.FixLabel, GUILayout.Width(80f), GUILayout.ExpandHeight(true)))
                {
                    issue.Fix();
                    issues = null;          // 修正可能改變檔案, 下一次重畫時重新整理
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // 檔案: 預設檔、覆蓋檔連結與目前生效的檔案; 按鈕靠右
        private void DrawFiles(LogSettings effective)
        {
            Header("Files");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Default", defaultSettings, typeof(LogSettings), false);
            EditorGUI.BeginChangeCheck();
            var projectOverride = (LogSettings)EditorGUILayout.ObjectField("Project Override", defaultSettings.projectOverride, typeof(LogSettings), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (projectOverride == null) LogSettingsAssets.ClearOverride(defaultSettings);
                else if (projectOverride != defaultSettings) LogSettingsAssets.LinkOverride(defaultSettings, projectOverride);
                Refresh();
            }
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Effective", effective, typeof(LogSettings), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (defaultSettings.projectOverride == null && GUILayout.Button("Create Project Override", GUILayout.ExpandWidth(false)))
                {
                    LogSettingsAssets.CreateProjectOverride();
                    Refresh();
                    GUIUtility.ExitGUI();
                }
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent("Save Current State", "Play mode only: overwrite the effective file with the current state."), GUILayout.ExpandWidth(false)))
                    {
                        LogSettingsAssets.SaveCurrentState();
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("Edit in Inspector", GUILayout.ExpandWidth(false)))
                {
                    Selection.activeObject = effective;
                    EditorGUIUtility.PingObject(effective);
                }
            }
        }

        // Development Alert: 編輯生效中的檔案 (有覆蓋檔時為覆蓋檔), 以 SerializedObject 寫入, 支援 Undo
        private void DrawDevelopmentAlert(LogSettings effective)
        {
            Header("Runtime Check");
            if (alertObject == null || alertObject.targetObject != effective)
            {
                alertObject?.Dispose();
                alertObject = LogSettingsAssets.CreateDevelopmentAlertObject(defaultSettings);
            }
            alertObject.Update();
            var level = alertObject.FindProperty(LogSettingsAssets.DevelopmentAlertLevelPath);
            if (level == null) return;
            EditorGUILayout.PropertyField(level, new GUIContent("Development Alert",
                "When the settings have problems at startup (DebugLogs.Install), or development settings are still enabled in a non-development build, report them once with Debug.LogWarning (Warning) or Debug.LogError (Error)."));
            alertObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField("Off disables both settings problem alerts and the reminder for development settings in non-development builds.", EditorStyles.wordWrappedMiniLabel);
        }

        // 建置: 開關在左; 各平台與 Build Profile 的狀態以表格顯示
        private void DrawBuild()
        {
            Header("Build");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                bool isKept = EditorGUILayout.ToggleLeft(
                    new GUIContent($"Keep Debug Log in Builds ({ScriptingDefineUtility.LogDebugSymbol})", "Adds / removes the symbol in Player Settings of the active platform."),
                    LogSettingsAssets.IsKeepDebugLogInBuilds);
                if (EditorGUI.EndChangeCheck())
                {
                    LogSettingsAssets.SetKeepDebugLogInBuilds(isKept);
                    issues = LogSettingsChecker.Run();
                }
                GUILayout.FlexibleSpace();
            }
            foreach (var (name, target) in platforms)
                Row(name, LogSettingsChecker.IsLogDebugDefined(target) ? "defined" : "not defined");
            foreach (var issue in issues)
                if (issue.Level == LogSettingsIssueLevel.Info && issue.Message.StartsWith("Active Build Profile"))
                    EditorGUILayout.HelpBox(issue.Message, MessageType.Info);
        }

        // 目前生效的設定 (唯讀表格, 順序同 Inspector: bool 在上, 其他欄位在下)
        private void DrawTables(LogSettings effective)
        {
            var log = effective.log ?? new LogOptions();
            Header("Log");
            Row("Print Log", OnOff(log.isPrintLog));
            Row("Debug Log", OnOff(log.isDebugLog));
            Row("Stack Trace", Levels(log.stackTrace, false));

            var output = effective.output ?? new DebugLogsOptions();
            Header("Output");
            Row("Open Window On Start", OnOff(output.openWindowOnStart));
            Row("Console With GUI", OnOff(output.isConsoleWithGUI));
            Row("Collect Unity Log", OnOff(output.isUnityLog));

            var window = effective.window ?? new DebugWindowOptions();
            Header("Window");
            Row("Draggable", OnOff(window.isDraggable));
            Row("Auto Scroll", OnOff(window.autoScroll));
            Row("Show Log / Unity Messages", $"{OnOff(window.showLogMessages)} / {OnOff(window.showUnityMessages)}");
            Row("Show Time / Location", $"{OnOff(window.showTime)} / {OnOff(window.showLocation)}");
            Row("Collapse", OnOff(window.collapse));
            Row("Visible Levels", Levels(window.visibleLevels, true));
            Row("Visible Buttons", Buttons(window.visibleButtons));
            Row("Capacity (Log / Unity)", $"{window.logCapacity} / {window.unityCapacity}");
            Row("Size", $"{window.size.x} x {window.size.y}");
            Row("Position", window.useDefaultPosition ? "default (bottom right)" : $"{window.position.x}, {window.position.y}");
            Row("Scale", window.scale > 0f ? window.scale.ToString() : "auto");
            Row("Level Colors", Colors(window.levelColors));
        }

        private static void Header(string text)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private void Row(string label, string value) => EditorGUILayout.LabelField(label, value, rowStyle);

        private static string OnOff(bool value) => value ? "On" : "Off";

        private static string Levels(LogLevelSwitch[] switches, bool fallback)
        {
            var on = new List<string>();
            foreach (var level in levels)
                if (LogLevelSwitch.Find(switches, level, fallback)) on.Add(level.ToString());
            return on.Count == 0 ? "None" : on.Count == levels.Length ? "All" : string.Join(", ", on);
        }

        private static string Buttons(DebugWindowButtons buttons)
        {
            if (buttons == DebugWindowButtons.None) return "None";
            var names = new List<string>();
            var hidden = new List<string>();
            foreach (DebugWindowButtons button in System.Enum.GetValues(typeof(DebugWindowButtons)))
            {
                if (button == DebugWindowButtons.None || button == DebugWindowButtons.All) continue;
                ((buttons & button) != 0 ? names : hidden).Add(button.ToString());
            }
            return hidden.Count == 0 ? "All" : string.Join(", ", names);
        }

        private static string Colors(LogLevelColor[] colors)
        {
            var parts = new List<string>();
            foreach (var level in levels)
            {
                string color = LogLevelColor.Find(colors, level);
                parts.Add($"{level} {(string.IsNullOrEmpty(color) ? "default" : color)}");
            }
            return string.Join(", ", parts);
        }
    }
}
