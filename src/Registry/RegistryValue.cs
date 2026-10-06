using System;
using System.Linq;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry;

/// <summary>Immutable native value data. Integers preserve their signed CLR representation and raw bits.</summary>
/// <remarks>String and ExpandString require string; DWord requires Int32; QWord requires Int64; Binary and None require byte[];
/// MultiString requires string[]. Unsigned integer factory overloads preserve the raw bits. Arrays are copied on input and output.
/// Strings cannot contain NUL. Multi-string elements must be nonempty, although an empty array is valid. Unsupported kinds and
/// mismatched CLR data are rejected rather than coerced. ExpandString remains raw unless expansion is explicitly requested.</remarks>
public sealed class RegistryValue : IEquatable<RegistryValue>
{
    private readonly object data;
    public RegistryValueKind Kind { get; }
    public object Data => Clone(data);

    public RegistryValue(RegistryValueKind kind, object data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var valid =
            kind == RegistryValueKind.String || kind == RegistryValueKind.ExpandString
                ? data is string
            : kind == RegistryValueKind.DWord ? data is int
            : kind == RegistryValueKind.QWord ? data is long
            : kind == RegistryValueKind.Binary || kind == RegistryValueKind.None ? data is byte[]
            : kind == RegistryValueKind.MultiString && data is string[];

        if (!valid)
            throw new ArgumentException(
                "The data type must match the explicit registry value kind.",
                nameof(data)
            );

        if (
            data is string text && text.IndexOf('\0') >= 0
            || data is string[] strings
                && strings.Any(s => string.IsNullOrEmpty(s) || s.IndexOf('\0') >= 0)
        )
            throw new ArgumentException(
                "Strings must not contain NUL; multi-string elements must also be nonempty.",
                nameof(data)
            );

        Kind = kind;
        this.data = Clone(data);
    }

    public static RegistryValue String(string value) =>
        new RegistryValue(RegistryValueKind.String, value);

    public static RegistryValue ExpandString(string value) =>
        new RegistryValue(RegistryValueKind.ExpandString, value);

    public static RegistryValue DWord(int value) =>
        new RegistryValue(RegistryValueKind.DWord, value);

    public static RegistryValue DWord(uint value) => DWord(unchecked((int)value));

    public static RegistryValue QWord(long value) =>
        new RegistryValue(RegistryValueKind.QWord, value);

    public static RegistryValue QWord(ulong value) => QWord(unchecked((long)value));

    public static RegistryValue Binary(byte[] value) =>
        new RegistryValue(RegistryValueKind.Binary, value);

    public static RegistryValue MultiString(params string[] value) =>
        new RegistryValue(RegistryValueKind.MultiString, value);

    /// <summary>Returns data as the requested CLR type, cloning arrays. Throws when the requested type does not match the native value.</summary>
    public T GetData<T>() =>
        Data is T typed
            ? typed
            : throw new InvalidCastException(
                "The requested CLR type does not match the registry data."
            );

    /// <summary>Expands using the calling process environment, including for values read from another machine.</summary>
    public string GetString(bool expandEnvironmentVariables = false)
    {
        var value = GetData<string>();

        return expandEnvironmentVariables && Kind == RegistryValueKind.ExpandString
            ? Environment.ExpandEnvironmentVariables(value)
            : value;
    }

    public bool Equals(RegistryValue? other) =>
        other != null
        && Kind == other.Kind
        && (
            data is byte[] bytes ? bytes.SequenceEqual((byte[])other.data)
            : data is string[] strings
                ? strings.SequenceEqual((string[])other.data, StringComparer.Ordinal)
            : data.Equals(other.data)
        );

    public override bool Equals(object? obj) => obj is RegistryValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = (int)Kind;

        if (data is Array array)
            foreach (var item in array)
                hash = unchecked(hash * 31 + item.GetHashCode());
        else
            hash = unchecked(hash * 31 + data.GetHashCode());

        return hash;
    }

    private static object Clone(object value) => value is Array array ? array.Clone() : value;
}

/// <summary>A named value; an empty name denotes the key's default value.</summary>
public sealed class RegistryValueEntry
{
    public string Name { get; }
    public RegistryValue Value { get; }

    public RegistryValueEntry(string name, RegistryValue value)
    {
        ValidateName(name);
        Name = name;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static void ValidateName(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        if (name.Length > 16383 || name.IndexOf('\0') >= 0)
            throw new ArgumentException("Invalid registry value name.", nameof(name));
    }
}
