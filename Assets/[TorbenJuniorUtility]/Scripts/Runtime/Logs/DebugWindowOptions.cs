using UnityEngine;

namespace Torben
{
    /// <summary>DebugWindow 上可以隱藏的按鈕. 隱藏的只是按鈕, 功能仍可透過 DebugWindow 的靜態屬性控制.</summary>
    [System.Flags]
    public enum DebugWindowButtons
    {
        None = 0,
        Levels = 1 << 0,    // N/W/E/D
        Source = 1 << 1,    // Log / Unity
        Clear = 1 << 2,
        Close = 1 << 3,
        Search = 1 << 4,
        Time = 1 << 5,
        Location = 1 << 6,
        Collapse = 1 << 7,
        Copy = 1 << 8,      // 每一列的複製按鈕與工具列的 Copy
        All = ~0,
    }

    /// <summary>某個等級的訊息文字顏色; 以陣列儲存時 Inspector 會顯示等級名稱.</summary>
    [System.Serializable]
    public struct LogLevelColor
    {
        public LogLevel level;
        /// <summary>例如 "#FF6B6B"; 空字串表示使用預設文字顏色.</summary>
        public string color;

        public LogLevelColor(LogLevel level, string color)
        {
            this.level = level;
            this.color = color;
        }

        /// <summary>在 colors 中找 level 的顏色; 找不到時回傳 null. 有重複時以第一筆為準.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static string Find(LogLevelColor[] colors, LogLevel level)
        {
            if (colors == null) return null;
            for (int i = 0; i < colors.Length; i++)
                if (colors[i].level == level) return colors[i].color;
            return null;
        }
    }

    /// <summary>
    /// DebugWindow 的初始設定, 由 LogSettings 在啟動時套用一次.
    /// 預設值見欄位初始值; DebugWindow 每次進入 Play 時也以這些初始值重設.
    /// </summary>
    [System.Serializable]
    public sealed class DebugWindowOptions
    {
        /// <summary>
        /// 容量的建議上限: 超過時 Editor 狀態檢查與 Development Alert 會提醒 (每筆紀錄都會被視窗複製並組字串, 過大會拖慢繪製).
        /// </summary>
        public const int MaxRecommendedCapacity = 10000;

        // Inspector 依宣告順序顯示: bool 在上, 其他欄位在下 (序列化以欄位名稱對應, 調整順序不影響既有資料)
        /// <summary>對應 DebugWindow.IsDraggable.</summary>
        public bool isDraggable = true;

        /// <summary>對應 DebugWindow.AutoScroll.</summary>
        public bool autoScroll = true;

        /// <summary>true 時使用預設位置 (右下角), 忽略 position.</summary>
        public bool useDefaultPosition = true;

        /// <summary>對應 DebugWindow.ShowLogMessages.</summary>
        public bool showLogMessages = true;

        /// <summary>對應 DebugWindow.ShowUnityMessages.</summary>
        public bool showUnityMessages = true;

        /// <summary>對應 DebugWindow.ShowTime.</summary>
        public bool showTime = false;

        /// <summary>對應 DebugWindow.ShowLocation.</summary>
        public bool showLocation = false;

        /// <summary>對應 DebugWindow.Collapse.</summary>
        public bool collapse = false;

        /// <summary>對應 DebugWindow.SetLevelVisible; 沒有列出的等級視為顯示.</summary>
        public LogLevelSwitch[] visibleLevels =
        {
            new LogLevelSwitch(LogLevel.Normal, true),
            new LogLevelSwitch(LogLevel.Warning, true),
            new LogLevelSwitch(LogLevel.Error, true),
            new LogLevelSwitch(LogLevel.Debug, true),
        };

        /// <summary>對應 DebugWindow.VisibleButtons.</summary>
        public DebugWindowButtons visibleButtons = DebugWindowButtons.All;

        /// <summary>DebugWindow.History 的容量 (最小 1). 只在啟動 (DebugLogs.Install) 時生效, Apply 不會改變.</summary>
        [Min(1)] public int logCapacity = 200;

        /// <summary>DebugWindow.UnityHistory 的容量 (最小 1). 只在啟動 (DebugLogs.Install) 時生效, Apply 不會改變.</summary>
        [Min(1)] public int unityCapacity = 200;

        /// <summary>對應 DebugWindow.Size (沿用其最小值限制).</summary>
        public Vector2 size = new Vector2(600f, 180f);

        /// <summary>對應 DebugWindow.Position; useDefaultPosition 為 false 時才使用.</summary>
        public Vector2 position = Vector2.zero;

        /// <summary>對應 DebugWindow.Scale; 0 表示依畫面高度自動計算.</summary>
        [Min(0f)] public float scale = 0f;

        /// <summary>對應 DebugWindow.SetLevelColor; 沒有列出的等級使用預設文字顏色.</summary>
        public LogLevelColor[] levelColors =
        {
            new LogLevelColor(LogLevel.Normal, ""),
            new LogLevelColor(LogLevel.Warning, "#FFD54F"),
            new LogLevelColor(LogLevel.Error, "#FF6B6B"),
            new LogLevelColor(LogLevel.Debug, "#9E9E9E"),
        };

        /// <summary>寫入 DebugWindow 的靜態設定; 不包含容量 (容量只在啟動時生效).</summary>
        /// <remarks>配置: 每次呼叫配置 (列舉等級值); 只在載入期使用.</remarks>
        public void Apply()
        {
            DebugWindow.Size = size;
            if (useDefaultPosition) DebugWindow.ResetPosition();
            else DebugWindow.Position = position;
            DebugWindow.Scale = scale;
            DebugWindow.IsDraggable = isDraggable;
            DebugWindow.AutoScroll = autoScroll;
            foreach (LogLevel level in System.Enum.GetValues(typeof(LogLevel)))
            {
                string color = LogLevelColor.Find(levelColors, level);
                DebugWindow.SetLevelColor(level, string.IsNullOrEmpty(color) ? null : color);
                DebugWindow.SetLevelVisible(level, LogLevelSwitch.Find(visibleLevels, level, true));
            }
            DebugWindow.ShowLogMessages = showLogMessages;
            DebugWindow.ShowUnityMessages = showUnityMessages;
            DebugWindow.ShowTime = showTime;
            DebugWindow.ShowLocation = showLocation;
            DebugWindow.Collapse = collapse;
            DebugWindow.VisibleButtons = visibleButtons;
        }

        /// <summary>從 DebugWindow 讀取目前設定; 容量取目前歷史紀錄的容量.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static DebugWindowOptions Capture()
        {
            var levels = (LogLevel[])System.Enum.GetValues(typeof(LogLevel));
            var colors = new LogLevelColor[levels.Length];
            var visible = new LogLevelSwitch[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                colors[i] = new LogLevelColor(levels[i], DebugWindow.GetLevelColor(levels[i]) ?? string.Empty);
                visible[i] = new LogLevelSwitch(levels[i], DebugWindow.IsLevelVisible(levels[i]));
            }
            bool useDefaultPosition = DebugWindow.UsesDefaultPosition;
            return new DebugWindowOptions
            {
                logCapacity = DebugWindow.LogCapacity,
                unityCapacity = DebugWindow.UnityCapacity,
                size = DebugWindow.Size,
                useDefaultPosition = useDefaultPosition,
                position = useDefaultPosition ? Vector2.zero : DebugWindow.Position,
                scale = DebugWindow.Scale,
                isDraggable = DebugWindow.IsDraggable,
                autoScroll = DebugWindow.AutoScroll,
                levelColors = colors,
                visibleLevels = visible,
                showLogMessages = DebugWindow.ShowLogMessages,
                showUnityMessages = DebugWindow.ShowUnityMessages,
                showTime = DebugWindow.ShowTime,
                showLocation = DebugWindow.ShowLocation,
                collapse = DebugWindow.Collapse,
                visibleButtons = DebugWindow.VisibleButtons,
            };
        }
    }
}
