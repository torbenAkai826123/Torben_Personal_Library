namespace Torben.Events
{
    /// <summary>
    /// 訂閱者例外與巢狀深度錯誤的回報入口 (比照 Log.SinkErrorHandler).
    /// </summary>
    public static class EventErrors
    {
        /// <summary>
        /// 參數為 (頻道 DebugName, 例外). 以下情況呼叫:
        /// 訂閱者的回呼拋出例外 (每個訂閱只回報第一次);
        /// 巢狀發布超過 <see cref="EventChannel.MaxPublishDepth"/> 而略過該次 Publish (每個頻道只回報第一次).
        /// 例外不會傳回發布端; 本處理器拋出的例外會被捕捉並忽略. null 時不回報 (也不記錄為已回報).
        /// Runtime 啟動時會設定為以 UnityEngine.Debug.LogException 輸出.
        /// </summary>
        public static System.Action<string, System.Exception> Handler;

        // 目前是否設定了處理器. 沒有處理器時不記錄「已回報」, 之後設定處理器仍會收到第一次回報
        internal static bool HasHandler => Handler != null;

        // 只在錯誤時執行
        internal static void Report(string channelName, System.Exception exception)
        {
            var handler = Handler;
            if (handler == null) return;
            try { handler(channelName, exception); }
            catch (System.Exception) { }    // 回報失敗時忽略, 不傳回發布端
        }
    }
}
