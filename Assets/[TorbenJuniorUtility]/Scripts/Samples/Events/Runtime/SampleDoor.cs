using UnityEngine;

namespace Torben.Events.Samples
{
    /// <summary>
    /// 發布者: 按下畫面上的 Open Door 按鈕時發布 DoorOpened.
    /// 只持有 IEventPublisher, 不知道誰在聽, 也不能訂閱.
    /// </summary>
    public sealed class SampleDoor : MonoBehaviour
    {
        [SerializeField] int doorId = 1;

        IEventPublisher<DoorOpened> doorOpened;

        /// <summary>由 Installer 注入發布介面.</summary>
        public void Initialize(IEventPublisher<DoorOpened> doorOpened)
        {
            this.doorOpened = doorOpened;
        }

        /// <summary>開門: 發布 DoorOpened.</summary>
        public void Open()
        {
            if (doorOpened == null)
            {
                Debug.LogWarning("[Sample] SampleDoor has not been initialized by SampleEventInstaller yet.", this);
                return;
            }
            doorOpened.Publish(new DoorOpened(doorId));
        }

        void OnGUI()
        {
            SampleEventGui.ApplyScale();
            if (GUI.Button(new Rect(10f, 10f, 200f, 40f), "Open Door")) Open();
        }
    }
}
