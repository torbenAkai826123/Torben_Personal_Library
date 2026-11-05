using UnityEngine;

namespace Torben.Events.Samples
{
    /// <summary>訂閱者: 門打開時播放音效 (沒有設定音效時只以 Log 顯示).</summary>
    public sealed class SampleDoorSound : MonoBehaviour
    {
        [SerializeField] AudioSource audioSource;

        /// <summary>收到 DoorOpened 的次數.</summary>
        public int PlayCount { get; private set; }

        /// <summary>由 Installer 注入訂閱介面. 訂閱與生命週期綁定寫在同一行.</summary>
        public void Initialize(IEventSource<DoorOpened> doorOpened)
        {
            doorOpened.Subscribe(OnDoorOpened).BindTo(this);
        }

        void OnDoorOpened(in DoorOpened e)
        {
            PlayCount++;
            if (audioSource != null && audioSource.clip != null) audioSource.Play();
            Log.Format(LogLevel.Normal, "[Sample] Door {0} opened: play sound", e.DoorId);
        }
    }
}
