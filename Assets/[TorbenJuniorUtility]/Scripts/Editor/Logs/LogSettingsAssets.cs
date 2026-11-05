using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Torben.Editor
{
    /// <summary>
    /// LogSettings 的寫入操作 (建立預設檔、建立覆蓋檔、儲存目前狀態、切換 TORBEN_LOG_DEBUG).
    /// 一律由使用者透過按鈕或選單觸發, 不在載入或匯入期間自動建立檔案.
    /// </summary>
    public static class LogSettingsAssets
    {
        /// <summary>函式庫的根資料夾.</summary>
        public const string LibraryRoot = "Assets/[TorbenJuniorUtility]";

        /// <summary>預設檔的 asset 路徑 (函式庫的 Resources 資料夾 + LogSettings.ResourcesPath).</summary>
        public const string DefaultAssetPath = LibraryRoot + "/Resources/" + LogSettings.ResourcesPath + ".asset";

        /// <summary>目前的預設檔; 不存在時為 null.</summary>
        /// <remarks>配置: 零配置 (第一次載入時由 Unity 配置).</remarks>
        public static LogSettings LoadDefault() => Resources.Load<LogSettings>(LogSettings.ResourcesPath);

        /// <summary>目前生效的檔案 (有覆蓋檔時為覆蓋檔); 預設檔不存在時為 null.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static LogSettings LoadEffective()
        {
            var defaultSettings = LoadDefault();
            return defaultSettings != null ? defaultSettings.ResolveEffective() : null;
        }

        /// <summary>在 DefaultAssetPath 建立預設檔 (內容為欄位初始值); 已存在時不覆蓋, 直接回傳現有的檔案.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static LogSettings CreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LogSettings>(DefaultAssetPath);
            if (existing != null) return existing;
            EnsureFolder(System.IO.Path.GetDirectoryName(DefaultAssetPath).Replace('\\', '/'));
            var settings = ScriptableObject.CreateInstance<LogSettings>();
            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            Undo.RegisterCreatedObjectUndo(settings, "Create Log Settings");
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// 讓使用者選擇位置, 複製預設檔的內容建立覆蓋檔, 並設為預設檔的 projectOverride.
        /// 預設檔不存在、取消選擇或在函式庫資料夾內選擇後取消時回傳 null.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static LogSettings CreateProjectOverride()
        {
            var defaultSettings = LoadDefault();
            if (defaultSettings == null)
            {
                EditorUtility.DisplayDialog("Create Project Override", "Create the default settings first.", "OK");
                return null;
            }
            string path = EditorUtility.SaveFilePanelInProject("Create Project Override", "LogSettings.Project", "asset",
                "Choose where to save the project's LogSettings (outside the library folder).");
            if (string.IsNullOrEmpty(path)) return null;
            if (IsInLibrary(path) && !EditorUtility.DisplayDialog("Create Project Override",
                    $"'{path}' is inside the library folder ({LibraryRoot}). The override should belong to the game project, or it may be overwritten when the library is updated.",
                    "Create Anyway", "Cancel"))
                return null;

            var created = Object.Instantiate(defaultSettings);
            created.projectOverride = null;     // 覆蓋檔的 projectOverride 不使用
            AssetDatabase.CreateAsset(created, path);
            Undo.RegisterCreatedObjectUndo(created, "Create Log Settings Override");
            LinkOverride(defaultSettings, created);
            return created;
        }

        /// <summary>path 是否在函式庫資料夾內.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool IsInLibrary(string path)
            => path != null && path.Replace('\\', '/').StartsWith(LibraryRoot + "/", System.StringComparison.Ordinal);

        /// <summary>把 defaultSettings 的 projectOverride 設為 target (支援 Undo) 並存檔.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static void LinkOverride(LogSettings defaultSettings, LogSettings target)
        {
            Undo.RecordObject(defaultSettings, "Link Log Settings Override");
            defaultSettings.projectOverride = target;
            EditorUtility.SetDirty(defaultSettings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>清除 settings 的 projectOverride (支援 Undo) 並存檔.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static void ClearOverride(LogSettings settings)
        {
            Undo.RecordObject(settings, "Clear Log Settings Override");
            settings.projectOverride = null;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 讀取 Log / DebugLogs / DebugWindow 目前的狀態, 確認後寫入目前生效的檔案 (有覆蓋檔時寫覆蓋檔, 否則寫預設檔).
        /// 寫入了回傳 true. 預設檔不存在或使用者取消時回傳 false.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static bool SaveCurrentState()
        {
            var defaultSettings = LoadDefault();
            if (defaultSettings == null)
            {
                EditorUtility.DisplayDialog("Save Current State as Settings", "Create the default settings first.", "OK");
                return false;
            }
            return SaveCurrentState(defaultSettings.ResolveEffective());
        }

        /// <summary>同 SaveCurrentState(), 但寫入指定的檔案 (Inspector 的 Save 按鈕使用). 使用者取消時回傳 false.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static bool SaveCurrentState(LogSettings target)
        {
            string path = AssetDatabase.GetAssetPath(target);
            if (!EditorUtility.DisplayDialog("Save Current State as Settings",
                    $"Overwrite the settings in '{path}' with the current Log state?", "Save", "Cancel"))
                return false;

            Undo.RecordObject(target, "Save Current Log State");
            target.log = LogOptions.Capture();
            target.output = DebugLogsOptions.Capture();
            target.window = DebugWindowOptions.Capture();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return true;
        }

        /// <summary>Development Alert 等級在 LogSettings 中的序列化路徑 (欄位為 private, 以 SerializedObject 編輯).</summary>
        public const string DevelopmentAlertLevelPath = "developmentAlert.level";

        /// <summary>
        /// Project Settings 頁面編輯 Development Alert 用的 SerializedObject: 對象為 defaultSettings 生效中的檔案
        /// (有覆蓋檔時為覆蓋檔), 與 Save Current State 的規則相同. 以 ApplyModifiedProperties 寫入時支援 Undo.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static SerializedObject CreateDevelopmentAlertObject(LogSettings defaultSettings)
            => new SerializedObject(defaultSettings.ResolveEffective());

        /// <summary>目前建置平台.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static NamedBuildTarget ActiveBuildTarget
            => NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        /// <summary>目前建置平台的 Player Settings 是否定義 TORBEN_LOG_DEBUG (建置版保留 Log.Debug).</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static bool IsKeepDebugLogInBuilds => LogSettingsChecker.IsLogDebugDefined(ActiveBuildTarget);

        /// <summary>在目前建置平台的 Player Settings 加入或移除 TORBEN_LOG_DEBUG; 有變更時 Unity 會重新編譯.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static void SetKeepDebugLogInBuilds(bool isKept)
        {
            var target = ActiveBuildTarget;
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            string next = ScriptingDefineUtility.Set(current, ScriptingDefineUtility.LogDebugSymbol, isKept);
            if (next != ScriptingDefineUtility.Normalize(current)) PlayerSettings.SetScriptingDefineSymbols(target, next);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
