# C# Naming Conventions

## General Principles

- Prefer standard C# naming conventions unless the project explicitly defines otherwise.
- Naming should primarily communicate **domain meaning and responsibility**.
- Use only the field prefixes defined below; express mutability and ownership with C# modifiers and APIs.
- Prefer consistency over rigid formula-based naming.
- Avoid renaming existing project APIs purely for stylistic consistency unless the task explicitly includes refactoring.

---

## Namespace

Choose the namespace root by scope:

```text
Torben[.ReusableTool]
TorbenJuniorUtility[.ProjectFeature]
```

Examples:

```text
Torben.StateMachine
Torben.Pool
TorbenJuniorUtility.Calculator
TorbenJuniorUtility.Tests
```

Guidelines:

- Features specific to this repository use `TorbenJuniorUtility` as their root.
- Tools intended for reuse across projects use `Torben` or `Torben.<ToolName>`.
- Choose by intended ownership, not the folder where the source file happens to live.
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

Older projects may contain multiple historical naming systems.

When editing existing code:

- preserve local consistency unless the task includes naming cleanup;
- do not perform broad renaming unrelated to the requested change;
- new APIs and new standalone tools should follow the conventions above;
- when introducing a new subsystem, keep its naming internally consistent even if surrounding legacy code differs.

---

## Decision Rule

When uncertain about a name, prioritize in this order:

1. Domain meaning
2. Existing public API consistency
3. Standard C# convention
4. Local project consistency
5. Brevity

Avoid inventing a custom naming rule when access modifiers, types, or existing C# conventions already communicate the same information.
