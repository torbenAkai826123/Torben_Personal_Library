#nullable enable
namespace Torben.StateMachine
{

    /// <summary>Guard 求值結果.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public enum GuardResult
    {
        Allow,
        Deny
    }
}
