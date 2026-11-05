namespace Torben.Events
{
    /// <summary>
    /// 訂閱者的回呼. 事件以 in 傳遞, struct 不會被複製.
    /// </summary>
    /// <typeparam name="T">事件型別. 建議寫成 readonly struct.</typeparam>
    public delegate void EventCallback<T>(in T e) where T : struct;

    /// <summary>
    /// 頻道「可發布」的一面: 只交給發布者. 拿到這個介面的物件只能發布, 不能訂閱.
    /// </summary>
    public interface IEventPublisher<T> where T : struct
    {
        /// <summary>同步通知目前所有訂閱者. 詳細規則見 <see cref="EventChannel{T}.Publish(in T)"/>.</summary>
        /// <remarks>配置: 零配置.</remarks>
        void Publish(in T e);
    }

    /// <summary>
    /// 頻道「可訂閱」的一面: 只交給訂閱者. 拿到這個介面的物件只能訂閱, 不能發布.
    /// </summary>
    public interface IEventSource<T> where T : struct
    {
        /// <summary>
        /// 訂閱. 回傳的 handle 是唯一的取消方式: Dispose 即取消.
        /// Unity 物件請在同一行以 BindTo(this) 綁定生命週期, 其他物件加入 <see cref="EventSubscriptionGroup"/>.
        /// </summary>
        /// <remarks>配置: 每次呼叫配置 (handle 與新的訂閱者陣列).</remarks>
        EventSubscription Subscribe(EventCallback<T> callback);

        /// <summary>目前有效的訂閱數.</summary>
        /// <remarks>配置: 零配置.</remarks>
        int SubscriberCount { get; }
    }
}
