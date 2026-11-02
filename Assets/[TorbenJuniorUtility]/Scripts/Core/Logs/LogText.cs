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
    }
}
