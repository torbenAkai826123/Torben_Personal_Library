#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{

    /// <summary>固定路徑與唯讀 Guards.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public sealed class Transition<TContext>
    {
        internal Transition(TransitionId id, StateId from, StateId to, IGuard<TContext>[] guards)
        {
            Id = id;
            From = from;
            To = to;
            Guards = Array.AsReadOnly((IGuard<TContext>[])guards.Clone());
        }

        /// <summary>固定路徑與唯讀 Guards.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public TransitionId Id { get; }
        /// <summary>固定路徑與唯讀 Guards.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateId From { get; }
        /// <summary>固定路徑與唯讀 Guards.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateId To { get; }
        /// <summary>固定路徑與唯讀 Guards.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public IReadOnlyList<IGuard<TContext>> Guards { get; }
    }
}
