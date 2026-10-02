#nullable enable
namespace Torben.StateMachine
{

    /// <summary>單一 Pending slot 的接受結果.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public enum RequestResult
    {
        Accepted,
        Replaced,
        Ignored
    }
}
