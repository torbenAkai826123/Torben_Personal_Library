namespace Torben
{
    /// <summary>
    /// Unity 引擎訊息 (未捕捉的例外、引擎警告、直接呼叫 UnityEngine.Debug.Log 的訊息) 的分類, 標籤為 "[Unity]".
    /// 由 DebugLogs 收集到 DebugWindow.UnityHistory; 可修改 tagColorHex 調整標籤顏色.
    /// </summary>
    public sealed class UnityLogCategory : LogCategory
    {
        public static readonly UnityLogCategory Instance = new UnityLogCategory();
        private UnityLogCategory() : base("Unity") { }
    }
}
