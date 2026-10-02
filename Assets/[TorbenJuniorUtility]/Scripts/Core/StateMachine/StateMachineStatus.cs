#nullable enable
namespace Torben.StateMachine
{

    /// <summary>機器執行狀態.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public enum StateMachineStatus
    {
        Stopped,
        Running,
        Faulted
    }
}
