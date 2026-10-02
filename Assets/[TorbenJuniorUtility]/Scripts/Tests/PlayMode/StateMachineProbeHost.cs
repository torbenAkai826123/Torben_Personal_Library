using Torben.StateMachine;

namespace Torben.StateMachine.Tests
{
    public sealed class StateMachineProbeHost : StateMachineHost<bool, string>
    {
        ProbeState _initial;
        ProbeState _next;
        ProbeGuard _guard;

        public int InitialEnterCount => _initial.EnterCount;
        public int InitialExecuteCount => _initial.ExecuteCount;
        public int InitialExitCount => _initial.ExitCount;
        public int NextExecuteCount => _next.ExecuteCount;
        public int NextExitCount => _next.ExitCount;
        public int GuardCount => _guard.Count;
        public bool ThrowOnExecute { get; set; }

        protected override IStateMachine<bool, string> CreateMachine()
        {
            _initial = new ProbeState(new StateId("initial"), false, this);
            _next = new ProbeState(new StateId("next"), true, this);
            _guard = new ProbeGuard();
            var flow = new StateFlowBuilder<bool, string>()
                .Initial(_initial.Id).State(_initial).State(_next)
                .Transition(new TransitionId("go"), _initial.Id, _next.Id, _guard).Build();
            return new StateMachine<bool, string>(flow);
        }

        protected override bool CreateInitialContext() => false;

        sealed class ProbeState : State<bool, string>
        {
            readonly StateId _id;
            readonly bool _completeOnEnter;
            readonly StateMachineProbeHost _host;
            internal int EnterCount;
            internal int ExecuteCount;
            internal int ExitCount;
            internal ProbeState(StateId id, bool completeOnEnter, StateMachineProbeHost host)
            {
                _id = id;
                _completeOnEnter = completeOnEnter;
                _host = host;
            }
            public override StateId Id => _id;
            protected override void OnEnter(bool context)
            {
                EnterCount++;
                if (_completeOnEnter) Complete("done");
            }
            public override void Execute()
            {
                ExecuteCount++;
                if (_host.ThrowOnExecute)
                    throw new System.InvalidOperationException("Expected host fault.");
            }
            public override void Exit() => ExitCount++;
        }

        sealed class ProbeGuard : IGuard<bool>
        {
            internal int Count;
            public GuardResult Evaluate(bool context)
            {
                Count++;
                return context ? GuardResult.Allow : GuardResult.Deny;
            }
        }
    }
}
