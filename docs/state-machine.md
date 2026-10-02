# Torben.StateMachine v1 使用與 API 說明

本手冊對應目前倉庫的整合版本: C# 9、.NET Standard 2.1, 純邏輯由 `Torben.Core` 編譯, namespace 為 `Torben.StateMachine`。Unity Host 由 `Torben.Runtime` 編譯, 示範程式由 `Torben.Samples` 系列組件編譯。

原契約為 [Frozen Implementation Specification](inbox/archive/2026-10-01/State%20Machine%20交接檔案/FROZEN_IMPLEMENTATION_SPEC.md), 已接受的 C# 9 語法差異見 [HANDOFF](inbox/archive/2026-10-01/State%20Machine%20交接檔案/HANDOFF.md)。本手冊說明現有行為, 不新增公開 API 或改寫核心契約。

## Architecture 與責任邊界

- `Assets/[TorbenJuniorUtility]/Scripts/Core/StateMachine/`: 固定 Flow、State lifecycle 調度、Guard 求值、單一 Pending slot、SerialNumber、完成結果與 Fault 狀態。這裡的 `Runtime/` 子資料夾仍是純 C#。
- `Assets/[TorbenJuniorUtility]/Scripts/Runtime/StateMachine/`: Unity Host, 將 Unity lifecycle 接到 Core。Core 不引用 UnityEngine 或 UnityEditor。
- `Assets/[TorbenJuniorUtility]/Scripts/Samples/StateMachine/`: 門鎖示範的 Context、Output、具體 State、Guard 與 Flow。`Runtime/` 中的 `DoorStateMachineHost` 是可附加到 GameObject 的具體 Host。
- `dotnet/Library.Tests/StateMachineContractTests.cs`: 42 項交接行為案例與 8 項 completion policy 回歸案例, 共 50 項 NUnit 契約測試。
- `dotnet/Samples.Tests/DoorFlowTests.cs`: 門鎖示範與 State 實例獨立性測試。
- `Assets/[TorbenJuniorUtility]/Scripts/Tests/PlayMode/StateMachineHostTests.cs`: Unity lifecycle 整合測試。
- `Assets/[TorbenJuniorUtility]/Scripts/Samples/StateMachine/Tests/PlayMode/DoorStateMachineHostTests.cs`: 門鎖 Host 示範整合測試, 編入 `Samples.Tests.PlayMode`。

Core 處理機器執行規則。Project 決定 Flow 內容、具體 State/Guard、Context/Output 的業務意義、Request 時機、Unity 物件、時間系統與故障後策略。完成結果不代表遊戲成功或失敗。

目前沒有獨立的 `Torben.StateMachine.asmdef`: 所有 Core 來源由既有 `Torben.Core.asmdef` 與 `dotnet/Torben.Core/Torben.Core.csproj` 編譯。新增 StateMachine Core 檔案會由既有 glob 自動收進 .NET 專案。

## 純 C# 使用範例

以下範例只依賴 `Torben.Core`, 示範自訂 State、Guard、固定 Flow 與 Host 端驅動。State 在 `OnEnter` 完成工作, 不自動退出或轉換。

```csharp
using Torben.StateMachine;

namespace Torben.StateMachine.Usage
{
    public static class DoorExample
    {
        public static string Run()
        {
            var idleId = new StateId("idle");
            var doorId = new StateId("door");
            var openId = new TransitionId("open");
            var flow = new StateFlowBuilder<bool, string>()
                .Initial(idleId)
                .State(new IdleState())
                .State(new DoorState())
                .Transition(openId, idleId, doorId, new HasKeyGuard())
                .Build();
            var machine = new StateMachine<bool, string>(flow);

            machine.Start(false);
            machine.Request(new TransitionRequest<bool>(openId, false));
            machine.Execute(); // Guard Deny: 仍為 idle, Serial 不變.

            machine.Request(new TransitionRequest<bool>(openId, true));
            machine.Execute(); // Exit idle, Enter door, 擷取完成結果.
            // 此 Tick 不呼叫 DoorState.Execute(); 下一個 Tick 才呼叫.
            machine.Execute();

            string result = machine.CurrentStateId?.Value + ": "
                + machine.LastResult?.Output;
            machine.Stop();
            return result; // "door: Door opened"
        }

        sealed class IdleState : State<bool, string>
        {
            public override StateId Id => new StateId("idle");
            protected override void OnEnter(bool context) { }
            public override void Execute() { }
        }

        sealed class DoorState : State<bool, string>
        {
            public override StateId Id => new StateId("door");
            protected override void OnEnter(bool context) => Complete("Door opened");
            public override void Execute() { }
        }

        sealed class HasKeyGuard : IGuard<bool>
        {
            public GuardResult Evaluate(bool context)
                => context ? GuardResult.Allow : GuardResult.Deny;
        }
    }
}
```

`Request()` 只放入 Pending slot, 不立即處理路徑。純 C# Host 必須自行呼叫 `Execute()`; 一次呼叫是一個 Tick, 不是由 Core 決定的時間間隔。

## 公開 API

### 識別值與資料

- `StateId(string value)`: Flow 節點或 State kind。`Value` 是 Project 定義的識別字串, 不代表一次 activation。
- `TransitionId(string value)`: Project 的路徑意圖。路徑查找鍵是 `(From StateId, TransitionId)`, 同一 TransitionId 可以出現在不同 From。
- `TransitionRequest<TContext>(TransitionId transitionId, TContext context)`: 攜帶路徑意圖與 Context。同一份 Context 交給 Guards 與目標 State 的 `Enter`。
- `StateResult<TOutput>(StateId stateId, long serialNumber, TOutput output)`: 正常完成的 activation 結果, 提供 `StateId`、`SerialNumber` 與 `Output`。
- 以上四個型別是 C# 9 `readonly struct`, 支援值相等、hash、`==`/`!=`、`Deconstruct` 與 `ToString`。它們不提供 record 的 `with`/`init` 語法能力。
- `StateMachineStatus`: `Stopped`、`Running`、`Faulted`。
- `GuardResult`: `Allow`、`Deny`。
- `RequestResult`: `Accepted`、`Replaced`、`Ignored`, 只描述 slot 結果, 不表示轉換成功。

### State 與 Guard

`IState<TContext, TOutput>` 提供 `Id`、`IsComplete`、`Output`、`Enter(context)`、`Execute()` 與 `Exit()`。`Id` 應在機器使用期間保持固定。

繼承 `State<TContext, TOutput>` 時, 實作 `Id`、`OnEnter(context)` 與 `Execute()`; 可覆寫 `Exit()`。基底 `Enter` 會先清除上次 completion 與 Output, 再呼叫 `OnEnter`。只有子類能呼叫 protected `Complete(output)`; 同一 activation 重複 Complete 會拋例外。

`IsComplete == false` 時, Output 沒有有效業務語意。以 `IsComplete` 判斷完成, 不使用 `Output != null`。Complete 表示工作正常完成, 不自動 Exit, 不自動 Transition; 已完成 State 仍可作為 Current 被 Execute。

`IGuard<TContext>.Evaluate(context)` 回傳 Allow 或 Deny。Guards 依設定順序做 AND, 遇 Deny 立即停止。Project 可用單一 Guard 實作自己的 OR/NOT 規則。

### Flow 與 Builder

`IStateFlow<TContext, TOutput>` 提供:

- `InitialStateId`: 初始節點。
- `GetState(StateId id)`: 取得 State; 缺少節點屬於配置不變條件破壞。
- `FindTransition(StateId currentState, TransitionId transitionId)`: 查找路徑; 沒有路徑回傳 null, 是正常無效 Request。

`StateFlowBuilder<TContext, TOutput>` 提供可串接的 `Initial(stateId)`、`State(state)`、`Transition(id, from, to, params guards)` 及 `Build()`。`Build()` 回傳 `IStateFlow<TContext, TOutput>`。

加入時拒絕重複 StateId 或重複 `(From, TransitionId)`; Build 驗證 Initial 已設定且存在, 所有路徑的 From/To 都存在。Build 成功後 Builder 已消耗, 再次配置或 Build 會拋例外。Builder 不驗證可達性、死路、cycle、Guard 業務規則或 Flow 品質。

`Transition<TContext>` 公開 `Id`、`From`、`To` 與唯讀 `Guards`。其建構式為 internal, 由 Builder 建立。Flow 保存 Dictionary 副本, Transition 保存 Guards 集合副本; State/Guard 實例本身不是深複製。每台 machine 建議建立自己的可變 State 實例。

### Machine

`StateMachine<TContext, TOutput>` 的公開建構式接收 `IStateFlow<TContext, TOutput>`。`IStateMachine<TContext, TOutput>` 與具體類別公開:

- `Status`: 機器狀態。
- `CurrentStateId`、`CurrentSerialNumber`: 沒有 Current 時兩者皆為 null; 有 Current 時兩者皆有值。
- `LastResult`: 最近一次擷取的 activation 完成結果, 尚無結果時為 null。不保證屬於 Current, 需要時核對 `StateId` 與 `SerialNumber`。
- `Start(initialContext)`: 只允許從 Stopped 開始。清空 Pending/LastResult, Initial.Enter 成功後才分配 Serial 並提交 Current。
- `Execute()`: Running 時執行一個 Tick; Stopped 時 no-op; Faulted 時拋 `InvalidOperationException`。
- `Request(request)`: Running 時接受或取代 Pending; Stopped/Faulted 時回傳 Ignored。不直接 Exit/Enter, 也不直接更新 LastResult; 結果可能在後續 `Execute()` 擷取完成時更新。
- `Stop()`: Running 時 Exit Current 並清除 Current/Pending; Stopped 時 no-op; Faulted 時只清除狀態, 不再 Exit。

Serial counter 在同一 machine lifetime 單調遞增, Stop/Start 不重置。Enter 失敗不分配 Serial; checked 溢位視為 Fault。Current 表示最後成功 Enter 並提交的 activation, 不保證 Fault 後 State 仍可安全執行。

## Lifecycle / Pipeline

沒有 Pending 的 Tick 呼叫 Current.Execute, 正常返回後擷取首次 completion。有 Pending 的 Tick 取出並消耗 Request, 只處理路徑, 然後返回; 不呼叫 Current 或 Next 的 Execute。

正常轉換順序是查找路徑、順序求值 Guards、取得 Next、Old.Exit、Next.Enter、遞增 Serial、提交 Current。Next.Execute 留到下一個 Tick。

取出的 Request 已經不占 Pending slot, 所以在 Running 的處理期間提出新 Request, 可以留給下一個 Tick。單一 slot 只保留尚未開始處理的最新 Request; Deny 或沒有路徑都消耗 Request, 不保留等待、不重試。

`Complete(output)` 表示正常工作完成, 合法時機為 Enter 與 Execute; Exit 只負責清理。只在 lifecycle callback 正常返回後擷取 Result: Enter 完成時, 須先成功分配 Serial 並提交 Current 才擷取; Execute 完成時, 在正常返回後擷取。callback 在 Complete 後又拋例外時不擷取, 依 Fault 規則處理。每個 activation 最多擷取一次。中斷或 Stop 不偽造完成結果。

Exit 期間才產生的 completion 不擷取, 轉換與 Stop 採相同規則 (設計端確認, 補充並限縮原規格 §18)。在 Exit 中呼叫 Complete 違反 State 契約, v1 不以 Public API 強制阻止, 只保證 Machine 不擷取該結果。舊 activation 的合法完成已在先前 Enter/Execute 返回後擷取; 轉換回同一個 State 實例時, Next.Enter 會重設 completion, 之後讀到的資料屬於新 activation。

LastResult 在中斷、Stop 或 Fault 時保留最近一次已擷取的結果; 新 Start 清空它。新 State 在 Enter 就完成時, 處理該 Request 的那次 Execute 會更新 LastResult; 需要保存特定結果時, 在可能更新它的 Execute 前保存, 並核對 StateId 與 SerialNumber。不要將 LastResult 誤認為 Current 的即時 Output。

Core 的 Start/Execute/Stop 不允許巢狀 lifecycle 呼叫。Project 需自行協調驅動順序與執行緒; v1 不提供多執行緒同步機制。

## 故障語意

無路徑與 Guard Deny 是正常結果: 保持 Running, 不 Exit、不 Enter, Current/Serial/LastResult 不變。Guard 或 lifecycle 的例外、錯誤的自訂 Flow 回傳值與不變條件破壞會進入 Faulted, 清除 Pending 並重新拋出例外。

- Guard/Current.Execute 例外: 保留最後提交的 Current, 不自動 Exit。
- Old.Exit 例外: 保留 Old, 不 Enter Next。
- Next.Enter 例外: 保留 Old 的 ID/Serial, 即使 Old 已成功 Exit; 不分配新 Serial。
- Initial.Enter 例外: Current/Serial/LastResult 為 null, Serial counter 不增加。

Core 不補做 lifecycle、不回復遊戲物件、不自動重試。Project 決定故障後如何處置; 可用 Stop 終結 Faulted machine, 之後才可 Start 新 run。

## Unity Host 使用

繼承 `StateMachineHost<TContext, TOutput>`, 實作 `CreateMachine()` 與 `CreateInitialContext()`。Unity 可附加的是非泛型具體子類, 例如 [DoorStateMachineHost](../Assets/%5BTorbenJuniorUtility%5D/Scripts/Samples/StateMachine/Runtime/DoorStateMachineHost.cs)。

Host 的實際 lifecycle:

1. Awake 建立機器; `Machine` 屬性在 Awake 完成後才可使用。
2. OnEnable 在 Stopped 時呼叫 Start, 使用新初始 Context。
3. Update 在 Running 時呼叫一次 Execute; Pending 處理規則與 Core 相同。
4. OnDisable 呼叫 Stop。正常停止會 Exit 一次; Faulted 停止不再 Exit。
5. 重新啟用使用同一機器開始新 run, 清空 LastResult/Pending, 保留 Serial counter。

Host 沒有吞掉 Core 例外; Unity 記錄 lifecycle 中拋出的例外。機器 Faulted 後, 後續 Update 不再 Tick。停用會 Stop 故障機器, 再啟用會開始新 run; 是否重新啟用由 Project 決定。

門鎖範例可直接附加 `DoorStateMachineHost` 到 GameObject。`RequestOpen(false)` 在後續 Update 被 Deny; `RequestOpen(true)` 在後續 Update 進入 door 並產生 `Machine.LastResult.Value.Output.Message == "Door opened"`。送出 Request 後不要在同一次呼叫內假設已有新結果。

由 Host 驅動時, Project 只送 Request 與讀結果, 不再額外每幀呼叫 Machine.Execute, 以免一幀執行多個 Tick。自訂 Host 時保留基底 lifecycle 的呼叫順序。

## Design Decisions

- 固定 Flow 與 consumed Builder 讓配置驗證集中在建立階段, 執行期只查找既有節點與路徑。
- namespace 表達功能, assembly 表達編譯邊界; 不因資料夾分組而拆出 States/Flow/Runtime namespace。
- 單一 Pending slot 表達最新尚未處理意圖, 不提供 Queue 或轉換結果追蹤 API。
- activation Serial 與 StateId 分開, 可區分同一節點的多次進入。
- StateResult 只記正常完成, 不把停止、中斷、故障或遊戲成功/失敗混在一起。
- Core pipeline 封裝, 雖可繼承 StateMachine, v1 不提供任意 pipeline override hooks。
- Unity Host 與示範語意留在 Core 外, 維持純 C# 共用原始碼。

## 配置、測試與驗證範圍

Builder、Flow、State/Guard、Host 與示範 Context/Output 的建立允許配置。現有 Guards 列舉、自訂 lifecycle/Flow/Guard、泛型值比較或字串表示可能配置; 不能將 .NET 正確性測試當作 Unity 零配置或效能驗證。公開 API 的 XML remarks 補充各操作配置行為。

2026-10-02 completion policy 複查: 執行 `dotnet test "dotnet/TorbenJunior.sln"`, .NET 契約 50/50、範例與計算機 8/8, 共 58 項通過, 退出碼為 0。此次未重跑 Unity EditMode/PlayMode。

先前整合驗證紀錄為 Unity EditMode 8/8、Unity PlayMode 5/5, 各指令退出碼為 0; 不代表本次 completion policy 修改後的 Unity 驗證。PlayMode 含四項 Host/門鎖整合案例與一項計算機回歸案例。GC 量測與 Player/Android IL2CPP 建置尚未執行。

執行方式與工具選擇見 [測試手冊](dotnet-test.md)。

## Explicit Non-goals

v1 不包含 Dynamic Flow、Pause/Resume、Save/Restore、Output History、Request Queue、Automatic Retry、Cancellation、Async Request Handle、Network Synchronization、Replay、TimeSystem、Gameplay Object Lifecycle、任意 pipeline hooks、Project recovery、TransitionResult 或 LastTransitionResult。

需要保存時, Project 保存 world data; 不將整台 machine、State 或 Context 當作 v1 的序列化契約。上述能力需要獨立需求與契約決策, 不由整合過程自行補入。
