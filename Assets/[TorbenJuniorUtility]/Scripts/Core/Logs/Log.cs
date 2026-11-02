namespace Torben
{
    // Call Log facade.
    public static class Log
    {
        // LOG 的全體總開關，只要關閉就會被擋住所有訊息。（預設關閉）
        public static bool isPrintLog = false; // Flag to control whether logs should be printed to the console.
        public static bool isDebugLog = false; // Flag to control whether debug logs should be printed to the console.


        // 接收訊息的 sink 清單. 新增 / 移除時複製新陣列再整個替換 (copy-on-write), 寫入時走訪不需加鎖
        private static readonly object sinkGate = new object();
        private static readonly ILogSink[] emptySinks = new ILogSink[0];
        private static volatile ILogSink[] sinks = emptySinks;

        /// <summary>目前是否至少有一個 sink.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool HasSink => sinks.Length > 0;

        /// <summary>新增 sink; 已存在或為 null 時不做事.</summary>
        /// <remarks>配置: 每次呼叫配置新陣列.</remarks>
        public static void AddSink(ILogSink sink)
        {
            if (sink == null) return;
            lock (sinkGate)
            {
                var current = sinks;
                if (System.Array.IndexOf(current, sink) >= 0) return;
                var next = new ILogSink[current.Length + 1];
                System.Array.Copy(current, next, current.Length);
                next[current.Length] = sink;
                sinks = next;
            }
        }

        /// <summary>移除 sink; 不存在時不做事.</summary>
        /// <remarks>配置: 有移除時配置新陣列.</remarks>
        public static void RemoveSink(ILogSink sink)
        {
            lock (sinkGate)
            {
                var current = sinks;
                int index = System.Array.IndexOf(current, sink);
                if (index < 0) return;
                var next = current.Length == 1 ? emptySinks : new ILogSink[current.Length - 1];
                System.Array.Copy(current, 0, next, 0, index);
                System.Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                sinks = next;
            }
        }

        /// <summary>移除全部 sink.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static void ClearSinks()
        {
            lock (sinkGate) sinks = emptySinks;
        }

        public static void Information(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Normal);
        public static void Warning(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Warning);
        public static void Error(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Error);
        public static void Debug(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Debug);

        // Internal method to send log entries to the sink.
        private static void SinkLog(string message, LogCategory category, LogLevel level)
        {
            if (!isPrintLog) return; // If logging is disabled, exit early.
            if (level == LogLevel.Debug && !isDebugLog) return; // Debug 訊息未開啟時, 所有 sink 都不輸出
            if (category == null) category = LogCategoryEmpty.None;
            var entry = new LogEntry(level, category, message);
            var current = sinks;
            for (int i = 0; i < current.Length; i++)
                current[i].Write(entry);
        }
    }
}

// 簡易筆記
/*  # 設置說明
     1. 系統載入時會自動讀取 DebugLogs.cs 中的 Install 方法，以 Log.AddSink 加入 DebugLogs (Console) 與 DebugWindow 的歷史紀錄 sink。
     2. Log 類別提供了三個靜態方法：Information、 Warning 和 Error，用於記錄不同級別的日誌。
     3. 每個方法都接受一個消息字符串和一個可選的 LogCategory 參數。如果未提供 LogCategory，將使用 LogCategoryEmpty.None 作為默認值。

    # 使用說明
     1. 在 Unity 中，您可以在任何地方調用 Log.Information、Log.Warning 或 Log.Error 方法來記錄日誌。
     2. 您可以選擇性地提供一個自定義的 LogCategory，以便對日誌進行分類和標記。
     3. 日誌會傳給所有已加入的 ILogSink (預設為 DebugLogs 與畫面視窗的歷史紀錄)。

    # 注意事項
     1. 沒有任何 sink 時日誌不會被輸出，可用 Log.HasSink 確認。
     2. 自訂輸出可用 Log.AddSink / Log.RemoveSink 加入或移除。

    # 運作流程摘要
     1. DebugLogs.Install 方法在應用程序啟動時被調用，加入 Console 與歷史紀錄兩個 sink。
     2. 當調用 Log.Information、Log.Warning 或 Log.Error 方法時，
        這些方法會創建一個 LogEntry 對象，並依序傳給每個 sink 的 Write 方法。
     3. DebugLogs 的 Write 方法根據日誌級別將日誌輸出到 Unity 控制台，並附加相應的標籤和消息內容。

 */