using System.Collections.Generic;
using UnityEngine;

namespace Torben.Events
{
    // BindTo 加在 GameObject 上的隱藏元件: 保存訂閱, 在 OnDestroy 時全部 Dispose.
    // 每個 GameObject 最多一個, 由 EventSubscriptionUnityExtensions.BindTo 建立
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class EventSubscriptionBinder : MonoBehaviour
    {
        private readonly List<EventSubscription> subscriptions = new List<EventSubscription>();

        internal int Count => subscriptions.Count;

        internal void Add(EventSubscription subscription)
        {
            // 先移除已在別處取消的訂閱, 避免反覆訂閱 / 取消時清單無限增長
            subscriptions.RemoveAll(s => !s.IsActive);
            subscriptions.Add(subscription);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < subscriptions.Count; i++) subscriptions[i].Dispose();
            subscriptions.Clear();
        }
    }
}
