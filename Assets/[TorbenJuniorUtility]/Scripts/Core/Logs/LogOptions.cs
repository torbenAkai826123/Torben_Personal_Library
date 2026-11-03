using System;

namespace Torben
{
    /// <summary>某個等級的開關; 以陣列儲存時 Inspector 會顯示等級名稱.</summary>
    [Serializable]
    public struct LogLevelSwitch
    {
        public LogLevel level;
        public bool isOn;

        public LogLevelSwitch(LogLevel level, bool isOn)
        {
            this.level = level;
            this.isOn = isOn;
        }

        /// <summary>
        /// 在 switches 中找 level 的開關; 找不到時回傳 fallback. 有重複時以第一筆為準.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool Find(LogLevelSwitch[] switches, LogLevel level, bool fallback)
        {
            if (switches == null) return fallback;
            for (int i = 0; i < switches.Length; i++)
                if (switches[i].level == level) return switches[i].isOn;
            return fallback;
        }
    }

    /// <summary>
    /// Log 的初始設定 (總開關、Debug 開關、各等級的堆疊追蹤), 由 LogSettings 在啟動時套用一次.
    /// 預設值見欄位初始值, 與 Log 未經設定時的行為相同.
    /// </summary>
    [Serializable]
    public sealed class LogOptions
    {
        /// <summary>對應 Log.isPrintLog.</summary>
        public bool isPrintLog = false;

        /// <summary>對應 Log.isDebugLog.</summary>
        public bool isDebugLog = false;

        /// <summary>
        /// 各等級是否擷取堆疊追蹤 (對應 Log.SetStackTraceEnabled). 沒有列出的等級視為關閉;
        /// 未定義的等級值會被忽略.
        /// </summary>
        public LogLevelSwitch[] stackTrace =
        {
            new LogLevelSwitch(LogLevel.Normal, false),
            new LogLevelSwitch(LogLevel.Warning, false),
            new LogLevelSwitch(LogLevel.Error, true),
            new LogLevelSwitch(LogLevel.Debug, false),
        };

        /// <summary>寫入 Log 的靜態狀態.</summary>
        /// <remarks>配置: 每次呼叫配置 (列舉等級值); 只在載入期使用.</remarks>
        public void Apply()
        {
            Log.isPrintLog = isPrintLog;
            Log.isDebugLog = isDebugLog;
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
                Log.SetStackTraceEnabled(level, LogLevelSwitch.Find(stackTrace, level, false));
        }

        /// <summary>從 Log 讀取目前狀態; stackTrace 依 LogLevel 的定義順序列出每個等級.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static LogOptions Capture()
        {
            var levels = (LogLevel[])Enum.GetValues(typeof(LogLevel));
            var switches = new LogLevelSwitch[levels.Length];
            for (int i = 0; i < levels.Length; i++)
                switches[i] = new LogLevelSwitch(levels[i], Log.IsStackTraceEnabled(levels[i]));
            return new LogOptions
            {
                isPrintLog = Log.isPrintLog,
                isDebugLog = Log.isDebugLog,
                stackTrace = switches,
            };
        }
    }
}
