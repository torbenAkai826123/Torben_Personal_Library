namespace Torben
{
    /// <summary>
    /// The level is compatible with UnityEngine.LogType, but is not a direct mapping. It is used to categorize log messages into different levels of severity.
    /// </summary>
    // 1102: Add Debug level: 內部測試用的 LogLevel，主要是為了在開發階段提供更詳細的訊息，方便開發者進行除錯和追蹤程式流程。這個等級的訊息通常不會在正式版本中顯示，僅供開發者在開發過程中使用。
    public enum LogLevel
    {
        Normal,
        Warning,
        Error,
        Debug,
    }

    // Log sink interface for writing log entries.
    public interface ILogSink
    {
        void Write(in LogEntry entry);
    }

    // Log entry structure containing log level, category, and message.
    public readonly struct LogEntry
    {
        public readonly LogLevel Level;
        public readonly LogCategory Category;
        public readonly string Message;

        /// <summary>寫入時間 (UTC). 以 3 個參數建構時為 default.</summary>
        public readonly System.DateTime Time;

        /// <summary>呼叫 Log 的原始碼檔案 (編譯期決定的完整路徑); 沒有資訊時為 null 或空字串.</summary>
        public readonly string CallerFilePath;

        /// <summary>呼叫 Log 的方法名稱; 沒有資訊時為 null 或空字串.</summary>
        public readonly string CallerMemberName;

        /// <summary>呼叫 Log 的行號; 沒有資訊時為 0.</summary>
        public readonly int CallerLineNumber;

        /// <summary>堆疊追蹤; 未擷取時為 null (見 Log.SetStackTraceEnabled).</summary>
        public readonly LogStackTrace StackTrace;

        public LogEntry(LogLevel level, LogCategory category, string message)
            : this(level, category, message, default, null, null, 0, null) { }

        public LogEntry(LogLevel level, LogCategory category, string message, System.DateTime time,
            string callerFilePath, string callerMemberName, int callerLineNumber, LogStackTrace stackTrace)
        {
            Level = level;
            Category = category;
            Message = message;
            Time = time;
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
            CallerLineNumber = callerLineNumber;
            StackTrace = stackTrace;
        }

        /// <summary>是否有呼叫端位置 (檔案路徑).</summary>
        public bool HasCaller => !string.IsNullOrEmpty(CallerFilePath);
    }

    /// <summary>
    /// 分隔 Log.Format 的格式參數與呼叫端資訊參數, 不要傳入.
    /// 沒有它時, 多傳一個字串或整數參數會被編譯器當成呼叫端資訊, 而不是報錯.
    /// </summary>
    public readonly struct LogArgsEnd { }
}
