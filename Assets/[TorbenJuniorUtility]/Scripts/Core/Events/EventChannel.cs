using System.Threading;

namespace Torben.Events
{
    /// <summary>
    /// 所有頻道共用的限制與狀態 (與泛型 <see cref="EventChannel{T}"/> 同名, 不含任何頻道實例).
    /// </summary>
    public static class EventChannel
    {
        /// <summary>
        /// 同一執行緒上巢狀發布的深度上限 (跨所有頻道計算). 已在這個深度時再呼叫 Publish,
        /// 該次 Publish 不執行, 並以 <see cref="EventErrors.Handler"/> 回報 (每個頻道一次).
        /// </summary>
        public const int MaxPublishDepth = 32;

        // 目前執行緒正在進行中的 Publish 層數. 跨頻道共用, 才能擋下「A 觸發 B, B 又觸發 A」的循環
        [System.ThreadStatic] internal static int publishDepth;
    }

    /// <summary>
    /// 一種事件的頻道. 由組裝的地方 (Composition Root) 建立, 再把
    /// <see cref="IEventPublisher{T}"/> 交給發布者、<see cref="IEventSource{T}"/> 交給訂閱者.
    /// 只用於通知「發生了什麼」, 不用於流程控制 (要求轉換用 TransitionRequest, 回報結果用 Output).
    /// </summary>
    /// <typeparam name="T">事件型別. 建議寫成 readonly struct.</typeparam>
    public sealed class EventChannel<T> : IEventPublisher<T>, IEventSource<T>, IEventSubscriptionHost where T : struct
    {
        // 訂閱者陣列: 訂閱 / 取消時複製新陣列再整個替換 (copy-on-write); 發布時只讀一次參考, 不需加鎖
        private readonly struct Entry
        {
            public readonly EventSubscription Subscription;
            public readonly EventCallback<T> Callback;

            public Entry(EventSubscription subscription, EventCallback<T> callback)
            {
                Subscription = subscription;
                Callback = callback;
            }
        }

        private static readonly Entry[] emptyEntries = new Entry[0];
        private readonly object gate = new object();
        private volatile Entry[] entries = emptyEntries;
        // 1 = 已回報過深度錯誤
        private int depthErrorReported;

        /// <summary>建立頻道.</summary>
        /// <param name="debugName">用於錯誤回報與追查訊息的名稱; null 時使用事件型別名稱.</param>
        /// <remarks>配置: 僅建立時配置.</remarks>
        public EventChannel(string debugName = null)
        {
            DebugName = debugName ?? typeof(T).Name;
        }

        /// <summary>用於錯誤回報與追查訊息的名稱.</summary>
        public string DebugName { get; }

        /// <summary>
        /// 開啟時每次 Publish 以 Log 記錄「頻道名稱 → 訂閱數」(等級 Normal, 需 Log 總開關開啟才會輸出).
        /// 不記錄事件內容. 預設值見欄位初始值.
        /// </summary>
        /// <remarks>配置: Log 關閉時零配置; Log 開啟時每次 Publish 配置訊息字串.</remarks>
        public bool IsTraceEnabled { get; set; } = false;

        /// <inheritdoc/>
        public int SubscriberCount => entries.Length;

        /// <summary>
        /// 同步通知目前所有訂閱者, 在呼叫者的執行緒上執行.
        /// <list type="bullet">
        /// <item>依訂閱順序呼叫, 但使用端不應依賴順序.</item>
        /// <item>以開始發布時的訂閱者為準: 發布中新增的訂閱這次不會被呼叫; 發布中取消、尚未輪到的訂閱也不會被呼叫.</item>
        /// <item>每個訂閱者各自捕捉例外, 交給 <see cref="EventErrors.Handler"/> (每個訂閱只回報第一次); 例外不傳回發布端.</item>
        /// <item>回呼中可以再發布 (同步執行); 巢狀深度達 <see cref="EventChannel.MaxPublishDepth"/> 時該次 Publish 不執行並回報.</item>
        /// </list>
        /// </summary>
        /// <remarks>配置: 零配置 (IsTraceEnabled 開啟且 Log 開啟時除外). 訂閱者回呼本身的配置不計.</remarks>
        public void Publish(in T e)
        {
            int depth = EventChannel.publishDepth;
            if (depth >= EventChannel.MaxPublishDepth)
            {
                ReportDepthExceeded();
                return;
            }

            var current = entries;
            if (IsTraceEnabled) Log.Format(LogLevel.Normal, "[Event] {0} -> {1} subscriber(s)", DebugName, current.Length);
            if (current.Length == 0) return;

            EventChannel.publishDepth = depth + 1;
            try
            {
                for (int i = 0; i < current.Length; i++)
                {
                    var subscription = current[i].Subscription;
                    if (!subscription.IsActive) continue;   // 發布中被取消, 尚未輪到
                    // 一個訂閱者失敗不影響其他訂閱者, 例外也不傳回發布端
                    try { current[i].Callback(in e); }
                    catch (System.Exception exception) { ReportSubscriberError(subscription, exception); }
                }
            }
            finally
            {
                EventChannel.publishDepth = depth;
            }
        }

        /// <summary>
        /// 訂閱. 同一個 callback 訂閱兩次視為兩個獨立訂閱, 會被呼叫兩次, 各自以自己的 handle 取消.
        /// 可從任何執行緒呼叫.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">callback 為 null.</exception>
        /// <remarks>配置: 每次呼叫配置 (handle 與新的訂閱者陣列).</remarks>
        public EventSubscription Subscribe(EventCallback<T> callback)
        {
            if (callback == null) throw new System.ArgumentNullException(nameof(callback));
            var subscription = new EventSubscription(this);
            lock (gate)
            {
                var current = entries;
                var next = new Entry[current.Length + 1];
                System.Array.Copy(current, next, current.Length);
                next[current.Length] = new Entry(subscription, callback);
                entries = next;
            }
            return subscription;
        }

        // 由 EventSubscription.Dispose 呼叫 (此時有效旗標已設為無效)
        void IEventSubscriptionHost.Unsubscribe(EventSubscription subscription)
        {
            lock (gate)
            {
                var current = entries;
                int index = -1;
                for (int i = 0; i < current.Length; i++)
                {
                    if (ReferenceEquals(current[i].Subscription, subscription)) { index = i; break; }
                }
                if (index < 0) return;
                var next = current.Length == 1 ? emptyEntries : new Entry[current.Length - 1];
                System.Array.Copy(current, 0, next, 0, index);
                System.Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                entries = next;
            }
        }

        // 只在例外發生時執行
        private void ReportSubscriberError(EventSubscription subscription, System.Exception exception)
        {
            if (!EventErrors.HasHandler) return;
            if (!subscription.TryMarkErrorReported()) return;
            EventErrors.Report(DebugName, exception);
        }

        // 只在超過深度時執行
        private void ReportDepthExceeded()
        {
            if (!EventErrors.HasHandler) return;
            if (Interlocked.Exchange(ref depthErrorReported, 1) != 0) return;
            EventErrors.Report(DebugName, new System.InvalidOperationException(
                $"Publish on '{DebugName}' was skipped: nested publish depth reached {EventChannel.MaxPublishDepth}. " +
                "This is probably an infinite loop of events triggering each other (later skips on this channel are not reported)."));
        }
    }
}
