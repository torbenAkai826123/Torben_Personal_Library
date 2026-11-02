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

        public LogEntry(LogLevel level, LogCategory category, string message)
        {
            Level = level;
            Category = category;
            Message = message; 
        }
    }
}