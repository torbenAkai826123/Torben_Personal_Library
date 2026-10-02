# C# Naming Conventions

## General Principles

- Prefer standard C# naming conventions unless the project explicitly defines otherwise.
- Naming should primarily communicate **domain meaning and responsibility**.
- Use only the field prefixes defined below; express mutability and ownership with C# modifiers and APIs.
- Prefer consistency over rigid formula-based naming.
- Avoid renaming existing project APIs purely for stylistic consistency unless the task explicitly includes refactoring.

---

## Namespace

All repository-owned C# code uses `Torben` as the namespace root, including reusable libraries, repository-specific features, samples, Editor tools, and tests:

```text
Torben[.<Feature>]
```

Examples:

```text
Torben.StateMachine
Torben.Pool
Torben.Calculator
Torben.Calculator.Editor
Torben.Calculator.Tests
```

Guidelines:

- Use `Torben` as the single root; choose the suffix by domain meaning, not by whether the code is reusable or repository-specific.
- `TorbenJuniorUtility` is a legacy project/resource-path name, not an allowed namespace root. Existing uses do not establish a second naming convention.
- Project names and resource paths such as `Assets/[TorbenJuniorUtility]/` and `TorbenJunior.sln` may remain unchanged; they do not determine namespace names.
- Assembly names express compilation boundaries; namespaces express features. For example, `CalculatorModel` in `Torben.Samples` and `CalculatorView` in `Torben.Samples.Runtime` both use `Torben.Calculator`.
- The `Torben` namespace root does not require a `Torben` prefix on test assembly, class, or helper names; test naming follows the rules below.
- Do not add unnecessary category layers such as `Core`, `Common`, `Utility`, or `Framework` unless they represent a real project boundary.
- Source-code folders do not need to match namespaces.
- Do not split a small public API into multiple namespaces only because files are stored in different folders.

Example:

```text
Folders:
Torben.StateMachine/
├─ States/
├─ Guards/
├─ Flow/
└─ Runtime/

Namespace:
Torben.StateMachine
```

---

## Standard C# Casing

Use:

```text
Namespace                   PascalCase
Type                        PascalCase
Interface                   IPascalCase
Method                      PascalCase
Property                    PascalCase
Enum                        PascalCase
Enum Member                 PascalCase
Parameter                   camelCase
Local Variable              camelCase
Private Instance Field      _camelCase
Private Static Field        camelCase
Serialized Inspector Field  camelCase
Generic Type                TName
```

Examples:

```csharp
public interface IStateFlow<TContext, TOutput>
{
    StateId InitialStateId { get; }

    IState<TContext, TOutput> GetState(StateId id);
}

private readonly IStateFlow<TContext, TOutput> _flow;
private IState<TContext, TOutput>? _currentState;

private void ProcessRequest(
    TransitionRequest<TContext> request)
{
    var transition = FindTransition(request);
}
```

---

## Methods

Prefer names that describe the actual domain action.

Good:

```csharp
Start()
Stop()
Execute()
Request()
Build()
FindTransition()
GetState()
AddScore()
```

Avoid vague names:

```csharp
Get()
Set()
Process()
Handle()
DoThing()
```

`Get` and `Set` are allowed when the target is explicit.

Good:

```csharp
GetState()
SetVolume()
```

Do not replace clear standard verbs with unusual alternatives merely to avoid `Get` or `Set`.

Do not repeat context already supplied by the containing type.

Prefer:

```csharp
machine.Start();
flow.GetState(id);
builder.Build();
```

Over:

```csharp
machine.StartStateMachine();
flow.GetFlowState(id);
builder.BuildStateFlow();
```

---

## Boolean Naming

Boolean names should read as a state, condition, capability, or decision.

Prefer prefixes such as:

```text
Is
Has
Can
Should
```

Examples:

```csharp
IsComplete
IsEnabled
HasPendingRequest
CanTransition
ShouldRetry
```

Avoid action-like boolean names when possible:

```csharp
EnableFeature
CheckState
RunValidation
```

If a Unity Inspector field is intentionally used as a manual feature toggle or debug switch, an `enable...` style name may be retained when it improves Inspector readability.

Do not rename existing Inspector-facing fields solely to satisfy this convention.

---

## Private Instance Fields

Use:

```csharp
_camelCase
```

Example:

```csharp
private StateId _currentStateId;
private long _serialNumber;
private readonly Dictionary<StateId, IState> _states;
```

The `_` prefix means:

> private, non-serialized instance field

It does **not** mean:

- readonly
- temporary
- immutable
- comparison value

Use C# modifiers to express those properties.

Example:

```csharp
private readonly IStateFlow<TContext, TOutput> _flow;
```

---

## Static Fields

Private static fields use `camelCase`; reserve `_camelCase` for private, non-serialized instance fields. The `static` modifier expresses static ownership.

Do not use custom prefixes such as:

```text
static_
common_
```

Example:

```csharp
private static readonly object lockGate = new();
```

Keep `const` names in `PascalCase`.

---

## Unity Inspector Fields

Use `camelCase` for private `[SerializeField]` fields so Inspector labels remain short and readable. These fields are editable and serialized by Unity, but they remain private to other C# code. Prefer one or two meaningful words; use up to four when needed for clarity. Do not shorten a name until its meaning is unclear.

When several settings form one cohesive Inspector group, place them in a `[System.Serializable]` type and use short names within that group:

```csharp
[SerializeField] private DisplaySettings displaySettings;

[System.Serializable]
public sealed class DisplaySettings
{
    [SerializeField] private int regionLimit;
}
```

Group by shared responsibility, not only to shorten names. Keep temporary runtime values such as a calculated result cache as private instance fields (for example, `_resultCache`) unless they must be saved with the scene or prefab. When renaming an existing serialized field, use `[UnityEngine.Serialization.FormerlySerializedAs("oldName")]` where applicable and verify the saved values in affected scenes and prefabs.

---

## Mutability and Ownership

Do not rely on variable names to indicate whether a value may be changed.

Prefer expressing ownership and mutability through:

- `private`
- `protected`
- `readonly`
- getter-only properties
- `private set`
- interfaces
- controlled mutation methods

Example:

```csharp
public StateId? CurrentStateId { get; private set; }
```

This means:

- callers can read the value;
- only the owning type may assign it.

For stricter control:

```csharp
private int _score;

public int Score => _score;

public void AddScore(int amount)
{
    _score += amount;
}
```

Prefer this over exposing writable data and relying on naming conventions to discourage modification.

---

## `readonly` Reference Types

Remember that `readonly` prevents reassignment of the field itself; it does not automatically make the referenced object immutable.

Example:

```csharp
private readonly List<StateId> _states = new();
```

Allowed:

```csharp
_states.Add(stateId);
```

Not allowed:

```csharp
_states = new List<StateId>();
```

Use readonly interfaces or controlled APIs when callers should not mutate a collection.

Example:

```csharp
private readonly List<StateId> _states = new();

public IReadOnlyList<StateId> States => _states;
```

---

## Public API vs Private Implementation

Public API names should be clear and domain-specific.

Private implementation names may be shorter when their scope makes the meaning obvious.

Good:

```csharp
private void Commit()
private void Reset()
private bool TryResolve(...)
```

Do not create excessively descriptive private names merely to restate implementation context.

Avoid names such as:

```csharp
ResetCurrentRuntimeStateMachineInternalData()
```

unless that level of specificity is genuinely required.

---

## Existing Code

Older code may contain historical names that do not follow this rule. Those names are migration debt, not exceptions for new code.

When editing existing code:

- preserve local consistency unless the task includes naming cleanup;
- do not perform broad renaming unrelated to the requested change;
- new APIs and new standalone tools should follow the conventions above;
- when introducing a new subsystem, keep its naming internally consistent even if surrounding legacy code differs.

The single `Torben` root takes precedence over legacy namespace and production assembly names when choosing names for new code. Test assembly names follow the test naming rules below. Retaining an existing name outside the authorized scope does not permit copying a name that violates these rules into new code. When a task explicitly includes namespace or assembly migration, update legacy names and their references within that scope together; preserve resource GUIDs and verify affected Unity references and tests.

---

## 組件名稱

| 用途 | 組件名稱 | namespace |
| -- | -- | -- |
| 可重用函式庫本體 | Torben.Core、Torben.Runtime、Torben.Editor | Torben.<功能> |
| 範例本體與工具 | Torben.Samples (noEngineReferences)、Torben.Samples.Runtime、Torben.Samples.Editor | Torben.<功能>，例如 Torben.Calculator、Torben.Calculator.Editor |

本倉庫的函式庫本體、範例本體與工具組件使用 `Torben` 根; 測試組件不強制此字首。所有自有 C# namespace 仍使用 `Torben` 根, 組件名稱不要求 namespace 同步更名。同一組件的 asmdef `name`、引用名稱與 .NET `AssemblyName` 須保持一致; 不同測試邊界的 Unity 與 .NET 測試組件可以各自命名。此處為目標命名規則, 既有程式碼是否已完成遷移須另行驗證。

### 測試名稱與組件邊界

- 測試組件採 PascalCase 與點分隔, 使用 `<測試對象或群組>.Tests[.<模式或必要邊界>]`; 不強制 `Torben` 字首, 也不禁止有理由保留完整被測組件名稱。
- 例如函式庫共用測試可命名 `Library.Tests.PlayMode`, 範例測試可命名 `Samples.Tests.EditMode`、`Samples.Tests.PlayMode`; 只有已存在功能專屬測試邊界時, 才使用如純 .NET 的 `StateMachine.Tests` 的名稱。這些是命名範例, 不要求建立對應組件。
- Unity 測試組件以 `EditMode` / `PlayMode` 表達實際執行模式; 由 asmdef 平台設定及測試內容確認模式, 不能只靠名稱判定。純 .NET 測試沒有 Unity 模式時, 使用 `<測試對象或群組>.Tests` 即可。
- 測試 class 使用 `<被測型別或行為>Tests`, 例如 `StateMachineHostTests`; helper 依角色命名, 例如 `StateMachineProbeHost`。不因 package identity 重複加上 `Torben` 字首。
- 測試組件只依實際依賴、平台、打包或測試類型拆分, 不比照 production 的 Core / Runtime / Editor 自動展開, 也不因新增功能或資料夾就新增 asmdef。相同邊界的測試優先共用組件。
- 函式庫測試與範例測試維持依賴邊界: Samples 測試可引用函式庫, 函式庫及其測試不得引用 Samples。
- 組件名稱必須在 Unity 專案內唯一; 可能與其他 package 碰撞時, 加上足以辨識的功能或 package 名稱, 而非一律套用固定字首。
- 既有 `Torben.*` 測試組件名稱仍可保留; 修改本規則不代表授權重命名組件、拆分測試或擴張 Public API。實際遷移時須同步處理 asmdef / asmref、引用與 .NET 組件名稱, 保留資源 GUID 並驗證測試。

---

## Decision Rule

First satisfy the required `Torben` namespace root, production assembly root, and test naming and boundary rules above. Within those constraints, prioritize in this order:

1. Domain meaning
2. Existing public API consistency
3. Standard C# convention
4. Local project consistency
5. Brevity

Avoid inventing a custom naming rule when access modifiers, types, or existing C# conventions already communicate the same information.
