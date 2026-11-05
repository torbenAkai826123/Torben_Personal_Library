using UnityEditor;
using UnityEngine;

namespace Torben.Editor
{
    /// <summary>
    /// LogSettings 的 Inspector: 上方為 Save Current State 按鈕; projectOverride 只在預設檔 (或覆蓋檔誤設時) 顯示.
    /// log 不折疊, 直接列在最上方, output.startWithWindow 畫在 log.isDebugLog 下方; output、window 為折疊區塊.
    /// window 的位置與尺寸類欄位集中在 Layout 標題下: useDefaultPosition 開啟時隱藏 position;
    /// scale 以 Auto Scale 開關 (存 0) 與拉桿 (MinScale ~ MaxScale) 編輯. 欄位以 SerializedObject 繪製, 支援 Undo.
    /// </summary>
    [CustomEditor(typeof(LogSettings))]
    public sealed class LogSettingsInspector : UnityEditor.Editor
    {
        // Inspector 拉桿的範圍; 執行中的 DebugWindow.Scale 不受此限制
        private const float MinScale = 0.1f;
        private const float MaxScale = 3f;

        // window 中集中到 Layout 標題下的欄位 (依此順序繪製)
        private static readonly string[] layoutFields =
        {
            nameof(DebugWindowOptions.isDraggable),
            nameof(DebugWindowOptions.scale),
            nameof(DebugWindowOptions.useDefaultPosition),
            nameof(DebugWindowOptions.position),
            nameof(DebugWindowOptions.size),
        };

        private static readonly string[] outputSkipped = { nameof(DebugLogsOptions.startWithWindow) };

        public override void OnInspectorGUI()
        {
            var settings = (LogSettings)target;
            serializedObject.Update();
            DrawCurrentState(settings);
            DrawOverride(settings);
            var log = serializedObject.FindProperty(nameof(LogSettings.log));
            var output = serializedObject.FindProperty(nameof(LogSettings.output));
            var window = serializedObject.FindProperty(nameof(LogSettings.window));
            DrawLog(log, output);
            if (Foldout(output)) DrawChildren(output, 1, outputSkipped);
            if (Foldout(window))
            {
                DrawChildren(window, 1, layoutFields);
                DrawLayout(window);
            }
            serializedObject.ApplyModifiedProperties();
        }

        // log 不折疊: isPrintLog、isDebugLog、output.startWithWindow, 其餘 log 欄位 (stackTrace) 接在後面
        private static void DrawLog(SerializedProperty log, SerializedProperty output)
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(log.FindPropertyRelative(nameof(LogOptions.isPrintLog)));
            EditorGUILayout.PropertyField(log.FindPropertyRelative(nameof(LogOptions.isDebugLog)));
            EditorGUILayout.PropertyField(output.FindPropertyRelative(nameof(DebugLogsOptions.startWithWindow)));
            DrawChildren(log, 0, nameof(LogOptions.isPrintLog), nameof(LogOptions.isDebugLog));
        }

        // 折疊標題 (點文字也能展開); 展開時回傳 true, 呼叫端在縮排內繪製子欄位
        private static bool Foldout(SerializedProperty property)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName, true);
            return property.isExpanded;
        }

        // 依宣告順序繪製 parent 的直接子欄位, 略過 skipped; indent 為額外縮排的層數
        private static void DrawChildren(SerializedProperty parent, int indent, params string[] skipped)
        {
            using (new EditorGUI.IndentLevelScope(indent))
            {
                var child = parent.Copy();
                var end = parent.GetEndProperty();
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    if (System.Array.IndexOf(skipped, child.name) >= 0) continue;
                    EditorGUILayout.PropertyField(child, true);
                }
            }
        }

        // window 的 Layout 區塊: 預設位置開啟時隱藏 position; scale 為 Auto Scale 開關 + 拉桿
        private static void DrawLayout(SerializedProperty window)
        {
            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(window.FindPropertyRelative(nameof(DebugWindowOptions.isDraggable)));
                DrawScale(window.FindPropertyRelative(nameof(DebugWindowOptions.scale)));
                var useDefault = window.FindPropertyRelative(nameof(DebugWindowOptions.useDefaultPosition));
                EditorGUILayout.PropertyField(useDefault);
                if (useDefault.hasMultipleDifferentValues || !useDefault.boolValue)
                    EditorGUILayout.PropertyField(window.FindPropertyRelative(nameof(DebugWindowOptions.position)));
                EditorGUILayout.PropertyField(window.FindPropertyRelative(nameof(DebugWindowOptions.size)));
            }
        }

        // scale: 0 為依畫面高度自動 (Auto Scale 勾起); 取消勾選時從 1 開始, 以拉桿在 MinScale ~ MaxScale 間調整
        private static void DrawScale(SerializedProperty scale)
        {
            bool isAuto = scale.floatValue <= 0f;
            EditorGUI.BeginChangeCheck();
            isAuto = EditorGUILayout.Toggle(new GUIContent("Auto Scale", "Scale with screen height (720 px = 1x, minimum 1x)."), isAuto);
            if (EditorGUI.EndChangeCheck()) scale.floatValue = isAuto ? 0f : 1f;
            if (isAuto) return;
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(new GUIContent("Scale", scale.tooltip), scale.floatValue, MinScale, MaxScale);
            if (EditorGUI.EndChangeCheck()) scale.floatValue = value;
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
