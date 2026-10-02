using System;
using System.Collections.Generic;
using Torben.StateMachine;

internal static class Program
{
    private static readonly StateId A = new("A");
    private static readonly StateId B = new("B");
    private static readonly StateId C = new("C");
    private static readonly TransitionId Go = new("go");
    private static int _passed;

    private static void Main()
    {
        Run(nameof(NormalTransition_ExitsOldAndEntersNext), NormalTransition_ExitsOldAndEntersNext);
        Run(nameof(Transition_DoesNotExecuteNextStateInSameTick), Transition_DoesNotExecuteNextStateInSameTick);
        Run(nameof(InvalidRequest_DoesNotExitCurrentState), InvalidRequest_DoesNotExitCurrentState);
        Run(nameof(GuardDeny_DoesNotExitCurrentState), GuardDeny_DoesNotExitCurrentState);
        Run(nameof(GuardDeny_DoesNotChangeSerialNumber), GuardDeny_DoesNotChangeSerialNumber);
        Run(nameof(Guards_UseAndShortCircuit), Guards_UseAndShortCircuit);
        Run(nameof(Request_ReplacesOnlyPendingSlot), Request_ReplacesOnlyPendingSlot);
        Run(nameof(Request_DuringProcessing_RemainsPendingForNextTick), Request_DuringProcessing_RemainsPendingForNextTick);
        Run(nameof(Request_WhenStopped_IsIgnored), Request_WhenStopped_IsIgnored);
        Run(nameof(Complete_UpdatesLastResult), Complete_UpdatesLastResult);
        Run(nameof(Stop_PreservesLastResult), Stop_PreservesLastResult);
        Run(nameof(Fault_PreservesLastResult), Fault_PreservesLastResult);
        Run(nameof(CompletedState_CanRemainCurrent), CompletedState_CanRemainCurrent);
        Run(nameof(Complete_InEnter_IsCapturedAfterCommit), Complete_InEnter_IsCapturedAfterCommit);
        Run(nameof(InterruptedState_DoesNotOverwriteLastResult), InterruptedState_DoesNotOverwriteLastResult);
        Run(nameof(Stop_DoesNotCompleteCurrentState), Stop_DoesNotCompleteCurrentState);
        Run(nameof(Stop_DoesNotCaptureCompletionDuringExit), Stop_DoesNotCaptureCompletionDuringExit);
        Run(nameof(Stop_WhenAlreadyStopped_DoesNothing), Stop_WhenAlreadyStopped_DoesNothing);
        Run(nameof(Stop_CallsExitOnlyOnce), Stop_CallsExitOnlyOnce);
        Run(nameof(Start_ClearsLastResult), Start_ClearsLastResult);
        Run(nameof(SerialNumber_DoesNotResetAcrossStopAndStart), SerialNumber_DoesNotResetAcrossStopAndStart);
        Run(nameof(SerialNumber_IncrementsOnlyAfterSuccessfulEnter), SerialNumber_IncrementsOnlyAfterSuccessfulEnter);
        Run(nameof(EnterFailure_DoesNotCommitNextState), EnterFailure_DoesNotCommitNextState);
        Run(nameof(EnterFailure_DoesNotConsumeSerialNumber), EnterFailure_DoesNotConsumeSerialNumber);
        Run(nameof(InitialEnterFailure_LeavesCurrentNull), InitialEnterFailure_LeavesCurrentNull);
        Run(nameof(ExecuteException_FaultsMachine), ExecuteException_FaultsMachine);
        Run(nameof(GuardException_FaultsMachine), GuardException_FaultsMachine);
        Run(nameof(ExitException_FaultsMachine), ExitException_FaultsMachine);
        Run(nameof(EnterException_FaultsMachine), EnterException_FaultsMachine);
        Run(nameof(FaultedStop_DoesNotCallExitAgain), FaultedStop_DoesNotCallExitAgain);
        Run(nameof(FaultedRequest_IsIgnored), FaultedRequest_IsIgnored);
        Run(nameof(BrokenFlowInvariant_FaultsMachine), BrokenFlowInvariant_FaultsMachine);
        Run(nameof(Builder_RejectsDuplicateStateId), Builder_RejectsDuplicateStateId);
        Run(nameof(Builder_RejectsDuplicateTransitionKey), Builder_RejectsDuplicateTransitionKey);
        Run(nameof(Builder_RejectsMissingInitialState), Builder_RejectsMissingInitialState);
        Run(nameof(Builder_RejectsMissingFromState), Builder_RejectsMissingFromState);
        Run(nameof(Builder_RejectsMissingToState), Builder_RejectsMissingToState);
        Run(nameof(Builder_CannotBeUsedAfterBuild), Builder_CannotBeUsedAfterBuild);
        Run(nameof(Builder_FlowIsFixedAfterBuild), Builder_FlowIsFixedAfterBuild);
        Run(nameof(Builder_AllowsSameTransitionIdFromDifferentStates), Builder_AllowsSameTransitionIdFromDifferentStates);
        Run(nameof(Start_RejectsRunningAndFaulted), Start_RejectsRunningAndFaulted);
        Run(nameof(ValueTypes_PreserveIdentityAndDeconstruction), ValueTypes_PreserveIdentityAndDeconstruction);
        Console.WriteLine($"PASS: {_passed} tests");
    }

    private static void Run(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception error) { Console.Error.WriteLine($"FAIL {name}: {error}"); Environment.ExitCode = 1; }
    }

    private static (StateMachine<int, string> machine, ProbeState a, ProbeState b) Pair(params IGuard<int>[] guards)
    {
        var a = new ProbeState(A);
        var b = new ProbeState(B);
        var flow = new StateFlowBuilder<int, string>()
            .Initial(A).State(a).State(b).Transition(Go, A, B, guards).Build();
        return (new StateMachine<int, string>(flow), a, b);
    }

    private static TransitionRequest<int> Request(TransitionId? id = null, int context = 7) => new(id ?? Go, context);
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}; got {actual}.");
    }
    private static void True(bool value) { if (!value) throw new Exception("Expected true."); }
    private static void Same(object expected, object actual) { if (!ReferenceEquals(expected, actual)) throw new Exception("Expected same object."); }
    private static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }

    private static void NormalTransition_ExitsOldAndEntersNext()
    {
        var (machine, a, b) = Pair();
        machine.Start(1);
        Equal(RequestResult.Accepted, machine.Request(Request(context: 42)));
        machine.Execute();
        Equal(1, a.ExitCount); Equal(1, b.EnterCount); Equal(42, b.LastContext);
        Equal((StateId?)B, machine.CurrentStateId);
        Equal((long?)2, machine.CurrentSerialNumber);
    }
    private static void Transition_DoesNotExecuteNextStateInSameTick()
    {
        var (machine, _, b) = Pair(); machine.Start(1); machine.Request(Request()); machine.Execute();
        Equal(0, b.ExecuteCount); machine.Execute(); Equal(1, b.ExecuteCount);
    }
    private static void InvalidRequest_DoesNotExitCurrentState()
    {
        var (machine, a, b) = Pair(); machine.Start(1);
        machine.Request(Request(new TransitionId("missing"))); machine.Execute();
        Equal(0, a.ExitCount); Equal(0, a.ExecuteCount); Equal(0, b.EnterCount);
        Equal((long?)1, machine.CurrentSerialNumber); Equal(StateMachineStatus.Running, machine.Status);
        machine.Execute(); Equal(1, a.ExecuteCount);
    }
    private static void GuardDeny_DoesNotExitCurrentState()
    {
        var (machine, a, b) = Pair(new ProbeGuard(GuardResult.Deny)); machine.Start(1);
        machine.Request(Request()); machine.Execute(); Equal(0, a.ExitCount); Equal(0, b.EnterCount);
        Equal(StateMachineStatus.Running, machine.Status);
    }
    private static void GuardDeny_DoesNotChangeSerialNumber()
    {
        var (machine, _, _) = Pair(new ProbeGuard(GuardResult.Deny)); machine.Start(1);
        machine.Request(Request()); machine.Execute(); Equal((long?)1, machine.CurrentSerialNumber);
    }
    private static void Guards_UseAndShortCircuit()
    {
        var first = new ProbeGuard(GuardResult.Deny); var second = new ProbeGuard(GuardResult.Allow);
        var (machine, _, _) = Pair(first, second); machine.Start(1); machine.Request(Request()); machine.Execute();
        Equal(1, first.Count); Equal(0, second.Count);
    }
    private static void Request_ReplacesOnlyPendingSlot()
    {
        var (machine, a, b) = Pair(); machine.Start(1);
        Equal(RequestResult.Accepted, machine.Request(Request(new TransitionId("missing"))));
        Equal(RequestResult.Replaced, machine.Request(Request(context: 99)));
        machine.Execute(); Equal(1, a.ExitCount); Equal(99, b.LastContext);
    }
    private static void Request_DuringProcessing_RemainsPendingForNextTick()
    {
        var guard = new CallbackGuard(); var (machine, a, b) = Pair(guard);
        guard.Callback = () => Equal(RequestResult.Accepted, machine.Request(Request(new TransitionId("missing"))));
        machine.Start(1); machine.Request(Request()); machine.Execute(); Equal((StateId?)B, machine.CurrentStateId);
        machine.Execute(); Equal(0, b.ExecuteCount); machine.Execute(); Equal(1, b.ExecuteCount);
    }
    private static void Request_WhenStopped_IsIgnored()
    {
        var (machine, _, _) = Pair(); Equal(RequestResult.Ignored, machine.Request(Request()));
    }
    private static void Complete_UpdatesLastResult()
    {
        var (machine, a, _) = Pair(); a.OnExecute = () => a.Finish("done"); machine.Start(1); machine.Execute();
        Equal(new StateResult<string>(A, 1, "done"), machine.LastResult!.Value);
    }
    private static void Stop_PreservesLastResult()
    {
        var (machine, a, _) = Pair(); a.OnExecute = () => a.Finish("done");
        machine.Start(1); machine.Execute(); machine.Stop();
        Equal(new StateResult<string>(A, 1, "done"), machine.LastResult!.Value);
    }
    private static void Fault_PreservesLastResult()
    {
        var (machine, a, b) = Pair(); a.OnExecute = () => a.Finish("done");
        b.OnEnterAction = _ => throw new TestException();
        machine.Start(1); machine.Execute(); machine.Request(Request());
        Throws<TestException>(() => machine.Execute());
        Equal(new StateResult<string>(A, 1, "done"), machine.LastResult!.Value);
        Equal((StateId?)A, machine.CurrentStateId);
    }
    private static void CompletedState_CanRemainCurrent()
    {
        var (machine, a, _) = Pair(); a.OnExecute = () => { if (!a.IsComplete) a.Finish("done"); };
        machine.Start(1); machine.Execute(); machine.Execute();
        Equal((StateId?)A, machine.CurrentStateId); Equal(2, a.ExecuteCount);
        Equal(new StateResult<string>(A, 1, "done"), machine.LastResult!.Value);
    }
    private static void Complete_InEnter_IsCapturedAfterCommit()
    {
        var (machine, a, _) = Pair(); a.OnEnterAction = _ => a.Finish("entered"); machine.Start(1);
        Equal(new StateResult<string>(A, 1, "entered"), machine.LastResult!.Value);
    }
    private static void InterruptedState_DoesNotOverwriteLastResult()
    {
        var (machine, a, b) = Pair(); a.OnExecute = () => a.Finish("done");
        machine.Start(1); machine.Execute(); machine.Request(Request()); machine.Execute();
        Equal(new StateResult<string>(A, 1, "done"), machine.LastResult!.Value);
        True(!b.IsComplete);
    }
    private static void Stop_DoesNotCompleteCurrentState()
    {
        var (machine, a, _) = Pair(); machine.Start(1); machine.Stop();
        True(!a.IsComplete); Equal(null, machine.LastResult);
        Equal(null, machine.CurrentStateId); Equal(null, machine.CurrentSerialNumber);
    }
    private static void Stop_DoesNotCaptureCompletionDuringExit()
    {
        var (machine, a, _) = Pair(); a.OnExit = () => a.Finish("exit");
        machine.Start(1); machine.Stop();
        True(a.IsComplete); Equal(null, machine.LastResult);
    }
    private static void Stop_WhenAlreadyStopped_DoesNothing()
    {
        var (machine, a, _) = Pair(); machine.Stop(); Equal(0, a.ExitCount);
        machine.Start(1); machine.Stop(); machine.Stop(); Equal(1, a.ExitCount);
    }
    private static void Stop_CallsExitOnlyOnce() => Stop_WhenAlreadyStopped_DoesNothing();
    private static void Start_ClearsLastResult()
    {
        var (machine, a, _) = Pair(); a.OnExecute = () => a.Finish("done");
        machine.Start(1); machine.Execute(); machine.Stop(); a.OnExecute = null;
        machine.Start(2); Equal(null, machine.LastResult);
    }
    private static void SerialNumber_DoesNotResetAcrossStopAndStart()
    {
        var (machine, _, _) = Pair(); machine.Start(1); machine.Stop(); machine.Start(2);
        Equal((long?)2, machine.CurrentSerialNumber);
    }
    private static void SerialNumber_IncrementsOnlyAfterSuccessfulEnter()
    {
        var (machine, _, b) = Pair(); machine.Start(1); b.OnEnterAction = _ => throw new TestException();
        machine.Request(Request()); Throws<TestException>(() => machine.Execute());
        machine.Stop(); b.OnEnterAction = null; machine.Start(1);
        Equal((long?)2, machine.CurrentSerialNumber);
    }
    private static void EnterFailure_DoesNotCommitNextState()
    {
        var (machine, a, b) = Pair(); machine.Start(1); b.OnEnterAction = _ => throw new TestException();
        machine.Request(Request()); Throws<TestException>(() => machine.Execute());
        Equal((StateId?)A, machine.CurrentStateId); Equal((long?)1, machine.CurrentSerialNumber);
        Equal(1, a.ExitCount); Equal(StateMachineStatus.Faulted, machine.Status);
    }
    private static void EnterFailure_DoesNotConsumeSerialNumber() => SerialNumber_IncrementsOnlyAfterSuccessfulEnter();
    private static void InitialEnterFailure_LeavesCurrentNull()
    {
        var (machine, a, _) = Pair(); a.OnEnterAction = _ => throw new TestException();
        Throws<TestException>(() => machine.Start(1)); Equal(null, machine.CurrentStateId);
        Equal(null, machine.CurrentSerialNumber); Equal(null, machine.LastResult);
        Equal(StateMachineStatus.Faulted, machine.Status);
        machine.Stop(); a.OnEnterAction = null; machine.Start(1); Equal((long?)1, machine.CurrentSerialNumber);
    }
    private static void ExecuteException_FaultsMachine()
    {
        var (machine, a, _) = Pair(); var error = new TestException(); a.OnExecute = () => throw error;
        machine.Start(1); Same(error, Throws<TestException>(() => machine.Execute()));
        Equal(StateMachineStatus.Faulted, machine.Status); Equal(0, a.ExitCount);
        Throws<InvalidOperationException>(() => machine.Execute());
    }
    private static void GuardException_FaultsMachine()
    {
        var error = new TestException(); var guard = new ThrowingGuard(error);
        var (machine, a, _) = Pair(guard); machine.Start(1); machine.Request(Request());
        Same(error, Throws<TestException>(() => machine.Execute()));
        Equal(StateMachineStatus.Faulted, machine.Status); Equal(0, a.ExitCount);
        Equal((long?)1, machine.CurrentSerialNumber);
    }
    private static void ExitException_FaultsMachine()
    {
        var (machine, a, b) = Pair(); var error = new TestException(); a.OnExit = () => throw error;
        machine.Start(1); machine.Request(Request()); Same(error, Throws<TestException>(() => machine.Execute()));
        Equal((StateId?)A, machine.CurrentStateId); Equal((long?)1, machine.CurrentSerialNumber);
        Equal(0, b.EnterCount); Equal(StateMachineStatus.Faulted, machine.Status);
    }
    private static void EnterException_FaultsMachine() => EnterFailure_DoesNotCommitNextState();
    private static void FaultedStop_DoesNotCallExitAgain()
    {
        var (machine, a, _) = Pair(); a.OnExit = () => throw new TestException();
        machine.Start(1); Throws<TestException>(() => machine.Stop());
        machine.Stop(); Equal(1, a.ExitCount); Equal(StateMachineStatus.Stopped, machine.Status);
        Equal(null, machine.CurrentStateId); Equal(null, machine.CurrentSerialNumber);
    }
    private static void FaultedRequest_IsIgnored()
    {
        var (machine, a, _) = Pair(); a.OnExecute = () => throw new TestException();
        machine.Start(1); Throws<TestException>(() => machine.Execute());
        Equal(RequestResult.Ignored, machine.Request(Request()));
    }
    private static void BrokenFlowInvariant_FaultsMachine()
    {
        var a = new ProbeState(A); var machine = new StateMachine<int, string>(new BrokenFlow(a));
        machine.Start(1); machine.Request(Request()); Throws<InvalidOperationException>(() => machine.Execute());
        Equal(StateMachineStatus.Faulted, machine.Status); Equal(0, a.ExitCount);
    }
    private static void Builder_RejectsDuplicateStateId()
    {
        var builder = new StateFlowBuilder<int, string>().State(new ProbeState(A));
        Throws<InvalidOperationException>(() => builder.State(new ProbeState(A)));
    }
    private static void Builder_RejectsDuplicateTransitionKey()
    {
        var builder = new StateFlowBuilder<int, string>().Transition(Go, A, B);
        Throws<InvalidOperationException>(() => builder.Transition(Go, A, C));
    }
    private static void Builder_RejectsMissingInitialState()
    {
        var builder = new StateFlowBuilder<int, string>();
        Throws<InvalidOperationException>(() => builder.Build());
        builder.Initial(A); Throws<InvalidOperationException>(() => builder.Build());
    }
    private static void Builder_RejectsMissingFromState()
    {
        var builder = new StateFlowBuilder<int, string>().Initial(A).State(new ProbeState(A))
            .Transition(Go, B, A);
        Throws<InvalidOperationException>(() => builder.Build());
    }
    private static void Builder_RejectsMissingToState()
    {
        var builder = new StateFlowBuilder<int, string>().Initial(A).State(new ProbeState(A))
            .Transition(Go, A, B);
        Throws<InvalidOperationException>(() => builder.Build());
    }
    private static void Builder_CannotBeUsedAfterBuild()
    {
        var builder = new StateFlowBuilder<int, string>().Initial(A).State(new ProbeState(A)); builder.Build();
        Throws<InvalidOperationException>(() => builder.Initial(B));
        Throws<InvalidOperationException>(() => builder.State(new ProbeState(B)));
        Throws<InvalidOperationException>(() => builder.Transition(Go, A, A));
        Throws<InvalidOperationException>(() => builder.Build());
    }
    private static void Builder_FlowIsFixedAfterBuild()
    {
        var builder = new StateFlowBuilder<int, string>().Initial(A).State(new ProbeState(A));
        var flow = builder.Build(); Equal(A, flow.InitialStateId);
        Throws<InvalidOperationException>(() => flow.GetState(B));
    }
    private static void Builder_AllowsSameTransitionIdFromDifferentStates()
    {
        var flow = new StateFlowBuilder<int, string>()
            .Initial(A).State(new ProbeState(A)).State(new ProbeState(B))
            .Transition(Go, A, B).Transition(Go, B, A).Build();
        Equal(B, flow.FindTransition(A, Go)!.To);
        Equal(A, flow.FindTransition(B, Go)!.To);
    }
    private static void Start_RejectsRunningAndFaulted()
    {
        var (machine, a, _) = Pair(); machine.Start(1);
        Throws<InvalidOperationException>(() => machine.Start(2));
        a.OnExecute = () => throw new TestException(); Throws<TestException>(() => machine.Execute());
        Throws<InvalidOperationException>(() => machine.Start(3));
    }
    private static void ValueTypes_PreserveIdentityAndDeconstruction()
    {
        var first = new StateId("same"); var second = new StateId("same");
        True(first == second); Equal(first.GetHashCode(), second.GetHashCode());
        True(new TransitionId("same") == new TransitionId("same"));
        var request = new TransitionRequest<int>(Go, 3);
        var (transitionId, context) = request;
        Equal(Go, transitionId); Equal(3, context);
        True(request == new TransitionRequest<int>(Go, 3));
        var result = new StateResult<string>(A, 5, "done");
        var (stateId, serial, output) = result;
        Equal(A, stateId); Equal(5L, serial); Equal("done", output);
        True(result == new StateResult<string>(A, 5, "done"));
    }

    private sealed class ProbeState : State<int, string>
    {
        private readonly StateId _id;
        internal ProbeState(StateId id) => _id = id;
        public override StateId Id => _id;
        internal int EnterCount, ExecuteCount, ExitCount, LastContext;
        internal Action<int>? OnEnterAction;
        internal Action? OnExecute;
        internal Action? OnExit;
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
        internal Action? Callback;
        public GuardResult Evaluate(int context) { Callback?.Invoke(); return GuardResult.Allow; }
    }
    private sealed class ThrowingGuard : IGuard<int>
    {
        private readonly Exception _error;
        internal ThrowingGuard(Exception error) => _error = error;
        public GuardResult Evaluate(int context) => throw _error;
    }
    private sealed class BrokenFlow : IStateFlow<int, string>
    {
        private readonly ProbeState _a;
        internal BrokenFlow(ProbeState a) => _a = a;
        public StateId InitialStateId => A;
        public IState<int, string> GetState(StateId id) => id == A ? _a : throw new InvalidOperationException("missing target");
        public Transition<int>? FindTransition(StateId currentState, TransitionId transitionId)
        {
            // A valid built Flow cannot do this; emulate a broken custom Flow.
            var builder = new StateFlowBuilder<int, string>().Initial(A).State(_a).State(new ProbeState(B))
                .Transition(Go, A, B);
            return builder.Build().FindTransition(A, Go);
        }
    }
    private sealed class TestException : Exception { }
}
