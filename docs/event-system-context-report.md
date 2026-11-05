# Event 系統設計前置報告: StateMachine 與現有溝通方式

對象: 看不到原始碼、要為本函式庫設計 Event 系統的 AI。
依據: 2026-11-05 `main` 分支 (提交編號 `c436eac`) 的原始碼與文件。凡是「推論」都會標明, 其餘皆可在列出的檔案中查到。

## 0. 必要背景

- Unity 6.5, C# 9, .NET Standard 2.1。目標平台 PC / Android / iOS, 行動平台用 IL2CPP (AOT), 執行期不能產生程式碼。
- 函式庫分三層:
  - **Core** (`Scripts/Core/`, 組件 `Torben.Core`): 純 C#, 不得引用 `UnityEngine` / `UnityEditor`, 以 `dotnet test` 測試。
  - **Runtime** (`Scripts/Runtime/`, 組件 `Torben.Runtime`): 必須用 Unity API 的部分, 盡量精簡。
  - **Editor** (`Scripts/Editor/`): Editor 工具。
  - **Samples** 可引用函式庫; 函式庫不得引用 Samples。
- 目前只有兩個系統: **Log 系統** (Core + Runtime + Editor) 與 **StateMachine** (Core + Runtime Host)。另有範例 Calculator 與門鎖 StateMachine。
- 以下路徑中的「專案根」= `Assets/[TorbenJuniorUtility]/`。

---

## 1. StateMachine 的設計

### 1.1 用途與責任邊界

`Torben.StateMachine` v1 是**純 C#、同步、固定 Flow** 的狀態機 Core。設計原則見規格 §28: "Compatible, not omnipotent."

| Core 負責 | Project (使用端) 負責 |
|---|---|
| 執行 pipeline、State lifecycle 調度、Flow 查找、Guard 求值、單一 Pending Request、activation SerialNumber、最近一次完成結果、Fault 狀態 | Flow 定義、State / Guard 實作、Context / Output 的意義、**Request 時機**、Unity 物件、時間系統、**Update 驅動**、故障後處置 |

來源: `docs/inbox/archive/2026-10-01/State Machine 交接檔案/FROZEN_IMPLEMENTATION_SPEC.md` §1; `docs/state-machine.md`「Architecture 與責任邊界」。

檔案位置:

- Core: 專案根 `Scripts/Core/StateMachine/` (含 `States/`、`Guards/`、`Transitions/`、`Flow/`、`Runtime/`; `Runtime/` 子資料夾仍是純 C#)
- Unity Host: 專案根 `Scripts/Runtime/StateMachine/StateMachineHost.cs`
- 範例: 專案根 `Scripts/Samples/StateMachine/`
- 說明: `docs/state-machine.md`; 原始凍結規格: `docs/inbox/archive/2026-10-01/State Machine 交接檔案/`

### 1.2 公開 API (型別定義)

全部在 `namespace Torben.StateMachine`。以下為實際原始碼去掉 XML 註解與方法本體後的簽章。

```csharp
// ---- 識別值與資料 (C# 9 readonly struct, 值相等, 有 ==, !=, Deconstruct, ToString) ----
public readonly struct StateId : IEquatable<StateId>
{
    public StateId(string value);
    public string Value { get; }          // 以 StringComparer.Ordinal 比較
}

public readonly struct TransitionId : IEquatable<TransitionId>
{
    public TransitionId(string value);
    public string Value { get; }
}

public readonly struct TransitionRequest<TContext> : IEquatable<TransitionRequest<TContext>>
{
    public TransitionRequest(TransitionId transitionId, TContext context);
    public TransitionId TransitionId { get; }
    public TContext Context { get; }      // 同一份交給 Guards 與目標 State 的 Enter
}

public readonly struct StateResult<TOutput> : IEquatable<StateResult<TOutput>>
{
    public StateResult(StateId stateId, long serialNumber, TOutput output);
    public StateId StateId { get; }
    public long SerialNumber { get; }     // activation 身分, 每台 machine 單調遞增, Stop/Start 不重置
    public TOutput Output { get; }
}

public enum StateMachineStatus { Stopped, Running, Faulted }
public enum GuardResult { Allow, Deny }
public enum RequestResult { Accepted, Replaced, Ignored }  // 只描述 Pending slot, 不表示轉換成功

// ---- State 與 Guard ----
public interface IState<TContext, TOutput>
{
    StateId Id { get; }
    bool IsComplete { get; }
    TOutput? Output { get; }              // IsComplete == false 時沒有有效語意
    void Enter(TContext context);
    void Execute();
    void Exit();
}

public abstract class State<TContext, TOutput> : IState<TContext, TOutput>
{
    public abstract StateId Id { get; }
    public bool IsComplete { get; private set; }
    public TOutput? Output { get; private set; }
    public void Enter(TContext context);              // 先清 IsComplete / Output, 再呼叫 OnEnter
    protected abstract void OnEnter(TContext context);
    public abstract void Execute();
    public virtual void Exit() { }
    protected void Complete(TOutput output);          // 同一 activation 重複呼叫會拋 InvalidOperationException
}

public interface IGuard<TContext>
{
    GuardResult Evaluate(TContext context);           // 規格要求無副作用; 多個 Guard 依序 AND, 遇 Deny 立即停止
}

// ---- Flow ----
public sealed class Transition<TContext>
{
    internal Transition(TransitionId id, StateId from, StateId to, IGuard<TContext>[] guards);
    public TransitionId Id { get; }
    public StateId From { get; }
    public StateId To { get; }
    public IReadOnlyList<IGuard<TContext>> Guards { get; }
}

public interface IStateFlow<TContext, TOutput>
{
    StateId InitialStateId { get; }
    IState<TContext, TOutput> GetState(StateId id);
    Transition<TContext>? FindTransition(StateId currentState, TransitionId transitionId); // 無路徑回傳 null
}

public sealed class StateFlowBuilder<TContext, TOutput>
{
    public StateFlowBuilder<TContext, TOutput> Initial(StateId stateId);
    public StateFlowBuilder<TContext, TOutput> State(IState<TContext, TOutput> state);
    public StateFlowBuilder<TContext, TOutput> Transition(
        TransitionId id, StateId from, StateId to, params IGuard<TContext>[] guards);
    public IStateFlow<TContext, TOutput> Build();     // Build 後 Builder 已消耗, Flow 固定不可改
}

// ---- Machine ----
public interface IStateMachine<TContext, TOutput>
{
    StateMachineStatus Status { get; }
    StateId? CurrentStateId { get; }                  // 沒有 Current 時與 CurrentSerialNumber 同為 null
    long? CurrentSerialNumber { get; }
    StateResult<TOutput>? LastResult { get; }         // 最近一次擷取的完成結果, 不保證屬於 Current
    void Start(TContext initialContext);
    void Execute();                                   // 一次呼叫 = 一個 Tick
    void Stop();
    RequestResult Request(TransitionRequest<TContext> request);
}

public class StateMachine<TContext, TOutput> : IStateMachine<TContext, TOutput>
{
    public StateMachine(IStateFlow<TContext, TOutput> flow);
    // 成員同 IStateMachine; pipeline 封裝, 沒有 virtual hook
}
```

Unity Host (Runtime 組件, 同 namespace):

```csharp
public abstract class StateMachineHost<TContext, TOutput> : MonoBehaviour
{
    public IStateMachine<TContext, TOutput> Machine { get; private set; }
    protected abstract IStateMachine<TContext, TOutput> CreateMachine();
    protected abstract TContext CreateInitialContext();
    protected virtual void OnHostAwake() { }
    protected virtual void OnHostEnabled() { }
    protected virtual void OnHostDisabled() { }

    protected void Awake();      // 建立 Machine, 再呼叫 OnHostAwake
    protected void OnEnable();   // Stopped 時 Machine.Start(CreateInitialContext()), 再呼叫 OnHostEnabled
    protected void Update();     // Running 時呼叫一次 Machine.Execute()
    protected void OnDisable();  // 呼叫 OnHostDisabled, 再 Machine.Stop()
}
```

範例 (專案根 `Scripts/Samples/StateMachine/Runtime/DoorStateMachineHost.cs`):

```csharp
public sealed class DoorStateMachineHost : StateMachineHost<DoorContext, DoorOutput>
{
    protected override IStateMachine<DoorContext, DoorOutput> CreateMachine()
        => new StateMachine<DoorContext, DoorOutput>(DoorFlow.Create());
    protected override DoorContext CreateInitialContext() => new DoorContext(false);

    public RequestResult RequestOpen(bool hasKey)
        => Machine.Request(new TransitionRequest<DoorContext>(new TransitionId("open"), new DoorContext(hasKey)));
}
```

### 1.3 狀態轉換如何觸發

**只有一種方式: 外部直接呼叫 `Request()`, 再由下一次 `Execute()` 處理。** 沒有事件觸發, 也沒有 Core 自動做的條件輪詢。

1. 任何持有 machine 參照的程式 (通常是 Host 子類的公開方法, 如 `RequestOpen`) 呼叫 `Machine.Request(new TransitionRequest<TContext>(transitionId, context))`。
   - 只放進**單一 Pending slot**; 已有未處理的 Request 時直接取代 (`Replaced`)。不是佇列。
   - Stopped / Faulted 時回傳 `Ignored`。
2. 下一次 `Execute()` (Unity 中由 `StateMachineHost.Update` 每幀呼叫一次):
   - **有 Pending**: 取出並消耗 → 以 `(CurrentStateId, TransitionId)` 查找路徑 → 依序求值 Guards → `Old.Exit()` → `Next.Enter(context)` → 遞增 Serial → 提交 Current → 若 Next 在 Enter 中 `Complete` 就擷取結果 → **返回**。這個 Tick 不呼叫任何 State 的 `Execute`。
   - **沒有 Pending**: 呼叫 `Current.Execute()`, 正常返回後若首次完成就寫入 `LastResult`。
3. 沒有路徑或 Guard Deny: Request 被消耗, 保持 Running, 不 Exit / Enter, **不重試, 也不通知任何人**。
4. State 的 `Complete(output)` **不會**自動轉換。要轉換, 必須有人看到完成後再送 Request。

其他規則:

- Guard 是「被動條件」: 只在 Request 被處理時求值一次, 不是持續輪詢的條件。
- `Start` / `Execute` / `Stop` 不允許巢狀呼叫 (內部以 `StateMachinePhase` 檢查, 違反時拋 `InvalidOperationException`)。`Request` 沒有這個檢查, 所以 State 在 `Execute` 中送 Request 是允許的, 會留到下一個 Tick。
- Guard / lifecycle 拋例外 → machine 變 `Faulted` 並原樣重拋; Core 不重試、不補 Exit。
- 執行緒: v1 不提供同步機制, 由使用端協調。

**外部如何得知狀態變化: 輪詢。** 使用端每次需要時讀 `Status`、`CurrentStateId`、`CurrentSerialNumber`、`LastResult`。文件明確要求以 `IsComplete` / `LastResult` 判斷完成, 並用 `StateId + SerialNumber` 核對結果屬於哪一次 activation (`docs/state-machine.md`「Lifecycle / Pipeline」)。測試也用 `Func<bool>` 輪詢等待 (例: 專案根 `Scripts/Tests/PlayMode/StateMachineHostTests.cs:126` 的 `Until(System.Func<bool> condition)`)。

### 1.4 規劃時減少事件使用的決定

**先講結論: 文件中沒有任何一處寫著「原本要在 X 用事件, 改成 Y, 理由是 Z」。** 交接包 (`FROZEN_IMPLEMENTATION_SPEC.md`、`ARCHITECTURE.md`、`HANDOFF.md`、`README.md`)、`docs/state-machine.md`、git 提交訊息 (提交編號 `b4e20d3` 為 StateMachine 加入) 都查過, 沒有 `event`、`OnStateChanged`、`OnTransition` 之類的字眼, 也沒有「改用輪詢」的紀錄。規格是以「凍結的最終結果」形式交付, 沒有保留討論過程。

能引用的是以下**明文排除**的項目, 它們實際上就是事件 / 回呼可能出現的位置:

| 可能用事件的位置 | 規格的處置 | 出處 |
|---|---|---|
| Transition 上的回呼 | Transition 不得加入 `callback`、`IsEnabled`、`Priority`、執行期可變狀態 | `FROZEN_IMPLEMENTATION_SPEC.md` §8 |
| pipeline 各階段的 hook (例如轉換前後通知) | 「不提供 arbitrary pipeline hooks」; 「Core pipeline order 保持封裝」; 「只有真實需求出現時才增加穩定 extension point」 | 同 §28、§29 |
| 轉換結果通知 | 不公開 `TransitionResult` 與 `LastTransitionResult`; `RequestResult` 只描述 slot | 同 §3、§12、§23 (「v1 不公開 TransitionResult。」)、§29 |
| 非同步請求完成通知 | 不提供 `Async Request Handle`、`Request Cancellation` | 同 §29 |
| 完成結果歷史 | 不提供 `Output History`, 只保留最近一次 `LastResult` | 同 §12、§29 |
| Machine 的狀態屬性 | 不提供 `IsRunning`、公開 `Phase` | 同 §3、§12 |

規格 §29 的結語: 「不要因為看起來『Framework 應該有』而自行補回來。」

取代做法 (由程式與文件可直接確認):

- **通知 → 讀取狀態**: 用 `Status` / `CurrentSerialNumber` / `LastResult` 輪詢; `SerialNumber` 讓輪詢端分辨「同一個 State 被進入了第二次」。
- **Host 生命週期的擴充 → 模板方法**: `StateMachineHost` 提供 `protected virtual OnHostAwake / OnHostEnabled / OnHostDisabled`, 而不是事件 (`StateMachineHost.cs`)。
- **Project 擴充 → 繼承**: 規格 §28 允許 `class GameStateMachine : StateMachine<GameContext, GameOutput>`, 但不把內部改成 `protected`、不讓公開方法 `virtual`。

理由 (規格中有寫的部分):

- Core 只負責執行規則, 不理解專案語意; 完成結果不代表遊戲成功或失敗 (§1、`docs/state-machine.md`)。
- 「Compatible, not omnipotent」, 有真實需求才加穩定的擴充點 (§28)。
- `docs/state-machine.md`「Design Decisions」: 「單一 Pending slot 表達最新尚未處理意圖, 不提供 Queue 或轉換結果追蹤 API」; 「Core pipeline 封裝, 雖可繼承 StateMachine, v1 不提供任意 pipeline override hooks」。

推論 (文件未寫明, 僅供參考): 這些排除讓 pipeline 的同步、單執行緒、不可重入順序容易驗證 (50 項契約測試即依此撰寫); 若加入轉換回呼, 回呼內呼叫 `Execute` / `Stop` 會撞上巢狀檢查, 回呼拋例外也需要另定 Fault 語意。新增事件時需要回答這兩點。

---

## 2. 專案中現有的事件類用法

搜尋範圍: 專案根 `Scripts/` 全部 58 個 `.cs`。

**總覽: 專案中沒有任何 C# `event` 關鍵字宣告、沒有 `UnityEvent` 欄位、沒有 `InputAction` 回呼 (`performed` / `started` / `canceled`)、沒有 `SendMessage` / `BroadcastMessage`。** 現有的「訂閱」都是對 Unity / Editor 內建事件的訂閱, 或以介面 / 靜態欄位實作的觀察者。

### 2.1 清單

| # | 位置 | 種類 | 用途 | 訂閱 | 取消 |
|---|---|---|---|---|---|
| 1 | `Scripts/Core/Logs/Log.cs:75` | `public static System.Action<ILogSink, System.Exception> SinkErrorHandler` (公開靜態**欄位**, 非 `event`) | 某個 sink 的 `Write` 拋例外時回報; 每個 sink 實例只回報第一次 | `DebugLogs.Install` 以 `=` 指定為 `ReportSinkError` (`Scripts/Runtime/Logs/DebugLogs.cs:129`) | 不取消; 每次 Install 以 `=` 覆寫, 所以不會累積 |
| 2 | `Log.AddSink / RemoveSink / ClearSinks` + `interface ILogSink { void Write(in LogEntry entry); }` | 介面式觀察者 (copy-on-write 陣列) | `Log.Information` 等同步呼叫每個 sink | `DebugLogs.Install` 加入 `DebugLogs` 與 `DebugWindow` 的 `LogHistorySink` (`DebugLogs.cs:136-137`); 測試加入 `ThrowingSink` | `Install` 開頭 `Log.ClearSinks()` (`DebugLogs.cs:128`), 註解說明是為了關閉 Domain Reload 時不重複加入; 離開 Play 時不移除 |
| 3 | `Application.logMessageReceivedThreaded` | Unity 內建 C# event | 收集 Unity 引擎訊息到 `DebugWindow.UnityHistory`; 可能在任何執行緒觸發 | `DebugLogs.isUnityLog` setter 先 `-=` 再 `+=` (`DebugLogs.cs:42-43`) | setter 設 false 時; `Application.quitting` 時 (`DebugLogs.cs:97`) |
| 4 | `Application.quitting` | Unity 內建 C# event | 結束時取消 #3 | `DebugLogs.Install` 先 `-=` 再 `+=` (`DebugLogs.cs:140-141`) | **沒有取消** (見 §5) |
| 5 | `EditorApplication.playModeStateChanged` | Editor 內建 C# event | 進入 Play 時檢查 LogSettings, 有問題輸出一次警告 | `[InitializeOnLoad]` 靜態建構式先 `-=` 再 `+=` (`Scripts/Editor/Logs/LogSettingsChecker.cs:193-194`) | **沒有取消**; Editor 靜態類別, Domain Reload 時自然消失, 先 `-=` 防重複 |
| 6 | `Button.onClick.AddListener(...)` × 9 (數字 0-9 共用迴圈、+ − × ÷ = C) | UGUI 的 `UnityEvent` (以程式訂閱, 非 Inspector) | 計算機按鈕 → 呼叫 Model 再更新顯示 | `Scripts/Samples/Calculator/Runtime/CalculatorView.cs:51-59` 的 `Initialize()` (由 `Awake` 呼叫, `_model != null` 時跳過, 防重複) | **沒有 `RemoveListener`**; 按鈕與 View 同場景生命週期 (範例程式) |
| 7 | `Scripts/Editor/Logs/LogSettingsChecker.cs:21` `public readonly System.Action Fix` | 委派欄位 (資料, 非訂閱) | 檢查項目附帶的「一鍵修正」動作, 由 UI 按鈕呼叫 | — | — |
| 8 | `[RuntimeInitializeOnLoadMethod]` (`DebugLogs.cs:103` SubsystemRegistration; `Scripts/Runtime/Logs/DebugWindow.cs:422` AfterSceneLoad) | Unity 啟動回呼 (屬性) | 啟動時安裝 Log; 場景載入後才顯示視窗 | — | — |
| 9 | `System.Func<bool>` 參數 (`StateMachineHostTests.cs:126`、`DoorStateMachineHostTests.cs:39`) | 測試用條件委派 | `yield` 等待直到條件成立 (輪詢) | — | — |
| 10 | `StateMachineHost` 的 `OnHostAwake / OnHostEnabled / OnHostDisabled` | `protected virtual` 模板方法 (非事件) | 子類掛 lifecycle 邏輯 | — | — |

InputSystem: 專案有安裝 `com.unity.inputsystem` 1.19.0, 但程式中唯一用法是 `Scripts/Samples/Calculator/Editor/CalculatorSceneBuilder.cs:44-45` 建立 `EventSystem` + `InputSystemUIInputModule` 並 `AssignDefaultActions()`, 讓 UGUI 按鈕可點。沒有任何 `InputAction` 回呼。

### 2.2 取消訂閱的整理

- 有成對取消: #3 (`logMessageReceivedThreaded`)。
- 以「先 `-=` 再 `+=`」防重複、但不取消: #4、#5。這是專案現有的固定寫法, 目的寫在註解: 關閉 Domain Reload (Enter Play Mode Options) 時靜態狀態會保留。
- 以「整批清空」代替取消: #2 (`ClearSinks`)、#1 (`=` 覆寫)。
- 完全沒有取消: #6 `onClick` (範例, 生命週期相同, 實際上不會洩漏)。

---

## 3. AGENTS.md 中相關的規則

AGENTS.md **沒有**直接規範「物件之間如何溝通」或「何時用事件」的條文。與事件 / 委派相關的只有一條, 其餘是會限制 Event 系統設計的周邊規則。以下為原文或精確摘述:

**直接相關 (「書寫慣例」一節):**

> 委派點: Runtime 中需在 Inspector 設定的事件, 優先用 `UnityEvent` (含泛型) + lambda; Core 與熱路徑使用 `System.Action` / `System.Func` 或自訂 delegate。

> 必須用 `System` 時寫完整名, 勿單獨 `using System;` (Core 不受此限)。

(這就是為什麼現有程式寫 `System.Action<ILogSink, System.Exception>`。)

**會限制 Event 系統的周邊規則:**

- 架構: Core 為純 C#, 不得引用 `UnityEngine` / `UnityEditor`; 「新功能先判斷能否放進 Core, 不行才放 Runtime」; Runtime 只放必須用 Unity API 的部分。函式庫不得引用 Samples。
- 平台 (AOT): 通用類避免執行期反射、`System.Reflection.Emit`、`dynamic`; 「執行期才決定型別的泛型」列為已知風險; 必要的反射需加 `[Preserve]` 或 `link.xml`。寫到可能影響平台的功能時要先提醒。
- GC: 「每幀路徑上的 API 以零配置為目標; 載入期或一次性 API 允許配置。」公開 API 的 XML 註解需標明配置行為 (零配置 / 僅初始化時配置 / 每次呼叫配置)。自製資料結構需同時符合: 位於每幀路徑、Performance Testing 顯示內建方案有問題、成本可接受。dotnet 測試只驗證邏輯, GC 數字以 Unity Performance Testing 為準。裝箱與字串配置情境見 `docs/note/gc-boxing.md` (泛型參數避免裝箱是 `Log.Format` 的做法)。
- Log: 函式庫 (Core / Runtime) 內部不得呼叫 `Log.Debug`; `Log.Debug` 參數不得有副作用 (正式版會被編譯期移除)。
- 測試: 新增公開 API 時一併新增測試; 函式庫的 PlayMode 測試不得整份包在 `#if UNITY_EDITOR`。
- Editor 選單放 `TorbenTools/{系統名稱}/`; Project Settings 頁面用 `Project/TorbenTools/{系統名稱}`。
- XML 註解不寫死預設數值。
- 協作原則 3: 能用 Inspector / 資料設定達成時先指出設定欄位, 非必要不改程式邏輯 (與「Inspector 設定的事件用 `UnityEvent`」方向一致)。

---

## 4. 跨模組直接呼叫的代表例子

專案目前的系統間溝通**全部是直接呼叫或直接寫入對方的公開靜態欄位**。

1. **使用端 → StateMachine**: 範例 Host 的 `RequestOpen(bool)` 直接呼叫 `Machine.Request(...)`; 讀結果則直接讀 `Machine.LastResult.Value.Output.Message` (`DoorStateMachineHostTests.cs`)。這是規格預期的唯一互動方式。
2. **DebugLogs ↔ DebugWindow (雙向)**:
   - `DebugLogs.isGUILog` 的 setter 直接呼叫 `DebugWindow.SetVisible(value)` (`DebugLogs.cs` 約第 20-23 行)。
   - `DebugWindow` 的關閉按鈕直接寫回 `DebugLogs.isGUILog = false` (`DebugWindow.cs:621`)。
   - `DebugLogs.OnUnityLog` 直接寫入 `DebugWindow.UnityHistory`。
3. **設定資料 → 執行期靜態狀態**: `LogSettings.Apply()` 依序呼叫 `log?.Apply(); window?.Apply(); output?.Apply();` (`Scripts/Runtime/Logs/LogSettings.cs:34-39`), 各 Options 直接寫入其他類別的靜態欄位:
   - `DebugLogsOptions.Apply()`: `DebugLogs.isConsoleWithGUI = ...; DebugLogs.isUnityLog = ...; DebugLogs.isGUILog = startWithWindow;`
   - `DebugWindowOptions.Apply()`: `DebugWindow.Size / Position / Scale / IsDraggable / AutoScroll`、`DebugWindow.SetLevelColor(...)`、`DebugWindow.SetLevelVisible(...)`。
   - 反方向 `DebugLogsOptions.Capture()` 直接讀這些靜態欄位存回設定檔。
4. **Editor → Runtime 靜態狀態**: 選單 `LogMenu.ToggleGuiWindow()` 直接 `DebugLogs.isGUILog = !DebugLogs.isGUILog` (`Scripts/Editor/Logs/LogMenu.cs:22`)。
5. **DebugWindow → CrossObject**: `DebugWindow.EnsureInstance()` 取 `CrossObject.Instance.gameObject` 並 `AddComponent<DebugWindow>()` (`DebugWindow.cs` 約第 430 行); `CrossObject` 是 `DontDestroyOnLoad` 的單例 (`Scripts/Runtime/StaticObject/CrossObject.cs`)。
6. **UI → Model (範例)**: `CalculatorView` 按鈕回呼直接呼叫 `_model.PressDigit(...)` 再 `RefreshDisplay()`; Model 不通知 View, 由 View 每次操作後主動重讀 `_model.Display`。

---

## 5. 已知的問題或不方便的地方

有文件或程式註解佐證的:

- **StateMachine 沒有任何變化通知**: 轉換成功、Guard Deny、無路徑、Request 被 `Replaced`、進入 `Faulted` 都不會主動告知。外部只能輪詢, 而 Deny 與無路徑在狀態上完全看不出來 (Current / Serial / LastResult 都不變)。這是 v1 刻意排除 (§1.4), 不是缺陷。
- **`LastResult` 只保留一筆**: 文件提醒「需要保存特定結果時, 在可能更新它的 Execute 前保存, 並核對 StateId 與 SerialNumber。不要將 LastResult 誤認為 Current 的即時 Output」(`docs/state-machine.md`)。輪詢太慢就可能漏掉中間的完成結果。
- **送出 Request 後不能在同一呼叫內看到結果**: 「送出 Request 後不要在同一次呼叫內假設已有新結果」; 「由 Host 驅動時, Project 只送 Request 與讀結果, 不再額外每幀呼叫 Machine.Execute, 以免一幀執行多個 Tick」(`docs/state-machine.md`「Unity Host 使用」)。
- **`CrossObject` 被摧毀時沒有通知**: `OnDestroy` 內的註解「需要確認其他物件是否依賴於這個單例，如果有，可能需要在這裡做一些清理工作。」目前依賴它的 `DebugWindow` 是掛在同一個 GameObject 上, 會一起消失。
- 舊的 sink 例外問題 (`docs/note/log_system-note-sink.md:124`「某個 sink 拋例外時，會中斷後面的 sink，並傳回呼叫端」) **已修正**: 現在每個 sink 各自 `try / catch`, 例外交給 `SinkErrorHandler` (`docs/log-system.md`)。設計 Event 系統時可參考這個「一個接收者失敗不影響其他接收者、例外不傳回發送端」的既有決定。

觀察到、但文件沒有記錄的 (推論, 未實測):

- `Log.SinkErrorHandler` 是公開靜態**欄位**而非 `event`: 任何人都能以 `=` 覆寫或設成 `null`, 只能有一個處理者。
- `Application.quitting` 與 `playModeStateChanged` 從不取消, 依賴「先 `-=` 再 `+=`」與 Domain Reload 防重複。若之後的 Event 系統也用靜態訂閱, 需要沿用同樣的處理, 否則關閉 Domain Reload 時會重複觸發。
- `DebugLogs` 與 `DebugWindow` 彼此直接引用對方的靜態成員, 是雙向耦合; 新增「設定改變時通知 UI」之類需求時, 目前只能在 setter 裡再加一個直接呼叫。
- StateMachineHost 的 `Update` 與送 Request 的 MonoBehaviour 之間沒有固定的執行順序: 同一幀送出的 Request 是在本幀還是下一幀被處理, 取決於 Unity 的 Script Execution Order。
- 尚未有任何跨系統的事件需求實例 (例如 StateMachine 狀態改變要通知 Log 或 UI); 目前兩個系統彼此完全獨立, StateMachine 不呼叫 Log。
