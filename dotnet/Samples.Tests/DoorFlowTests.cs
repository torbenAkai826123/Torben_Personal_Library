using NUnit.Framework;
using Torben.StateMachine;
using Torben.StateMachine.Samples;

namespace Torben.StateMachine.Tests
{
    public sealed class DoorFlowTests
    {
        [Test]
        public void GuardDenyThenAllowProducesOriginalExampleOutput()
        {
            var machine = new StateMachine<DoorContext, DoorOutput>(DoorFlow.Create());
            machine.Start(new DoorContext(false));
            machine.Request(new TransitionRequest<DoorContext>(new TransitionId("open"), new DoorContext(false)));
            machine.Execute();
            Assert.AreEqual(new StateId("idle"), machine.CurrentStateId);
            Assert.IsNull(machine.LastResult);

            machine.Request(new TransitionRequest<DoorContext>(new TransitionId("open"), new DoorContext(true)));
            machine.Execute();
            Assert.AreEqual(new StateId("door"), machine.CurrentStateId);
            Assert.AreEqual("Door opened", machine.LastResult.Value.Output.Message);
            machine.Stop();
            Assert.AreEqual(StateMachineStatus.Stopped, machine.Status);
        }

        [Test]
        public void EachFlowHasIndependentMutableStates()
        {
            var first = DoorFlow.Create();
            var second = DoorFlow.Create();
            Assert.AreNotSame(first.GetState(new StateId("idle")), second.GetState(new StateId("idle")));
            Assert.AreNotSame(first.GetState(new StateId("door")), second.GetState(new StateId("door")));
        }
    }
}
