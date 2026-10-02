#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{
    /// <summary>路徑意圖與下一個 State 的 Context.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public readonly struct TransitionRequest<TContext> : IEquatable<TransitionRequest<TContext>>
    {
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 本實作正常路徑零配置; 例外路徑可能配置.</remarks>
        public TransitionRequest(TransitionId transitionId, TContext context)
        {
            TransitionId = transitionId;
            Context = context;
        }

        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public TransitionId TransitionId { get; }
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public TContext Context { get; }

        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public bool Equals(TransitionRequest<TContext> other) =>
            TransitionId == other.TransitionId && EqualityComparer<TContext>.Default.Equals(Context, other.Context);
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public override bool Equals(object? obj) => obj is TransitionRequest<TContext> other && Equals(other);
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                return TransitionId.GetHashCode() * 31 +
                    (Context is null ? 0 : EqualityComparer<TContext>.Default.GetHashCode(Context));
            }
        }
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public static bool operator ==(TransitionRequest<TContext> left, TransitionRequest<TContext> right) => left.Equals(right);
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public static bool operator !=(TransitionRequest<TContext> left, TransitionRequest<TContext> right) => !left.Equals(right);
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public void Deconstruct(out TransitionId transitionId, out TContext context)
        {
            transitionId = TransitionId;
            context = Context;
        }
        /// <summary>路徑意圖與下一個 State 的 Context.</summary>
        /// <remarks>配置: 每次呼叫配置字串; 泛型值的字串表示可能另有配置.</remarks>
        public override string ToString() => $"TransitionRequest {{ TransitionId = {TransitionId}, Context = {Context} }}";
    }
}
