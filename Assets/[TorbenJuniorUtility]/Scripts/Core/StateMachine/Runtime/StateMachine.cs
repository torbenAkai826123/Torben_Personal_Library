#nullable enable
using System;

namespace Torben.StateMachine
{

    /// <summary>同步狀態機與 lifecycle 調度.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public class StateMachine<TContext, TOutput> : IStateMachine<TContext, TOutput>
    {
        private readonly IStateFlow<TContext, TOutput> _flow;
        private IState<TContext, TOutput>? _currentState;
        private StateId? _currentStateId;
        private long _serialNumber;
        private long? _capturedSerialNumber;
        private TransitionRequest<TContext>? _pendingRequest;
        private StateMachinePhase _phase;

        /// <summary>同步狀態機與 lifecycle 調度.</summary>
        /// <remarks>配置: 僅初始化時配置; Flow 由呼叫端提供.</remarks>
        public StateMachine(IStateFlow<TContext, TOutput> flow)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        /// <summary>同步狀態機與 lifecycle 調度.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateMachineStatus Status { get; private set; } = StateMachineStatus.Stopped;
        /// <summary>同步狀態機與 lifecycle 調度.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateId? CurrentStateId => _currentStateId;
        /// <summary>同步狀態機與 lifecycle 調度.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public long? CurrentSerialNumber { get; private set; }
        /// <summary>
        /// 最近一次擷取的 activation 完成結果; 尚無結果時為 null.
        /// 只在 Enter (已分配 Serial 並提交 Current 後) 或 Execute 正常返回後擷取, 每個 activation 最多一次;
        /// callback 在 Complete 後拋例外不擷取, Exit 期間 (轉換或 Stop) 的 completion 不擷取.
        /// Request 不直接更新此值; 中斷、Stop、Fault 保留, 新一次 Start 才清空. 不保證屬於 Current, 請核對 StateId 與 SerialNumber.
        /// </summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateResult<TOutput>? LastResult { get; private set; }

        /// <summary>同步狀態機與 lifecycle 調度.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
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

        /// <summary>
        /// 執行一個 Tick. 有 Pending 時取出並處理: 查找路徑與求值 Guards; 成功轉換依序呼叫 Old.Exit、Next.Enter,
        /// 本 Tick 不呼叫 Old 或 Next 的 Execute; 無路徑或 Guard Deny 時消耗 Request, 不 Exit.
        /// 沒有 Pending 時呼叫 Current.Execute. 兩種路徑都可能在 callback 正常返回後更新 LastResult.
        /// </summary>
        /// <remarks>配置: 每次呼叫可能配置; 轉換時列舉 Guards 及自訂 State/Guard/Flow 實作可能配置.</remarks>
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
                    ProcessRequest(current, currentId, request);
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

        /// <summary>Running 時 Exit Current 並清除 Current 與 Pending; Exit 期間的 completion 不擷取, LastResult 保留. Stopped 時 no-op; Faulted 時只清除, 不再 Exit.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
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

        /// <summary>
        /// Running 時寫入單一 Pending slot, 留待後續 Execute 處理; 不直接 Exit/Enter, 也不直接更新 LastResult.
        /// 尚未處理的 Pending 會被新 Request 取代 (Replaced). Accepted 不代表轉換成功. Stopped/Faulted 時回傳 Ignored.
        /// </summary>
        /// <remarks>配置: 本實作正常路徑零配置; 例外路徑可能配置.</remarks>
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

            // 舊 activation 的合法完成已在先前 Enter/Execute 返回後擷取; Exit 期間的 completion 不擷取
            _phase = StateMachinePhase.Exiting;
            current.Exit();
            _phase = StateMachinePhase.Entering;
            next.Enter(request.Context);
            var nextSerialNumber = checked(++_serialNumber);
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
