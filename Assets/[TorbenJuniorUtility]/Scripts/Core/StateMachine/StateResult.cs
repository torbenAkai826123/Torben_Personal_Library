#nullable enable
using System;
using System.Collections.Generic;

namespace Torben.StateMachine
{
    /// <summary>正常完成的 activation 結果.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public readonly struct StateResult<TOutput> : IEquatable<StateResult<TOutput>>
    {
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateResult(StateId stateId, long serialNumber, TOutput output)
        {
            StateId = stateId;
            SerialNumber = serialNumber;
            Output = output;
        }

        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateId StateId { get; }
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public long SerialNumber { get; }
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public TOutput Output { get; }

        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public bool Equals(StateResult<TOutput> other) =>
            StateId == other.StateId && SerialNumber == other.SerialNumber &&
            EqualityComparer<TOutput>.Default.Equals(Output, other.Output);

        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public override bool Equals(object? obj) => obj is StateResult<TOutput> other && Equals(other);
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
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
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public static bool operator ==(StateResult<TOutput> left, StateResult<TOutput> right) => left.Equals(right);
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 比較器僅初始化時可能配置; 泛型值的自訂比較實作可能每次呼叫配置.</remarks>
        public static bool operator !=(StateResult<TOutput> left, StateResult<TOutput> right) => !left.Equals(right);
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public void Deconstruct(out StateId stateId, out long serialNumber, out TOutput output)
        {
            stateId = StateId;
            serialNumber = SerialNumber;
            output = Output;
        }
        /// <summary>正常完成的 activation 結果.</summary>
        /// <remarks>配置: 每次呼叫配置字串; 泛型值的字串表示可能另有配置.</remarks>
        public override string ToString() => $"StateResult {{ StateId = {StateId}, SerialNumber = {SerialNumber}, Output = {Output} }}";
    }
}
