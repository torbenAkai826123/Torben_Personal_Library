namespace Torben
{
    public enum LogLevel
    {
        Normal,
        Warning,
        Error
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