using System;
using Torben.StateMachine;

// Project-owned types. In Unity, a MonoBehaviour may call machine.Execute() from Update().
internal sealed class GameContext
{
    internal GameContext(bool hasKey) => HasKey = hasKey;
    internal bool HasKey { get; }
}
internal sealed class GameOutput
{
    internal GameOutput(string message) => Message = message;
    internal string Message { get; }
}

internal sealed class IdleState : State<GameContext, GameOutput>
{
    public override StateId Id => new("idle");
    protected override void OnEnter(GameContext context) { }
    public override void Execute() { }
}

internal sealed class DoorState : State<GameContext, GameOutput>
{
    public override StateId Id => new("door");
    protected override void OnEnter(GameContext context) => Complete(new GameOutput("Door opened"));
    public override void Execute() { }
}

internal sealed class HasKeyGuard : IGuard<GameContext>
{
    public GuardResult Evaluate(GameContext context) => context.HasKey ? GuardResult.Allow : GuardResult.Deny;
}

internal static class Program
{
    private static void Main()
    {
        var idle = new StateId("idle");
        var door = new StateId("door");
        var open = new TransitionId("open");

        var flow = new StateFlowBuilder<GameContext, GameOutput>()
            .Initial(idle)
            .State(new IdleState())
            .State(new DoorState())
            .Transition(open, idle, door, new HasKeyGuard())
            .Build();

        IStateMachine<GameContext, GameOutput> machine = new StateMachine<GameContext, GameOutput>(flow);
        machine.Start(new GameContext(hasKey: false));

        machine.Request(new TransitionRequest<GameContext>(open, new GameContext(hasKey: false)));
        machine.Execute(); // Guard denies; idle remains current.

        machine.Request(new TransitionRequest<GameContext>(open, new GameContext(hasKey: true)));
        machine.Execute(); // Exit idle, enter door, capture completion; door.Execute() waits until next tick.

        Console.WriteLine($"{machine.CurrentStateId?.Value}: {machine.LastResult?.Output.Message}");
        machine.Stop();
    }
}
