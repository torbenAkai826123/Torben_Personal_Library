#nullable enable
using System;

namespace Torben.StateMachine
{

    public abstract class State<TContext, TOutput> : IState<TContext, TOutput>
    {
        public abstract StateId Id { get; }
        public bool IsComplete { get; private set; }
        public TOutput? Output { get; private set; }

        public void Enter(TContext context)
        {
            IsComplete = false;
            Output = default;
            OnEnter(context);
        }

        protected abstract void OnEnter(TContext context);
        public abstract void Execute();
        public virtual void Exit() { }

        protected void Complete(TOutput output)
        {
            if (IsComplete)
                throw new InvalidOperationException($"State '{Id}' has already completed.");

            Output = output;
            IsComplete = true;
        }
    }
}
