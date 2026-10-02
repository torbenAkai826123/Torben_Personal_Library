依已 Freeze 的 Torben.StateMachine v1 規格，完成可直接放入 Unity 的 reusable Core（基礎 Types、State/IState、Guard、Transition/TransitionRequest、Flow/Builder、Runtime、PendingRequest、SerialNumber、Fault handling）、自動測試、基本範例及 Architecture/API/Lifecycle/Design Decisions/Non-goals 文件；逐項驗證既定 Public API 與 lifecycle、fault、Stop、PendingRequest、StateResult/LastResult、Builder invariants，不加入未核准 API 或排除功能。


````csharp
# Torben.StateMachine v1 — Frozen Implementation Specification

這份文件是本次實作的 Authority。除非下列規格彼此產生實際矛盾，否則不要重新設計 Public API，也不要自行增加「未來可能需要」的功能。

---

# 1. Core Responsibility

Core 負責：

- StateMachine execution pipeline
- State lifecycle
- Flow lookup
- Guard evaluation
- Pending Request
- State activation SerialNumber
- Completed StateResult
- Fault state

Project 負責：

- Flow 定義
- State implementation
- Guard implementation
- Context
- Output
- Request 時機
- Gameplay / Unity objects
- TimeSystem
- Runtime Host / Update 驅動
- Recovery strategy

Core 不理解 Project-specific semantics。

---

# 2. Namespace / Naming

所有 Public API 使用：

```csharp
namespace Torben.StateMachine;
```

資料夾不決定 namespace，不拆成：

```text
Torben.StateMachine.States
Torben.StateMachine.Flow
...
```

命名採標準 C# 慣例：

```text
Type / Method / Property / Enum : PascalCase
Parameter / Local               : camelCase
Private field                   : _camelCase
Generic                         : TName
```

規則：

- `Get` / `Set` 可以使用，只避免模糊的 `Get()` / `Set()`。
- readonly 由語言表達，不使用名稱前綴表示。
- 不使用 `common_` / `static_` 等自訂前綴。
- Bool 優先使用 `Is / Has / Can / Should` 等語意。
- 名稱負責表達「它是什麼」，存取權限負責表達「誰可以改」。

---

# 3. Public Basic Types

```csharp
public readonly record struct StateId(string Value);

public readonly record struct TransitionId(string Value);
```

`StateId`：

- 表示 State kind / Flow node。
- 不表示一次 activation。
- 一個 StateMachine 內唯一。

`TransitionId`：

- 表示 Project 的 route intent。
- 不要求整台 machine global unique。
- lookup uniqueness 為：

```text
(From StateId, TransitionId)
```

---

```csharp
public readonly record struct StateResult<TOutput>(
    StateId StateId,
    long SerialNumber,
    TOutput Output);
```

定義：

> 一次正常完成的 State Activation 所產生的 completed result。

只有 `Complete()` 產生 StateResult。

Interrupted / Stop / Fault 未 Complete 的 State 不產生 Result。

---

```csharp
public enum StateMachineStatus
{
    Stopped,
    Running,
    Faulted
}
```

不另外提供 `IsRunning`。

---

```csharp
public enum GuardResult
{
    Allow,
    Deny
}
```

---

```csharp
public enum RequestResult
{
    Accepted,
    Replaced,
    Ignored
}
```

它只描述 Request 是否進入 Pending slot。

它不表示 Transition 最終成功或失敗。

---

# 4. State API

```csharp
public interface IState<TContext, TOutput>
{
    StateId Id { get; }

    bool IsComplete { get; }

    TOutput? Output { get; }

    void Enter(TContext context);

    void Execute();

    void Exit();
}
```

Base class：

```csharp
public abstract class State<TContext, TOutput>
    : IState<TContext, TOutput>
{
    public abstract StateId Id { get; }

    public bool IsComplete { get; private set; }

    public TOutput? Output { get; private set; }

    public void Enter(TContext context)
    {
        IsComplete = false;
        Output = default;
        OnEnter(context);
    }

    protected abstract void OnEnter(TContext context);

    public abstract void Execute();

    public virtual void Exit() { }

    protected void Complete(TOutput output)
    {
        if (IsComplete)
            throw new InvalidOperationException(
                $"State '{Id}' has already completed.");

        Output = output;
        IsComplete = true;
    }
}
```

Invariant：

```text
IsComplete == false
→ Output 沒有有效 semantic meaning

IsComplete == true
→ Output 是由 Complete(output) 產生的有效結果
```

禁止用：

```text
Output != null
```

推斷 completion。

`Complete()`：

- 只能由 State 自己呼叫。
- 不公開在 `IState`。
- 表示 State 自己的工作已完成。
- 不代表 State 已離開。
- 不自動 Transition。

---

# 5. Complete / Exit / Transition Semantics

```text
Complete
= State 自己的工作正常完成

Exit
= State 實際離開並 cleanup

Transition
= Core 從 State A 移往 State B
```

重要：

- State 可以 Complete 後仍維持 Current。
- State 可以尚未 Complete 就被 interrupt 並 Exit。
- Guard Deny 不 Exit。
- Stop 不偽造 Complete。
- 正常 Output 只能由 Complete 產生。

---

# 6. Transition Request

```csharp
public readonly record struct TransitionRequest<TContext>(
    TransitionId TransitionId,
    TContext Context);
```

只包含：

- TransitionId
- Context

不包含：

- FromStateId
- ToStateId
- SerialNumber
- RequestId
- Timestamp
- Priority
- Retry
- Cancellation
- Result

Context pipeline：

```text
TransitionRequest.Context
        ↓
Guard.Evaluate(Context)
        ↓ Allow
NextState.Enter(Context)
```

同一份 Request Context 同時供 Guard 驗證及下一 State Enter。

---

# 7. Guard

```csharp
public interface IGuard<TContext>
{
    GuardResult Evaluate(TContext context);
}
```

Guard contract：

- 應為 side-effect-free validation。
- 不是 gameplay action。
- 多個 Guard 由 Core 使用 AND + short-circuit。
- OR / NOT / complex condition 屬於單一 Project Guard 內部。

例如：

```text
CanOpenDoorGuard
    = HasKey OR BossDefeated

PlayerAliveGuard

Core:
CanOpenDoorGuard
AND PlayerAliveGuard
```

不要建立：

- GuardGroup
- AndGuard
- OrGuard
- Condition Tree

Guard Deny：

```text
Request consumed
Current 不變
Serial 不變
LastResult 不變
不 Exit
不 Enter
不 Retry
Status 維持 Running
```

Guard Exception：

- 不是 Deny。
- 視為 unexpected lifecycle/pipeline failure。
- Machine → Faulted。
- rethrow 原 Exception。

---

# 8. Transition

```csharp
public sealed class Transition<TContext>
{
    public TransitionId Id { get; }

    public StateId From { get; }

    public StateId To { get; }

    public IReadOnlyList<IGuard<TContext>> Guards { get; }
}
```

Transition 是 long-lived Flow blueprint item。

不要加入：

- IsEnabled
- Priority
- Weight
- callback
- runtime mutable state
- metadata system

No Guards 時使用空 collection。

Constructor 是否 internal 可由 implementation 決定；不要增加不必要 Public API。

---

# 9. Flow

```csharp
public interface IStateFlow<TContext, TOutput>
{
    StateId InitialStateId { get; }

    IState<TContext, TOutput> GetState(StateId id);

    Transition<TContext>? FindTransition(
        StateId currentState,
        TransitionId transitionId);
}
```

語意：

### `InitialStateId`

Initial State 屬於 Flow。

所以：

```csharp
machine.Start(initialContext);
```

而不是：

```csharp
Start(initialStateId, context);
```

### `GetState`

Missing State 是 configuration / invariant error。

不是正常 runtime branch。

### `FindTransition`

Missing：

```text
(Current State, requested TransitionId)
```

是正常 invalid Request。

不是 Fault。

---

# 10. Flow Storage

Runtime Flow 為 fixed / immutable from Project perspective。

推薦 storage：

```csharp
Dictionary<StateId, IState<TContext, TOutput>>
```

以及 internal key：

```csharp
internal readonly record struct TransitionKey(
    StateId From,
    TransitionId TransitionId);
```

Transition storage：

```csharp
Dictionary<TransitionKey, Transition<TContext>>
```

不要公開整個 graph collection。

---

# 11. StateFlowBuilder

Public shape：

```csharp
public sealed class StateFlowBuilder<TContext, TOutput>
{
    public StateFlowBuilder<TContext, TOutput>
        Initial(StateId stateId);

    public StateFlowBuilder<TContext, TOutput>
        State(IState<TContext, TOutput> state);

    public StateFlowBuilder<TContext, TOutput>
        Transition(
            TransitionId id,
            StateId from,
            StateId to,
            params IGuard<TContext>[] guards);

    public IStateFlow<TContext, TOutput> Build();
}
```

Builder 是 configuration validation boundary。

必須驗證：

1. Initial 已設定。
2. Initial State 存在。
3. StateId unique。
4. Transition From 存在。
5. Transition To 存在。
6. `(From, TransitionId)` unique。

建議：

- Duplicate StateId：加入時立即 throw。
- Duplicate Transition key：加入時立即 throw。
- Cross reference（Initial / From / To）在 Build 驗證。

不要驗證：

- Reachability
- Dead end
- Cycle
- 是否每個 State 都有出口
- Guard business logic
- Flow 是否「設計良好」

Builder：

```text
mutable configuration
    ↓
Build + validate
    ↓
fixed StateFlow
```

Build 後 Builder 視為 consumed。

之後再次呼叫：

- Initial
- State
- Transition
- Build

可丟 `InvalidOperationException`。

不需要支援 reusable Builder。

---

# 12. StateMachine Public API

```csharp
public interface IStateMachine<TContext, TOutput>
{
    StateMachineStatus Status { get; }

    StateId? CurrentStateId { get; }

    long? CurrentSerialNumber { get; }

    StateResult<TOutput>? LastResult { get; }

    void Start(TContext initialContext);

    void Execute();

    void Stop();

    RequestResult Request(
        TransitionRequest<TContext> request);
}
```

不要增加：

- IsRunning
- Phase public property
- Pause
- Resume
- Reset
- Restart
- TransitionResult public API
- LastTransitionResult
- Output History

---

# 13. Current Activation

`CurrentStateId` / `CurrentSerialNumber`：

```text
沒有 Current Activation
→ 兩者皆 null

有 Current Activation
→ 兩者皆有值
```

不要使用 `0` 或 `default(StateId)` 表示「沒有 Current」。

Invariant：

```text
CurrentStateId == null
⇔
CurrentSerialNumber == null
```

`CurrentStateId` 代表：

> 最後一個成功完成 Enter 並被 commit 成 Current 的 State。

---

# 14. SerialNumber

SerialNumber：

- StateMachine/Core 擁有。
- 不存在 State instance 上。
- 不存在 TransitionRequest 上。
- `long`。
- 同一 StateMachine instance lifetime 單調遞增。
- Stop / Start 不重置。
- 新 machine instance 可重新開始。
- 使用 `checked` increment。
- 不設計 rollover / string ID。

SerialNumber 定義為：

> successful State Activation identity。

因此：

```text
Next.Enter()
↓
成功
↓
才 allocate 下一個 SerialNumber
↓
Commit Current
```

Enter Failure：

- 不消耗新的 SerialNumber。
- 不存在失敗 activation 的 serial。

若未來需要追蹤 attempt，應新增不同的 Attempt / Trace identifier，而不是改變 SerialNumber 語意。

---

# 15. StateResult / LastResult

`TOutput` 由 State：

```csharp
Complete(output)
```

產生。

Machine 對一次完成的 Activation 建立：

```csharp
StateResult<TOutput>
```

內容：

- StateId
- SerialNumber
- Output

`LastResult`：

> 最近一次正常 Complete 的 State activation result。

若 current State 被 interrupt、Stop、Guard Deny、Fault，而沒有新的 Complete：

```text
LastResult 保留上一個 completed result
```

新的 Start 開始新的 run 時：

```text
LastResult = null
```

Initial Enter failure 後 LastResult 也保持 null。

不要恢復 `IOutput`。

不要要求 `TOutput.Success`。

Gameplay success/failure 是 Project Output semantic。

---

# 16. Pending Request

v1：

- Single PendingRequest。
- No Queue。
- No automatic Retry。
- No Cancellation。

Pending 與 processing 必須區分：

```text
Pending
= 尚未開始處理，可被新 request replace

Processing
= Core 已取出並開始處理，不再是 Pending
```

`RequestResult`：

```text
沒有 Pending
→ Accepted

已有 Pending
→ Replaced

當下 Machine 不接受 request
→ Ignored
```

Deny / Invalid 後 Request 結束。

不要讓 Denied Request 保持 Pending 等未來條件成立。

---

# 17. Execute Ordering

`Execute()` 每 Tick：

```text
Execute()
    ↓
有 PendingRequest？
    ├─ Yes
    │    ↓
    │  Process Request / Transition
    │    ↓
    │  return
    │
    └─ No
         ↓
       CurrentState.Execute()
```

PendingRequest 優先。

成功 Transition：

```text
Old.Exit()
    ↓
Next.Enter(Context)
    ↓
成功
    ↓
checked(++Serial)
    ↓
Commit Current = Next
    ↓
return
```

同一個 `Execute()` 不呼叫：

```text
Next.Execute()
```

下一個 Tick 才執行新 State。

---

# 18. Completion Capture

Machine 必須確保：

> 每一個 activation 第一次進入 `IsComplete == true` 時，只建立一次對應 `StateResult`。

State 可以在其 lifecycle 中呼叫 `Complete(output)`。

建立 Result 時必須使用該 activation：

- StateId
- SerialNumber
- Output

Complete 不自動 Exit、不自動 Transition。

---

# 19. Start

```csharp
void Start(TContext initialContext);
```

Initial State 從：

```csharp
Flow.InitialStateId
```

取得。

正常：

```text
Initial.Enter(initialContext)
    ↓ success
allocate checked SerialNumber
    ↓
commit Current
    ↓
Status = Running
```

Start 新 run 時清除：

```text
LastResult
PendingRequest
```

Serial counter 不重置。

Initial Enter throws：

```text
CurrentStateId      = null
CurrentSerialNumber = null
LastResult          = null
Status              = Faulted
Serial 不增加
Exception rethrow
```

Running 狀態再次 Start：

```text
InvalidOperationException
```

Faulted 狀態不能直接重新 Start。

不要加入 Reset / Restart API。

---

# 20. Stop

正常 Running Stop：

```text
Current.Exit()
    ↓
clear Current
clear Pending
Status = Stopped
```

Stop：

- 不呼叫 Complete。
- 不製造 StateResult。
- 不重置 Serial counter。
- LastResult 保留。

Stopped 狀態再次 Stop：

```text
no-op
```

也就是 Stop 為 idempotent：

```text
Stop 一次和 Stop 多次
最終 machine state 相同
且 Exit 不會重複執行
```

Faulted → Stop：

```text
不再呼叫 Exit
clear Current
clear Pending
Status = Stopped
```

原因：

Fault 後 State lifecycle 狀態可能已不可靠，Core 不嘗試 recovery / second cleanup lifecycle。

---

# 21. Internal Machine Phase

Internal only：

```csharp
internal enum StateMachinePhase
{
    Idle,
    Evaluating,
    Entering,
    Executing,
    Exiting
}
```

不公開給 Project。

`Transitioning` 不需要。

`Complete` 不是 Machine Phase。

合法狀態：

```text
Phase = Idle
Current State IsComplete = true
```

---

# 22. Transition Pipeline

正常：

```text
PendingRequest
    ↓
取出 Request
    ↓
Resolve Transition
    ↓
Validate Current / Route
    ↓
Evaluate Guards
    ↓
Old.Exit()
    ↓
Next.Enter(Context)
    ↓
Enter Success
    ↓
Allocate checked Serial
    ↓
Commit Current
    ↓
return
```

`CurrentStateId` 在 Next.Enter 成功前仍保持 Old。

---

# 23. Invalid Transition Request

如果：

```text
FindTransition(Current, TransitionId) == null
```

這是正常 invalid Request。

結果：

```text
Request consumed
Current 不變
Serial 不變
LastResult 不變
Status = Running
不 Exit
不 Enter
無 Exception
```

v1 不公開 TransitionResult。

---

# 24. Guard Deny

```text
Guard Allow...
Guard Deny
    ↓
short-circuit
```

結果：

```text
Request consumed
Current 不變
Serial 不變
LastResult 不變
Status = Running
不 Exit
不 Enter
無 Retry
```

---

# 25. Fault Rule

核心原則：

> Deny / Invalid Request 是正常結果。
> Exception / broken invariant 是 Fault。

Fault 時：

- Core 不猜。
- 不 retry。
- 不 recovery。
- 不偷偷呼叫額外 lifecycle。
- 保存能確定的 state。
- `Status = Faulted`
- rethrow 原 Exception。

---

# 26. Fault Semantics

## Guard throws

```text
Current        = unchanged
Serial         = unchanged
LastResult     = unchanged
Status         = Faulted
Request        = consumed
Exception      = rethrow
```

---

## Current.Execute throws

```text
Current        = same
Serial         = same
LastResult     = unchanged
Status         = Faulted
```

不要自動：

```text
Exit()
```

Exception rethrow。

---

## Old.Exit throws

```text
Current        = Old
Serial         = Old serial
LastResult     = unchanged
Status         = Faulted
Next.Enter     = 不執行
```

Exception rethrow。

Current 只代表：

> 最後成功 Enter 的 activation。

不表示 Fault 後它仍安全可執行。

---

## Next.Enter throws

流程：

```text
Old.Exit() success
Next.Enter() throws
```

結果：

```text
CurrentStateId      = Old
CurrentSerialNumber = Old serial
LastResult          = unchanged
Status              = Faulted
新 Serial           = 不分配
```

Old 已 Exit，不可恢復 Execute。

Current 的意義仍是：

> last successfully committed activation。

Exception rethrow。

---

## Flow invariant broken

例如 Transition 指向不存在 State。

這不是 invalid request，而是 configuration invariant failure。

```text
Status = Faulted
Exception rethrow
```

---

## Initial.Enter throws

```text
Current = null
Serial = null
LastResult = null
Status = Faulted
Serial counter 不增加
Exception rethrow
```

---

# 27. Faulted Machine Behavior

Faulted 後：

### Execute

不可繼續正常 pipeline。

可直接 throw `InvalidOperationException`。

### Request

```text
Ignored
```

### Stop

允許終結 Faulted Machine：

```text
clear Current
clear Pending
Status = Stopped
```

不再次呼叫 Exit。

---

# 28. Extensibility

設計原則：

> Compatible, not omnipotent.

Project 未來可以：

```csharp
public class GameStateMachine
    : StateMachine<GameContext, GameOutput>
{
}
```

但：

- 不把所有 internals 改成 protected。
- 不讓所有 public method virtual。
- 不提供 arbitrary pipeline hooks。
- 不鼓勵 override Execute 而忘記 base.Execute。
- Core pipeline order 保持封裝。

只有真實需求出現時才增加穩定 extension point。

---

# 29. Explicit v1 Non-goals

v1 明確不實作：

- Dynamic Flow
- Pause / Resume
- Save / Restore
- Output History
- Request Queue
- Automatic Request Retry
- Request Cancellation
- Async Request Handle
- Network synchronization
- Replay system
- TimeSystem
- Gameplay object lifecycle
- arbitrary pipeline hooks
- Project-specific recovery strategy
- TransitionResult public reporting
- LastTransitionResult

不要因為看起來「Framework 應該有」而自行補回來。

---

# 30. Dynamic Flow Decision

Flow 在 Build 後固定。

如果未來某一個 Project 真正需要 runtime 修改 graph：

- 先做 Project-specific StateMachine / Flow variant。
- 不提前污染通用 Core。
- 只有多個 Project 出現相同需求後，才考慮 Core v2。

---

# 31. Save / Restore Future Compatibility

v1 不做 Save/Restore。

未來原則：

```text
SaveData != Context != State != StateMachine
```

保存 Project/world data，不 serialize 整台 runtime StateMachine。

Core v1 不需要讓每個 State / Transition / Context Serializable。

---

# 32. Suggested File Structure

```text
Torben.StateMachine/
│
├─ StateId.cs
├─ TransitionId.cs
├─ StateResult.cs
├─ StateMachineStatus.cs
├─ RequestResult.cs
│
├─ States/
│  ├─ IState.cs
│  └─ State.cs
│
├─ Guards/
│  ├─ IGuard.cs
│  └─ GuardResult.cs
│
├─ Transitions/
│  ├─ Transition.cs
│  └─ TransitionRequest.cs
│
├─ Flow/
│  ├─ IStateFlow.cs
│  ├─ StateFlow.cs
│  └─ StateFlowBuilder.cs
│
└─ Runtime/
   ├─ IStateMachine.cs
   ├─ StateMachine.cs
   └─ StateMachinePhase.cs
```

`TransitionKey` 可留在 Flow implementation 內，不必單獨 Public file。

---

# 33. Required Tests

至少覆蓋：

```text
NormalTransition_ExitsOldAndEntersNext

Transition_DoesNotExecuteNextStateInSameTick

InvalidRequest_DoesNotExitCurrentState

GuardDeny_DoesNotExitCurrentState

GuardDeny_DoesNotChangeSerialNumber

Complete_UpdatesLastResult

CompletedState_CanRemainCurrent

InterruptedState_DoesNotOverwriteLastResult

Stop_DoesNotCompleteCurrentState

Stop_WhenAlreadyStopped_DoesNothing

Stop_CallsExitOnlyOnce

Start_ClearsLastResult

SerialNumber_DoesNotResetAcrossStopAndStart

SerialNumber_IncrementsOnlyAfterSuccessfulEnter

EnterFailure_DoesNotCommitNextState

EnterFailure_DoesNotConsumeSerialNumber

InitialEnterFailure_LeavesCurrentNull

ExecuteException_FaultsMachine

GuardException_FaultsMachine

ExitException_FaultsMachine

EnterException_FaultsMachine

FaultedStop_DoesNotCallExitAgain

FaultedRequest_IsIgnored

BrokenFlowInvariant_FaultsMachine

Builder_RejectsDuplicateStateId

Builder_RejectsDuplicateTransitionKey

Builder_RejectsMissingInitialState

Builder_RejectsMissingFromState

Builder_RejectsMissingToState

Builder_CannotBeUsedAfterBuild
```

可增加 implementation-focused tests，但不要因此改 Public API。

---

# 34. Definition of Done

完成條件：

1. 上述 Frozen Public API 全部實作。
2. 正常 lifecycle 正確。
3. Guard Deny / Invalid Request 不 Fault。
4. lifecycle exception 正確 Fault + rethrow。
5. Enter failure 不 commit State / Serial。
6. Stop semantics 符合規格。
7. PendingRequest 行為符合 single-slot model。
8. StateResult / LastResult semantics 正確。
9. Builder 完成 structural validation。
10. Core behavior 有 automated tests。
11. Core 不依賴 Unity gameplay semantics。
12. 可作 reusable Core。
13. 文件包含：
    - Architecture
    - Lifecycle / Pipeline
    - API usage
    - Design Decisions
    - Explicit Non-goals
14. 不自行加入未核准功能。

---

# 35. Stop Condition

只有以下情況 BLOCKED 並要求人工決策：

1. 本 Frozen Spec 內部真的存在無法同時滿足的矛盾。
2. 正確實作必須改變既有 Public API。
3. 必須改變 Core / Project Responsibility Boundary。
4. 測試揭露兩種以上重大語意選擇，而且無法由本規格推導。

以下情況不要 BLOCKED，直接自行決定：

- private helper naming
- internal method decomposition
- Dictionary initialization
- test fixture organization
- private fields
- file grouping
- exception helper implementation
- null checking details
- internal constructor visibility
- implementation-only utility types

若目標 Unity/C# compiler 無法支援 Frozen API 中已指定的語法（例如 `readonly record struct`），不要自行替換 Public API；將它列為 compatibility BLOCKED 並回報。
````