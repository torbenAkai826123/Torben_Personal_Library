using UnityEngine;

namespace Torben
{
    /// <summary>
    /// 把 Log 訊息輸出到 Unity Console 的 sink, 並負責在啟動時安裝 Console 與畫面視窗兩個 sink.
    /// </summary>
    public sealed class DebugLogs : ILogSink
    {
        /// <summary>
        /// 開啟時在畫面視窗 (DebugWindow) 顯示訊息, 並停止輸出到 Console (除非 isConsoleWithGUI 開啟).
        /// 關閉時隱藏視窗, 恢復 Console 輸出.
        /// </summary>
        /// <remarks>配置: 第一次開啟視窗時建立元件; 之後切換零配置.</remarks>
        public static bool isGUILog
        {
            get => guiLog;
            set
            {
                guiLog = value;
                DebugWindow.SetVisible(value);
            }
        }
        private static bool guiLog = false;

        /// <summary>isGUILog 開啟時, 是否仍同時輸出到 Console. (預設關閉, 避免系統訊息蓋過自己的訊息)</summary>
        public static bool isConsoleWithGUI = false;

        public void Write(in LogEntry entry)
        {
            if (guiLog && !isConsoleWithGUI) return;
            string tag = string.IsNullOrEmpty(entry.Category.tagColorHex) ? entry.Category.Tag : $"<color={entry.Category.tagColorHex}>{entry.Category.Tag}</color>";
            string text = tag + entry.Message;
            switch (entry.Level)
            {
                case LogLevel.Warning: Debug.LogWarning(text); break;
                case LogLevel.Error: Debug.LogError(text); break;
                default: Debug.Log(text); break;
            }
        }

        // 遊戲啟動時 (最早的載入階段) 重設狀態, 並加入 Console 與畫面視窗的歷史紀錄兩個 sink.
        // 在這之後才寫的 Log 都會被歷史紀錄保留, 視窗晚點開啟也看得到.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            guiLog = false;
            Log.ClearSinks();   // 關閉 Domain Reload 時靜態狀態會保留, 先清掉避免重複加入
            Log.AddSink(new DebugLogs());
            Log.AddSink(DebugWindow.ResetStatics());
        }
    }

}
