#nullable enable
using System;

namespace Torben.StateMachine
{

    public class StateMachine<TContext, TOutput> : IStateMachine<TContext, TOutput>
    {
        private readonly IStateFlow<TContext, TOutput> _flow;
        private IState<TContext, TOutput>? _currentState;
        private StateId? _currentStateId;
        private long _serialNumber;
        private long? _capturedSerialNumber;
        private TransitionRequest<TContext>? _pendingRequest;
        private StateMachinePhase _phase;

        public StateMachine(IStateFlow<TContext, TOutput> flow)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public StateMachineStatus Status { get; private set; } = StateMachineStatus.Stopped;
        public StateId? CurrentStateId => _currentStateId;
        public long? CurrentSerialNumber { get; private set; }
        public StateResult<TOutput>? LastResult { get; private set; }

        public void Start(TContext initialContext)
        {
            EnsureIdle();
            if (Status != StateMachineStatus.Stopped)
                throw new InvalidOperationException("Only a stopped machine can start.");

            LastResult = null;
            _pendingRequest = null;
            _capturedSerialNumber = null;
            _currentState = null;
            _currentStateId = null;
            CurrentSerialNumber = null;

            try
            {
                var initialState = _flow.GetState(_flow.InitialStateId);
                if (initialState is null || initialState.Id != _flow.InitialStateId)
                    throw new InvalidOperationException("Flow returned an invalid initial state.");
                EnsureCanAllocateSerial();
                _phase = StateMachinePhase.Entering;
                initialState.Enter(initialContext);
                var serialNumber = checked(++_serialNumber);
                _currentState = initialState;
                _currentStateId = _flow.InitialStateId;
                CurrentSerialNumber = serialNumber;
                Status = StateMachineStatus.Running;
                CaptureCompletion(initialState, _flow.InitialStateId, serialNumber);
            }
            catch
            {
                Status = StateMachineStatus.Faulted;
                _pendingRequest = null;
                throw;
            }
            finally
            {
                _phase = StateMachinePhase.Idle;
            }
        }

        public void Execute()
        {
            EnsureIdle();
            if (Status == StateMachineStatus.Faulted)
                throw new InvalidOperationException("A faulted machine cannot execute.");
            if (Status != StateMachineStatus.Running) return;

            try
            {
                var current = _currentState ?? throw new InvalidOperationException("Running machine has no current state.");
                var currentId = _currentStateId ?? throw new InvalidOperationException("Running machine has no current state ID.");
                var serialNumber = CurrentSerialNumber ?? throw new InvalidOperationException("Running machine has no serial number.");
                if (current.Id != currentId)
                    throw new InvalidOperationException("Current state ID changed after activation.");

                if (_pendingRequest is TransitionRequest<TContext> request)
                {
                    _pendingRequest = null;
                    ProcessRequest(current, currentId, serialNumber, request);
                    return;
                }

                _phase = StateMachinePhase.Executing;
                current.Execute();
                CaptureCompletion(current, currentId, serialNumber);
            }
            catch
            {
                Status = StateMachineStatus.Faulted;
                _pendingRequest = null;
                throw;
            }
            finally
            {
                _phase = StateMachinePhase.Idle;
            }
        }

        public void Stop()
        {
            EnsureIdle();
            if (Status == StateMachineStatus.Stopped) return;

            if (Status == StateMachineStatus.Faulted)
            {
                ClearCurrent();
                Status = StateMachineStatus.Stopped;
                return;
            }

            try
            {
                var current = _currentState ?? throw new InvalidOperationException("Running machine has no current state.");
                _phase = StateMachinePhase.Exiting;
                current.Exit();
                ClearCurrent();
                Status = StateMachineStatus.Stopped;
            }
            catch
            {
                Status = StateMachineStatus.Faulted;
                _pendingRequest = null;
                throw;
            }
            finally
            {
                _phase = StateMachinePhase.Idle;
            }
        }

        public RequestResult Request(TransitionRequest<TContext> request)
        {
            if (Status != StateMachineStatus.Running) return RequestResult.Ignored;
            var result = _pendingRequest.HasValue ? RequestResult.Replaced : RequestResult.Accepted;
            _pendingRequest = request;
            return result;
        }

        private void ProcessRequest(
            IState<TContext, TOutput> current,
            StateId currentId,
            long currentSerialNumber,
            TransitionRequest<TContext> request)
        {
            _phase = StateMachinePhase.Evaluating;
            var transition = _flow.FindTransition(currentId, request.TransitionId);
            if (transition is null) return;
            if (transition.From != currentId || transition.Id != request.TransitionId)
                throw new InvalidOperationException("Flow returned a transition with a mismatched route.");

            foreach (var guard in transition.Guards)
            {
                if (guard is null) throw new InvalidOperationException("Flow contains a null guard.");
                var result = guard.Evaluate(request.Context);
                if (result == GuardResult.Deny) return;
                if (result != GuardResult.Allow)
                    throw new InvalidOperationException("Guard returned an unknown result.");
            }

            var next = _flow.GetState(transition.To);
            if (next is null || next.Id != transition.To)
                throw new InvalidOperationException("Flow returned an invalid target state.");
            EnsureCanAllocateSerial();

            _phase = StateMachinePhase.Exiting;
            current.Exit();
            _phase = StateMachinePhase.Entering;
            next.Enter(request.Context);
            var nextSerialNumber = checked(++_serialNumber);
            CaptureCompletion(current, currentId, currentSerialNumber);
            _currentState = next;
            _currentStateId = transition.To;
            CurrentSerialNumber = nextSerialNumber;
            _capturedSerialNumber = null;
            CaptureCompletion(next, transition.To, nextSerialNumber);
        }

        private void CaptureCompletion(IState<TContext, TOutput> state, StateId stateId, long serialNumber)
        {
            if (!state.IsComplete || _capturedSerialNumber == serialNumber) return;
            LastResult = new StateResult<TOutput>(stateId, serialNumber, state.Output!);
            _capturedSerialNumber = serialNumber;
        }

        private void ClearCurrent()
        {
            _currentState = null;
            _currentStateId = null;
            CurrentSerialNumber = null;
            _capturedSerialNumber = null;
            _pendingRequest = null;
        }

        private void EnsureIdle()
        {
            if (_phase != StateMachinePhase.Idle)
                throw new InvalidOperationException("Machine lifecycle calls cannot be nested.");
        }

        private void EnsureCanAllocateSerial()
        {
            _ = checked(_serialNumber + 1);
        }
    }
}
