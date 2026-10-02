#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{

    /// <summary>結構驗證與固定 Flow 建置.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public sealed class StateFlowBuilder<TContext, TOutput>
    {
        private readonly Dictionary<StateId, IState<TContext, TOutput>> _states = new();
        private readonly Dictionary<TransitionKey, Transition<TContext>> _transitions = new();
        private StateId? _initialStateId;
        private bool _isConsumed;

        /// <summary>結構驗證與固定 Flow 建置.</summary>
        /// <remarks>配置: 僅 Flow 配置與建置時可能配置; 例外路徑配置例外物件.</remarks>
        public StateFlowBuilder<TContext, TOutput> Initial(StateId stateId)
        {
            EnsureAvailable();
            _initialStateId = stateId;
            return this;
        }

        /// <summary>結構驗證與固定 Flow 建置.</summary>
        /// <remarks>配置: 僅 Flow 配置與建置時可能配置; 例外路徑配置例外物件.</remarks>
        public StateFlowBuilder<TContext, TOutput> State(IState<TContext, TOutput> state)
        {
            EnsureAvailable();
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (!_states.TryAdd(state.Id, state))
                throw new InvalidOperationException($"Duplicate state ID '{state.Id}'.");
            return this;
        }

        /// <summary>結構驗證與固定 Flow 建置.</summary>
        /// <remarks>配置: 僅 Flow 配置與建置時可能配置; 例外路徑配置例外物件.</remarks>
        public StateFlowBuilder<TContext, TOutput> Transition(
            TransitionId id, StateId from, StateId to, params IGuard<TContext>[] guards)
        {
            EnsureAvailable();
            if (guards is null) throw new ArgumentNullException(nameof(guards));
            for (var i = 0; i < guards.Length; i++)
                if (guards[i] is null) throw new ArgumentException("Guards cannot contain null.", nameof(guards));

            var key = new TransitionKey(from, id);
            if (!_transitions.TryAdd(key, new Transition<TContext>(id, from, to, guards)))
                throw new InvalidOperationException($"Duplicate transition '{id}' from '{from}'.");
            return this;
        }

        /// <summary>結構驗證與固定 Flow 建置.</summary>
        /// <remarks>配置: 僅 Flow 配置與建置時可能配置; 例外路徑配置例外物件.</remarks>
        public IStateFlow<TContext, TOutput> Build()
        {
            EnsureAvailable();
            if (_initialStateId is not StateId initialStateId)
                throw new InvalidOperationException("Initial state ID is required.");
            if (!_states.ContainsKey(initialStateId))
                throw new InvalidOperationException($"Initial state '{initialStateId}' does not exist.");

            foreach (var transition in _transitions.Values)
            {
                if (!_states.ContainsKey(transition.From))
                    throw new InvalidOperationException($"Transition from state '{transition.From}' does not exist.");
                if (!_states.ContainsKey(transition.To))
                    throw new InvalidOperationException($"Transition to state '{transition.To}' does not exist.");
            }

            _isConsumed = true;
            return new StateFlow<TContext, TOutput>(initialStateId, _states, _transitions);
        }

        private void EnsureAvailable()
        {
            if (_isConsumed) throw new InvalidOperationException("Builder has already been consumed by Build().");
        }
    }
}
