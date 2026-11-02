using UnityEngine;

namespace Torben
{
    /// <summary>
    /// 在遊戲畫面上顯示最近的 Log 訊息與 Unity 引擎訊息 (IMGUI 視窗).
    /// 由 DebugLogs.isGUILog 開關; 元件掛在跨場景的 CrossObject 上, 第一次開啟時才建立.
    /// 設定 (大小、位置、篩選、顏色...) 都是靜態的, 視窗建立前就可以先設定.
    /// </summary>
    public class DebugWindow : MonoBehaviour
    {
        private const int Capacity = 200;   // Log 與 Unity 訊息各保留的筆數
        private const int WindowId = 0x4C4F47;
        private const float Margin = 10f;
        private const float TitleBarHeight = 18f;
        private const float ReferenceHeight = 720f;   // 自動縮放時, 畫面高 720 為 1 倍

        private static readonly string[] levelNames = System.Enum.GetNames(typeof(LogLevel));
        private static readonly int levelCount = levelNames.Length;

        // ---- 靜態狀態 ----
        private static LogHistorySink history;
        private static LogHistorySink unityHistory;
        private static DebugWindow current;
        private static bool wantVisible;
        private static bool sceneLoaded;    // AfterSceneLoad 之前不建立 GameObject

        // ---- 靜態設定 ----
        private static Vector2 size;
        private static Vector2 position;
        private static bool hasCustomPosition;  // false = 預設右下角
        private static bool draggable;
        private static bool autoScroll;
        private static float scale;             // 0 = 依畫面高度自動
        private static string searchText;
        private static bool[] levelVisible;
        private static bool showLogMessages;
        private static bool showUnityMessages;
        private static string[] levelColors;
        private static int filterVersion;       // 篩選條件改變時遞增
        private static int styleVersion;        // 顏色改變時遞增 (需要重組字串)

        // ---- 元件狀態 ----
        private readonly Source logSource = new Source();
        private readonly Source unitySource = new Source();
        private readonly int[] visible = new int[Capacity * 2]; // 小於 Capacity 為 logSource 的索引, 否則為 unitySource 的索引 + Capacity
        private int visibleCount;
        private int seenFilterVersion = -1;
        private int seenStyleVersion = -1;
        private Vector2 scroll;
        private GUI.WindowFunction drawWindow;
        private GUIStyle lineStyle;
        private GUIContent[] levelToggleLabels;
        private GUIContent logSourceLabel;
        private GUIContent unitySourceLabel;
        private GUIContent clearLabel;
        private GUIContent closeLabel;
        private bool dragging;
        private bool clearArmed;    // 清除狀態: 第一次點 Clear 進入, 再點一次才真的清除
        private bool clearPressed;  // 清除狀態下按住 Clear 中

        // 確保尚未進入 Play (未經 DebugLogs.Install) 時存取設定也有預設值
        static DebugWindow() => ResetStatics();

        // ==== 公開設定 ====

        /// <summary>Log 訊息的歷史紀錄 (最多 200 筆). 啟動時建立, 視窗未開啟也會持續記錄. 視窗的 Clear 按鈕只清除這份紀錄.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static LogHistorySink History => history;

        /// <summary>Unity 引擎訊息的歷史紀錄 (最多 200 筆), 由 DebugLogs.isUnityLog 控制是否收集. 視窗的 Clear 按鈕不會清除.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static LogHistorySink UnityHistory => unityHistory;

        /// <summary>視窗大小 (縮放前的單位). 預設 600 x 180, 最小 80 x 60.
        /// 開啟視窗時, 寬或高超過畫面的那一邊會改為畫面該方向的一半.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static Vector2 Size
        {
            get => size;
            set => size = new Vector2(Mathf.Max(80f, value.x), Mathf.Max(60f, value.y));
        }

        /// <summary>視窗左上角位置 (縮放前的單位). 設定後不再自動貼齊右下角; 拖曳也會更新此值.
        /// 開啟視窗與拖曳結束時, 超出畫面的部分會被拉回畫面邊緣.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static Vector2 Position
        {
            get => hasCustomPosition ? position : DefaultPosition(EffectiveScale());
            set
            {
                position = value;
                hasCustomPosition = true;
            }
        }

        /// <summary>恢復預設位置 (右下角).</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static void ResetPosition() => hasCustomPosition = false;

        /// <summary>是否可以拖曳標題列移動視窗. 預設開啟.</summary>
        public static bool IsDraggable
        {
            get => draggable;
            set => draggable = value;
        }

        /// <summary>有新訊息時是否自動捲到最下方. 預設開啟.</summary>
        public static bool AutoScroll
        {
            get => autoScroll;
            set => autoScroll = value;
        }

        /// <summary>介面縮放倍率; 0 表示依畫面高度自動計算 (畫面高 720 為 1 倍, 最小 1 倍). 預設 0.</summary>
        public static float Scale
        {
            get => scale;
            set => scale = Mathf.Max(0f, value);
        }

        /// <summary>搜尋文字 (比對分類標籤與訊息, 不分大小寫); 空字串表示不篩選.</summary>
        /// <remarks>配置: 零配置; 比對在篩選條件或訊息改變時才執行.</remarks>
        public static string SearchText
        {
            get => searchText;
            set
            {
                value ??= string.Empty;
                if (value == searchText) return;
                searchText = value;
                filterVersion++;
            }
        }

        /// <summary>是否顯示 Log 訊息 (經過 Torben.Log 的訊息). 預設開啟; 視窗上方 Log 開關同步此設定.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool ShowLogMessages
        {
            get => showLogMessages;
            set
            {
                if (showLogMessages == value) return;
                showLogMessages = value;
                filterVersion++;
            }
        }

        /// <summary>是否顯示 Unity 引擎訊息. 預設開啟; 視窗上方 Unity 開關同步此設定.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool ShowUnityMessages
        {
            get => showUnityMessages;
            set
            {
                if (showUnityMessages == value) return;
                showUnityMessages = value;
                filterVersion++;
            }
        }

        /// <summary>是否顯示某個等級的訊息. 預設全部顯示.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool IsLevelVisible(LogLevel level) => levelVisible[LevelIndex(level)];

        /// <summary>設定是否顯示某個等級的訊息.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static void SetLevelVisible(LogLevel level, bool isVisible)
        {
            int index = LevelIndex(level);
            if (levelVisible[index] == isVisible) return;
            levelVisible[index] = isVisible;
            filterVersion++;
        }

        /// <summary>某個等級訊息文字的顏色 (例如 "#FF6B6B"); null 或空字串表示使用預設文字顏色.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static string GetLevelColor(LogLevel level) => levelColors[LevelIndex(level)];

        /// <summary>設定某個等級訊息文字的顏色. 分類標籤的顏色由各 LogCategory 的 tagColorHex 決定.</summary>
        /// <remarks>配置: 零配置; 下次繪製時重組所有顯示字串 (每次變更配置一次).</remarks>
        public static void SetLevelColor(LogLevel level, string levelColorHex)
        {
            levelColors[LevelIndex(level)] = levelColorHex;
            styleVersion++;
        }

        // ==== 安裝與顯示 (由 DebugLogs 呼叫) ====

        // 重設所有靜態狀態並建立新的歷史紀錄 (Log 與 Unity 各一份). 在 SubsystemRegistration 階段由 DebugLogs.Install 呼叫.
        internal static LogHistorySink ResetStatics()
        {
            history = new LogHistorySink(Capacity);
            unityHistory = new LogHistorySink(Capacity);
            current = null;
            wantVisible = false;
            sceneLoaded = false;

            size = new Vector2(600f, 180f);
            position = Vector2.zero;
            hasCustomPosition = false;
            draggable = true;
            autoScroll = true;
            scale = 0f;
            searchText = string.Empty;
            levelVisible = new bool[levelCount];
            for (int i = 0; i < levelCount; i++) levelVisible[i] = true;
            showLogMessages = true;
            showUnityMessages = true;
            levelColors = new string[levelCount];
            levelColors[(int)LogLevel.Warning] = "#FFD54F";
            levelColors[(int)LogLevel.Error] = "#FF6B6B";
            levelColors[(int)LogLevel.Debug] = "#9E9E9E";
            filterVersion = 0;
            styleVersion = 0;
            return history;
        }

        // 顯示 / 隱藏. 隱藏時設 enabled = false, OnGUI 完全不會被呼叫.
        internal static void SetVisible(bool isVisible)
        {
            wantVisible = isVisible;
            if (isVisible)
            {
                if (!sceneLoaded) return;   // 等 AfterSceneLoad 再建立
                EnsureInstance();
                FitToScreen();
                current.enabled = true;
            }
            else if (current != null)
            {
                current.enabled = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            sceneLoaded = true;
            if (wantVisible) SetVisible(true);
        }

        // 掛到 CrossObject 上, 跟著它跨場景存在, 也跟著它一起釋放
        private static void EnsureInstance()
        {
            if (current != null) return;
            var host = CrossObject.Instance.gameObject;
            current = host.GetComponent<DebugWindow>();
            if (current == null) current = host.AddComponent<DebugWindow>();
        }

        // ==== 元件生命週期 ====

        private void Awake()
        {
            drawWindow = DrawWindow;    // 先快取委派, 避免每幀配置
            levelToggleLabels = new GUIContent[levelCount];
            for (int i = 0; i < levelCount; i++)
                levelToggleLabels[i] = new GUIContent(levelNames[i].Substring(0, 1), levelNames[i]);
            logSourceLabel = new GUIContent("Log", "Log messages");
            unitySourceLabel = new GUIContent("Unity", "Unity engine messages");
            clearLabel = new GUIContent("Clear", "Clear Log messages (Unity messages are kept)");
            closeLabel = new GUIContent("X", "Close");
        }

        private void OnDestroy()
        {
            // CrossObject 被銷毀時一併清掉參照; 下次開啟會重新建立
            if (current == this) current = null;
        }

        private void OnGUI()
        {
            if (history == null || unityHistory == null) return;
            if (lineStyle == null)
                lineStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, margin = new RectOffset(2, 2, 0, 0) };

            float s = EffectiveScale();
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            var rect = new Rect(Position, size);
            var moved = GUI.Window(WindowId, rect, drawWindow, "Log");
            if (draggable && moved.position != rect.position)
            {
                Position = moved.position;
                dragging = true;
            }
            // 拖曳結束 (hotControl 釋放) 後的第一個事件才檢查邊界, 不每幀檢查.
            // 視窗的位移會晚一個事件才由 GUI.Window 回傳, 在放開的當下檢查會被這筆位移蓋掉.
            if (dragging && GUIUtility.hotControl == 0)
            {
                dragging = false;
                FitToScreen();
            }

            GUI.matrix = oldMatrix;
        }

        private void DrawWindow(int id)
        {
            // 只在 Layout 事件更新資料, 讓 Layout 與 Repaint 畫出的控制項數量一致
            var e = Event.current;
            if (e.type == EventType.Layout) Refresh();

            // 先記下按下位置: 其他控制項處理後會把事件標成 Used, 到 Clear 時就讀不到了
            bool isMouseDown = e.type == EventType.MouseDown && new Rect(Vector2.zero, size).Contains(e.mousePosition);
            Vector2 mouseDownPosition = e.mousePosition;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < levelCount; i++)
            {
                bool isOn = GUILayout.Toggle(levelVisible[i], levelToggleLabels[i]);
                if (isOn != levelVisible[i]) SetLevelVisible((LogLevel)i, isOn);
            }
            ShowLogMessages = GUILayout.Toggle(showLogMessages, logSourceLabel);
            ShowUnityMessages = GUILayout.Toggle(showUnityMessages, unitySourceLabel);
            GUILayout.FlexibleSpace();
            DrawClearButton(isMouseDown, mouseDownPosition);
            if (GUILayout.Button(closeLabel)) DebugLogs.isGUILog = false;   // 與關閉 isGUILog 相同, 一併恢復 Console 輸出
            GUILayout.EndHorizontal();

            SearchText = GUILayout.TextField(searchText);

            scroll = GUILayout.BeginScrollView(scroll);
            for (int i = 0; i < visibleCount; i++)
            {
                int index = visible[i];
                GUILayout.Label(index < Capacity ? logSource.Lines[index] : unitySource.Lines[index - Capacity], lineStyle);
            }
            GUILayout.EndScrollView();

            if (draggable) GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleBarHeight));
        }

        // 第一次點擊進入清除狀態 (黃色), 清除狀態下再點才清除 (按住時紅色, 放開後恢復原色);
        // 在視窗其他位置按下滑鼠就取消清除狀態
        private void DrawClearButton(bool isMouseDown, Vector2 mouseDownPosition)
        {
            var rect = GUILayoutUtility.GetRect(clearLabel, GUI.skin.button);
            if (isMouseDown)
            {
                if (rect.Contains(mouseDownPosition)) clearPressed = clearArmed;
                else clearArmed = clearPressed = false;
            }
            else if (GUIUtility.hotControl == 0)
            {
                clearPressed = false;   // 在按鈕外放開, 沒有觸發點擊
            }

            var oldColor = GUI.backgroundColor;
            if (clearArmed) GUI.backgroundColor = clearPressed ? Color.red : Color.yellow;
            if (GUI.Button(rect, clearLabel))
            {
                if (clearArmed)
                {
                    history.Clear();    // 只清 Log 訊息, Unity 訊息保留
                    scroll = Vector2.zero;
                }
                clearArmed = !clearArmed;
                clearPressed = false;
            }
            GUI.backgroundColor = oldColor;
        }

        // 有新訊息時只組新進來的字串; 顏色改變時全部重組; 篩選條件或內容改變時重算可見清單
        private void Refresh()
        {
            bool restyle = seenStyleVersion != styleVersion;
            // 用 | 而非 ||, 兩份紀錄都要更新
            bool linesAdded = logSource.Refresh(history, restyle, out bool logChanged)
                            | unitySource.Refresh(unityHistory, restyle, out bool unityChanged);
            seenStyleVersion = styleVersion;
            if (logChanged || unityChanged) seenFilterVersion = -1;

            if (seenFilterVersion != filterVersion)
            {
                RebuildVisible();
                seenFilterVersion = filterVersion;
            }

            if (linesAdded && autoScroll) scroll.y = float.MaxValue;
        }

        // 依流水號合併兩份紀錄 (各自已是舊到新), 再套用來源、等級與搜尋篩選
        private void RebuildVisible()
        {
            visibleCount = 0;
            int logCount = showLogMessages ? logSource.Count : 0;
            int unityCount = showUnityMessages ? unitySource.Count : 0;
            int i = 0, j = 0;
            while (i < logCount || j < unityCount)
            {
                if (j >= unityCount || (i < logCount && logSource.Sequences[i] < unitySource.Sequences[j]))
                {
                    if (IsVisible(logSource.Snapshot[i])) visible[visibleCount++] = i;
                    i++;
                }
                else
                {
                    if (IsVisible(unitySource.Snapshot[j])) visible[visibleCount++] = Capacity + j;
                    j++;
                }
            }
        }

        private static bool IsVisible(in LogEntry entry)
        {
            if (!levelVisible[LevelIndex(entry.Level)]) return false;
            if (searchText.Length == 0) return true;
            return Contains(entry.Message, searchText) || Contains(entry.Category?.Tag, searchText);
        }

        private static string Format(in LogEntry entry)
        {
            string tag = entry.Category?.Tag ?? string.Empty;
            string tagColor = entry.Category?.tagColorHex;
            if (tag.Length > 0 && !string.IsNullOrEmpty(tagColor)) tag = $"<color={tagColor}>{tag}</color>";
            string levelColor = levelColors[LevelIndex(entry.Level)];
            return string.IsNullOrEmpty(levelColor) ? tag + entry.Message : $"{tag}<color={levelColor}>{entry.Message}</color>";
        }

        private static bool Contains(string text, string value) =>
            text != null && text.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static int LevelIndex(LogLevel level)
        {
            int index = (int)level;
            return index >= 0 && index < levelCount ? index : 0;
        }

        // 依畫面高度縮放, 讓視窗佔畫面的比例固定, 不受螢幕實體 DPI 與解析度設定影響
        private static float EffectiveScale()
        {
            if (scale > 0f) return scale;
            return Mathf.Max(1f, Screen.height / ReferenceHeight);
        }

        private static Vector2 DefaultPosition(float s) =>
            new Vector2(Mathf.Max(0f, Screen.width / s - size.x - Margin), Mathf.Max(0f, Screen.height / s - size.y - Margin));

        // 一份歷史紀錄的快照與已格式化字串
        private sealed class Source
        {
            public readonly LogEntry[] Snapshot = new LogEntry[Capacity];
            public readonly long[] Sequences = new long[Capacity];
            public readonly string[] Lines = new string[Capacity];
            public int Count;
            private int seenVersion;
            private bool hasSnapshot;

            // 有新訊息時只組新進來的字串, 舊字串直接搬移; restyle 時全部重組.
            // changed: 快照是否更新; 回傳值: 是否有新增或清除 (用於自動捲動)
            public bool Refresh(LogHistorySink history, bool restyle, out bool changed)
            {
                changed = false;
                if (hasSnapshot && history.Version == seenVersion && !restyle) return false;

                int oldCount = Count;
                Count = history.CopyTo(Snapshot, Sequences, out int version);
                int added = unchecked(version - seenVersion);

                if (!hasSnapshot || restyle || added < 0 || added >= Count)
                {
                    for (int i = 0; i < Count; i++) Lines[i] = Format(Snapshot[i]);
                }
                else
                {
                    // 舊字串往前移, 只組新進來的
                    int overlap = Count - added;
                    int shiftFrom = oldCount - overlap;
                    if (shiftFrom > 0) System.Array.Copy(Lines, shiftFrom, Lines, 0, overlap);
                    for (int i = overlap; i < Count; i++) Lines[i] = Format(Snapshot[i]);
                }

                bool linesAdded = added != 0 || !hasSnapshot;
                hasSnapshot = true;
                seenVersion = version;
                changed = true;
                return linesAdded;
            }
        }

        // 開啟時與拖曳結束時呼叫: 寬或高超過畫面就改為畫面該方向的一半; 位置超出畫面就拉回邊緣
        private static void FitToScreen()
        {
            float s = EffectiveScale();
            float screenWidth = Screen.width / s;
            float screenHeight = Screen.height / s;
            if (size.x > screenWidth) size.x = screenWidth * 0.5f;
            if (size.y > screenHeight) size.y = screenHeight * 0.5f;
            if (!hasCustomPosition) return;    // 預設位置 (右下角) 每次繪製都依畫面重新計算
            position.x = Mathf.Clamp(position.x, 0f, screenWidth - size.x);
            position.y = Mathf.Clamp(position.y, 0f, screenHeight - size.y);
        }
    }

}
