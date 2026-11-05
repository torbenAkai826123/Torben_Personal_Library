namespace Torben.Events.Samples
{
    /// <summary>
    /// 事件: 某扇門打開了. 只描述「發生了什麼」, 不要求任何人做什麼.
    /// 寫成 readonly struct: 發布時不配置記憶體, 訂閱者也改不了內容.
    /// </summary>
    public readonly struct DoorOpened
    {
        /// <summary>打開的門的編號.</summary>
        public readonly int DoorId;

        public DoorOpened(int doorId)
        {
            DoorId = doorId;
        }
    }
}
