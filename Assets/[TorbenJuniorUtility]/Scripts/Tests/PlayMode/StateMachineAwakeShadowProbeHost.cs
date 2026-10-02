using Torben.StateMachine;

namespace Torben.StateMachine.Tests
{
    // 刻意遮蔽基底 Awake, 驗證 OnEnable 會補建機器
    public sealed class StateMachineAwakeShadowProbeHost : StateMachineHost<bool, string>
    {
        public bool ShadowAwakeCalled { get; private set; }

        new void Awake() => ShadowAwakeCalled = true;

        protected override IStateMachine<bool, string> CreateMachine()
        {
            var idle = new IdleState();
            var flow = new StateFlowBuilder<bool, string>()
                .Initial(idle.Id).State(idle).Build();
            return new StateMachine<bool, string>(flow);
        }

        protected override bool CreateInitialContext() => false;

        sealed class IdleState : State<bool, string>
        {
            static readonly StateId idleId = new StateId("idle");
            public override StateId Id => idleId;
            protected override void OnEnter(bool context) { }
            public override void Execute() { }
        }
    }
}
