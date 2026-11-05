namespace Torben
{
    /// <summary>
    /// DebugLogs 的初始設定 (輸出目標), 由 LogSettings 在啟動時套用一次.
    /// 預設值見欄位初始值, 與 DebugLogs 未經設定時的行為相同.
    /// </summary>
    [System.Serializable]
    public sealed class DebugLogsOptions
    {
        /// <summary>啟動時是否開啟畫面視窗 (設定 DebugLogs.isGUILog). 視窗元件仍在 AfterSceneLoad 之後才建立.
        /// 舊名稱 openWindowOnStart 的資料仍可讀取. Inspector 畫在 log.isDebugLog 下方 (見 LogSettingsInspector).</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("openWindowOnStart")]
        public bool startWithWindow = false;

        /// <summary>對應 DebugLogs.isConsoleWithGUI.</summary>
        public bool isConsoleWithGUI = false;

        /// <summary>對應 DebugLogs.isUnityLog.</summary>
        public bool isUnityLog = true;

        /// <summary>寫入 DebugLogs 的靜態狀態.</summary>
        /// <remarks>配置: 零配置 (isGUILog 開啟且已過 AfterSceneLoad 時, 第一次會建立視窗元件).</remarks>
        public void Apply()
        {
            DebugLogs.isConsoleWithGUI = isConsoleWithGUI;
            DebugLogs.isUnityLog = isUnityLog;
            DebugLogs.isGUILog = startWithWindow;
        }

        /// <summary>從 DebugLogs 讀取目前狀態; startWithWindow 取目前的 isGUILog.</summary>
        /// <remarks>配置: 每次呼叫配置.</remarks>
        public static DebugLogsOptions Capture() => new DebugLogsOptions
        {
            startWithWindow = DebugLogs.isGUILog,
            isConsoleWithGUI = DebugLogs.isConsoleWithGUI,
            isUnityLog = DebugLogs.isUnityLog,
        };
    }
}
