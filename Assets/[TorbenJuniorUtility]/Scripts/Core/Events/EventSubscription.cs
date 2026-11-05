using System.Threading;

namespace Torben.Events
{
    // 讓 handle 取消時通知所屬頻道; 頻道以泛型實作, handle 本身不需要知道事件型別
    internal interface IEventSubscriptionHost
    {
        void Unsubscribe(EventSubscription subscription);
    }

    /// <summary>
    /// 訂閱的 handle, 由 <see cref="IEventSource{T}.Subscribe"/> 回傳. Dispose 即取消訂閱; 重複 Dispose 不做事.
    /// </summary>
    public sealed class EventSubscription : System.IDisposable
    {
        // 1 = 有效, 0 = 已取消. 發布中取消時, 先把旗標設為無效再從陣列移除, 讓還沒輪到的這次呼叫被略過
        private int active = 1;
        // 1 = 已回報過例外; 每個訂閱只回報第一次
        private int errorReported;
        private IEventSubscriptionHost host;

        internal EventSubscription(IEventSubscriptionHost host)
        {
            this.host = host;
        }

        /// <summary>訂閱是否仍有效 (尚未 Dispose).</summary>
        /// <remarks>配置: 零配置.</remarks>
        public bool IsActive => Volatile.Read(ref active) == 1;

        /// <summary>取消訂閱. 可從任何執行緒呼叫; 重複呼叫不做事.</summary>
        /// <remarks>配置: 第一次呼叫時配置新的訂閱者陣列 (只剩一個訂閱者時除外); 之後零配置.</remarks>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref active, 0) == 0) return;
            var current = host;
            host = null;
            current?.Unsubscribe(this);
        }

        // 第一次呼叫回傳 true, 之後一律 false
        internal bool TryMarkErrorReported() => Interlocked.Exchange(ref errorReported, 1) == 0;
    }
}
