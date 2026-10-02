#nullable enable
using System;

namespace Torben.StateMachine
{
    /// <summary>Project 定義的路徑意圖識別值.</summary>
    /// <remarks>配置: 值型別本身零配置; 各操作的配置行為見成員說明.</remarks>
    public readonly struct TransitionId : IEquatable<TransitionId>
    {
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public TransitionId(string value) => Value = value;
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public string Value { get; }

        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public bool Equals(TransitionId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public override bool Equals(object? obj) => obj is TransitionId other && Equals(other);
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public static bool operator ==(TransitionId left, TransitionId right) => left.Equals(right);
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 呼叫端將值型別轉為 object 時可能配置.</remarks>
        public static bool operator !=(TransitionId left, TransitionId right) => !left.Equals(right);
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        public void Deconstruct(out string value) => value = Value;
        /// <summary>Project 定義的路徑意圖識別值.</summary>
        /// <remarks>配置: 每次呼叫配置字串; 泛型值的字串表示可能另有配置.</remarks>
        public override string ToString() => $"TransitionId {{ Value = {Value} }}";
    }
}
