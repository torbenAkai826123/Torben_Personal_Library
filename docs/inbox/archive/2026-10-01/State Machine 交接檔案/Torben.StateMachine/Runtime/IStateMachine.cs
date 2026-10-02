#nullable enable
namespace Torben.StateMachine
{

    public interface IStateMachine<TContext, TOutput>
    {
        StateMachineStatus Status { get; }
        StateId? CurrentStateId { get; }
        long? CurrentSerialNumber { get; }
        StateResult<TOutput>? LastResult { get; }
        void Start(TContext initialContext);
        void Execute();
        void Stop();
        RequestResult Request(TransitionRequest<TContext> request);
    }
}
