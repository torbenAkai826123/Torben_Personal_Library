namespace Torben
{
    // Call Log facade.
    public static class Log
    {
        public static bool isPrintLog = false; // Flag to control whether logs should be printed to the console.

        // Log sink that receives log entries. Can be set to any implementation of ILogSink.
        public static ILogSink Sink { get; set; } 

        public static void Information(string message, LogCategory category = default(LogCategory)) 
            => SinkLog(message, category, LogLevel.Normal);
        public static void Warning(string message, LogCategory category = default(LogCategory)) 
            => SinkLog(message, category, LogLevel.Warning);
        public static void Error(string message, LogCategory category = default(LogCategory)) 
            => SinkLog(message, category, LogLevel.Error);

        // Internal method to send log entries to the sink.
        private static void SinkLog(string message, LogCategory category, LogLevel level)
        { 
            if (!isPrintLog) return; // If logging is disabled, exit early.
            if (category == null)  category = LogCategoryEmpty.None;
            Sink?.Write(new LogEntry(level, category, message));
        }
    }
}

// 簡易筆記
/*  # 設置說明
     1. 系統載入時會自動讀取 DebugLogs.cs 中的 Install 方法，將 Log.Sink 設置為 DebugLogs 的實例。
     2. Log 類別提供了三個靜態方法：Information、 Warning 和 Error，用於記錄不同級別的日誌。
     3. 每個方法都接受一個消息字符串和一個可選的 LogCategory 參數。如果未提供 LogCategory，將使用 LogCategoryEmpty.None 作為默認值。

    # 使用說明
     1. 在 Unity 中，您可以在任何地方調用 Log.Information、Log.Warning 或 Log.Error 方法來記錄日誌。
     2. 您可以選擇性地提供一個自定義的 LogCategory，以便對日誌進行分類和標記。
     3. 日誌將被傳遞到設置的 ILogSink 實例（默認為 DebugLogs），並根據日誌級別輸出到 Unity 控制台。

    # 注意事項
     1. 確保在使用 Log 類別之前，已經設置了 Log.Sink，以便日誌能夠正確輸出。
     2. 如果未設置 Log.Sink，日誌將不會被輸出，請確保在應用程序啟動時進行設置。

    # 運作流程摘要
     1. DebugLogs.Install 方法在應用程序啟動時被調用，將 Log.Sink 設置為 DebugLogs 的實例。
     2. 當調用 Log.Information、Log.Warning 或 Log.Error 方法時，
        這些方法會創建一個 LogEntry 對象，並將其傳遞給 Log.Sink 的 Write 方法。
     3. DebugLogs 的 Write 方法根據日誌級別將日誌輸出到 Unity 控制台，並附加相應的標籤和消息內容。

 */