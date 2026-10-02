#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{

    public sealed class Transition<TContext>
    {
        internal Transition(TransitionId id, StateId from, StateId to, IGuard<TContext>[] guards)
        {
            Id = id;
            From = from;
            To = to;
            Guards = Array.AsReadOnly((IGuard<TContext>[])guards.Clone());
        }

        public TransitionId Id { get; }
        public StateId From { get; }
        public StateId To { get; }
        public IReadOnlyList<IGuard<TContext>> Guards { get; }
    }
}
