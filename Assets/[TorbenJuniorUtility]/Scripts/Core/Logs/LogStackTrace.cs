namespace Torben
{
    /// <summary>
    /// 一筆訊息的堆疊追蹤. 由 Log 擷取時只記錄堆疊框架, 第一次讀取 Text 才轉成字串並快取.
    /// </summary>
    public sealed class LogStackTrace
    {
        // Log 自己的框架 (Log.Error、Log.SinkLog...) 以此開頭, 轉成文字時從最上方去掉
        private const string LogFramePrefix = "Torben.Log.";

        private readonly System.Diagnostics.StackTrace trace;
        private string text;

        private LogStackTrace(System.Diagnostics.StackTrace trace, string text)
        {
            this.trace = trace;
            this.text = text;
        }

        /// <summary>擷取目前執行緒的堆疊 (平台支援時包含檔名與行號).</summary>
        /// <remarks>配置: 每次呼叫配置. 成本比一般 Log 高, 只在 Log.SetStackTraceEnabled 開啟的等級使用.</remarks>
        public static LogStackTrace Capture() => new LogStackTrace(new System.Diagnostics.StackTrace(1, true), null);

        /// <summary>以已組好的文字建立 (例如 Unity 提供的 stackTrace); null 或空白時回傳 null.</summary>
        /// <remarks>配置: 每次呼叫配置 (本物件).</remarks>
        public static LogStackTrace FromText(string text)
            => string.IsNullOrWhiteSpace(text) ? null : new LogStackTrace(null, text.TrimEnd());

        /// <summary>堆疊文字, 每個框架一行. Capture 擷取的堆疊會去掉最上方屬於 Torben.Log 的框架.</summary>
        /// <remarks>配置: 第一次讀取時配置 (Capture 建立的才需要), 之後零配置. 多執行緒同時第一次讀取可能各轉一次, 結果相同.</remarks>
        public string Text
        {
            get
            {
                var current = text;
                if (current != null) return current;
                current = StripLogFrames(trace.ToString());
                text = current;
                return current;
            }
        }

        public override string ToString() => Text;

        // 從第一個不屬於 Log 的框架開始
        internal static string StripLogFrames(string stackText)
        {
            int start = 0;
            while (start < stackText.Length)
            {
                int end = stackText.IndexOf('\n', start);
                if (end < 0) end = stackText.Length;
                if (stackText.IndexOf(LogFramePrefix, start, end - start, System.StringComparison.Ordinal) < 0) break;
                start = end + 1;
            }
            return start >= stackText.Length ? string.Empty : stackText.Substring(start).TrimEnd();
        }
    }
}
