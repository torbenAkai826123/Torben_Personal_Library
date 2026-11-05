using UnityEngine;

namespace Torben
{
    /// <summary>
    /// 把 Log 訊息輸出到 Unity Console 的 sink, 並負責在啟動時安裝 Console 與畫面視窗兩個 sink,
    /// 以及收集 Unity 引擎訊息到 DebugWindow.UnityHistory.
    /// </summary>
    public sealed class DebugLogs : ILogSink
    {
        /// <summary>
        /// 開啟時在畫面視窗 (DebugWindow) 顯示訊息, 並停止輸出到 Console (除非 isConsoleWithGUI 開啟).
        /// 關閉時隱藏視窗, 恢復 Console 輸出.
        /// </summary>
        /// <remarks>配置: 第一次開啟視窗時建立元件; 之後切換零配置.</remarks>
        public static bool isGUILog
        {
            get => guiLog;
            set
            {
                guiLog = value;
                DebugWindow.SetVisible(value);
            }
        }
        private static bool guiLog = false;

        /// <summary>isGUILog 開啟時, 是否仍同時輸出到 Console (關閉可避免系統訊息蓋過自己的訊息). 預設值見 DebugLogsOptions 欄位初始值.</summary>
        public static bool isConsoleWithGUI = false;

        /// <summary>
        /// 是否把 Unity 引擎訊息 (未捕捉的例外、引擎警告、直接呼叫 UnityEngine.Debug.Log 的訊息) 收集到 DebugWindow.UnityHistory.
        /// 不受 Log.isPrintLog 影響; 每次進入 Play 時恢復為設定檔的值 (預設值見 DebugLogsOptions 欄位初始值). 關閉期間的訊息不會補記.
        /// </summary>
        /// <remarks>配置: 零配置 (訊息字串由 Unity 提供).</remarks>
        public static bool isUnityLog
        {
            get => unityLog;
            set
            {
                unityLog = value;
                Application.logMessageReceivedThreaded -= OnUnityLog;
                if (value) Application.logMessageReceivedThreaded += OnUnityLog;
            }
        }
        private static bool unityLog = true;

        // 自己輸出到 Console 時為 true, 避免同一則訊息又被當成 Unity 訊息收集一次.
        // logMessageReceivedThreaded 在呼叫 Debug.Log 的執行緒上同步觸發, 所以依執行緒分開記錄.
        [System.ThreadStatic] private static bool writingConsole;

        public void Write(in LogEntry entry)
        {
            if (guiLog && !isConsoleWithGUI) return;
            string tag = string.IsNullOrEmpty(entry.Category.tagColorHex) ? entry.Category.Tag : $"<color={entry.Category.tagColorHex}>{entry.Category.Tag}</color>";
            string text = tag + entry.Message;
            writingConsole = true;
            try
            {
                switch (entry.Level)
                {
                    case LogLevel.Warning: Debug.LogWarning(text); break;
                    case LogLevel.Error: Debug.LogError(text); break;
                    default: Debug.Log(text); break;
                }
            }
            finally
            {
                writingConsole = false;
            }
        }

        // 可能在任何執行緒觸發; 寫入的 LogHistorySink 有加鎖.
        // 堆疊文字由 Unity 提供, 不受 Log.SetStackTraceEnabled 影響; 是否有內容依 Unity 的 Stack Trace Logging 設定
        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (writingConsole) return;
            var history = DebugWindow.UnityHistory;
            if (history == null) return;
            history.Write(new LogEntry(ToLevel(type), UnityLogCategory.Instance, condition, System.DateTime.UtcNow,
                null, null, 0, LogStackTrace.FromText(stackTrace)));
        }

        private static LogLevel ToLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return LogLevel.Warning;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception: return LogLevel.Error;
                default: return LogLevel.Normal;
            }
        }

        // 離開 Play (Editor) 或結束程式時取消訂閱, 避免在 Edit 模式繼續收集
        private static void OnQuitting() => Application.logMessageReceivedThreaded -= OnUnityLog;

        // 啟動時從 Resources 讀到的預設檔; 找不到時為 null
        internal static LogSettings StartupSettings { get; private set; }

        // 遊戲啟動時 (最早的載入階段) 讀取設定檔並安裝. 在這之後才寫的 Log 都會被歷史紀錄保留, 視窗晚點開啟也看得到.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallOnLoad()
        {
            DebugWindow.ResetSceneState();
            StartupSettings = Resources.Load<LogSettings>(LogSettings.ResourcesPath);
            Install(StartupSettings);
        }

        // 重設狀態, 加入 Console 與畫面視窗的歷史紀錄兩個 sink, 套用設定檔, 並開始收集 Unity 引擎訊息.
        // settings 為預設檔 (有 projectOverride 時改用它); null 時以欄位初始值運作並警告一次.
        // 最後依 Development Alert 的等級提醒設定問題 (此時已開始收集 Unity 訊息, 畫面視窗收得到)
        internal static void Install(LogSettings settings)
        {
            guiLog = false;
            Log.ClearSinks();   // 關閉 Domain Reload 時靜態狀態會保留, 先清掉避免重複加入
            Log.SinkErrorHandler = ReportSinkError;

            LogSettings effective = settings != null ? settings.ResolveEffective() : null;
            LogSettings defaults = null;
            if (effective == null) effective = defaults = ScriptableObject.CreateInstance<LogSettings>();
            var window = effective.window ?? new DebugWindowOptions();

            Log.AddSink(new DebugLogs());
            Log.AddSink(DebugWindow.ResetStatics(window.logCapacity, window.unityCapacity));
            isUnityLog = true;  // 先取消再訂閱, 關閉 Domain Reload 時也不會重複
            effective.Apply();
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;

            if (defaults == null)
            {
                Alert(effective.DevelopmentAlert.level, FindProblems(effective));
                return;
            }
            Object.Destroy(defaults);
            // 不經過 Log: 此時 Log 的設定剛套用, 避免順序問題. 找不到設定檔時讀不到等級, 一律以 Warning 提醒
            Debug.LogWarning($"[Log] LogSettings not found at Resources/{LogSettings.ResourcesPath}; using default values. Create it from Project Settings > TorbenTools > Log.");
        }

        // sink 的 Write 拋例外時 (每個 sink 只有第一次) 由 Log 呼叫; 不經過 Log.
        // 包成一個例外輸出, 讓訊息第一行帶有 sink 型別名稱且只佔一則.
        // 不用 InnerException: Unity 會改以最內層例外的訊息顯示, sink 名稱就看不到; 原例外 (含堆疊) 放在訊息後面
        private static void ReportSinkError(ILogSink sink, System.Exception exception)
        {
            Debug.LogException(new System.Exception(
                $"[Log] Sink '{sink.GetType().FullName}' threw an exception while writing (later failures of this sink are not reported).\n{exception}"));
        }

        // 依等級輸出一則提醒; 不經過 Log. problems 為 null 時不輸出
        private static void Alert(DevelopmentAlertLevel level, string problems)
        {
            if (problems == null || level == DevelopmentAlertLevel.Off) return;
            string text = "[Log] LogSettings problems (Development Alert; Project Settings > TorbenTools > Log):" + problems;
            if (level == DevelopmentAlertLevel.Error) Debug.LogError(text);
            else Debug.LogWarning(text);
        }

        // 啟動時檢查設定檔: 容量超出範圍、等級陣列重複. 每個問題一行 (以換行開頭), 沒有問題時回傳 null
        // 配置: 每次呼叫配置; 只在載入期使用
        internal static string FindProblems(LogSettings settings)
        {
            System.Text.StringBuilder text = null;
            var window = settings.window;
            if (window != null)
            {
                CheckCapacity(ref text, "window.logCapacity", window.logCapacity);
                CheckCapacity(ref text, "window.unityCapacity", window.unityCapacity);
                var levels = new LogLevel[window.levelColors?.Length ?? 0];
                for (int i = 0; i < levels.Length; i++) levels[i] = window.levelColors[i].level;
                CheckDuplicates(ref text, "window.levelColors", levels);
            }
            if (settings.log?.stackTrace != null)
            {
                var switches = settings.log.stackTrace;
                var levels = new LogLevel[switches.Length];
                for (int i = 0; i < levels.Length; i++) levels[i] = switches[i].level;
                CheckDuplicates(ref text, "log.stackTrace", levels);
            }
            return text?.ToString();
        }

        private static void CheckCapacity(ref System.Text.StringBuilder text, string field, int capacity)
        {
            if (capacity < 1)
                Append(ref text, $"{field} is {capacity}; it must be at least 1 (1 is used).");
            else if (capacity > DebugWindowOptions.MaxRecommendedCapacity)
                Append(ref text, $"{field} is {capacity}; values above {DebugWindowOptions.MaxRecommendedCapacity} may slow down the window.");
        }

        // 同一等級出現多次時, 只有最上面那筆生效
        private static void CheckDuplicates(ref System.Text.StringBuilder text, string field, LogLevel[] levels)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                if (System.Array.IndexOf(levels, levels[i]) != i) continue;     // 只在第一次出現時檢查
                if (System.Array.IndexOf(levels, levels[i], i + 1) < 0) continue;
                Append(ref text, $"{field} lists {levels[i]} more than once; only the first entry is used.");
            }
        }

        private static void Append(ref System.Text.StringBuilder text, string line)
        {
            if (text == null) text = new System.Text.StringBuilder();
            text.Append("\n- ").Append(line);
        }
    }

}
