#nullable enable
using NUnit.Framework;
using System.Collections.Generic;
using Torben.StateMachine;

namespace Torben.StateMachine.Tests
{
    public sealed class StateMachineContractTests
    {
        private static readonly StateId stateA = new("A");
        private static readonly StateId stateB = new("B");
        private static readonly StateId stateC = new("C");
        private static readonly TransitionId go = new("Go");

        private static (StateMachine<int, string> machine, ProbeState a, ProbeState b) Pair(params IGuard<int>[] guards)
        {
            var a = new ProbeState(stateA);
            var b = new ProbeState(stateB);
            var flow = new StateFlowBuilder<int, string>()
                .Initial(stateA).State(a).State(b).Transition(go, stateA, stateB, guards).Build();
            return (new StateMachine<int, string>(flow), a, b);
        }

        private static (StateMachine<int, string> machine, ProbeState a) Self()
        {
            var a = new ProbeState(stateA);
            var flow = new StateFlowBuilder<int, string>()
                .Initial(stateA).State(a).Transition(go, stateA, stateA).Build();
            return (new StateMachine<int, string>(flow), a);
        }

        private static TransitionRequest<int> Request(TransitionId? id = null, int context = 7) => new(id ?? go, context);
        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new System.Exception($"Expected {expected}; got {actual}.");
        }
        private static void True(bool value) { if (!value) throw new System.Exception("Expected true."); }
        private static void Same(object expected, object actual) { if (!ReferenceEquals(expected, actual)) throw new System.Exception("Expected same object."); }
        private static T Throws<T>(System.Action action) where T : System.Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new System.Exception($"Expected {typeof(T).Name}.");
        }

        [Test]
        public void NormalTransition_ExitsOldAndEntersNext()
        {
            var (machine, a, b) = Pair();
            machine.Start(1);
            Equal(RequestResult.Accepted, machine.Request(Request(context: 42)));
            machine.Execute();
            Equal(1, a.ExitCount); Equal(1, b.EnterCount); Equal(42, b.LastContext);
            Equal((StateId?)stateB, machine.CurrentStateId);
            Equal((long?)2, machine.CurrentSerialNumber);
        }
        [Test]
        public void Transition_DoesNotExecuteNextStateInSameTick()
        {
            var (machine, _, b) = Pair(); machine.Start(1); machine.Request(Request()); machine.Execute();
            Equal(0, b.ExecuteCount); machine.Execute(); Equal(1, b.ExecuteCount);
        }
        [Test]
        public void InvalidRequest_DoesNotExitCurrentState()
        {
            var (machine, a, b) = Pair(); machine.Start(1);
            machine.Request(Request(new TransitionId("missing"))); machine.Execute();
            Equal(0, a.ExitCount); Equal(0, a.ExecuteCount); Equal(0, b.EnterCount);
            Equal((long?)1, machine.CurrentSerialNumber); Equal(StateMachineStatus.Running, machine.Status);
            machine.Execute(); Equal(1, a.ExecuteCount);
        }
        [Test]
        public void GuardDeny_DoesNotExitCurrentState()
        {
            var (machine, a, b) = Pair(new ProbeGuard(GuardResult.Deny)); machine.Start(1);
            machine.Request(Request()); machine.Execute(); Equal(0, a.ExitCount); Equal(0, b.EnterCount);
            Equal(StateMachineStatus.Running, machine.Status);
        }
        [Test]
        public void GuardDeny_DoesNotChangeSerialNumber()
        {
            var (machine, _, _) = Pair(new ProbeGuard(GuardResult.Deny)); machine.Start(1);
            machine.Request(Request()); machine.Execute(); Equal((long?)1, machine.CurrentSerialNumber);
        }
        [Test]
        public void Guards_UseAndShortCircuit()
        {
            var first = new ProbeGuard(GuardResult.Deny); var second = new ProbeGuard(GuardResult.Allow);
            var (machine, _, _) = Pair(first, second); machine.Start(1); machine.Request(Request()); machine.Execute();
            Equal(1, first.Count); Equal(0, second.Count);
        }
        [Test]
        public void Request_ReplacesOnlyPendingSlot()
        {
            var (machine, a, b) = Pair(); machine.Start(1);
            Equal(RequestResult.Accepted, machine.Request(Request(new TransitionId("missing"))));
            Equal(RequestResult.Replaced, machine.Request(Request(context: 99)));
            machine.Execute(); Equal(1, a.ExitCount); Equal(99, b.LastContext);
        }
        [Test]
        public void Request_DuringProcessing_RemainsPendingForNextTick()
        {
            var guard = new CallbackGuard(); var (machine, a, b) = Pair(guard);
            guard.Callback = () => Equal(RequestResult.Accepted, machine.Request(Request(new TransitionId("missing"))));
            machine.Start(1); machine.Request(Request()); machine.Execute(); Equal((StateId?)stateB, machine.CurrentStateId);
            machine.Execute(); Equal(0, b.ExecuteCount); machine.Execute(); Equal(1, b.ExecuteCount);
        }
        [Test]
        public void Request_WhenStopped_IsIgnored()
        {
            var (machine, _, _) = Pair(); Equal(RequestResult.Ignored, machine.Request(Request()));
        }
        [Test]
        public void Complete_UpdatesLastResult()
        {
            var (machine, a, _) = Pair(); a.OnExecute = () => a.Finish("done"); machine.Start(1); machine.Execute();
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
        }
        [Test]
        public void Stop_PreservesLastResult()
        {
            var (machine, a, _) = Pair(); a.OnExecute = () => a.Finish("done");
            machine.Start(1); machine.Execute(); machine.Stop();
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
        }
        [Test]
        public void Fault_PreservesLastResult()
        {
            var (machine, a, b) = Pair(); a.OnExecute = () => a.Finish("done");
            b.OnEnterAction = _ => throw new TestException();
            machine.Start(1); machine.Execute(); machine.Request(Request());
            Throws<TestException>(() => machine.Execute());
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
            Equal((StateId?)stateA, machine.CurrentStateId);
        }
        [Test]
        public void CompletedState_CanRemainCurrent()
        {
            var (machine, a, _) = Pair(); a.OnExecute = () => { if (!a.IsComplete) a.Finish("done"); };
            machine.Start(1); machine.Execute(); machine.Execute();
            Equal((StateId?)stateA, machine.CurrentStateId); Equal(2, a.ExecuteCount);
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
        }
        [Test]
        public void Complete_InEnter_IsCapturedAfterCommit()
        {
            var (machine, a, _) = Pair(); a.OnEnterAction = _ => a.Finish("entered"); machine.Start(1);
            Equal(new StateResult<string>(stateA, 1, "entered"), machine.LastResult!.Value);
        }
        [Test]
        public void InterruptedState_DoesNotOverwriteLastResult()
        {
            var (machine, a, b) = Pair(); a.OnExecute = () => a.Finish("done");
            machine.Start(1); machine.Execute(); machine.Request(Request()); machine.Execute();
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
            True(!b.IsComplete);
        }
        [Test]
        public void Stop_DoesNotCompleteCurrentState()
        {
            var (machine, a, _) = Pair(); machine.Start(1); machine.Stop();
            True(!a.IsComplete); Equal(null, machine.LastResult);
            Equal(null, machine.CurrentStateId); Equal(null, machine.CurrentSerialNumber);
        }
        [Test]
        public void Stop_DoesNotCaptureCompletionDuringExit()
        {
            var (machine, a, _) = Pair(); a.OnExit = () => a.Finish("exit");
            machine.Start(1); machine.Stop();
            True(a.IsComplete); Equal(null, machine.LastResult);
        }
        [Test]
        public void Transition_DoesNotCaptureCompletionDuringExit()
        {
            var (machine, a, _) = Pair(); a.OnExit = () => a.Finish("exit");
            machine.Start(1); machine.Request(Request()); machine.Execute();
            True(a.IsComplete); Equal(null, machine.LastResult);
            Equal((StateId?)stateB, machine.CurrentStateId);
        }
        [Test]
        public void Transition_ExitCompletion_DoesNotOverwritePreviousResult()
        {
            var a = new ProbeState(stateA); var b = new ProbeState(stateB);
            var flow = new StateFlowBuilder<int, string>()
                .Initial(stateA).State(a).State(b).Transition(go, stateA, stateB).Transition(go, stateB, stateA).Build();
            var machine = new StateMachine<int, string>(flow);
            a.OnExecute = () => a.Finish("done"); b.OnExit = () => b.Finish("exit");
            machine.Start(1); machine.Execute();
            machine.Request(Request()); machine.Execute();
            machine.Request(Request()); machine.Execute();
            True(b.IsComplete); Equal((StateId?)stateA, machine.CurrentStateId);
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
        }
        [Test]
        public void Start_CompleteInEnterThenThrow_IsNotCaptured()
        {
            var (machine, a, _) = Pair(); a.OnEnterAction = _ => { a.Finish("entered"); throw new TestException(); };
            Throws<TestException>(() => machine.Start(1));
            Equal(null, machine.LastResult); Equal(StateMachineStatus.Faulted, machine.Status);
        }
        [Test]
        public void Transition_CompleteInEnterThenThrow_IsNotCaptured()
        {
            var (machine, a, b) = Pair(); a.OnExecute = () => a.Finish("done");
            b.OnEnterAction = _ => { b.Finish("entered"); throw new TestException(); };
            machine.Start(1); machine.Execute(); machine.Request(Request());
            Throws<TestException>(() => machine.Execute());
            Equal(new StateResult<string>(stateA, 1, "done"), machine.LastResult!.Value);
            Equal((StateId?)stateA, machine.CurrentStateId); Equal(StateMachineStatus.Faulted, machine.Status);
        }
        [Test]
        public void CompleteInExecuteThenThrow_IsNotCaptured()
        {
            var (machine, a, _) = Pair(); a.OnExecute = () => { a.Finish("done"); throw new TestException(); };
            machine.Start(1); Throws<TestException>(() => machine.Execute());
            True(a.IsComplete); Equal(null, machine.LastResult); Equal(StateMachineStatus.Faulted, machine.Status);
        }
        [Test]
        public void SelfReentry_ExitCompletionIsNotCaptured()
        {
            var (machine, a) = Self(); a.OnExit = () => a.Finish("exit");
            machine.Start(1); machine.Request(Request()); machine.Execute();
            Equal(null, machine.LastResult); Equal((long?)2, machine.CurrentSerialNumber);
            True(!a.IsComplete);
        }
        [Test]
        public void SelfReentry_EnterCompletionUsesNewSerial()
        {
            var (machine, a) = Self(); a.OnExecute = () => a.Finish("first");
            machine.Start(1); machine.Execute(); a.OnExecute = null;
            Equal(new StateResult<string>(stateA, 1, "first"), machine.LastResult!.Value);
            a.OnEnterAction = _ => a.Finish("second");
            machine.Request(Request()); machine.Execute();
            Equal(new StateResult<string>(stateA, 2, "second"), machine.LastResult!.Value);
        }
        [Test]
        public void SelfReentry_WithoutCompletion_KeepsPreviousActivationResult()
        {
            var (machine, a) = Self(); a.OnExecute = () => { if (!a.IsComplete) a.Finish("first"); };
            machine.Start(1); machine.Execute(); machine.Execute();
            Equal(new StateResult<string>(stateA, 1, "first"), machine.LastResult!.Value);
            a.OnExecute = null; machine.Request(Request()); machine.Execute(); machine.Execute();
            Equal((long?)2, machine.CurrentSerialNumber); True(!a.IsComplete);
            Equal(new StateResult<string>(stateA, 1, "first"), machine.LastResult!.Value);
        }
        [Test]
        public void Stop_WhenAlreadyStopped_DoesNothing()
        {
            var (machine, a, _) = Pair(); machine.Stop(); Equal(0, a.ExitCount);
            machine.Start(1); machine.Stop(); machine.Stop(); Equal(1, a.ExitCount);
        }
        [Test]
        public void Stop_CallsExitOnlyOnce() => Stop_WhenAlreadyStopped_DoesNothing();
        [Test]
        public void Start_ClearsLastResult()
        {
            var (machine, a, _) = Pair(); a.OnExecute = () => a.Finish("done");
            machine.Start(1); machine.Execute(); machine.Stop(); a.OnExecute = null;
            machine.Start(2); Equal(null, machine.LastResult);
        }
        [Test]
        public void SerialNumber_DoesNotResetAcrossStopAndStart()
        {
            var (machine, _, _) = Pair(); machine.Start(1); machine.Stop(); machine.Start(2);
            Equal((long?)2, machine.CurrentSerialNumber);
        }
        [Test]
        public void SerialNumber_IncrementsOnlyAfterSuccessfulEnter()
        {
            var (machine, _, b) = Pair(); machine.Start(1); b.OnEnterAction = _ => throw new TestException();
            machine.Request(Request()); Throws<TestException>(() => machine.Execute());
            machine.Stop(); b.OnEnterAction = null; machine.Start(1);
            Equal((long?)2, machine.CurrentSerialNumber);
        }
        [Test]
        public void EnterFailure_DoesNotCommitNextState()
        {
            var (machine, a, b) = Pair(); machine.Start(1); b.OnEnterAction = _ => throw new TestException();
            machine.Request(Request()); Throws<TestException>(() => machine.Execute());
            Equal((StateId?)stateA, machine.CurrentStateId); Equal((long?)1, machine.CurrentSerialNumber);
            Equal(1, a.ExitCount); Equal(StateMachineStatus.Faulted, machine.Status);
        }
        [Test]
        public void EnterFailure_DoesNotConsumeSerialNumber() => SerialNumber_IncrementsOnlyAfterSuccessfulEnter();
        [Test]
        public void InitialEnterFailure_LeavesCurrentNull()
        {
            var (machine, a, _) = Pair(); a.OnEnterAction = _ => throw new TestException();
            Throws<TestException>(() => machine.Start(1)); Equal(null, machine.CurrentStateId);
            Equal(null, machine.CurrentSerialNumber); Equal(null, machine.LastResult);
            Equal(StateMachineStatus.Faulted, machine.Status);
            machine.Stop(); a.OnEnterAction = null; machine.Start(1); Equal((long?)1, machine.CurrentSerialNumber);
        }
        [Test]
        public void ExecuteException_FaultsMachine()
        {
            var (machine, a, _) = Pair(); var error = new TestException(); a.OnExecute = () => throw error;
            machine.Start(1); Same(error, Throws<TestException>(() => machine.Execute()));
            Equal(StateMachineStatus.Faulted, machine.Status); Equal(0, a.ExitCount);
            Throws<System.InvalidOperationException>(() => machine.Execute());
        }
        [Test]
        public void GuardException_FaultsMachine()
        {
            var error = new TestException(); var guard = new ThrowingGuard(error);
            var (machine, a, _) = Pair(guard); machine.Start(1); machine.Request(Request());
            Same(error, Throws<TestException>(() => machine.Execute()));
            Equal(StateMachineStatus.Faulted, machine.Status); Equal(0, a.ExitCount);
            Equal((long?)1, machine.CurrentSerialNumber);
        }
        [Test]
        public void ExitException_FaultsMachine()
        {
            var (machine, a, b) = Pair(); var error = new TestException(); a.OnExit = () => throw error;
            machine.Start(1); machine.Request(Request()); Same(error, Throws<TestException>(() => machine.Execute()));
            Equal((StateId?)stateA, machine.CurrentStateId); Equal((long?)1, machine.CurrentSerialNumber);
            Equal(0, b.EnterCount); Equal(StateMachineStatus.Faulted, machine.Status);
        }
        [Test]
        public void EnterException_FaultsMachine() => EnterFailure_DoesNotCommitNextState();
        [Test]
        public void FaultedStop_DoesNotCallExitAgain()
        {
            var (machine, a, _) = Pair(); a.OnExit = () => throw new TestException();
            machine.Start(1); Throws<TestException>(() => machine.Stop());
            machine.Stop(); Equal(1, a.ExitCount); Equal(StateMachineStatus.Stopped, machine.Status);
            Equal(null, machine.CurrentStateId); Equal(null, machine.CurrentSerialNumber);
        }
        [Test]
        public void FaultedRequest_IsIgnored()
        {
            var (machine, a, _) = Pair(); a.OnExecute = () => throw new TestException();
            machine.Start(1); Throws<TestException>(() => machine.Execute());
            Equal(RequestResult.Ignored, machine.Request(Request()));
        }
        [Test]
        public void BrokenFlowInvariant_FaultsMachine()
        {
            var a = new ProbeState(stateA); var machine = new StateMachine<int, string>(new BrokenFlow(a));
            machine.Start(1); machine.Request(Request()); Throws<System.InvalidOperationException>(() => machine.Execute());
            Equal(StateMachineStatus.Faulted, machine.Status); Equal(0, a.ExitCount);
        }
        [Test]
        public void Builder_RejectsDuplicateStateId()
        {
            var builder = new StateFlowBuilder<int, string>().State(new ProbeState(stateA));
            Throws<System.InvalidOperationException>(() => builder.State(new ProbeState(stateA)));
        }
        [Test]
        public void Builder_RejectsDuplicateTransitionKey()
        {
            var builder = new StateFlowBuilder<int, string>().Transition(go, stateA, stateB);
            Throws<System.InvalidOperationException>(() => builder.Transition(go, stateA, stateC));
        }
        [Test]
        public void Builder_RejectsMissingInitialState()
        {
            var builder = new StateFlowBuilder<int, string>();
            Throws<System.InvalidOperationException>(() => builder.Build());
            builder.Initial(stateA); Throws<System.InvalidOperationException>(() => builder.Build());
        }
        [Test]
        public void Builder_RejectsMissingFromState()
        {
            var builder = new StateFlowBuilder<int, string>().Initial(stateA).State(new ProbeState(stateA))
                .Transition(go, stateB, stateA);
            Throws<System.InvalidOperationException>(() => builder.Build());
        }
        [Test]
        public void Builder_RejectsMissingToState()
        {
            var builder = new StateFlowBuilder<int, string>().Initial(stateA).State(new ProbeState(stateA))
                .Transition(go, stateA, stateB);
            Throws<System.InvalidOperationException>(() => builder.Build());
        }
        [Test]
        public void Builder_CannotBeUsedAfterBuild()
        {
            var builder = new StateFlowBuilder<int, string>().Initial(stateA).State(new ProbeState(stateA)); builder.Build();
            Throws<System.InvalidOperationException>(() => builder.Initial(stateB));
            Throws<System.InvalidOperationException>(() => builder.State(new ProbeState(stateB)));
            Throws<System.InvalidOperationException>(() => builder.Transition(go, stateA, stateA));
            Throws<System.InvalidOperationException>(() => builder.Build());
        }
        [Test]
        public void Builder_FlowIsFixedAfterBuild()
        {
            var builder = new StateFlowBuilder<int, string>().Initial(stateA).State(new ProbeState(stateA));
            var flow = builder.Build(); Equal(stateA, flow.InitialStateId);
            Throws<System.InvalidOperationException>(() => flow.GetState(stateB));
        }
        [Test]
        public void Builder_AllowsSameTransitionIdFromDifferentStates()
        {
            var flow = new StateFlowBuilder<int, string>()
                .Initial(stateA).State(new ProbeState(stateA)).State(new ProbeState(stateB))
                .Transition(go, stateA, stateB).Transition(go, stateB, stateA).Build();
            Equal(stateB, flow.FindTransition(stateA, go)!.To);
            Equal(stateA, flow.FindTransition(stateB, go)!.To);
        }
        [Test]
        public void Start_RejectsRunningAndFaulted()
        {
            var (machine, a, _) = Pair(); machine.Start(1);
            Throws<System.InvalidOperationException>(() => machine.Start(2));
            a.OnExecute = () => throw new TestException(); Throws<TestException>(() => machine.Execute());
            Throws<System.InvalidOperationException>(() => machine.Start(3));
        }
        [Test]
        public void ValueTypes_PreserveIdentityAndDeconstruction()
        {
            var first = new StateId("same"); var second = new StateId("same");
            True(first == second); Equal(first.GetHashCode(), second.GetHashCode());
            True(new TransitionId("same") == new TransitionId("same"));
            var request = new TransitionRequest<int>(go, 3);
            var (transitionId, context) = request;
            Equal(go, transitionId); Equal(3, context);
            True(request == new TransitionRequest<int>(go, 3));
            var result = new StateResult<string>(stateA, 5, "done");
            var (stateId, serial, output) = result;
            Equal(stateA, stateId); Equal(5L, serial); Equal("done", output);
            True(result == new StateResult<string>(stateA, 5, "done"));
        }

        private sealed class ProbeState : State<int, string>
        {
            private readonly StateId _id;
            internal ProbeState(StateId id) => _id = id;
            public override StateId Id => _id;
            internal int EnterCount, ExecuteCount, ExitCount, LastContext;
            internal System.Action<int>? OnEnterAction;
            internal System.Action? OnExecute;
            internal System.Action? OnExit;
            protected override void OnEnter(int context) { EnterCount++; LastContext = context; OnEnterAction?.Invoke(context); }
            public override void Execute() { ExecuteCount++; OnExecute?.Invoke(); }
            public override void Exit() { ExitCount++; OnExit?.Invoke(); }
            internal void Finish(string output) => Complete(output);
        }
        private sealed class ProbeGuard : IGuard<int>
        {
            private readonly GuardResult _result;
            internal ProbeGuard(GuardResult result) => _result = result;
            internal int Count;
            public GuardResult Evaluate(int context) { Count++; return _result; }
        }
        private sealed class CallbackGuard : IGuard<int>
        {
            internal System.Action? Callback;
            public GuardResult Evaluate(int context) { Callback?.Invoke(); return GuardResult.Allow; }
        }
        private sealed class ThrowingGuard : IGuard<int>
        {
            private readonly System.Exception _error;
            internal ThrowingGuard(System.Exception error) => _error = error;
            public GuardResult Evaluate(int context) => throw _error;
        }
        private sealed class BrokenFlow : IStateFlow<int, string>
        {
            private readonly ProbeState _a;
            internal BrokenFlow(ProbeState a) => _a = a;
            public StateId InitialStateId => stateA;
            public IState<int, string> GetState(StateId id) => id == stateA ? _a : throw new System.InvalidOperationException("missing target");
            public Transition<int>? FindTransition(StateId currentState, TransitionId transitionId)
            {
                // stateA valid built Flow cannot do this; emulate a broken custom Flow.
                var builder = new StateFlowBuilder<int, string>().Initial(stateA).State(_a).State(new ProbeState(stateB))
                    .Transition(go, stateA, stateB);
                return builder.Build().FindTransition(stateA, go);
            }
        }
        private sealed class TestException : System.Exception { }
    }
}
