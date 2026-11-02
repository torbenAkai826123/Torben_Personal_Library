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

        /// <summary>
        /// 指定等級的訊息目前是否會送到 sink (總開關開啟、至少有一個 sink; Debug 等級另需 isDebugLog).
        /// 呼叫端組字串 (例如 $"hp={hp}") 前先檢查, 可避免關閉時的配置.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool IsEnabled(LogLevel level)
            => isPrintLog && sinks.Length > 0 && (level != LogLevel.Debug || isDebugLog);

        public static void Information(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Normal);
        public static void Warning(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Warning);
        public static void Error(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Error);
        public static void Debug(string message, LogCategory category = default(LogCategory))
            => SinkLog(message, category, LogLevel.Debug);

        // ---- InformationFormat: 開啟時才以 string.Format 組字串; 關閉時零配置 ----

        /// <summary>Information 等級的格式化訊息; 只有 IsEnabled(LogLevel.Normal) 時才組字串.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時每次呼叫配置 (格式化字串, 值型別參數裝箱).</remarks>
        public static void InformationFormat<T1>(string format, T1 arg1)
            => WriteFormat(LogLevel.Normal, null, format, arg1);
        /// <inheritdoc cref="InformationFormat{T1}(string, T1)"/>
        public static void InformationFormat<T1, T2>(string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Normal, null, format, arg1, arg2);
        /// <inheritdoc cref="InformationFormat{T1}(string, T1)"/>
        public static void InformationFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Normal, null, format, arg1, arg2, arg3);
        /// <inheritdoc cref="InformationFormat{T1}(string, T1)"/>
        public static void InformationFormat<T1>(LogCategory category, string format, T1 arg1)
            => WriteFormat(LogLevel.Normal, category, format, arg1);
        /// <inheritdoc cref="InformationFormat{T1}(string, T1)"/>
        public static void InformationFormat<T1, T2>(LogCategory category, string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Normal, category, format, arg1, arg2);
        /// <inheritdoc cref="InformationFormat{T1}(string, T1)"/>
        public static void InformationFormat<T1, T2, T3>(LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Normal, category, format, arg1, arg2, arg3);

        // ---- WarningFormat: 開啟時才以 string.Format 組字串; 關閉時零配置 ----

        /// <summary>Warning 等級的格式化訊息; 只有 IsEnabled(LogLevel.Warning) 時才組字串.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時每次呼叫配置 (格式化字串, 值型別參數裝箱).</remarks>
        public static void WarningFormat<T1>(string format, T1 arg1)
            => WriteFormat(LogLevel.Warning, null, format, arg1);
        /// <inheritdoc cref="WarningFormat{T1}(string, T1)"/>
        public static void WarningFormat<T1, T2>(string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Warning, null, format, arg1, arg2);
        /// <inheritdoc cref="WarningFormat{T1}(string, T1)"/>
        public static void WarningFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Warning, null, format, arg1, arg2, arg3);
        /// <inheritdoc cref="WarningFormat{T1}(string, T1)"/>
        public static void WarningFormat<T1>(LogCategory category, string format, T1 arg1)
            => WriteFormat(LogLevel.Warning, category, format, arg1);
        /// <inheritdoc cref="WarningFormat{T1}(string, T1)"/>
        public static void WarningFormat<T1, T2>(LogCategory category, string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Warning, category, format, arg1, arg2);
        /// <inheritdoc cref="WarningFormat{T1}(string, T1)"/>
        public static void WarningFormat<T1, T2, T3>(LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Warning, category, format, arg1, arg2, arg3);

        // ---- ErrorFormat: 開啟時才以 string.Format 組字串; 關閉時零配置 ----

        /// <summary>Error 等級的格式化訊息; 只有 IsEnabled(LogLevel.Error) 時才組字串.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時每次呼叫配置 (格式化字串, 值型別參數裝箱).</remarks>
        public static void ErrorFormat<T1>(string format, T1 arg1)
            => WriteFormat(LogLevel.Error, null, format, arg1);
        /// <inheritdoc cref="ErrorFormat{T1}(string, T1)"/>
        public static void ErrorFormat<T1, T2>(string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Error, null, format, arg1, arg2);
        /// <inheritdoc cref="ErrorFormat{T1}(string, T1)"/>
        public static void ErrorFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Error, null, format, arg1, arg2, arg3);
        /// <inheritdoc cref="ErrorFormat{T1}(string, T1)"/>
        public static void ErrorFormat<T1>(LogCategory category, string format, T1 arg1)
            => WriteFormat(LogLevel.Error, category, format, arg1);
        /// <inheritdoc cref="ErrorFormat{T1}(string, T1)"/>
        public static void ErrorFormat<T1, T2>(LogCategory category, string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Error, category, format, arg1, arg2);
        /// <inheritdoc cref="ErrorFormat{T1}(string, T1)"/>
        public static void ErrorFormat<T1, T2, T3>(LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Error, category, format, arg1, arg2, arg3);

        // ---- DebugFormat: 開啟時才以 string.Format 組字串; 關閉時零配置 ----

        /// <summary>Debug 等級的格式化訊息; 只有 IsEnabled(LogLevel.Debug) 時才組字串.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時每次呼叫配置 (格式化字串, 值型別參數裝箱).</remarks>
        public static void DebugFormat<T1>(string format, T1 arg1)
            => WriteFormat(LogLevel.Debug, null, format, arg1);
        /// <inheritdoc cref="DebugFormat{T1}(string, T1)"/>
        public static void DebugFormat<T1, T2>(string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Debug, null, format, arg1, arg2);
        /// <inheritdoc cref="DebugFormat{T1}(string, T1)"/>
        public static void DebugFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Debug, null, format, arg1, arg2, arg3);
        /// <inheritdoc cref="DebugFormat{T1}(string, T1)"/>
        public static void DebugFormat<T1>(LogCategory category, string format, T1 arg1)
            => WriteFormat(LogLevel.Debug, category, format, arg1);
        /// <inheritdoc cref="DebugFormat{T1}(string, T1)"/>
        public static void DebugFormat<T1, T2>(LogCategory category, string format, T1 arg1, T2 arg2)
            => WriteFormat(LogLevel.Debug, category, format, arg1, arg2);
        /// <inheritdoc cref="DebugFormat{T1}(string, T1)"/>
        public static void DebugFormat<T1, T2, T3>(LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3)
            => WriteFormat(LogLevel.Debug, category, format, arg1, arg2, arg3);

        // 參數以泛型傳入, 關閉時不會裝箱; 開啟時才交給 string.Format
        private static void WriteFormat<T1>(LogLevel level, LogCategory category, string format, T1 arg1)
        {
            if (IsEnabled(level)) SinkLog(string.Format(format, arg1), category, level);
        }
        private static void WriteFormat<T1, T2>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2)
        {
            if (IsEnabled(level)) SinkLog(string.Format(format, arg1, arg2), category, level);
        }
        private static void WriteFormat<T1, T2, T3>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (IsEnabled(level)) SinkLog(string.Format(format, arg1, arg2, arg3), category, level);
        }

        // Internal method to send log entries to the sink.
        private static void SinkLog(string message, LogCategory category, LogLevel level)
        {
            if (!IsEnabled(level)) return; // 總開關關閉、沒有 sink, 或 Debug 訊息未開啟時都不輸出
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
     2. Log 類別提供了四個靜態方法：Information、Warning、Error 和 Debug，用於記錄不同級別的日誌。
     3. 每個方法都接受一個消息字符串和一個可選的 LogCategory 參數。如果未提供 LogCategory，將使用 LogCategoryEmpty.None 作為默認值。
     4. 需要組字串時，用 XxxFormat (例如 Log.DebugFormat("hp={0}", hp)) 或先檢查 Log.IsEnabled(level)，關閉時不會配置。

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