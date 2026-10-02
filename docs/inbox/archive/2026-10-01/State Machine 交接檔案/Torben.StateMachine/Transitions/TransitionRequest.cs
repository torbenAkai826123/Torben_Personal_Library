#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{
    public readonly struct TransitionRequest<TContext> : IEquatable<TransitionRequest<TContext>>
    {
        public TransitionRequest(TransitionId transitionId, TContext context)
        {
            TransitionId = transitionId;
            Context = context;
        }

        public TransitionId TransitionId { get; }
        public TContext Context { get; }

        public bool Equals(TransitionRequest<TContext> other) =>
            TransitionId == other.TransitionId && EqualityComparer<TContext>.Default.Equals(Context, other.Context);
        public override bool Equals(object? obj) => obj is TransitionRequest<TContext> other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return TransitionId.GetHashCode() * 31 +
                    (Context is null ? 0 : EqualityComparer<TContext>.Default.GetHashCode(Context));
            }
        }
        public static bool operator ==(TransitionRequest<TContext> left, TransitionRequest<TContext> right) => left.Equals(right);
        public static bool operator !=(TransitionRequest<TContext> left, TransitionRequest<TContext> right) => !left.Equals(right);
        public void Deconstruct(out TransitionId transitionId, out TContext context)
        {
            transitionId = TransitionId;
            context = Context;
        }
        public override string ToString() => $"TransitionRequest {{ TransitionId = {TransitionId}, Context = {Context} }}";
    }
}
