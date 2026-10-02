#nullable enable
using System;

namespace Torben.StateMachine
{
    public readonly struct TransitionId : IEquatable<TransitionId>
    {
        public TransitionId(string value) => Value = value;
        public string Value { get; }

        public bool Equals(TransitionId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object? obj) => obj is TransitionId other && Equals(other);
        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(TransitionId left, TransitionId right) => left.Equals(right);
        public static bool operator !=(TransitionId left, TransitionId right) => !left.Equals(right);
        public void Deconstruct(out string value) => value = Value;
        public override string ToString() => $"TransitionId {{ Value = {Value} }}";
    }
}
