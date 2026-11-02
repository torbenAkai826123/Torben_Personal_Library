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

        /// <summary>isGUILog 開啟時, 是否仍同時輸出到 Console. (預設關閉, 避免系統訊息蓋過自己的訊息)</summary>
        public static bool isConsoleWithGUI = false;

        /// <summary>
        /// 是否把 Unity 引擎訊息 (未捕捉的例外、引擎警告、直接呼叫 UnityEngine.Debug.Log 的訊息) 收集到 DebugWindow.UnityHistory.
        /// 不受 Log.isPrintLog 影響; 預設開啟, 每次進入 Play 時恢復開啟. 關閉期間的訊息不會補記.
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

        // 遊戲啟動時 (最早的載入階段) 重設狀態, 加入 Console 與畫面視窗的歷史紀錄兩個 sink, 並開始收集 Unity 引擎訊息.
        // 在這之後才寫的 Log 都會被歷史紀錄保留, 視窗晚點開啟也看得到.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            guiLog = false;
            Log.ClearSinks();   // 關閉 Domain Reload 時靜態狀態會保留, 先清掉避免重複加入
            Log.AddSink(new DebugLogs());
            Log.AddSink(DebugWindow.ResetStatics());
            isUnityLog = true;  // 先取消再訂閱, 關閉 Domain Reload 時也不會重複
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }
    }

}
