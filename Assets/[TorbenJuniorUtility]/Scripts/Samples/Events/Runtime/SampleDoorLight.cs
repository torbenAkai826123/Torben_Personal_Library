using UnityEngine;

namespace Torben.Events.Samples
{
    /// <summary>訂閱者: 門打開時把燈點亮.</summary>
    public sealed class SampleDoorLight : MonoBehaviour
    {
        [SerializeField] Renderer lampRenderer;
        [SerializeField] Light lampLight;
        [SerializeField] Color offColor = new Color(0.25f, 0.25f, 0.25f);
        [SerializeField] Color onColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>燈是否已點亮.</summary>
        public bool IsOn { get; private set; }

        /// <summary>由場景建立工具設定參照.</summary>
        public void Configure(Renderer lampRenderer, Light lampLight)
        {
            this.lampRenderer = lampRenderer;
            this.lampLight = lampLight;
        }

        /// <summary>由 Installer 注入訂閱介面. 訂閱與生命週期綁定寫在同一行.</summary>
        public void Initialize(IEventSource<DoorOpened> doorOpened)
        {
            doorOpened.Subscribe(OnDoorOpened).BindTo(this);
        }

        void Awake() => Apply();

        void OnDoorOpened(in DoorOpened e)
        {
            IsOn = true;
            Apply();
        }

        void Apply()
        {
            if (lampRenderer != null) lampRenderer.material.color = IsOn ? onColor : offColor;
            if (lampLight != null) lampLight.enabled = IsOn;
        }
    }
}
