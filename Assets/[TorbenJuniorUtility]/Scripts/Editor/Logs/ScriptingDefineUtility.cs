using System.Collections.Generic;

namespace Torben.Editor
{
    /// <summary>
    /// Scripting Define Symbols 字串 (以分號分隔) 的處理. 純字串運算, 不讀寫 PlayerSettings.
    /// 結果一律正規化: 去掉前後空白與空項目, 移除重複 (保留第一次出現的順序), 以分號連接.
    /// </summary>
    public static class ScriptingDefineUtility
    {
        /// <summary>保留 Log.Debug 呼叫的 symbol (見 Log.Debug 的 [Conditional]).</summary>
        public const string LogDebugSymbol = "TORBEN_LOG_DEBUG";

        /// <summary>defines 中是否有 symbol (區分大小寫, 忽略前後空白).</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static bool Contains(string defines, string symbol)
        {
            symbol = symbol?.Trim();
            if (string.IsNullOrEmpty(symbol)) return false;
            return Split(defines).Contains(symbol);
        }

        /// <summary>加入 symbol 並回傳正規化後的字串; 已存在時不重複加入.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string Add(string defines, string symbol)
        {
            var list = Split(defines);
            symbol = symbol?.Trim();
            if (!string.IsNullOrEmpty(symbol) && !list.Contains(symbol)) list.Add(symbol);
            return string.Join(";", list);
        }

        /// <summary>移除 symbol (含重複的每一筆) 並回傳正規化後的字串.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string Remove(string defines, string symbol)
        {
            var list = Split(defines);
            symbol = symbol?.Trim();
            list.Remove(symbol);    // Split 已去除重複, 只會有一筆
            return string.Join(";", list);
        }

        /// <summary>依 isDefined 加入或移除 symbol.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string Set(string defines, string symbol, bool isDefined)
            => isDefined ? Add(defines, symbol) : Remove(defines, symbol);

        /// <summary>正規化 defines.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static string Normalize(string defines) => string.Join(";", Split(defines));

        private static List<string> Split(string defines)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(defines)) return list;
            foreach (var part in defines.Split(';'))
            {
                var symbol = part.Trim();
                if (symbol.Length > 0 && !list.Contains(symbol)) list.Add(symbol);
            }
            return list;
        }
    }
}
