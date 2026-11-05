using Torben.Events;
using UnityEngine;

// PlayMode 測試用的事件
public readonly struct ProbeEvent
{
    public readonly int Value;
    public ProbeEvent(int value) => Value = value;
}

// PlayMode 測試用的訂閱者: 收到事件時存取 transform, 物件被 Destroy 後會拋 MissingReferenceException
public sealed class EventProbeSubscriber : MonoBehaviour
{
    public int ReceivedCount;

    public void OnProbe(in ProbeEvent e)
    {
        transform.position = new Vector3(e.Value, 0f, 0f);
        ReceivedCount++;
    }
}
