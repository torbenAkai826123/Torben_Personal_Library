using System.Collections.Generic;
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
        private const float FoldWidth = 18f;          // 每筆左側展開按鈕的寬度
        private const float BadgeHeight = 16f;        // 合併次數膠囊的高度
        private const float FoldHeight = 14f;         // 展開按鈕的高度
        private static readonly Color separatorColor = new Color(1f, 1f, 1f, 0.12f);   // 列分隔線
        private const float DetailOffset = 2f;        // 第二行與訊息之間多留的距離

        private static readonly string[] levelNames = System.Enum.GetNames(typeof(LogLevel));
        private static readonly int levelCount = levelNames.Length;
        private static readonly string[] countNumberTexts = new string[Capacity * 2 + 1];  // "n" 快取

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
        private static bool showTime;
        private static bool showLocation;
        private static bool collapse;
        private static int filterVersion;       // 篩選條件改變時遞增
        private static int styleVersion;        // 顏色改變時遞增 (需要重組字串)

        // ---- 元件狀態 ----
        private readonly Source logSource = new Source();
        private readonly Source unitySource = new Source();
        private readonly int[] visible = new int[Capacity * 2]; // 小於 Capacity 為 logSource 的索引, 否則為 unitySource 的索引 + Capacity
        private int visibleCount;
        // 合併相同訊息用; 索引規則同 visible
        private readonly int[] order = new int[Capacity * 2];               // 篩選後依先後排序的索引
        private readonly int[] groupOf = new int[Capacity * 2];             // 每筆所屬的群組
        private readonly int[] previousOccurrence = new int[Capacity * 2];  // 同群組上一次出現的索引, -1 為沒有
        private readonly int[] groupLast = new int[Capacity * 2];           // 群組最後一次出現的索引
        private readonly int[] groupCount = new int[Capacity * 2];
        private readonly Dictionary<GroupKey, int> groupIds = new Dictionary<GroupKey, int>(Capacity * 2);
        // 展開狀態: 以流水號記錄, 新訊息進來或篩選改變時不會跑掉
        private readonly HashSet<long> expanded = new HashSet<long>();
        private readonly bool[] rowExpanded = new bool[Capacity * 2];
        private int pendingToggleRow = -1;  // 按下展開按鈕的列, 下一次 Layout 才套用, 避免同一事件內控制項數量改變
        private bool rowsCollapsed;         // 上次重算可見清單時的合併設定
        private bool drawTime;              // 本次 Layout 的顯示設定
        private bool drawLocation;
        private bool seenAutoScroll;        // 上次 Layout 時的自動捲動設定, 由關轉開時立即捲到最下方
        private int numberDigits;           // 行號欄寬目前容納的位數
        private string projectRoot;         // Editor 中為專案根目錄, 用來縮短路徑; 建置版為 null
        private int seenFilterVersion = -1;
        private int seenStyleVersion = -1;
        private Vector2 scroll;
        private GUI.WindowFunction drawWindow;
        private GUIStyle lineStyle;
        private GUIStyle detailStyle;       // 第二行與展開內容: 比主訊息小一級
        private GUIStyle inlineDetailStyle;
        private GUIStyle linkStyle;
        private GUIStyle foldStyle;
        private GUIStyle numberStyle;
        private GUIStyle badgeStyle;        // 合併次數的膠囊標籤
        private Texture2D badgeTexture;
        private GUIContent[] levelToggleLabels;
        private GUIContent logSourceLabel;
        private GUIContent unitySourceLabel;
        private GUIContent clearLabel;
        private GUIContent closeLabel;
        private GUIContent timeLabel;
        private GUIContent locationLabel;
        private GUIContent collapseLabel;
        private GUIContent autoScrollLabel;
        private GUIContent expandRowLabel;
        private GUIContent collapseRowLabel;
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

        /// <summary>有新訊息時是否自動捲到最下方; 由關轉開時立即捲到最下方. 預設開啟; 視窗的 Auto 開關同步此設定.</summary>
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

        /// <summary>是否在訊息下方第二行的開頭顯示時間 (當地時間 HH:mm:ss.ff). 預設關閉; 視窗的 Time 開關同步此設定.</summary>
        /// <remarks>配置: 零配置; 每筆第一次顯示時配置字串並快取.</remarks>
        public static bool ShowTime
        {
            get => showTime;
            set => showTime = value;
        }

        /// <summary>
        /// 是否在訊息下方第二行顯示發生位置 (檔案:行號). Log 訊息為呼叫端位置; Unity 訊息取堆疊中第一個有原始碼位置的框架.
        /// Editor 中點擊位置會開啟該檔案; 建置版只顯示. 預設關閉; 視窗的 Loc 開關同步此設定.
        /// </summary>
        /// <remarks>配置: 零配置; 每筆第一次顯示時配置字串並快取.</remarks>
        public static bool ShowLocation
        {
            get => showLocation;
            set => showLocation = value;
        }

        /// <summary>
        /// 是否合併相同訊息 (等級、分類、內容與呼叫端位置都相同). 合併後顯示在第一次出現的位置 (沿用其流水號), 最右側以膠囊顯示次數,
        /// 不顯示時間. 只合併仍在紀錄中的訊息 (Log 與 Unity 各最新 200 筆). 預設關閉; 視窗的 Collapse 開關同步此設定.
        /// </summary>
        /// <remarks>配置: 零配置; 內容改變時重新分組 (群組表擴充時才配置).</remarks>
        public static bool Collapse
        {
            get => collapse;
            set
            {
                if (collapse == value) return;
                collapse = value;
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
            showTime = false;
            showLocation = false;
            collapse = false;
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
            timeLabel = new GUIContent("Time", "Show time");
            locationLabel = new GUIContent("Loc", "Show source location");
            collapseLabel = new GUIContent("Collapse", "Collapse identical messages");
            autoScrollLabel = new GUIContent("Auto", "Auto scroll to newest");
            expandRowLabel = new GUIContent("+", "Expand");
            collapseRowLabel = new GUIContent("-", "Fold");
#if UNITY_EDITOR
            projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
#endif
        }

        private void OnDestroy()
        {
            if (badgeTexture != null) Destroy(badgeTexture);
            // CrossObject 被銷毀時一併清掉參照; 下次開啟會重新建立
            if (current == this) current = null;
        }

        private void OnGUI()
        {
            if (history == null || unityHistory == null) return;
            if (lineStyle == null) CreateStyles();

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

            GUILayout.BeginHorizontal();
            SearchText = GUILayout.TextField(searchText);
            ShowTime = GUILayout.Toggle(showTime, timeLabel, GUILayout.ExpandWidth(false));
            ShowLocation = GUILayout.Toggle(showLocation, locationLabel, GUILayout.ExpandWidth(false));
            Collapse = GUILayout.Toggle(collapse, collapseLabel, GUILayout.ExpandWidth(false));
            autoScroll = GUILayout.Toggle(autoScroll, autoScrollLabel, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            for (int row = 0; row < visibleCount; row++) DrawRow(row);
            GUILayout.EndScrollView();

            if (draggable) GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleBarHeight));
        }

        private void CreateStyles()
        {
            lineStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, margin = new RectOffset(2, 2, 0, 0) };
            // 字型大小 0 表示用字型預設大小; 第二行比主訊息小一級 (2 點)
            int baseSize = lineStyle.fontSize > 0 ? lineStyle.fontSize
                : GUI.skin.font != null && GUI.skin.font.fontSize > 0 ? GUI.skin.font.fontSize : 13;
            int detailSize = Mathf.Max(8, baseSize - 2);
            detailStyle = new GUIStyle(lineStyle) { richText = false, fontSize = detailSize };
            detailStyle.normal.textColor = new Color(0.72f, 0.72f, 0.72f);
            inlineDetailStyle = new GUIStyle(detailStyle) { wordWrap = false };
            linkStyle = new GUIStyle(inlineDetailStyle);
            linkStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);
            linkStyle.hover.textColor = new Color(0.7f, 0.85f, 1f);
            // 第一行左右的小元件 (行號、展開按鈕、次數膠囊) 都以訊息第一行文字的中線置中
            float textCenter = lineStyle.padding.top + lineStyle.lineHeight * 0.5f;
            foldStyle = new GUIStyle(GUI.skin.button) { fontSize = detailSize, padding = new RectOffset(0, 0, 0, 0), fixedHeight = FoldHeight };
            foldStyle.margin = new RectOffset(2, 2, CenterOffset(textCenter, FoldHeight), 0);
            numberStyle = new GUIStyle(inlineDetailStyle);
            numberStyle.margin = new RectOffset(2, 0, CenterOffset(textCenter, numberStyle.padding.top * 2f + numberStyle.lineHeight), 0);
            numberDigits = 0;   // 行號欄寬在 Refresh 中依目前最大的流水號計算

            badgeTexture = CreateCapsuleTexture(BadgeHeight);
            int radius = (int)(BadgeHeight / 2f);
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = detailSize,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(radius, radius, radius, radius),
                padding = new RectOffset(radius - 2, radius - 2, 0, 0),
                margin = new RectOffset(4, 2, CenterOffset(textCenter, BadgeHeight), 0),
                fixedHeight = BadgeHeight,
                wordWrap = false,
            };
            badgeStyle.normal.background = badgeTexture;
            badgeStyle.normal.textColor = Color.white;
        }

        // 高度 height 的元件要以 center 置中時的上邊距
        private static int CenterOffset(float center, float height) => Mathf.Max(0, Mathf.RoundToInt(center - height * 0.5f));

        // 左右半圓的白底圓形, 以 border 九宮格拉伸成膠囊; 顏色在像素中直接給定 (灰色半透明)
        private static Texture2D CreateCapsuleTexture(float height)
        {
            int size = Mathf.CeilToInt(height);
            float radius = size / 2f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);   // 邊緣 1px 反鋸齒
                    pixels[y * size + x] = new Color32(110, 110, 110, (byte)(alpha * 220f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 一筆 (或合併後的一組) 訊息: 第一行為行號 + 展開按鈕 + 訊息 (+ 合併次數); 第二行為時間與位置; 展開時顯示堆疊追蹤
        private void DrawRow(int row)
        {
            if (row > 0) DrawSeparator();
            int index = visible[row];
            var source = SourceOf(index, out int i);
            var stackTrace = source.Snapshot[i].StackTrace;
            int count = rowsCollapsed ? groupCount[groupOf[index]] : 1;

            GUILayout.BeginHorizontal();
            GUILayout.Label(source.GetNumber(i), numberStyle);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && stackTrace != null;   // 沒有堆疊追蹤時呈現無法使用
            if (GUILayout.Button(rowExpanded[row] ? collapseRowLabel : expandRowLabel, foldStyle, GUILayout.Width(FoldWidth)))
                pendingToggleRow = row;
            GUI.enabled = oldEnabled;
            GUILayout.Label(source.Lines[i], lineStyle);
            if (count > 1) GUILayout.Label(CountNumberText(count), badgeStyle, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            // 合併多次的訊息不顯示時間 (每次出現的時間不同, 只顯示一個沒有意義)
            bool rowTime = drawTime && count == 1;
            if (rowTime || drawLocation)
            {
                string location = drawLocation ? source.GetLocation(i, projectRoot) : string.Empty;
                GUILayout.Space(DetailOffset);
                GUILayout.BeginHorizontal();
                GUILayout.Space(numberStyle.fixedWidth + FoldWidth + 6f);
                if (rowTime) GUILayout.Label(source.GetTime(i), inlineDetailStyle, GUILayout.ExpandWidth(false));
                if (location.Length > 0) DrawLocation(source, i, location);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (rowExpanded[row] && stackTrace != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(numberStyle.fixedWidth + FoldWidth + 6f);
                GUILayout.Label(stackTrace.Text, detailStyle);
                GUILayout.EndHorizontal();
            }
        }

        // 列與列之間 1px 的分隔線, 也當對齊的參考線
        private static void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            var oldColor = GUI.color;
            GUI.color = separatorColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawLocation(Source source, int i, string location)
        {
#if UNITY_EDITOR
            if (GUILayout.Button(location, linkStyle, GUILayout.ExpandWidth(false)))
                OpenLocation(source.Snapshot[i], source.LocationFiles[i], source.LocationLines[i]);
#else
            GUILayout.Label(location, inlineDetailStyle, GUILayout.ExpandWidth(false));
#endif
        }

#if UNITY_EDITOR
        // 專案內的檔案用 AssetDatabase 開啟 (與 Console 相同的編輯器設定); 找不到資源時直接開啟檔案路徑
        private static void OpenLocation(in LogEntry entry, string file, int line)
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(file);
            if (asset != null)
            {
                UnityEditor.AssetDatabase.OpenAsset(asset, line);
                return;
            }
            string fullPath = entry.HasCaller ? entry.CallerFilePath : System.IO.Path.GetFullPath(file);
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, line);
        }
#endif

        private Source SourceOf(int index, out int i)
        {
            if (index < Capacity)
            {
                i = index;
                return logSource;
            }
            i = index - Capacity;
            return unitySource;
        }

        private long SequenceOf(int index) => SourceOf(index, out int i).Sequences[i];

        // "n" (合併次數)
        private static string CountNumberText(int count)
        {
            if (count >= countNumberTexts.Length) return count.ToString();
            return countNumberTexts[count] ??= count.ToString();
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
            // 在快照更新前套用, 列的索引才會對應到上次畫出的內容
            bool expandChanged = pendingToggleRow >= 0;
            if (expandChanged)
            {
                ToggleRow(pendingToggleRow);
                pendingToggleRow = -1;
            }

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
                expandChanged = true;
            }
            if (expandChanged) UpdateRowExpanded();
            drawTime = showTime;
            drawLocation = showLocation;

            if (linesAdded) UpdateNumberWidth();
            if (autoScroll && (linesAdded || !seenAutoScroll)) scroll.y = float.MaxValue;
            seenAutoScroll = autoScroll;
        }

        // 行號欄寬固定, 讓訊息左緣對齊; 依目前最大的流水號計算, 位數增加時才重算 (最少 3 位)
        private void UpdateNumberWidth()
        {
            long max = 0;
            if (logSource.Count > 0) max = logSource.Sequences[logSource.Count - 1];
            if (unitySource.Count > 0) max = System.Math.Max(max, unitySource.Sequences[unitySource.Count - 1]);
            int digits = 3;
            for (long n = max; n >= 1000; n /= 10) digits++;
            if (digits <= numberDigits) return;
            numberDigits = digits;
            numberStyle.fixedWidth = numberStyle.CalcSize(new GUIContent("[" + new string('9', digits) + "]")).x;
        }

        // 展開: 記下這一列的流水號. 收合: 合併時同一組每次出現的流水號都移除
        private void ToggleRow(int row)
        {
            if (row >= visibleCount) return;
            int index = visible[row];
            if (!rowExpanded[row])
            {
                expanded.Add(SequenceOf(index));
                return;
            }
            for (int k = FirstToWalk(index); k >= 0; k = rowsCollapsed ? previousOccurrence[k] : -1)
                expanded.Remove(SequenceOf(k));
        }

        // 合併時, 同一組任何一次出現被展開就算展開
        private void UpdateRowExpanded()
        {
            for (int row = 0; row < visibleCount; row++)
            {
                bool isExpanded = false;
                for (int k = FirstToWalk(visible[row]); k >= 0 && !isExpanded; k = rowsCollapsed ? previousOccurrence[k] : -1)
                    isExpanded = expanded.Contains(SequenceOf(k));
                rowExpanded[row] = isExpanded;
            }
        }

        // 走訪同一組每次出現的起點: 合併時從最後一次往前, 否則只有自己
        private int FirstToWalk(int index) => rowsCollapsed ? groupLast[groupOf[index]] : index;

        // 依流水號合併兩份紀錄 (各自已是舊到新), 再套用來源、等級與搜尋篩選.
        // 合併相同訊息時, 每組只留第一次出現的那一列 (同 Unity Console), 並以 previousOccurrence 串起同組的每一次.
        // 行號為流水號 (寫入順序, 不因擠掉舊紀錄或篩選而改變), 合併後的列沿用第一次出現的流水號
        private void RebuildVisible()
        {
            visibleCount = 0;
            rowsCollapsed = collapse;
            groupIds.Clear();
            int groupTotal = 0;
            int orderCount = 0;
            int logCount = showLogMessages ? logSource.Count : 0;
            int unityCount = showUnityMessages ? unitySource.Count : 0;
            int i = 0, j = 0;
            while (i < logCount || j < unityCount)
            {
                int index = j >= unityCount || (i < logCount && logSource.Sequences[i] < unitySource.Sequences[j])
                    ? i++
                    : Capacity + j++;
                var source = SourceOf(index, out int n);
                if (!IsVisible(source.Snapshot[n])) continue;
                if (!rowsCollapsed)
                {
                    visible[visibleCount++] = index;
                    continue;
                }

                var key = new GroupKey(source.Snapshot[n]);
                if (!groupIds.TryGetValue(key, out int group))
                {
                    group = groupTotal++;
                    groupIds.Add(key, group);
                    groupCount[group] = 0;
                    groupLast[group] = -1;
                }
                groupOf[index] = group;
                previousOccurrence[index] = groupLast[group];
                groupLast[group] = index;
                groupCount[group]++;
                order[orderCount++] = index;
            }

            if (rowsCollapsed)
            {
                for (int n = 0; n < orderCount; n++)
                {
                    int index = order[n];
                    if (previousOccurrence[index] < 0) visible[visibleCount++] = index;
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
            // 第二行用的快取, 第一次顯示時才組; null 為尚未計算
            public readonly string[] Times = new string[Capacity];
            public readonly string[] Numbers = new string[Capacity];        // "[流水號]"
            public readonly string[] Locations = new string[Capacity];      // "路徑:行號"; 空字串為沒有位置
            public readonly string[] LocationFiles = new string[Capacity];
            public readonly int[] LocationLines = new int[Capacity];
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
                    ClearDetails(0);
                }
                else
                {
                    // 舊字串往前移, 只組新進來的
                    int overlap = Count - added;
                    int shiftFrom = oldCount - overlap;
                    if (shiftFrom > 0)
                    {
                        System.Array.Copy(Lines, shiftFrom, Lines, 0, overlap);
                        System.Array.Copy(Times, shiftFrom, Times, 0, overlap);
                        System.Array.Copy(Numbers, shiftFrom, Numbers, 0, overlap);
                        System.Array.Copy(Locations, shiftFrom, Locations, 0, overlap);
                        System.Array.Copy(LocationFiles, shiftFrom, LocationFiles, 0, overlap);
                        System.Array.Copy(LocationLines, shiftFrom, LocationLines, 0, overlap);
                    }
                    for (int i = overlap; i < Count; i++) Lines[i] = Format(Snapshot[i]);
                    ClearDetails(overlap);
                }

                bool linesAdded = added != 0 || !hasSnapshot;
                hasSnapshot = true;
                seenVersion = version;
                changed = true;
                return linesAdded;
            }

            // 行號: "[流水號]"
            public string GetNumber(int i) => Numbers[i] ??= "[" + Sequences[i] + "]";

            // 時間: "(HH:mm:ss.ff)"; 沒有時間時為空字串
            public string GetTime(int i)
            {
                var text = Times[i];
                if (text != null) return text;
                var time = Snapshot[i].Time;
                text = time == default ? string.Empty : "(" + LogText.FormatTime(time) + ")";
                Times[i] = text;
                return text;
            }

            // 位置: Log 訊息用呼叫端資訊; 沒有時 (Unity 訊息) 從堆疊文字找第一個有原始碼位置的框架
            public string GetLocation(int i, string projectRoot)
            {
                var text = Locations[i];
                if (text != null) return text;
                ref readonly var entry = ref Snapshot[i];
                string file = null;
                int line = 0;
                if (entry.HasCaller)
                {
                    file = LogText.ToDisplayPath(entry.CallerFilePath, projectRoot);
                    line = entry.CallerLineNumber;
                }
                else if (entry.StackTrace != null && LogText.TryParseUnityLocation(entry.StackTrace.Text, out string path, out line))
                {
                    file = LogText.ToDisplayPath(path, projectRoot);
                }
                text = file == null ? string.Empty : file + ":" + line;
                Locations[i] = text;
                LocationFiles[i] = file;
                LocationLines[i] = line;
                return text;
            }

            private void ClearDetails(int from)
            {
                int length = Capacity - from;
                System.Array.Clear(Times, from, length);
                System.Array.Clear(Numbers, from, length);
                System.Array.Clear(Locations, from, length);
                System.Array.Clear(LocationFiles, from, length);
            }
        }

        // 合併相同訊息的比對條件: 等級、分類 (同一個實例)、內容、呼叫端檔案與行號
        private readonly struct GroupKey : System.IEquatable<GroupKey>
        {
            private readonly LogLevel level;
            private readonly LogCategory category;
            private readonly string message;
            private readonly string file;
            private readonly int line;

            public GroupKey(in LogEntry entry)
            {
                level = entry.Level;
                category = entry.Category;
                message = entry.Message;
                file = entry.CallerFilePath;
                line = entry.CallerLineNumber;
            }

            public bool Equals(GroupKey other) =>
                level == other.level && line == other.line && ReferenceEquals(category, other.category)
                && string.Equals(message, other.message) && string.Equals(file, other.file);

            public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)level;
                    hash = hash * 31 + (message?.GetHashCode() ?? 0);
                    hash = hash * 31 + (file?.GetHashCode() ?? 0);
                    hash = hash * 31 + line;
                    hash = hash * 31 + (category?.GetHashCode() ?? 0);
                    return hash;
                }
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
