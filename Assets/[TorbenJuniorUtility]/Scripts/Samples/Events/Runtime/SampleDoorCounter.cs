using UnityEngine;

namespace Torben.Events.Samples
{
    /// <summary>
    /// 訂閱者: 計算開門次數, 達到上限後 Destroy 自己.
    /// 用來示範 BindTo: 物件消失時訂閱自動取消, 之後再開門也不會出錯.
    /// </summary>
    public sealed class SampleDoorCounter : MonoBehaviour
    {
        [SerializeField] int destroyAfter = 3;

        /// <summary>收到 DoorOpened 的次數.</summary>
        public int Count { get; private set; }

        /// <summary>由 Installer 注入訂閱介面. 訂閱與生命週期綁定寫在同一行.</summary>
        public void Initialize(IEventSource<DoorOpened> doorOpened)
        {
            doorOpened.Subscribe(OnDoorOpened).BindTo(this);
        }

        void OnDoorOpened(in DoorOpened e)
        {
            Count++;
            if (Count == destroyAfter)
            {
                Log.Information("[Sample] Counter reached its limit and destroys itself; BindTo cancels its subscription.");
                Destroy(gameObject);
            }
        }

        void OnGUI()
        {
            SampleEventGui.ApplyScale();
            GUI.Label(new Rect(10f, 85f, 300f, 24f), $"Door opened: {Count} / {destroyAfter} (then the counter is destroyed)");
        }
    }
}
