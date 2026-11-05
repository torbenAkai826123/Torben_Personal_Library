using System.Collections.Generic;

namespace Torben.Events
{
    /// <summary>
    /// 一次取消多個訂閱. 用於非 Unity 物件、Composition Root, 或「停用時不接收」的 OnEnable / OnDisable 寫法.
    /// Unity 物件只需要在 Destroy 時取消的話, 改用 BindTo(this).
    /// </summary>
    public sealed class EventSubscriptionGroup : System.IDisposable
    {
        private readonly object gate = new object();
        private List<EventSubscription> subscriptions;
        private bool disposed;

        /// <summary>
        /// 加入一個訂閱. 群組已 Dispose 時, 立即取消該訂閱 (不加入).
        /// 同一個訂閱加入兩次沒有影響 (Dispose 可重複呼叫).
        /// </summary>
        /// <exception cref="System.ArgumentNullException">subscription 為 null.</exception>
        /// <remarks>配置: 第一次加入及清單擴充時配置.</remarks>
        public void Add(EventSubscription subscription)
        {
            if (subscription == null) throw new System.ArgumentNullException(nameof(subscription));
            lock (gate)
            {
                if (!disposed)
                {
                    if (subscriptions == null) subscriptions = new List<EventSubscription>();
                    subscriptions.Add(subscription);
                    return;
                }
            }
            subscription.Dispose();
        }

        /// <summary>目前保存的訂閱數 (包含已在別處取消、但仍在群組中的訂閱). Dispose 後為 0.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public int Count
        {
            get { lock (gate) return subscriptions?.Count ?? 0; }
        }

        /// <summary>
        /// 取消群組中的全部訂閱並清空. 之後仍可呼叫 Add, 但加入的訂閱會立即被取消;
        /// 需要再次接收時請建立新的群組.
        /// </summary>
        /// <remarks>配置: 每次取消訂閱時配置新的訂閱者陣列 (見 <see cref="EventSubscription.Dispose"/>).</remarks>
        public void Dispose()
        {
            List<EventSubscription> toDispose;
            lock (gate)
            {
                disposed = true;
                toDispose = subscriptions;
                subscriptions = null;
            }
            if (toDispose == null) return;
            // 在鎖外取消, 不在持有群組的鎖時進入頻道的鎖
            for (int i = 0; i < toDispose.Count; i++) toDispose[i].Dispose();
        }
    }
}
