#nullable enable
namespace Torben.StateMachine
{

    /// <summary>固定 Flow 的查找契約.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public interface IStateFlow<TContext, TOutput>
    {
        /// <summary>固定 Flow 的查找契約.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        StateId InitialStateId { get; }
        /// <summary>固定 Flow 的查找契約.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        IState<TContext, TOutput> GetState(StateId id);
        /// <summary>固定 Flow 的查找契約.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        Transition<TContext>? FindTransition(StateId currentState, TransitionId transitionId);
    }
}
