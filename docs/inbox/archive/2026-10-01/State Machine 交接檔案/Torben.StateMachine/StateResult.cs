#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{
    public readonly struct StateResult<TOutput> : IEquatable<StateResult<TOutput>>
    {
        public StateResult(StateId stateId, long serialNumber, TOutput output)
        {
            StateId = stateId;
            SerialNumber = serialNumber;
            Output = output;
        }

        public StateId StateId { get; }
        public long SerialNumber { get; }
        public TOutput Output { get; }

        public bool Equals(StateResult<TOutput> other) =>
            StateId == other.StateId && SerialNumber == other.SerialNumber &&
            EqualityComparer<TOutput>.Default.Equals(Output, other.Output);

        public override bool Equals(object? obj) => obj is StateResult<TOutput> other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StateId.GetHashCode();
                hash = hash * 31 + SerialNumber.GetHashCode();
                hash = hash * 31 + (Output is null ? 0 : EqualityComparer<TOutput>.Default.GetHashCode(Output));
                return hash;
            }
        }
        public static bool operator ==(StateResult<TOutput> left, StateResult<TOutput> right) => left.Equals(right);
        public static bool operator !=(StateResult<TOutput> left, StateResult<TOutput> right) => !left.Equals(right);
        public void Deconstruct(out StateId stateId, out long serialNumber, out TOutput output)
        {
            stateId = StateId;
            serialNumber = SerialNumber;
            output = Output;
        }
        public override string ToString() => $"StateResult {{ StateId = {StateId}, SerialNumber = {SerialNumber}, Output = {Output} }}";
    }
}
