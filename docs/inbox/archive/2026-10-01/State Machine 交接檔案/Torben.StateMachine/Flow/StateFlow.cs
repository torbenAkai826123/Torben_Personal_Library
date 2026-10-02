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
        public bool Equals(TransitionKey other) => From == other.From && TransitionId == other.TransitionId;
        public override bool Equals(object? obj) => obj is TransitionKey other && Equals(other);
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

        public StateId InitialStateId { get; }

        public IState<TContext, TOutput> GetState(StateId id)
        {
            if (!_states.TryGetValue(id, out var state))
                throw new InvalidOperationException($"Flow has no state '{id}'.");
            return state;
        }

        public Transition<TContext>? FindTransition(StateId currentState, TransitionId transitionId)
        {
            _transitions.TryGetValue(new TransitionKey(currentState, transitionId), out var transition);
            return transition;
        }
    }
}
