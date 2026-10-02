#nullable enable
using System;

namespace Torben.StateMachine
{
    /// <summary>Flow 節點識別值.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public readonly struct StateId : IEquatable<StateId>
    {
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public StateId(string value) => Value = value;
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public string Value { get; }

        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public bool Equals(StateId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public override bool Equals(object? obj) => obj is StateId other && Equals(other);
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public static bool operator ==(StateId left, StateId right) => left.Equals(right);
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public static bool operator !=(StateId left, StateId right) => !left.Equals(right);
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public void Deconstruct(out string value) => value = Value;
        /// <summary>Flow 節點識別值.</summary>
        /// <remarks>配置: 每次呼叫配置字串; 泛型值的字串表示可能另有配置.</remarks>
        public override string ToString() => $"StateId {{ Value = {Value} }}";
    }
}
