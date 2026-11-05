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
        private const int WindowId = 0x4C4F47;
        private const float Margin = 10f;
        private const float TitleBarHeight = 18f;
        private const float MinWidth = 150f;          // 標題文字不與圓點重疊的最小寬度
        private const float MinHeight = 60f;
        private const float ReferenceHeight = 720f;   // 自動縮放時, 畫面高 720 為 1 倍
        private const float FoldWidth = 18f;          // 每筆左側展開按鈕的寬度
        private const float BadgeHeight = 16f;        // 合併次數膠囊的高度
        private const float CheckSize = 14f;          // 第二列核取方塊 (Time / Loc / Collapse / Auto) 的方框大小
        private const float FoldHeight = 14f;         // 展開按鈕的高度
        private const float RowGap = 2f;              // 每列左緣與行號、行號與展開按鈕之間的距離
        private const float TextGap = 4f;             // 展開 (或複製) 按鈕與文字之間的距離
        private static readonly Color separatorColor = new Color(1f, 1f, 1f, 0.12f);   // 列分隔線
        private const float DetailOffset = 2f;        // 第二行與訊息之間多留的距離
        private const float CopiedFeedbackSeconds = 1f;   // 按下複製後顯示已複製狀態的時間
        private const string WindowTitle = "Log";
        private const float DotSize = 12f;            // 標題列紅黃綠圓點的直徑
        private const float DotSpacing = 6f;          // 圓點之間的距離
        private const float DotRightMargin = 6f;      // 最右邊的圓點與視窗右緣的距離
        private const float DotRingWidth = 1.5f;      // 綠點關閉時的圓線粗細
        private static readonly Color copiedColor = new Color(0.45f, 0.85f, 0.45f);    // 已複製狀態的按鈕底色
        private static readonly Color closeDotColor = new Color32(255, 95, 87, 255);       // 紅: Close
        private static readonly Color minimizeDotColor = new Color32(254, 188, 46, 255);   // 黃: 縮小 / 還原
        private static readonly Color autoDotColor = new Color32(40, 200, 64, 255);        // 綠: Auto (開: 實心, 關: 圓線)

        private static readonly string[] levelNames = System.Enum.GetNames(typeof(LogLevel));
        private static readonly int levelCount = levelNames.Length;

        // ---- 靜態狀態 ----
        private static LogHistorySink history;
        private static LogHistorySink unityHistory;
        private static int logCapacity;     // 兩份紀錄的容量, 只在 ResetStatics 時決定
        private static int unityCapacity;
        private static DebugWindow current;
        private static bool wantVisible;
        private static bool sceneLoaded;    // AfterSceneLoad 之前不建立 GameObject

        // ---- 靜態設定 ----
        private static Vector2 size;
        private static Vector2 position;
        private static bool hasCustomPosition;  // false = 預設右下角
        private static bool draggable;
        private static bool autoScroll;
        private static bool minimized;          // 縮小成只剩標題列
        private static bool minimizedToBottom;  // 縮小時往下縮 (標題列停在原本視窗的底部); 還原時往上展開
        private static float minimizedHeight = TitleBarHeight + 4f;    // 縮小時的視窗高度; 建立樣式時依視窗背景的邊框修正
        private static float scale;             // 0 = 依畫面高度自動
        private static string searchText;
        private static bool[] levelVisible;
        private static bool showLogMessages;
        private static bool showUnityMessages;
        private static string[] levelColors;
        private static bool showTime;
        private static bool showLocation;
        private static bool collapse;
        private static DebugWindowButtons visibleButtons;
        private static int filterVersion;       // 篩選條件改變時遞增
        private static int styleVersion;        // 顏色改變時遞增 (需要重組字串)

        // ---- 元件狀態 ----
        // 以下陣列依兩份紀錄的容量配置, 只在紀錄換新 (啟動或重新安裝) 時重新配置, 不在每幀配置
        private LogHistorySink boundHistory;        // 目前陣列對應的紀錄
        private LogHistorySink boundUnityHistory;
        private int boundLogCapacity;               // logSource 的容量, 也是 unitySource 索引的偏移量
        private Source logSource;
        private Source unitySource;
        private int[] visible;              // 小於 boundLogCapacity 為 logSource 的索引, 否則為 unitySource 的索引 + boundLogCapacity
        private int visibleCount;
        // 合併相同訊息用; 索引規則同 visible
        private int[] order;                // 篩選後依先後排序的索引
        private int[] groupOf;              // 每筆所屬的群組
        private int[] previousOccurrence;   // 同群組上一次出現的索引, -1 為沒有
        private int[] groupLast;            // 群組最後一次出現的索引
        private int[] groupCount;
        private Dictionary<GroupKey, int> groupIds;
        private string[] countNumberTexts;  // "n" (合併次數) 快取
        // 展開狀態: 以流水號記錄, 新訊息進來或篩選改變時不會跑掉
        private readonly HashSet<long> expanded = new HashSet<long>();
        private bool[] rowExpanded;
        private int pendingToggleRow = -1;  // 按下展開按鈕的列, 下一次 Layout 才套用, 避免同一事件內控制項數量改變
        private bool rowsCollapsed;         // 上次重算可見清單時的合併設定
        private bool drawTime;              // 本次 Layout 的顯示設定
        private bool drawLocation;
        private bool drawCopy;              // 本次 Layout 是否畫複製按鈕
        private bool drawCopiedAll;         // 本次 Layout 時工具列 Copy 是否為已複製狀態
        private long drawCopiedSequence = -1;   // 本次 Layout 時為已複製狀態的列 (流水號); -1 為沒有
        private long copiedSequence = -1;   // 最後一次單筆複製的列 (流水號)
        private float copiedRowUntil;       // 單筆 / 全部複製的已複製狀態結束時間 (Time.realtimeSinceStartup)
        private float copiedAllUntil;
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
        private GUIStyle checkStyle;        // 核取方塊的文字 (左側留出方框位置)
        private Texture2D checkOffTexture;
        private Texture2D checkOnTexture;
        private Texture2D dotTexture;       // 白色實心圓, 以 GUI.color 上色
        private Texture2D ringTexture;      // 白色圓線, 以 GUI.color 上色
        private GUIStyle minimizedTitleStyle;   // 縮小時自己畫的標題文字
        private GUIStyle rowCopyStyle;      // 每列的複製按鈕: 同展開按鈕, 但放在第二行 / 堆疊行時不加上邊距
        private bool pendingMinimizeToggle;
        private bool checksOnFirstRow = true;   // 本次 Layout 時核取方塊是否放在第一列
        private float levelsWidth;              // 工具列元件寬度, 見 MeasureToolbar
        private float sourceWidth;
        private float copyWidth;
        private float clearWidth;
        private float timeWidth;
        private float locationWidth;
        private float collapseWidth; // 按下黃點後, 下一次 Layout 才切換, 避免同一事件內版面改變
        private GUIContent[] levelToggleLabels;
        private GUIContent sourceBothLabel;     // 來源按鈕: 依目前狀態顯示, 點擊輪替 Unity -> Unity Off -> Both
        private GUIContent sourceUnityLabel;
        private GUIContent sourceLogLabel;
        private GUIContent sourceNoneLabel;
        private GUIContent clearLabel;
        private GUIContent copyAllLabel;
        private GUIContent copiedAllLabel;
        private GUIContent copyRowLabel;
        private GUIContent copiedRowLabel;
        private GUIContent timeLabel;
        private GUIContent locationLabel;
        private GUIContent collapseLabel;
        private GUIContent expandRowLabel;
        private GUIContent collapseRowLabel;
        private bool dragging;
        private bool clearArmed;    // 清除狀態: 第一次點 Clear 進入, 再點一次才真的清除
        private bool clearPressed;  // 清除狀態下按住 Clear 中

        // 確保尚未進入 Play (未經 DebugLogs.Install) 時存取設定也有預設值
        static DebugWindow()
        {
            var defaults = new DebugWindowOptions();
            ResetStatics(defaults.logCapacity, defaults.unityCapacity);
        }

        // ==== 公開設定 ====

        /// <summary>Log 訊息的歷史紀錄 (最多 LogCapacity 筆). 啟動時建立, 視窗未開啟也會持續記錄. 視窗的 Clear 按鈕只清除這份紀錄.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static LogHistorySink History => history;

        /// <summary>Unity 引擎訊息的歷史紀錄 (最多 UnityCapacity 筆), 由 DebugLogs.isUnityLog 控制是否收集. 視窗的 Clear 按鈕不會清除.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static LogHistorySink UnityHistory => unityHistory;

        /// <summary>History 的容量; 由 LogSettings 的 window.logCapacity 在啟動時決定, 執行中不能修改.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static int LogCapacity => logCapacity;

        /// <summary>UnityHistory 的容量; 由 LogSettings 的 window.unityCapacity 在啟動時決定, 執行中不能修改.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static int UnityCapacity => unityCapacity;

        /// <summary>視窗大小 (縮放前的單位). 預設值見 DebugWindowOptions 欄位初始值; 最小 150 x 60 (寬度小於 150 時標題文字會與圓點重疊).
        /// 開啟視窗時, 寬或高超過畫面的那一邊會改為畫面該方向的一半.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static Vector2 Size
        {
            get => size;
            set => size = new Vector2(Mathf.Max(MinWidth, value.x), Mathf.Max(MinHeight, value.y));
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

        /// <summary>目前是否使用預設位置 (右下角); 設定 Position 後為 false, ResetPosition 後為 true.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool UsesDefaultPosition => !hasCustomPosition;

        /// <summary>是否可以拖曳標題列移動視窗.</summary>
        public static bool IsDraggable
        {
            get => draggable;
            set => draggable = value;
        }

        /// <summary>有新訊息時是否自動捲到最下方; 由關轉開時立即捲到最下方. 標題列的綠點同步此設定 (開: 實心, 關: 圓線).</summary>
        public static bool AutoScroll
        {
            get => autoScroll;
            set => autoScroll = value;
        }

        /// <summary>
        /// 視窗是否縮小成只剩標題列 (保留紅黃綠圓點, 位置不變, 仍可拖曳). 標題列的黃點切換此設定.
        /// 不存進設定檔; 進入 Play (DebugLogs.Install) 時重設為 false. 縮小時不更新清單內容, 還原後才更新.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool Minimized
        {
            get => minimized;
            set => SetMinimized(value);
        }

        // 縮小時往較近的上下邊縮: 視窗中心在畫面下半部時往下縮 (標題列停在原本視窗的底部), 否則往上縮.
        // 還原時反向展開; 縮小 / 還原後都把超出畫面的部分拉回. 預設位置 (右下角) 依目前高度計算, 不需調整
        private static void SetMinimized(bool value)
        {
            if (value == minimized) return;
            float shift = size.y - minimizedHeight;
            if (value)
            {
                float screenHeight = Screen.height / EffectiveScale();
                minimizedToBottom = Position.y + size.y * 0.5f > screenHeight * 0.5f;
                minimized = true;
                if (hasCustomPosition && minimizedToBottom) position.y += shift;
            }
            else
            {
                minimized = false;
                if (hasCustomPosition && minimizedToBottom) position.y -= shift;
            }
            FitToScreen();
        }

        // 目前畫出的視窗高度 (縮小時只有標題列)
        private static float WindowHeight() => minimized ? minimizedHeight : size.y;

        // 測試用: 目前畫出的視窗高度
        internal static float CurrentHeight => WindowHeight();

        /// <summary>介面縮放倍率; 0 表示依畫面高度自動計算 (畫面高 720 為 1 倍, 最小 1 倍).</summary>
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

        /// <summary>是否顯示 Log 訊息 (經過 Torben.Log 的訊息). 視窗上方的來源按鈕 (Unity / Unity Off / Both) 同步此設定.</summary>
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

        /// <summary>是否顯示 Unity 引擎訊息. 視窗上方的來源按鈕 (Unity / Unity Off / Both) 同步此設定.</summary>
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

        /// <summary>是否在訊息下方第二行的開頭顯示時間 (當地時間 HH:mm:ss.ff). 視窗的 Time 開關同步此設定.</summary>
        /// <remarks>配置: 零配置; 每筆第一次顯示時配置字串並快取.</remarks>
        public static bool ShowTime
        {
            get => showTime;
            set => showTime = value;
        }

        /// <summary>
        /// 是否在訊息下方第二行顯示發生位置 (檔案:行號). Log 訊息為呼叫端位置; Unity 訊息取堆疊中第一個有原始碼位置的框架.
        /// Editor 中點擊位置會開啟該檔案; 建置版只顯示. 視窗的 Loc 開關同步此設定.
        /// </summary>
        /// <remarks>配置: 零配置; 每筆第一次顯示時配置字串並快取.</remarks>
        public static bool ShowLocation
        {
            get => showLocation;
            set => showLocation = value;
        }

        /// <summary>
        /// 是否合併相同訊息 (等級、分類、內容與呼叫端位置都相同). 合併後顯示在第一次出現的位置 (沿用其流水號), 最右側以膠囊顯示次數,
        /// 不顯示時間. 只合併仍在紀錄中的訊息 (Log 與 Unity 各自容量內的最新紀錄). 視窗的 Collapse 開關同步此設定.
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

        /// <summary>是否顯示某個等級的訊息.</summary>
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

        /// <summary>
        /// 視窗上顯示哪些按鈕. 隱藏的只是按鈕, 功能仍可透過對應的靜態屬性控制; 隱藏後版面不留空位.
        /// 標題列的紅 (Close)、黃 (縮小)、綠 (Auto) 圓點固定顯示, 不受此設定控制.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public static DebugWindowButtons VisibleButtons
        {
            get => visibleButtons;
            set => visibleButtons = value;
        }

        // ==== 安裝與顯示 (由 DebugLogs 呼叫) ====

        // 重設所有靜態狀態 (設定恢復 DebugWindowOptions 的欄位初始值) 並以指定容量建立新的歷史紀錄 (Log 與 Unity 各一份).
        // 容量小於 1 時以 1 計. 由 DebugLogs.Install 呼叫.
        internal static LogHistorySink ResetStatics(int newLogCapacity, int newUnityCapacity)
        {
            logCapacity = Mathf.Max(1, newLogCapacity);
            unityCapacity = Mathf.Max(1, newUnityCapacity);
            history = new LogHistorySink(logCapacity);
            unityHistory = new LogHistorySink(unityCapacity);
            current = null;
            wantVisible = false;

            searchText = string.Empty;
            levelVisible = new bool[levelCount];
            levelColors = new string[levelCount];
            filterVersion = 0;
            styleVersion = 0;
            minimized = false;
            minimizedToBottom = false;
            new DebugWindowOptions().Apply();
            return history;
        }

        // 進入 Play 時 (SubsystemRegistration) 由 DebugLogs 呼叫: AfterSceneLoad 之前不建立 GameObject
        internal static void ResetSceneState() => sceneLoaded = false;

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
            BindHistories();
            drawWindow = DrawWindow;    // 先快取委派, 避免每幀配置
            levelToggleLabels = new GUIContent[levelCount];
            for (int i = 0; i < levelCount; i++)
                levelToggleLabels[i] = new GUIContent(levelNames[i].Substring(0, 1), levelNames[i]);
            sourceBothLabel = new GUIContent("Both", "Showing Log and Unity messages (click: Unity only)");
            sourceUnityLabel = new GUIContent("Unity", "Showing Unity messages only (click: hide Unity)");
            sourceLogLabel = new GUIContent("Unity Off", "Unity messages hidden (click: show both)");
            sourceNoneLabel = new GUIContent("None", "Log and Unity messages hidden (click: show both)");
            clearLabel = new GUIContent("Clear", "Clear Log messages (Unity messages are kept)");
            copyAllLabel = new GUIContent("Copy", "Copy all visible messages");
            copiedAllLabel = new GUIContent("Copied", "Copied all visible messages");
            copyRowLabel = new GUIContent("C", "Copy this message");
            copiedRowLabel = new GUIContent("C", "Copied");
            timeLabel = new GUIContent("Time", "Show time");
            locationLabel = new GUIContent("Loc", "Show source location");
            collapseLabel = new GUIContent("Collapse", "Collapse identical messages");
            expandRowLabel = new GUIContent("+", "Expand");
            collapseRowLabel = new GUIContent("-", "Fold");
#if UNITY_EDITOR
            projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
#endif
        }

        private void OnDestroy()
        {
            if (badgeTexture != null) Destroy(badgeTexture);
            if (checkOffTexture != null) Destroy(checkOffTexture);
            if (checkOnTexture != null) Destroy(checkOnTexture);
            if (dotTexture != null) Destroy(dotTexture);
            if (ringTexture != null) Destroy(ringTexture);
            // CrossObject 被銷毀時一併清掉參照; 下次開啟會重新建立
            if (current == this) current = null;
        }

        private void OnGUI()
        {
            if (history == null || unityHistory == null) return;
            if (lineStyle == null) CreateStyles();

            // 縮小 / 還原在 Layout 時才套用, 讓同一事件的 Layout 與其他事件畫出相同的版面
            if (pendingMinimizeToggle && Event.current.type == EventType.Layout)
            {
                pendingMinimizeToggle = false;
                SetMinimized(!minimized);
            }

            float s = EffectiveScale();
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            // 縮小時只留標題列; 位置 (左上角) 不變, 還原後回到原本大小
            var rect = new Rect(Position, new Vector2(size.x, WindowHeight()));
            // 縮小時視窗太矮, 內建的標題文字會被裁掉; 改在 DrawWindow 中自己畫
            var moved = GUI.Window(WindowId, rect, drawWindow, minimized ? string.Empty : WindowTitle);
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
            if (e.type == EventType.Repaint) DrawnCopyButtons = 0;
            // 先記下按下位置: 其他控制項 (含圓點) 處理後會把事件標成 Used, 到 Clear 時就讀不到了
            bool isMouseDown = e.type == EventType.MouseDown && new Rect(Vector2.zero, size).Contains(e.mousePosition);
            Vector2 mouseDownPosition = e.mousePosition;

            if (minimized && e.type == EventType.Repaint)
                GUI.Label(new Rect(0f, 0f, size.x, minimizedHeight), WindowTitle, minimizedTitleStyle);
            // 標題列的圓點固定顯示; 先畫, 按下時事件被用掉, 不會觸發拖曳
            DrawTitleDots();
            if (minimized)
            {
                if (draggable) GUI.DragWindow(new Rect(0f, 0f, 10000f, minimizedHeight));
                return;
            }
            if (e.type == EventType.Layout) Refresh();

            // 第一列: 左邊為按鈕, 右邊為核取方塊; 寬度放不下時核取方塊移到第二列 (靠左); 搜尋框自成最後一列.
            // 隱藏的按鈕不佔位置; 一列沒有東西時整列不畫. 放第幾列只在 Layout 時決定
            var buttons = visibleButtons;
            bool hasLeft = (buttons & LeftButtons) != 0;
            bool hasChecks = (buttons & CheckButtons) != 0;
            if (e.type == EventType.Layout)
                checksOnFirstRow = !hasLeft || LeftWidth(buttons) + ChecksWidth(buttons) <= size.x - GUI.skin.window.padding.horizontal;
            bool checksFirst = hasChecks && checksOnFirstRow;
            if (hasLeft || checksFirst)
            {
                GUILayout.BeginHorizontal();
                if (HasButton(buttons, DebugWindowButtons.Levels))
                {
                    for (int i = 0; i < levelCount; i++)
                    {
                        bool isOn = GUILayout.Toggle(levelVisible[i], levelToggleLabels[i]);
                        if (isOn != levelVisible[i]) SetLevelVisible((LogLevel)i, isOn);
                    }
                }
                if (HasButton(buttons, DebugWindowButtons.Source)) DrawSourceButton();
                if (drawCopy) DrawCopyAllButton();
                if (HasButton(buttons, DebugWindowButtons.Clear)) DrawClearButton(isMouseDown, mouseDownPosition);
                GUILayout.FlexibleSpace();
                if (checksFirst) DrawCheckBoxes(buttons);
                GUILayout.EndHorizontal();
            }
            if (hasChecks && !checksFirst)
            {
                GUILayout.BeginHorizontal();
                DrawCheckBoxes(buttons);    // 換到第二列時靠左
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (HasButton(buttons, DebugWindowButtons.Search)) SearchText = GUILayout.TextField(searchText);

            scroll = GUILayout.BeginScrollView(scroll);
            for (int row = 0; row < visibleCount; row++) DrawRow(row);
            GUILayout.EndScrollView();

            if (draggable) GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleBarHeight));
        }

        private static bool HasButton(DebugWindowButtons buttons, DebugWindowButtons button) => (buttons & button) != 0;

        private const DebugWindowButtons LeftButtons = DebugWindowButtons.Levels | DebugWindowButtons.Source | DebugWindowButtons.Copy | DebugWindowButtons.Clear;
        private const DebugWindowButtons CheckButtons = DebugWindowButtons.Time | DebugWindowButtons.Location | DebugWindowButtons.Collapse;

        private void DrawCheckBoxes(DebugWindowButtons buttons)
        {
            if (HasButton(buttons, DebugWindowButtons.Time)) ShowTime = CheckBox(showTime, timeLabel);
            if (HasButton(buttons, DebugWindowButtons.Location)) ShowLocation = CheckBox(showLocation, locationLabel);
            if (HasButton(buttons, DebugWindowButtons.Collapse)) Collapse = CheckBox(collapse, collapseLabel);
        }

        // 工具列各元件的寬度 (含左右邊距, 略為高估); 建立樣式時量一次. 會變的文字取最寬的那個, 點擊後版面不跳動
        private void MeasureToolbar()
        {
            var toggle = GUI.skin.toggle;
            var button = GUI.skin.button;
            levelsWidth = 0f;
            foreach (var label in levelToggleLabels) levelsWidth += toggle.CalcSize(label).x + toggle.margin.horizontal;
            sourceWidth = Mathf.Max(Mathf.Max(button.CalcSize(sourceBothLabel).x, button.CalcSize(sourceUnityLabel).x),
                                    Mathf.Max(button.CalcSize(sourceLogLabel).x, button.CalcSize(sourceNoneLabel).x)) + button.margin.horizontal;
            copyWidth = Mathf.Max(button.CalcSize(copyAllLabel).x, button.CalcSize(copiedAllLabel).x) + button.margin.horizontal;
            clearWidth = button.CalcSize(clearLabel).x + button.margin.horizontal;
            timeWidth = checkStyle.CalcSize(timeLabel).x + checkStyle.margin.horizontal;
            locationWidth = checkStyle.CalcSize(locationLabel).x + checkStyle.margin.horizontal;
            collapseWidth = checkStyle.CalcSize(collapseLabel).x + checkStyle.margin.horizontal;
        }

        private float LeftWidth(DebugWindowButtons buttons) =>
            (HasButton(buttons, DebugWindowButtons.Levels) ? levelsWidth : 0f)
            + (HasButton(buttons, DebugWindowButtons.Source) ? sourceWidth : 0f)
            + (HasButton(buttons, DebugWindowButtons.Copy) ? copyWidth : 0f)
            + (HasButton(buttons, DebugWindowButtons.Clear) ? clearWidth : 0f);

        private float ChecksWidth(DebugWindowButtons buttons) =>
            (HasButton(buttons, DebugWindowButtons.Time) ? timeWidth : 0f)
            + (HasButton(buttons, DebugWindowButtons.Location) ? locationWidth : 0f)
            + (HasButton(buttons, DebugWindowButtons.Collapse) ? collapseWidth : 0f);

        // 標題列右側的紅 (Close)、黃 (縮小 / 還原)、綠 (Auto) 圓點; 固定顯示, 不受 VisibleButtons 控制
        private void DrawTitleDots()
        {
            float step = DotSize + DotSpacing;
            float x = size.x - DotRightMargin - DotSize - step * 2f;
            float y = (TitleBarHeight - DotSize) * 0.5f;
            if (TitleDot(new Rect(x, y, DotSize, DotSize), closeDotColor, true))
                DebugLogs.isGUILog = false;   // 與關閉 isGUILog 相同, 一併恢復 Console 輸出
            if (TitleDot(new Rect(x + step, y, DotSize, DotSize), minimizeDotColor, true))
                pendingMinimizeToggle = true;
            if (TitleDot(new Rect(x + step * 2f, y, DotSize, DotSize), autoDotColor, autoScroll))
                autoScroll = !autoScroll;
        }

        // 一個圓點按鈕: 點擊範圍比圓點大一圈 (方便觸控); 滑過時稍微變亮. isFilled 為 false 時畫圓線
        private bool TitleDot(Rect dot, Color color, bool isFilled)
        {
            var hit = new Rect(dot.x - DotSpacing * 0.5f, 0f, dot.width + DotSpacing, TitleBarHeight);
            bool clicked = GUI.Button(hit, GUIContent.none, GUIStyle.none);
            if (Event.current.type != EventType.Repaint) return clicked;
            var oldColor = GUI.color;
            GUI.color = hit.Contains(Event.current.mousePosition) ? color * new Color(1.15f, 1.15f, 1.15f, 1f) : color;
            GUI.DrawTexture(dot, isFilled ? dotTexture : ringTexture);
            GUI.color = oldColor;
            return clicked;
        }

        // 依目前的歷史紀錄配置快照與可見清單; 紀錄換新時 (啟動或重新安裝, 容量可能不同) 才重新配置
        private void BindHistories()
        {
            if (boundHistory == history && boundUnityHistory == unityHistory) return;
            boundHistory = history;
            boundUnityHistory = unityHistory;
            boundLogCapacity = logCapacity;
            int total = logCapacity + unityCapacity;
            logSource = new Source(logCapacity);
            unitySource = new Source(unityCapacity);
            visible = new int[total];
            visibleCount = 0;
            order = new int[total];
            groupOf = new int[total];
            previousOccurrence = new int[total];
            groupLast = new int[total];
            groupCount = new int[total];
            groupIds = new Dictionary<GroupKey, int>(total);
            countNumberTexts = new string[total + 1];
            rowExpanded = new bool[total];
            expanded.Clear();
            pendingToggleRow = -1;
            seenFilterVersion = -1;
            seenStyleVersion = -1;
            scroll = Vector2.zero;
        }

        private void CreateStyles()
        {
            // 每列內元件的左邊距為 0, 水平間距一律由 DrawRow / RowLeft 的 GUILayout.Space 指定, 讓各行文字左緣一致
            lineStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, margin = new RectOffset(0, 2, 0, 0) };
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
            foldStyle.margin = new RectOffset(0, 0, CenterOffset(textCenter, FoldHeight), 0);
            rowCopyStyle = new GUIStyle(foldStyle) { margin = new RectOffset(0, 0, 0, 0) };
            numberStyle = new GUIStyle(inlineDetailStyle);
            numberStyle.margin = new RectOffset(0, 0, CenterOffset(textCenter, numberStyle.padding.top * 2f + numberStyle.lineHeight), 0);
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

            minimizedHeight = Mathf.Max(TitleBarHeight + 4f, GUI.skin.window.border.vertical);    // 太矮時視窗背景的九宮格會變形
            minimizedTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = GUI.skin.window.font,
                fontSize = GUI.skin.window.fontSize,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
            };
            minimizedTitleStyle.normal.textColor = GUI.skin.window.normal.textColor;
            // 圓點以 2 倍解析度產生, 視窗放大 2 倍時仍清楚
            dotTexture = CreateDotTexture(DotSize * 2f, 0f);
            ringTexture = CreateDotTexture(DotSize * 2f, DotRingWidth * 2f);
            checkOffTexture = CreateCheckTexture(CheckSize, false);
            checkOnTexture = CreateCheckTexture(CheckSize, true);
            checkStyle = new GUIStyle(GUI.skin.label) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            checkStyle.padding.left = Mathf.CeilToInt(CheckSize) + 4;
            checkStyle.margin = new RectOffset(6, 2, GUI.skin.textField.margin.top, GUI.skin.textField.margin.bottom);
            checkStyle.fixedHeight = Mathf.Max(CheckSize, GUI.skin.textField.CalcHeight(GUIContent.none, 100f));   // 與搜尋框同高
            MeasureToolbar();
        }

        // 方形核取方塊: 左側畫方框 (開啟時打勾), 右側為文字; 點擊方框或文字都會切換
        private bool CheckBox(bool value, GUIContent label)
        {
            var rect = GUILayoutUtility.GetRect(label, checkStyle, GUILayout.ExpandWidth(false));
            bool next = GUI.Toggle(rect, value, GUIContent.none, GUIStyle.none);
            if (Event.current.type != EventType.Repaint) return next;
            var box = new Rect(rect.x, rect.y + (rect.height - CheckSize) * 0.5f, CheckSize, CheckSize);
            var oldColor = GUI.color;
            if (rect.Contains(Event.current.mousePosition)) GUI.color = oldColor * new Color(1.15f, 1.15f, 1.15f, 1f);   // 滑過時稍微變亮
            GUI.DrawTexture(box, next ? checkOnTexture : checkOffTexture);
            GUI.color = oldColor;
            checkStyle.Draw(rect, label, false, false, false, false);
            return next;
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

        // 白色圓形 (以 GUI.color 上色); ringWidth 為 0 時實心, 否則為該粗細的圓線. 邊緣做反鋸齒
        private static Texture2D CreateDotTexture(float diameter, float ringWidth)
        {
            int size = Mathf.CeilToInt(diameter);
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
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (ringWidth > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - (radius - ringWidth) + 0.5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 核取方塊的方框: 1px 邊框; 開啟時藍底白勾 (兩段線段, 依距離做反鋸齒). 顏色在像素中直接給定
        private static Texture2D CreateCheckTexture(float boxSize, bool isOn)
        {
            int size = Mathf.CeilToInt(boxSize);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var border = isOn ? new Color32(74, 144, 226, 255) : new Color32(150, 150, 150, 255);
            var fill = isOn ? new Color32(74, 144, 226, 255) : new Color32(30, 30, 30, 200);
            // 勾的三個點 (以方框大小的比例表示, y 向下)
            var a = new Vector2(0.24f, 0.52f) * size;
            var b = new Vector2(0.43f, 0.72f) * size;
            var c = new Vector2(0.78f, 0.30f) * size;
            float halfWidth = size * 0.08f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    Color color = isBorder ? border : fill;
                    if (isOn && !isBorder)
                    {
                        var p = new Vector2(x + 0.5f, size - y - 0.5f);   // 貼圖的 y 由下往上
                        float distance = Mathf.Min(DistanceToSegment(p, a, b), DistanceToSegment(p, b, c));
                        float weight = Mathf.Clamp01(halfWidth - distance + 0.5f);
                        color = Color.Lerp(color, Color.white, weight);
                    }
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // 一筆 (或合併後的一組) 訊息: 第一行為行號 + 展開按鈕 + 訊息 (+ 合併次數); 第二行為時間與位置; 展開時顯示堆疊追蹤.
        // 複製按鈕不另佔一欄, 依序放在: 第二行左側 > 展開後堆疊第一行左側 > 沒有堆疊時取代展開按鈕;
        // 有堆疊但未展開且沒有第二行時不畫 (展開後才出現). 前兩者與展開按鈕同一欄
        private void DrawRow(int row)
        {
            if (row > 0) DrawSeparator();
            int index = visible[row];
            var source = SourceOf(index, out int i);
            var stackTrace = source.Snapshot[i].StackTrace;
            int count = rowsCollapsed ? groupCount[groupOf[index]] : 1;
            // 合併多次的訊息不顯示時間 (每次出現的時間不同, 只顯示一個沒有意義)
            bool rowTime = drawTime && count == 1;
            bool hasSecondLine = rowTime || drawLocation;
            bool isExpanded = rowExpanded[row] && stackTrace != null;

            GUILayout.BeginHorizontal();
            GUILayout.Space(RowGap);
            GUILayout.Label(source.GetNumber(i), numberStyle);
            GUILayout.Space(RowGap);
            if (drawCopy && stackTrace == null && !hasSecondLine)
            {
                DrawCopyRowButton(row, foldStyle);
            }
            else
            {
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && stackTrace != null;   // 沒有堆疊追蹤時呈現無法使用
                if (GUILayout.Button(rowExpanded[row] ? collapseRowLabel : expandRowLabel, foldStyle, GUILayout.Width(FoldWidth)))
                    pendingToggleRow = row;
                GUI.enabled = oldEnabled;
            }
            GUILayout.Space(TextGap);
            GUILayout.Label(source.Lines[i], lineStyle);
            if (count > 1) GUILayout.Label(CountNumberText(count), badgeStyle, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            if (hasSecondLine)
            {
                string location = drawLocation ? source.GetLocation(i, projectRoot) : string.Empty;
                GUILayout.Space(DetailOffset);
                GUILayout.BeginHorizontal();
                RowLeft(row, drawCopy);
                if (rowTime) GUILayout.Label(source.GetTime(i), inlineDetailStyle, GUILayout.ExpandWidth(false));
                if (location.Length > 0) DrawLocation(source, i, location);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (isExpanded)
            {
                GUILayout.BeginHorizontal();
                RowLeft(row, drawCopy && !hasSecondLine);
                GUILayout.Label(stackTrace.Text, detailStyle);
                GUILayout.EndHorizontal();
            }
        }

        // 第二行與展開內容的左側縮排 = 第一行訊息文字的左緣: 間距 + 行號 + 間距 + 展開按鈕 + 間距
        private float RowIndent() => RowGap + numberStyle.fixedWidth + RowGap + FoldWidth + TextGap;

        // 第二行 / 堆疊行的左側: 縮排; withCopy 時在展開按鈕那一欄畫複製按鈕
        private void RowLeft(int row, bool withCopy)
        {
            if (!withCopy)
            {
                GUILayout.Space(RowIndent());
                return;
            }
            GUILayout.Space(RowGap + numberStyle.fixedWidth + RowGap);
            DrawCopyRowButton(row, rowCopyStyle);
            GUILayout.Space(TextGap);
        }

        // 每一列的複製按鈕: 尺寸同展開按鈕; 按下後約 1 秒顯示已複製狀態 (底色)
        private void DrawCopyRowButton(int row, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint) DrawnCopyButtons++;
            bool isCopied = drawCopiedSequence >= 0 && drawCopiedSequence == SequenceOf(visible[row]);
            var oldColor = GUI.backgroundColor;
            if (isCopied) GUI.backgroundColor = copiedColor;
            if (GUILayout.Button(isCopied ? copiedRowLabel : copyRowLabel, style, GUILayout.Width(FoldWidth))) CopyRow(row);
            GUI.backgroundColor = oldColor;
        }

        // 工具列的 Copy: 複製目前篩選後看得到的全部內容; 按下後約 1 秒顯示 Copied
        private void DrawCopyAllButton()
        {
            if (Event.current.type == EventType.Repaint) DrawnCopyButtons++;
            var oldColor = GUI.backgroundColor;
            if (drawCopiedAll) GUI.backgroundColor = copiedColor;
            if (GUILayout.Button(drawCopiedAll ? copiedAllLabel : copyAllLabel, GUILayout.ExpandWidth(false))) CopyAll();
            GUI.backgroundColor = oldColor;
        }

        // 複製一列 (合併時為第一次出現的那筆, 附上次數) 到系統剪貼簿; 回傳複製的文字. 只在按下按鈕時配置
        internal string CopyRow(int row)
        {
            if (row < 0 || row >= visibleCount) return null;
            string text = FormatRowForCopy(row);
            GUIUtility.systemCopyBuffer = text;
            copiedSequence = SequenceOf(visible[row]);
            copiedRowUntil = Time.realtimeSinceStartup + CopiedFeedbackSeconds;
            return text;
        }

        // 依顯示順序複製目前看得到的每一列 (套用等級、來源、搜尋與 Collapse), 每筆之間空一行; 回傳複製的文字
        internal string CopyAll()
        {
            var text = new System.Text.StringBuilder();
            for (int row = 0; row < visibleCount; row++)
            {
                if (row > 0) text.Append("\n\n");
                text.Append(FormatRowForCopy(row));
            }
            string result = text.ToString();
            GUIUtility.systemCopyBuffer = result;
            copiedAllUntil = Time.realtimeSinceStartup + CopiedFeedbackSeconds;
            return result;
        }

        private string FormatRowForCopy(int row)
        {
            int index = visible[row];
            var source = SourceOf(index, out int i);
            int count = rowsCollapsed ? groupCount[groupOf[index]] : 1;
            return LogText.FormatForCopy(source.Snapshot[i], count, projectRoot);
        }

        // 測試用: 上次 Repaint 畫出的複製按鈕數 (每一列 + 工具列)
        internal int DrawnCopyButtons { get; private set; }

        // 測試用: 上次 Layout 時核取方塊是否放在工具列第一列
        internal bool ChecksOnFirstRow => checksOnFirstRow;

        // 測試用: 與按下第 row 列的展開按鈕相同 (下一次 Layout 才套用)
        internal void RequestToggleRow(int row) => pendingToggleRow = row;

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
            if (index < boundLogCapacity)
            {
                i = index;
                return logSource;
            }
            i = index - boundLogCapacity;
            return unitySource;
        }

        private long SequenceOf(int index) => SourceOf(index, out int i).Sequences[i];

        // 測試用: 上次 Layout 時篩選 (與合併) 後的列
        internal int VisibleRowCount => visibleCount;
        internal LogEntry GetVisibleEntry(int row) => SourceOf(visible[row], out int i).Snapshot[i];

        // "n" (合併次數)
        private string CountNumberText(int count)
        {
            if (count >= countNumberTexts.Length) return count.ToString();
            return countNumberTexts[count] ??= count.ToString();
        }

        // 第一次點擊進入清除狀態 (黃色), 清除狀態下再點才清除 (按住時紅色, 放開後恢復原色);
        // 在視窗其他位置按下滑鼠就取消清除狀態
        // 來源按鈕輪替: Both -> Unity (只顯示 Unity) -> Unity Off (只顯示 Log) -> Both; 兩者都關時下一步為 Both
        private void DrawSourceButton()
        {
            GUIContent label = showLogMessages
                ? (showUnityMessages ? sourceBothLabel : sourceLogLabel)
                : (showUnityMessages ? sourceUnityLabel : sourceNoneLabel);
            if (!GUILayout.Button(label, GUILayout.ExpandWidth(false))) return;
            if (showLogMessages && showUnityMessages)
            {
                ShowLogMessages = false;
            }
            else if (showUnityMessages)
            {
                ShowLogMessages = true;
                ShowUnityMessages = false;
            }
            else
            {
                ShowLogMessages = true;
                ShowUnityMessages = true;
            }
        }

        private void DrawClearButton(bool isMouseDown, Vector2 mouseDownPosition)
        {
            var rect = GUILayoutUtility.GetRect(clearLabel, GUI.skin.button, GUILayout.ExpandWidth(false));
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
            BindHistories();
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
            drawCopy = HasButton(visibleButtons, DebugWindowButtons.Copy);
            float now = Time.realtimeSinceStartup;
            drawCopiedAll = now < copiedAllUntil;
            drawCopiedSequence = now < copiedRowUntil ? copiedSequence : -1;

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
                    : boundLogCapacity + j++;
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
            new Vector2(Mathf.Max(0f, Screen.width / s - size.x - Margin), Mathf.Max(0f, Screen.height / s - WindowHeight() - Margin));

        // 一份歷史紀錄的快照與已格式化字串; 陣列長度為該紀錄的容量
        private sealed class Source
        {
            public readonly LogEntry[] Snapshot;
            public readonly long[] Sequences;
            public readonly string[] Lines;
            // 第二行用的快取, 第一次顯示時才組; null 為尚未計算
            public readonly string[] Times;
            public readonly string[] Numbers;        // "[流水號]"
            public readonly string[] Locations;      // "路徑:行號"; 空字串為沒有位置
            public readonly string[] LocationFiles;
            public readonly int[] LocationLines;
            public int Count;
            private int seenVersion;
            private bool hasSnapshot;

            public Source(int capacity)
            {
                Snapshot = new LogEntry[capacity];
                Sequences = new long[capacity];
                Lines = new string[capacity];
                Times = new string[capacity];
                Numbers = new string[capacity];
                Locations = new string[capacity];
                LocationFiles = new string[capacity];
                LocationLines = new int[capacity];
            }

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
                LogText.TryGetLocation(Snapshot[i], projectRoot, out string file, out int line);
                text = file == null ? string.Empty : file + ":" + line;
                Locations[i] = text;
                LocationFiles[i] = file;
                LocationLines[i] = line;
                return text;
            }

            private void ClearDetails(int from)
            {
                int length = Snapshot.Length - from;
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
            position.y = Mathf.Clamp(position.y, 0f, screenHeight - WindowHeight());   // 縮小時以標題列的高度判斷
        }
    }

}
