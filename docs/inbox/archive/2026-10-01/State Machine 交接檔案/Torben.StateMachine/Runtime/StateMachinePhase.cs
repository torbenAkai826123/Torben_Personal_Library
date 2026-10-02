#nullable enable
namespace Torben.StateMachine
{

    internal enum StateMachinePhase
    {
        Idle,
        Evaluating,
        Entering,
        Executing,
        Exiting
    }
}
