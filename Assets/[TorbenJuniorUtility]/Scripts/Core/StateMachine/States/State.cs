#nullable enable
using System;

namespace Torben.StateMachine
{

    /// <summary>具有完成保護的 State 基底.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public abstract class State<TContext, TOutput> : IState<TContext, TOutput>
    {
        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public abstract StateId Id { get; }
        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public bool IsComplete { get; private set; }
        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public TOutput? Output { get; private set; }

        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        public void Enter(TContext context)
        {
            IsComplete = false;
            Output = default;
            OnEnter(context);
        }

        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        protected abstract void OnEnter(TContext context);
        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 每次呼叫可能配置; 轉換時列舉 Guards 及自訂 State/Guard/Flow 實作可能配置.</remarks>
        public abstract void Execute();
        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        public virtual void Exit() { }

        /// <summary>具有完成保護的 State 基底.</summary>
        /// <remarks>配置: 本實作正常路徑零配置; 例外路徑可能配置.</remarks>
        protected void Complete(TOutput output)
        {
            if (IsComplete)
                throw new InvalidOperationException($"State '{Id}' has already completed.");

            Output = output;
            IsComplete = true;
        }
    }
}
