namespace Torben
{
    /// <summary>
    /// Log 系統的初始設定檔. 預設檔放在 Resources 的 ResourcesPath, 由 DebugLogs.Install 在啟動時讀取並套用一次;
    /// 執行中的目前狀態仍是 Log / DebugLogs / DebugWindow 的靜態欄位, 熱路徑不會讀取此檔.
    /// 遊戲專案可另建覆蓋檔並由預設檔的 projectOverride 指向它, 覆蓋檔整份取代預設檔 (不做逐欄位覆蓋).
    /// 預設值見各欄位的初始值; 找不到設定檔時以 CreateInstance 的初始值運作.
    /// </summary>
    public sealed class LogSettings : UnityEngine.ScriptableObject
    {
        /// <summary>預設檔在 Resources 中的路徑 (不含副檔名). 路徑只在此定義.</summary>
        public const string ResourcesPath = "TorbenTools/LogSettings.Default";

        /// <summary>遊戲專案的覆蓋檔; 只在預設檔中有意義, 覆蓋檔自己的 projectOverride 不使用 (不支援串接).</summary>
        public LogSettings projectOverride;

        public LogOptions log = new LogOptions();
        public DebugLogsOptions output = new DebugLogsOptions();
        public DebugWindowOptions window = new DebugWindowOptions();

        /// <summary>
        /// 套用三組設定 (Log、DebugWindow、DebugLogs 依序); DebugWindow 的容量不在此套用.
        /// 欄位為 null 時 (例如舊資料) 略過該組.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置 (列舉等級值); 只在載入期使用.</remarks>
        public void Apply()
        {
            log?.Apply();
            window?.Apply();
            output?.Apply();    // 最後套用: 可能開啟視窗
        }

        /// <summary>projectOverride 不為 null 時回傳它, 否則回傳自己; 不遞迴.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public LogSettings ResolveEffective() => projectOverride != null ? projectOverride : this;
    }
}
