# Torben.StateMachine v1 — Architecture and API

## 責任邊界

Core 擁有同步 pipeline、State lifecycle 調度、固定 Flow 查找、Guard 求值、單一 Pending Request、activation SerialNumber、最近一次完成的結果與 Fault 狀態。Project 擁有 Flow 定義、State、Guard、Context、Output、Request 時機、Unity 物件與更新驅動、時間系統及故障後策略。Core 不推論遊戲成功／失敗。

## 公開 API

- `StateId`：Flow node／State kind；`TransitionId`：Project 的 route intent。Transition key 是 `(From, TransitionId)`。Unity 6.5 相容版以 C# 9 `readonly struct` 實作原規格的 record struct 資料及值相等語意；原因與差異見 README。
- `IState<TContext,TOutput>`：`Id`、`IsComplete`、`Output`、`Enter(context)`、`Execute()`、`Exit()`。`State<TContext,TOutput>` 提供 `OnEnter`、`Complete(output)` 與重複完成保護。
- `IGuard<TContext>.Evaluate(context)` 回傳 `Allow` 或 `Deny`。多個 Guard 依順序 AND，遇 Deny 立即停止。
- `TransitionRequest<TContext>` 只攜帶 `TransitionId` 及 `Context`。同一份 Context 交給 Guard 與下一個 State 的 `Enter`。
- `IStateFlow<TContext,TOutput>` 提供 `InitialStateId`、`GetState`、`FindTransition`。`StateFlowBuilder` 用 `Initial`、`State`、`Transition` 配置，再用 `Build` 取得固定 Flow。
- `IStateMachine<TContext,TOutput>`／`StateMachine<TContext,TOutput>` 提供 `Status`、`CurrentStateId`、`CurrentSerialNumber`、`LastResult`、`Start`、`Execute`、`Stop`、`Request`。Concrete machine 的唯一公開建構式接收 `IStateFlow<TContext,TOutput>`。

## Lifecycle 與每 Tick 順序

`Start(initialContext)` 從 Flow 取初始 State，`Enter` 成功後才分配 SerialNumber、提交 Current、切換至 Running。若 Enter 期間呼叫 `Complete`，提交後立即建立 `StateResult`。

每次 `Execute()` 優先取出 Pending Request。取出後即不再是 Pending，因此處理期間提出的新 Request 可佔下一 Tick 的 Pending slot。有 Request 的 Tick 只處理 Request 並返回；不呼叫 Current 或 Next 的 `Execute`。沒有 Request 才呼叫 Current 的 `Execute`，並在其正常返回後擷取首次完成結果。

Transition pipeline：查找 `(Current, TransitionId)` → 逐一求值 Guards → 取得 Next State → `Old.Exit()` → `Next.Enter(context)` → checked SerialNumber 遞增 → 提交 Current → 返回。沒有 route 或 Guard Deny 均消耗 Request，維持 Running，不離開 Current，也不改 SerialNumber 或 LastResult。

`Complete(output)` 表示該 State activation 正常完成，不代表它已離開。完成後仍可維持 Current。每一個 activation 最多建立一次 `StateResult<TOutput>`；其中的 ID 與 SerialNumber 來自該次已提交的 activation。`Output` 在 `IsComplete == false` 時沒有有效語意，不能以是否為 null 判斷完成。未完成就被中斷、Stop 或 Fault，不產生 Result。`LastResult` 保留最近一次完成結果；新 Start 會清空它，SerialNumber 不重置。

`Stop()` 在 Running 時呼叫 Current 的 `Exit`，清除 Current 與 Pending，保留 LastResult；重複 Stop 不會重複 Exit。Faulted 時的 Stop 只清除 Current／Pending，避免再呼叫不可靠的 lifecycle。Stopped 時呼叫 Request 回傳 `Ignored`。

## 故障語意

Guard、Execute、Exit、Enter 的例外，以及 Flow 不變條件破壞，都使 machine 進入 `Faulted` 並原樣重新擲出例外。Core 不自動重試、補做 Exit 或還原遊戲物件。Guard 例外與 Execute 例外保留 Current；Exit 例外保留 Old，且不 Enter Next；Next.Enter 例外保留 Old 的 Current ID／Serial（即使 Old 已成功 Exit），不分配新 Serial。Initial.Enter 例外則 Current 與 LastResult 為 null、Serial 不增加。Faulted 的 `Execute` 擲出 `InvalidOperationException`，`Request` 回傳 `Ignored`，`Stop` 可終結機器而不再次 Exit。

Current ID 表示最後一個 **成功 Enter 並提交** 的 activation，不宣稱 Fault 後仍可執行它。SerialNumber 是每台 machine 的成功 activation 身分；Stop／Start 不重置，checked 溢位視為 Fault。

## Builder validation

加入時拒絕重複 StateId 或重複 `(From, TransitionId)`。Build 時驗證 Initial 已設定且存在、每條 Transition 的 From／To 均存在。Build 成功後 Builder 已消耗；再次配置或 Build 擲出 `InvalidOperationException`。不驗證 reachability、dead end、cycle、Guard 的業務邏輯或 Flow 品質。

## Design decisions

- Flow 在 Build 後固定；Dictionary 由 Flow 持有獨立副本，Transition 的 Guards 為唯讀副本。State instance 本身仍由 Project 實作及持有，應由 Project 決定是否共用實例。
- 單一 Pending slot 讓新 Request 取代尚未開始處理的 Request；`Accepted`／`Replaced`／`Ignored` 僅描述 slot 結果，不回報 Transition 結果。
- Guard 僅用 AND 與 short-circuit。複合 OR／NOT 屬於單一 Project Guard，不建立通用條件樹。
- Core 只記錄最近一次 `StateResult`，不規定 `TOutput` 的成功與失敗意義。
- Public pipeline 順序封裝；`StateMachine` 可繼承，但不提供任意 override hooks 或把內部狀態升為 protected。

## Explicit non-goals

Dynamic Flow、Pause／Resume、Save／Restore、Output History、Request Queue、Automatic Retry、Cancellation、Async Request Handle、Network Synchronization、Replay、TimeSystem、Gameplay Object Lifecycle、任意 pipeline hooks、Project recovery、TransitionResult 及 LastTransitionResult 都不屬於 v1。若將來需要 Save／Restore，Project 保存 world data，而非序列化整台 runtime machine；不要求 Context、State、Transition 可序列化。
