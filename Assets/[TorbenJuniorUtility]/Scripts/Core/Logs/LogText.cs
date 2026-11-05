namespace Torben
{
    /// <summary>顯示 Log 用的文字轉換 (時間、檔案路徑、Unity 堆疊位置). 給視窗等顯示端使用, 結果應快取.</summary>
    public static class LogText
    {
        /// <summary>把 UTC 時間轉成當地時間 "HH:mm:ss.ff"; default 時回傳空字串.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string FormatTime(System.DateTime utcTime)
        {
            if (utcTime == default) return string.Empty;
            return utcTime.ToLocalTime().ToString("HH:mm:ss.ff", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 把原始碼路徑轉成顯示用的短路徑 (斜線統一為 '/'):
        /// 在 projectRoot 底下時去掉 projectRoot; 否則從第一個 "Assets/" 或 "Packages/" 開始; 都沒有時只留檔名.
        /// </summary>
        /// <param name="path">完整或相對路徑; null 或空字串時回傳空字串.</param>
        /// <param name="projectRoot">專案根目錄 (Assets 的上一層); 不知道時傳 null.</param>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string ToDisplayPath(string path, string projectRoot)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace('\\', '/');
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
                if (path.StartsWith(root, System.StringComparison.OrdinalIgnoreCase)) return path.Substring(root.Length);
            }
            if (path.StartsWith("Assets/", System.StringComparison.Ordinal) || path.StartsWith("Packages/", System.StringComparison.Ordinal)) return path;
            int index = path.IndexOf("/Assets/", System.StringComparison.Ordinal);
            if (index < 0) index = path.IndexOf("/Packages/", System.StringComparison.Ordinal);
            if (index >= 0) return path.Substring(index + 1);
            return path.Substring(path.LastIndexOf('/') + 1);
        }

        /// <summary>
        /// 從 Unity 格式的堆疊文字找出第一個有原始碼位置的框架, 例如 "Foo:Bar () (at Assets/Foo.cs:12)".
        /// 沒有位置資訊 (例如建置版的 "(at &lt;...&gt;:0)") 的框架會略過.
        /// </summary>
        /// <remarks>配置: 找到時配置路徑字串; 找不到時零配置.</remarks>
        public static bool TryParseUnityLocation(string stackTrace, out string path, out int line)
        {
            path = null;
            line = 0;
            if (string.IsNullOrEmpty(stackTrace)) return false;
            const string marker = "(at ";
            int search = 0;
            while (true)
            {
                int start = stackTrace.IndexOf(marker, search, System.StringComparison.Ordinal);
                if (start < 0) return false;
                start += marker.Length;
                int close = stackTrace.IndexOf(')', start);
                if (close < 0) return false;
                search = close + 1;
                int colon = stackTrace.LastIndexOf(':', close - 1, close - start);
                if (colon <= start || stackTrace[start] == '<') continue;
                if (!int.TryParse(stackTrace.Substring(colon + 1, close - colon - 1), out int number) || number <= 0) continue;
                path = stackTrace.Substring(start, colon - start);
                line = number;
                return true;
            }
        }

        /// <summary>
        /// 取得一筆訊息的顯示位置: 有呼叫端資訊時用它; 沒有時 (Unity 訊息) 從堆疊文字找第一個有原始碼位置的框架.
        /// file 為 ToDisplayPath 縮短後的路徑. 找不到時回傳 false, file 為 null、line 為 0.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置 (找到時).</remarks>
        public static bool TryGetLocation(in LogEntry entry, string projectRoot, out string file, out int line)
        {
            file = null;
            line = 0;
            if (entry.HasCaller)
            {
                file = ToDisplayPath(entry.CallerFilePath, projectRoot);
                line = entry.CallerLineNumber;
                return true;
            }
            if (entry.StackTrace != null && TryParseUnityLocation(entry.StackTrace.Text, out string path, out line))
            {
                file = ToDisplayPath(path, projectRoot);
                return true;
            }
            line = 0;
            return false;
        }

        /// <summary>
        /// 複製用的純文字. 第一行為 "[等級] " + 分類 Tag (空白則省略) + 訊息, count 大於 1 時行尾加 " (x次數)";
        /// 第二行為 "(時間) 路徑:行號" (沒有資料的部分省略, 都沒有時省略整行); 有堆疊追蹤時接著輸出 StackTrace.Text.
        /// 分類與訊息去掉 Unity rich text 標籤 (見 StripRichText). 換行一律為 '\n'.
        /// </summary>
        /// <param name="count">合併的次數 (Collapse); 1 表示沒有合併.</param>
        /// <param name="projectRoot">傳給 ToDisplayPath 的專案根目錄; 不知道時 (建置版) 傳 null.</param>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string FormatForCopy(in LogEntry entry, int count, string projectRoot)
        {
            var text = new System.Text.StringBuilder();
            text.Append('[').Append(entry.Level.ToString()).Append("] ");
            string tag = StripRichText(entry.Category?.Tag);
            if (!string.IsNullOrEmpty(tag)) text.Append(tag).Append(' ');
            text.Append(StripRichText(entry.Message));
            if (count > 1) text.Append(" (x").Append(count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(')');

            string time = FormatTime(entry.Time);
            bool hasLocation = TryGetLocation(entry, projectRoot, out string file, out int line);
            if (time.Length > 0 || hasLocation)
            {
                text.Append('\n');
                if (time.Length > 0) text.Append('(').Append(time).Append(')');
                if (time.Length > 0 && hasLocation) text.Append(' ');
                if (hasLocation) text.Append(file).Append(':').Append(line.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            string stack = entry.StackTrace?.Text;
            if (!string.IsNullOrEmpty(stack)) text.Append('\n').Append(stack.Replace("\r\n", "\n"));
            return text.ToString();
        }

        /// <summary>
        /// 去掉 Unity rich text 標籤: &lt;b&gt; &lt;i&gt; &lt;color=...&gt; &lt;size=...&gt; &lt;material=...&gt; 與其結束標籤, 以及 &lt;quad .../&gt;.
        /// 只去掉這些已知標籤 (區分大小寫), 其他 &lt;...&gt; (例如 List&lt;int&gt;、&lt;OnGUIFunc&gt;) 保留; 未閉合的 '&lt;' 原樣保留.
        /// null 時回傳空字串.
        /// </summary>
        /// <remarks>配置: 沒有標籤時零配置 (回傳原字串); 有標籤時每次呼叫配置.</remarks>
        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int open = text.IndexOf('<');
            if (open < 0) return text;
            System.Text.StringBuilder result = null;
            int copied = 0;     // text 中尚未複製到 result 的起點
            while (open >= 0)
            {
                int close = text.IndexOf('>', open + 1);
                if (close < 0) break;
                int nextOpen = text.IndexOf('<', open + 1, close - open - 1);
                if (nextOpen >= 0)
                {
                    open = nextOpen;    // 中間又有 '<': 前一個 '<' 視為一般文字
                    continue;
                }
                if (IsRichTextTag(text, open + 1, close - open - 1))
                {
                    if (result == null) result = new System.Text.StringBuilder(text.Length);
                    result.Append(text, copied, open - copied);
                    copied = close + 1;
                }
                open = text.IndexOf('<', close + 1);
            }
            if (result == null) return text;
            result.Append(text, copied, text.Length - copied);
            return result.ToString();
        }

        private static readonly string[] simpleTags = { "b", "/b", "i", "/i", "/color", "/size", "/material" };
        private static readonly string[] valueTags = { "color=", "size=", "material=" };

        // '<' 與 '>' 之間的內容是否為已知的 rich text 標籤
        private static bool IsRichTextTag(string text, int start, int length)
        {
            for (int i = 0; i < simpleTags.Length; i++)
                if (length == simpleTags[i].Length && string.CompareOrdinal(text, start, simpleTags[i], 0, length) == 0) return true;
            for (int i = 0; i < valueTags.Length; i++)
            {
                int nameLength = valueTags[i].Length;
                if (length > nameLength && string.CompareOrdinal(text, start, valueTags[i], 0, nameLength) == 0) return true;
            }
            // <quad .../> 或 <quad/>
            const string quad = "quad";
            return length > quad.Length && text[start + length - 1] == '/'
                && string.CompareOrdinal(text, start, quad, 0, quad.Length) == 0
                && (text[start + quad.Length] == ' ' || text[start + quad.Length] == '/');
        }
    }
}
