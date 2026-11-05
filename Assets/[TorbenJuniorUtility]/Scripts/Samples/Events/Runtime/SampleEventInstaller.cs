using UnityEngine;

namespace Torben.Events.Samples
{
    /// <summary>
    /// 範例的 Composition Root: 建立 DoorOpened 頻道, 把「可發布」的一面交給門,
    /// 把「可訂閱」的一面交給燈、音效、計數器. 所有接線都集中在 Awake, 看這裡就知道誰在聽.
    /// </summary>
    public sealed class SampleEventInstaller : MonoBehaviour
    {
        [SerializeField] SampleDoor door;
        [SerializeField] SampleDoorLight doorLight;
        [SerializeField] SampleDoorSound doorSound;
        [SerializeField] SampleDoorCounter doorCounter;

        [Tooltip("進入 Play 時打開 Log 總開關與畫面視窗, 讓頻道的追查紀錄 (IsTraceEnabled) 顯示在視窗中.")]
        [SerializeField] bool showTraceInLogWindow = true;

        // 頻道由 Installer 持有, 不放在靜態欄位或單例
        EventChannel<DoorOpened> doorOpened;

        /// <summary>DoorOpened 頻道目前的訂閱數 (畫面上顯示, 測試也用來確認自動取消).</summary>
        public int DoorOpenedSubscriberCount => doorOpened == null ? 0 : doorOpened.SubscriberCount;

        /// <summary>由場景建立工具設定參照.</summary>
        public void Configure(SampleDoor door, SampleDoorLight doorLight, SampleDoorSound doorSound, SampleDoorCounter doorCounter)
        {
            this.door = door;
            this.doorLight = doorLight;
            this.doorSound = doorSound;
            this.doorCounter = doorCounter;
        }

        void Awake()
        {
            if (showTraceInLogWindow)
            {
                Log.isPrintLog = true;
                DebugLogs.isGUILog = true;
            }

            // 1. 建立頻道. IsTraceEnabled 會在每次發布時記錄「頻道名稱 -> 訂閱數」
            doorOpened = new EventChannel<DoorOpened>("DoorOpened") { IsTraceEnabled = true };

            // 2. 發布者只拿到 IEventPublisher<DoorOpened>
            door.Initialize(doorOpened);

            // 3. 訂閱者只拿到 IEventSource<DoorOpened>
            doorLight.Initialize(doorOpened);
            doorSound.Initialize(doorOpened);
            doorCounter.Initialize(doorOpened);

            // 練習: 在這裡加入你的訂閱者
        }

        void OnGUI()
        {
            SampleEventGui.ApplyScale();
            GUI.Label(new Rect(10f, 60f, 300f, 24f), $"DoorOpened subscribers: {DoorOpenedSubscriberCount}");
        }
    }
}
