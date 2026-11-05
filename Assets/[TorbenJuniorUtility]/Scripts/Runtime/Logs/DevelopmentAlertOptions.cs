namespace Torben
{
    /// <summary>DebugLogs.Install 發現設定有問題, 或正式版仍開著開發設定時的提醒等級.</summary>
    public enum DevelopmentAlertLevel
    {
        /// <summary>不提醒.</summary>
        Off,
        /// <summary>以 UnityEngine.Debug.LogWarning 提醒.</summary>
        Warning,
        /// <summary>以 UnityEngine.Debug.LogError 提醒 (Development Build 會自動顯示開發主控台).</summary>
        Error,
    }

    /// <summary>
    /// Development Alert 的設定: 啟動時 (DebugLogs.Install) 檢查設定檔, 有問題時依 level 以 Unity 的 Log 提醒一則.
    /// 正式版 (非 Development Build) 另外檢查仍開著的開發設定 (openWindowOnStart、isPrintLog、isDebugLog、TORBEN_LOG_DEBUG), 併入同一則.
    /// 只提醒, 不改設定; 刻意在正式版開著時把 level 設為 Off. TORBEN_LOG_DEBUG 反映 Runtime 組件編譯時的狀態 (打包成 DLL 時只反映打包當下).
    /// 只在 Project Settings > TorbenTools > Log 編輯; 不屬於執行中的狀態, Save Current State 不會寫入. 預設值見欄位初始值.
    /// </summary>
    [System.Serializable]
    public sealed class DevelopmentAlertOptions
    {
        public DevelopmentAlertLevel level = DevelopmentAlertLevel.Warning;
    }
}
