#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{

    internal readonly struct TransitionKey : IEquatable<TransitionKey>
    {
        internal TransitionKey(StateId from, TransitionId transitionId)
        {
            From = from;
            TransitionId = transitionId;
        }

        internal StateId From { get; }
        internal TransitionId TransitionId { get; }
        /// <summary>固定 Flow 的內部查找.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public bool Equals(TransitionKey other) => From == other.From && TransitionId == other.TransitionId;
        /// <summary>固定 Flow 的內部查找.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public override bool Equals(object? obj) => obj is TransitionKey other && Equals(other);
        /// <summary>固定 Flow 的內部查找.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public override int GetHashCode()
        {
            unchecked { return From.GetHashCode() * 31 + TransitionId.GetHashCode(); }
        }
    }

    internal sealed class StateFlow<TContext, TOutput> : IStateFlow<TContext, TOutput>
    {
        private readonly Dictionary<StateId, IState<TContext, TOutput>> _states;
        private readonly Dictionary<TransitionKey, Transition<TContext>> _transitions;

        internal StateFlow(
            StateId initialStateId,
            Dictionary<StateId, IState<TContext, TOutput>> states,
            Dictionary<TransitionKey, Transition<TContext>> transitions)
        {
            InitialStateId = initialStateId;
            _states = new Dictionary<StateId, IState<TContext, TOutput>>(states);
            _transitions = new Dictionary<TransitionKey, Transition<TContext>>(transitions);
        }

        /// <summary>固定 Flow 的內部查找.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateId InitialStateId { get; }

        /// <summary>固定 Flow 的內部查找.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        public IState<TContext, TOutput> GetState(StateId id)
        {
            if (!_states.TryGetValue(id, out var state))
                throw new InvalidOperationException($"Flow has no state '{id}'.");
            return state;
        }

        /// <summary>固定 Flow 的內部查找.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        public Transition<TContext>? FindTransition(StateId currentState, TransitionId transitionId)
        {
            _transitions.TryGetValue(new TransitionKey(currentState, transitionId), out var transition);
            return transition;
        }
    }
}
