using UnityEngine;

namespace Torben.Events
{
    // 啟動時安裝 EventErrors.Handler, 把訂閱者例外與巢狀深度錯誤輸出到 Unity Console
    internal static class EventErrorReporter
    {
        // 以 = 指定: 每次進入 Play 都重設, 關閉 Domain Reload 時也不會累積
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallOnLoad() => EventErrors.Handler = Report;

        // 不經過 Log, 避免「事件 → Log → 事件」的循環可能.
        // 包成一個例外輸出, 讓訊息第一行帶有頻道名稱且只佔一則.
        // 不用 InnerException: Unity 會改以最內層例外的訊息顯示, 頻道名稱就看不到; 原例外 (含堆疊) 放在訊息後面
        internal static void Report(string channelName, System.Exception exception)
        {
            Debug.LogException(new System.Exception(
                $"[Event] Channel '{channelName}' reported an error (each subscription, and the depth limit of each channel, is reported only once).\n{exception}"));
        }
    }
}
