#nullable enable
namespace Torben.StateMachine
{

    public interface IState<TContext, TOutput>
    {
        StateId Id { get; }
        bool IsComplete { get; }
        TOutput? Output { get; }
        void Enter(TContext context);
        void Execute();
        void Exit();
    }
}
