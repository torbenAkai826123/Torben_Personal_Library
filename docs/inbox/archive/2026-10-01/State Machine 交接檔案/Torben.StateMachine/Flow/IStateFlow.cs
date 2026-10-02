#nullable enable
namespace Torben.StateMachine
{

    public interface IStateFlow<TContext, TOutput>
    {
        StateId InitialStateId { get; }
        IState<TContext, TOutput> GetState(StateId id);
        Transition<TContext>? FindTransition(StateId currentState, TransitionId transitionId);
    }
}
