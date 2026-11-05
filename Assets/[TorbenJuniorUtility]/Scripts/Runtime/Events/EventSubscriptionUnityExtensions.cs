using UnityEngine;

namespace Torben.Events
{
    /// <summary>把訂閱的生命週期綁到 Unity 物件.</summary>
    public static class EventSubscriptionUnityExtensions
    {
        /// <summary>
        /// 在 owner 所在的 GameObject 被 Destroy 時自動 Dispose 訂閱. 回傳原 subscription, 方便串接:
        /// <c>source.Subscribe(OnDoorOpened).BindTo(this);</c>
        /// 同一個 GameObject 多次綁定共用一個隱藏元件. 物件只是停用 (OnDisable) 時不會取消, 仍會收到事件.
        /// 從未啟用過就被 Destroy 的 GameObject 不會呼叫 OnDestroy (Unity 的行為), 這種情況訂閱不會被取消.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">subscription 為 null; 或 owner 為 null (此時先 Dispose 訂閱再拋出).</exception>
        /// <exception cref="System.InvalidOperationException">owner 已被 Destroy (此時先 Dispose 訂閱再拋出).</exception>
        /// <remarks>配置: 每次呼叫配置 (第一次在 GameObject 上加入隱藏元件, 及清單擴充).</remarks>
        public static EventSubscription BindTo(this EventSubscription subscription, Component owner)
        {
            if (subscription == null) throw new System.ArgumentNullException(nameof(subscription));
            if (ReferenceEquals(owner, null))
            {
                // 串接寫法 Subscribe(...).BindTo(owner) 拋出後呼叫端拿不到 handle, 先取消避免殘留
                subscription.Dispose();
                throw new System.ArgumentNullException(nameof(owner));
            }
            if (owner == null)
            {
                subscription.Dispose();
                throw new System.InvalidOperationException(
                    "[Event] BindTo: the owner has already been destroyed; the subscription was disposed.");
            }

            if (!owner.TryGetComponent(out EventSubscriptionBinder binder))
            {
                binder = owner.gameObject.AddComponent<EventSubscriptionBinder>();
                binder.hideFlags = HideFlags.HideInInspector;
            }
            binder.Add(subscription);
            return subscription;
        }
    }
}
