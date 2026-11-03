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

        /// <summary>以已組好的文字建立 (例如 Unity 提供的 stackTrace), 並套用與 Text 相同的清理規則; null 或空白時回傳 null.</summary>
        /// <remarks>配置: 每次呼叫配置 (本物件); 文字需要清理時另外配置清理後的字串.</remarks>
        public static LogStackTrace FromText(string text)
            => string.IsNullOrWhiteSpace(text) ? null : new LogStackTrace(null, CleanFrames(text.TrimEnd()));

        /// <summary>
        /// 堆疊文字, 每個框架一行. Capture 擷取的堆疊會去掉最上方屬於 Torben.Log 的框架,
        /// 再去掉 IL / 原生偏移與沒有資訊的組件位置, 並把原始碼路徑縮短成 LogText.ToDisplayPath 的格式.
        /// </summary>
        /// <remarks>配置: 第一次讀取時配置 (Capture 建立的才需要), 之後零配置. 多執行緒同時第一次讀取可能各轉一次, 結果相同.</remarks>
        public string Text
        {
            get
            {
                var current = text;
                if (current != null) return current;
                current = ToDisplayText(trace.ToString());
                text = current;
                return current;
            }
        }

        public override string ToString() => Text;

        // Capture 的堆疊轉成顯示文字: 先去掉 Log 的框架, 再逐行清理
        internal static string ToDisplayText(string stackText) => CleanFrames(StripLogFrames(stackText));

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
    
        // 逐行清理, 每行只套用第一條符合的規則; 都沒有變更時回傳原字串 (不配置)
        // A: "方法 [0x偏移] in 路徑:行號" → "方法 in 顯示路徑:行號"
        // B: "方法 <0x偏移 + 0x偏移> 數字 in <MVID>:行號" → "方法"
        // C: "方法 [0x偏移] in <MVID>:行號" → "方法"
        // 角括號內是十六進位 (MVID) 時沒有原始碼位置, 行號只是佔位 (IL2CPP 為 1, Mono 為 0)
        // 其他 (例如 Unity 的 "(at Assets/...:68)") 原樣保留
        internal static string CleanFrames(string stackText)
        {
            System.Text.StringBuilder builder = null;
            int copied = 0;
            int start = 0;
            while (start < stackText.Length)
            {
                int newline = stackText.IndexOf('\n', start);
                int next = newline < 0 ? stackText.Length : newline + 1;
                int end = newline < 0 ? stackText.Length : newline;
                if (end > start && stackText[end - 1] == '\r') end--;

                string replaced = CleanFrame(stackText, start, end, out int cut);
                if (cut >= 0)
                {
                    if (builder == null) builder = new System.Text.StringBuilder(stackText.Length);
                    builder.Append(stackText, copied, cut - copied);
                    if (replaced != null) builder.Append(replaced);
                    copied = end;
                }
                start = next;
            }
            if (builder == null) return stackText;
            builder.Append(stackText, copied, stackText.Length - copied);
            return builder.ToString();
        }

        // 判斷一行 [start, end) 的尾段; 要改寫時 cut 為尾段起點, 回傳接在 cut 後的文字 (null 表示直接去掉), 不改寫時 cut 為 -1
        // Mono 類別庫 (Editor 與 IL2CPP 共用) 的格式為 " in {0}:{1} ", 行尾多一個空白; 改寫時一併去掉
        private static string CleanFrame(string s, int start, int end, out int cut)
        {
            cut = -1;
            while (end > start && s[end - 1] == ' ') end--;
            if (end <= start) return null;
            // 行號: 最後一個 ':' 之後全是數字 (Windows 路徑本身含 ':', 只能從行尾找)
            int colon = s.LastIndexOf(':', end - 1, end - start);
            if (colon < 0 || colon == end - 1 || !IsDigits(s, colon + 1, end)) return null;
            // 路徑: 最後一個 " in " 與行號 ':' 之間
            int inIndex = colon - start >= 4 ? s.LastIndexOf(" in ", colon - 1, colon - start, System.StringComparison.Ordinal) : -1;
            if (inIndex < 0) return null;
            int pathStart = inIndex + 4;

            if (pathStart < colon && s[pathStart] == '<')
            {
                // 角括號內不是十六進位 (例如 "<filename unknown>") 時原樣保留
                if (s[colon - 1] != '>' || !IsHex(s, pathStart + 1, colon - 1)) return null;
                // 規則 C: " in " 前面緊接 " [0x偏移]"
                int offset = FindIlOffset(s, start, inIndex);
                if (offset >= 0)
                {
                    cut = offset;
                    return null;
                }
                // 規則 B: 尾段為 " <0x偏移 + 0x偏移> 數字 in <十六進位>"
                int open = inIndex - start >= 4 ? s.LastIndexOf(" <0x", inIndex - 1, inIndex - start, System.StringComparison.Ordinal) : -1;
                if (open < 0) return null;
                int plus = s.IndexOf(" + 0x", open, inIndex - open, System.StringComparison.Ordinal);
                if (plus < 0 || !IsHex(s, open + 4, plus)) return null;
                int close = s.IndexOf("> ", plus, inIndex - plus, System.StringComparison.Ordinal);
                if (close < 0 || !IsHex(s, plus + 5, close) || !IsDigits(s, close + 2, inIndex)) return null;
                cut = open;
                return null;
            }

            // 規則 A: " in " 前面緊接 " [0x偏移]"
            if (pathStart >= colon) return null;
            int bracket = FindIlOffset(s, start, inIndex);
            if (bracket < 0) return null;
            cut = bracket;
            string path = LogText.ToDisplayPath(s.Substring(pathStart, colon - pathStart), null);
            return " in " + path + s.Substring(colon, end - colon);
        }

        private static bool IsDigits(string s, int start, int end)
        {
            if (start >= end) return false;
            for (int i = start; i < end; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        // inIndex (" in " 的位置) 前面緊接 " [0x十六進位]" 時回傳 " [0x" 的位置, 否則 -1
        private static int FindIlOffset(string s, int start, int inIndex)
        {
            if (inIndex <= start || s[inIndex - 1] != ']') return -1;
            int bracket = inIndex - start >= 4 ? s.LastIndexOf(" [0x", inIndex - 1, inIndex - start, System.StringComparison.Ordinal) : -1;
            return bracket >= 0 && IsHex(s, bracket + 4, inIndex - 1) ? bracket : -1;
        }

        private static bool IsHex(string s, int start, int end)
        {
            if (start >= end) return false;
            for (int i = start; i < end; i++)
            {
                char c = s[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }
    }
}
