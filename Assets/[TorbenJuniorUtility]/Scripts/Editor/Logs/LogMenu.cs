using UnityEditor;
using UnityEngine;

namespace Torben.Editor
{
    // TorbenTools 選單: 常用開關放在 TorbenTools/ 同層, 其餘放 TorbenTools/Log/
    internal static class LogMenu
    {
        private const string Root = "TorbenTools/";
        private const string GuiWindowItem = Root + "Log GUI Window";
        private const string SettingsItem = Root + "Log/Settings...";
        private const string KeepDebugLogItem = Root + "Log/Keep Debug Log in Builds";
        private const string SaveStateItem = Root + "Log/Save Current State as Settings";
        private const string CreateOverrideItem = Root + "Log/Create Project Override";
        private const string CheckItem = Root + "Log/Check Settings";

        // Log GUI Window 排在 Log/ 子選單前面
        private const int GuiWindowPriority = 0;
        private const int LogPriority = 100;

        [MenuItem(GuiWindowItem, false, GuiWindowPriority)]
        private static void ToggleGuiWindow() => DebugLogs.isGUILog = !DebugLogs.isGUILog;

        [MenuItem(GuiWindowItem, true)]
        private static bool ValidateGuiWindow()
        {
            Menu.SetChecked(GuiWindowItem, EditorApplication.isPlaying && DebugLogs.isGUILog);
            return EditorApplication.isPlaying;
        }

        [MenuItem(SettingsItem, false, LogPriority)]
        private static void OpenSettings() => SettingsService.OpenProjectSettings(LogSettingsProvider.SettingsPath);

        [MenuItem(KeepDebugLogItem, false, LogPriority + 1)]
        private static void ToggleKeepDebugLog() => LogSettingsAssets.SetKeepDebugLogInBuilds(!LogSettingsAssets.IsKeepDebugLogInBuilds);

        [MenuItem(KeepDebugLogItem, true)]
        private static bool ValidateKeepDebugLog()
        {
            Menu.SetChecked(KeepDebugLogItem, LogSettingsAssets.IsKeepDebugLogInBuilds);
            return true;
        }

        [MenuItem(SaveStateItem, false, LogPriority + 2)]
        private static void SaveState() => LogSettingsAssets.SaveCurrentState();

        [MenuItem(SaveStateItem, true)]
        private static bool ValidateSaveState() => EditorApplication.isPlaying;

        [MenuItem(CreateOverrideItem, false, LogPriority + 3)]
        private static void CreateOverride() => LogSettingsAssets.CreateProjectOverride();

        [MenuItem(CheckItem, false, LogPriority + 4)]
        private static void Check()
        {
            var issues = LogSettingsChecker.Run();
            foreach (var issue in issues)
            {
                string text = "[Log Settings] " + issue;
                switch (issue.Level)
                {
                    case LogSettingsIssueLevel.Error: Debug.LogError(text); break;
                    case LogSettingsIssueLevel.Warning: Debug.LogWarning(text); break;
                    default: Debug.Log(text); break;
                }
            }
            if (!LogSettingsChecker.HasProblem(issues)) Debug.Log("[Log Settings] No problems found.");
        }
    }
}
