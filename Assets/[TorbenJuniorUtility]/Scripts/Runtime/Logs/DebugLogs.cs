using System.Collections.Generic;
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
        internal static void Install(LogSettings settings) => Install(settings, Debug.isDebugBuild, IsDebugSymbolDefined);

        // TORBEN_LOG_DEBUG 是否有定義. 反映的是 Runtime 組件編譯時的狀態: 以原始碼由遊戲專案編譯時正確;
        // 打包成 DLL 時只反映打包當下
#if TORBEN_LOG_DEBUG
        private const bool IsDebugSymbolDefined = true;
#else
        private const bool IsDebugSymbolDefined = false;
#endif

        // isDebugBuild / isDebugSymbolDefined 可注入, 讓測試模擬正式版 (Editor 與測試中 Debug.isDebugBuild 永遠為 true)
        internal static void Install(LogSettings settings, bool isDebugBuild, bool isDebugSymbolDefined)
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

            List<string> releaseProblems = null;
            if (!isDebugBuild)
            {
                releaseProblems = new List<string>();
                CollectReleaseProblems(defaults == null ? effective : null, isDebugBuild, isDebugSymbolDefined, releaseProblems);
            }

            if (defaults == null)
            {
                var level = effective.DevelopmentAlert.level;
                Alert(level, BuildAlertText(FindProblems(effective), releaseProblems, effective.name, level));
                return;
            }
            Object.Destroy(defaults);
            // 不經過 Log: 此時 Log 的設定剛套用, 避免順序問題. 找不到設定檔時讀不到等級, 一律以 Warning 提醒;
            // 正式版的開發設定 (只可能是 TORBEN_LOG_DEBUG) 以一行併入同一則 (Warning 一律一行)
            string text = $"[Log] LogSettings not found at Resources/{LogSettings.ResourcesPath}; using default values. Create it from Project Settings > TorbenTools > Log.";
            if (releaseProblems != null && releaseProblems.Count > 0)
                text += "\n" + ReleaseLine(releaseProblems, "create the settings file and set Development Alert to Off");
            Debug.LogWarning(text);
        }

        /// <summary>
        /// 正式版 (非 Development Build) 仍開著的開發設定 (B 類). isDebugBuild 為 true 時不加入任何項目.
        /// 依序檢查 output.startWithWindow、log.isPrintLog、log.isDebugLog、TORBEN_LOG_DEBUG, 符合的項目名稱加入 problems.
        /// settings 為生效中的設定檔; null (找不到設定檔) 時前三項依欄位初始值判斷.
        /// isDebugSymbolDefined 由 Install 以 Runtime 組件編譯時的 TORBEN_LOG_DEBUG 傳入, 打包成 DLL 時只反映打包當下.
        /// </summary>
        /// <remarks>配置: 每個項目配置 (加入 problems); 只在載入期使用.</remarks>
        internal static void CollectReleaseProblems(LogSettings settings, bool isDebugBuild, bool isDebugSymbolDefined, List<string> problems)
        {
            if (isDebugBuild) return;
            var output = settings?.output ?? new DebugLogsOptions();
            var log = settings?.log ?? new LogOptions();
            if (output.startWithWindow) problems.Add("output.startWithWindow");
            if (log.isPrintLog) problems.Add("log.isPrintLog");
            if (log.isDebugLog) problems.Add("log.isDebugLog");
            if (isDebugSymbolDefined) problems.Add("TORBEN_LOG_DEBUG (Log.Debug kept in builds)");
        }

        // 合併 A 類 (設定問題) 與 B 類 (正式版的開發設定) 為一則訊息; 兩者都沒有時回傳 null.
        // B 類在 Warning 時只寫一行 (ReleaseLine); Error 時為多行清單 (ReleaseSection). A 類一律多行.
        // 只有 B 類且為 Warning 時, 整則訊息就是那一行.
        // 配置: 每次呼叫配置; 只在載入期使用
        internal static string BuildAlertText(string settingsProblems, List<string> releaseProblems, string fileName, DevelopmentAlertLevel level)
        {
            bool hasRelease = releaseProblems != null && releaseProblems.Count > 0;
            if (settingsProblems == null && !hasRelease) return null;
            string fix = $"set Development Alert to Off in {fileName}";
            if (level != DevelopmentAlertLevel.Error)
            {
                if (settingsProblems == null) return "[Log] " + ReleaseLine(releaseProblems, fix);
                string head = SettingsProblemsHeader + settingsProblems;
                return hasRelease ? head + "\n" + ReleaseLine(releaseProblems, fix) : head;
            }
            string text = SettingsProblemsHeader + settingsProblems;
            if (hasRelease) text += ReleaseSection(releaseProblems, fix);
            return text;
        }

        private const string SettingsProblemsHeader = "[Log] LogSettings problems (Development Alert; Project Settings > TorbenTools > Log):";

        // B 類的一行版本 (Warning): 例如 "Development log settings left on in a non-development build: log.isPrintLog, log.isDebugLog (if intentional, set Development Alert to Off in LogSettings)."
        private static string ReleaseLine(List<string> releaseProblems, string fix) =>
            "Development log settings left on in a non-development build: " + string.Join(", ", releaseProblems) + " (if intentional, " + fix + ").";

        private static string ReleaseSection(List<string> releaseProblems, string fix)
        {
            var text = new System.Text.StringBuilder("\nDevelopment settings enabled in a non-development build:");
            foreach (var problem in releaseProblems) text.Append("\n- ").Append(problem);
            return text.Append("\nIf this is intentional, ").Append(fix).Append('.').ToString();
        }

        // sink 的 Write 拋例外時 (每個 sink 只有第一次) 由 Log 呼叫; 不經過 Log.
        // 包成一個例外輸出, 讓訊息第一行帶有 sink 型別名稱且只佔一則.
        // 不用 InnerException: Unity 會改以最內層例外的訊息顯示, sink 名稱就看不到; 原例外 (含堆疊) 放在訊息後面
        private static void ReportSinkError(ILogSink sink, System.Exception exception)
        {
            Debug.LogException(new System.Exception(
                $"[Log] Sink '{sink.GetType().FullName}' threw an exception while writing (later failures of this sink are not reported).\n{exception}"));
        }

        // 依等級輸出一則提醒; 不經過 Log. text 為 null 時不輸出
        private static void Alert(DevelopmentAlertLevel level, string text)
        {
            if (text == null || level == DevelopmentAlertLevel.Off) return;
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
