using System.Collections.Generic;
using Torben.StateMachine;

namespace Torben.StateMachine.Tests
{
    // 記錄掛勾呼叫順序與當下機器狀態
    public sealed class StateMachineHookProbeHost : StateMachineHost<bool, string>
    {
        public readonly List<string> Calls = new List<string>();

        protected override IStateMachine<bool, string> CreateMachine()
        {
            var idle = new IdleState();
            var flow = new StateFlowBuilder<bool, string>()
                .Initial(idle.Id).State(idle).Build();
            return new StateMachine<bool, string>(flow);
        }

        protected override bool CreateInitialContext() => false;

        protected override void OnHostAwake() => Record("awake");
        protected override void OnHostEnabled() => Record("enabled");
        protected override void OnHostDisabled() => Record("disabled");

        void Record(string hook) => Calls.Add(hook + ":" + (Machine == null ? "null" : Machine.Status.ToString()));

        sealed class IdleState : State<bool, string>
        {
            static readonly StateId idleId = new StateId("idle");
            public override StateId Id => idleId;
            protected override void OnEnter(bool context) { }
            public override void Execute() { }
        }
    }
}
