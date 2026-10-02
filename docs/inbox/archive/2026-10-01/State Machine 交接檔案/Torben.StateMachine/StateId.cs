#nullable enable
using System;

namespace Torben.StateMachine
{
    public readonly struct StateId : IEquatable<StateId>
    {
        public StateId(string value) => Value = value;
        public string Value { get; }

        public bool Equals(StateId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object? obj) => obj is StateId other && Equals(other);
        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(StateId left, StateId right) => left.Equals(right);
        public static bool operator !=(StateId left, StateId right) => !left.Equals(right);
        public void Deconstruct(out string value) => value = Value;
        public override string ToString() => $"StateId {{ Value = {Value} }}";
    }
}
