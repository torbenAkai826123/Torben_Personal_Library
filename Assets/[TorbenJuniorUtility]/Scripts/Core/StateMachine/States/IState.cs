#nullable enable
namespace Torben.StateMachine
{

    /// <summary>Project 提供的 State lifecycle.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public interface IState<TContext, TOutput>
    {
        /// <summary>Project 提供的 State lifecycle.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        StateId Id { get; }
        /// <summary>Project 提供的 State lifecycle.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        bool IsComplete { get; }
        /// <summary>Project 提供的 State lifecycle.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        TOutput? Output { get; }
        /// <summary>Project 提供的 State lifecycle.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        void Enter(TContext context);
        /// <summary>Project 提供的 State lifecycle.</summary>
        /// <remarks>配置: 每次呼叫可能配置; 轉換時列舉 Guards 及自訂 State/Guard/Flow 實作可能配置.</remarks>
        void Execute();
        /// <summary>Project 提供的 State lifecycle.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        void Exit();
    }
}
