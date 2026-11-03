using UnityEditor;
using UnityEngine;

namespace Torben.Editor
{
    /// <summary>
    /// LogSettings 的 Inspector: 在預設的折疊欄位上方加入 Save Current State 按鈕;
    /// projectOverride 只在預設檔 (或覆蓋檔誤設時) 顯示. 欄位以 SerializedObject 繪製, 支援 Undo.
    /// </summary>
    [CustomEditor(typeof(LogSettings))]
    public sealed class LogSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (LogSettings)target;
            serializedObject.Update();
            DrawCurrentState(settings);
            DrawOverride(settings);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(LogSettings.log)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(LogSettings.output)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(LogSettings.window)), true);
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawCurrentState(LogSettings settings)
        {
            bool isEffective = settings == LogSettingsAssets.LoadEffective();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !isEffective))
                {
                    var label = new GUIContent("Save Current State", "Play mode only: overwrite this file with the current Log / DebugLogs / DebugWindow state.");
                    if (GUILayout.Button(label, GUILayout.ExpandWidth(false)) && LogSettingsAssets.SaveCurrentState(settings))
                        GUIUtility.ExitGUI();
                }
            }
            if (!isEffective)
                EditorGUILayout.HelpBox("This file is not in effect (it is neither the default file nor its project override).", MessageType.Info);
        }

        private void DrawOverride(LogSettings settings)
        {
            bool isDefault = settings == LogSettingsAssets.LoadDefault();
            if (!isDefault && settings.projectOverride == null) return;   // 覆蓋檔不使用 projectOverride
            if (!isDefault)
                EditorGUILayout.HelpBox("projectOverride is only used in the default file; it is ignored here (chained overrides are not supported).", MessageType.Warning);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(LogSettings.projectOverride)));
        }
    }

    /// <summary>
    /// LogLevelColor 的欄位: 等級 + 是否自訂顏色 + Color picker. 不自訂時存空字串 (使用預設文字顏色);
    /// 自訂時存 "#RRGGBB" (透明度小於 1 時為 "#RRGGBBAA").
    /// </summary>
    [CustomPropertyDrawer(typeof(LogLevelColor))]
    public sealed class LogLevelColorDrawer : PropertyDrawer
    {
        private const float LevelWidth = 90f;
        private const float ToggleWidth = 18f;
        private const float Gap = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var level = property.FindPropertyRelative(nameof(LogLevelColor.level));
            var color = property.FindPropertyRelative(nameof(LogLevelColor.color));
            EditorGUI.BeginProperty(position, label, property);

            var rect = new Rect(position.x, position.y, LevelWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(rect, level, GUIContent.none);

            rect.x += LevelWidth + Gap;
            rect.width = ToggleWidth;
            bool hasColor = !string.IsNullOrEmpty(color.stringValue);
            bool isCustom = EditorGUI.Toggle(rect, new GUIContent("", "Custom color (off: default text color)"), hasColor);

            rect.x += ToggleWidth + Gap;
            rect.width = position.xMax - rect.x;
            if (isCustom)
            {
                var current = TryParse(color.stringValue, out var parsed) ? parsed : Color.white;
                EditorGUI.BeginChangeCheck();
                var next = EditorGUI.ColorField(rect, GUIContent.none, current, true, true, false);
                if (EditorGUI.EndChangeCheck() || !hasColor) color.stringValue = ToHex(next);
            }
            else
            {
                if (hasColor) color.stringValue = string.Empty;
                EditorGUI.LabelField(rect, "Default", EditorStyles.miniLabel);
            }
            EditorGUI.EndProperty();
        }

        /// <summary>顏色轉為 "#RRGGBB"; 透明度小於 1 時為 "#RRGGBBAA".</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string ToHex(Color color)
            => "#" + (color.a < 1f ? ColorUtility.ToHtmlStringRGBA(color) : ColorUtility.ToHtmlStringRGB(color));

        /// <summary>解析 "#RRGGBB" / "#RRGGBBAA" 等 HTML 色碼; 空字串或無法解析時回傳 false.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool TryParse(string hex, out Color color)
        {
            color = Color.white;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out color);
        }
    }
}
