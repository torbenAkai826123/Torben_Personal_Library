#nullable enable
namespace Torben.StateMachine
{

    /// <summary>Project 提供的 Guard.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public interface IGuard<TContext>
    {
        /// <summary>Project 提供的 Guard.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        GuardResult Evaluate(TContext context);
    }
}
