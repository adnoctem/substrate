using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry.Policies;

/// <summary>An immutable registry.pol record. Order, duplicate names and policy directives are significant; records never apply themselves.</summary>
public sealed class RegistryPolicyRecord
{
    private readonly byte[] payload;
    public string Key { get; }
    public string ValueName { get; }
    public uint Type { get; }
    public byte[] Payload => (byte[])payload.Clone();
    internal byte[] PayloadBytes => payload;

    /// <remarks>Payload is copied and may be opaque, including unknown native types. Decoding is an explicit operation.</remarks>
    public RegistryPolicyRecord(string key, string valueName, uint type, byte[] payload)
        : this(key, valueName, type, payload, true) { }

    internal RegistryPolicyRecord(
        string key,
        string valueName,
        uint type,
        byte[] payload,
        bool validateRelativeKey
    )
    {
        ValidateIdentifiers(key, valueName, validateRelativeKey);

        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        if (payload.Length > 65535)
            throw new ArgumentException("Policy payload exceeds 65535 bytes.", nameof(payload));

        Key = key;
        ValueName = valueName;
        Type = type;
        this.payload = (byte[])payload.Clone();
    }

    internal static void ValidateIdentifiers(
        string key,
        string name,
        bool validateRelativeKey = true
    )
    {
        if (
            string.IsNullOrEmpty(key)
            || validateRelativeKey
                && Regex.IsMatch(
                    key,
                    @"^(HKLM|HKCU|HKEY_LOCAL_MACHINE|HKEY_CURRENT_USER)(:|\\|$)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                )
        )
            throw new ArgumentException(
                "Policy Key must be a nonempty path relative to the registry hive."
            );

        if (name == null)
            throw new ArgumentException("Policy ValueName must be a string (empty is allowed).");

        if (
            key.Length > 32767
            || name.Length > 32767
            || key.IndexOf('\0') >= 0
            || name.IndexOf('\0') >= 0
        )
            throw new ArgumentException(
                "Policy identifiers must not contain nulls or exceed 32767 characters."
            );
    }

    /// <summary>Projects a relative policy key through the shared registry path validator. The caller chooses the hive; no machine is accessed.</summary>
    public RegistryPath GetRegistryPath(RegistryHive hive) => new RegistryPath(hive, Key);

    /// <summary>Returns a string, string array, UInt32, UInt64, opaque byte array, or null for a zero-byte payload. Arrays are independent copies.</summary>
    public object? Decode() => Decode(false);

    // v1 used culture-sensitive EndsWith, which can ignore NUL under modern globalization.
    // Only the PowerShell compatibility boundary retains that behavior; public decoding is ordinal.
    internal object? Decode(bool legacyStringComparison)
    {
        if (payload.Length == 0)
            return null;

        if (Type == 1 || Type == 2 || Type == 7)
        {
            if (payload.Length % 2 != 0)
                throw new InvalidDataException("String payload has an odd byte count.");

            var text = new UnicodeEncoding(false, false, true).GetString(payload);
            var comparison = legacyStringComparison
                ? StringComparison.CurrentCulture
                : StringComparison.Ordinal;

            if (!text.EndsWith("\0", comparison))
                throw new InvalidDataException("String payload is not null terminated.");

            if (Type == 7)
            {
                if (!text.EndsWith("\0\0", comparison))
                    throw new InvalidDataException("MULTI_SZ requires two terminating nulls.");

                text = text.Substring(0, text.Length - 2);

                return text.Length == 0 ? Array.Empty<string>() : text.Split('\0');
            }

            return text.Substring(0, text.Length - 1);
        }

        if (Type == 4 || Type == 5)
        {
            if (payload.Length != 4)
                throw new InvalidDataException(
                    Type == 4
                        ? "DWORD payload must contain four bytes."
                        : "DWORD_BIG_ENDIAN payload must contain four bytes."
                );

            return (uint)ReadNumber(payload, Type == 5);
        }

        if (Type == 11)
        {
            if (payload.Length != 8)
                throw new InvalidDataException("QWORD payload must contain eight bytes.");

            return ReadNumber(payload, false);
        }

        return Payload;
    }

    /// <summary>Maps ordinary supported payloads to the existing registry value model. Unknown kinds, empty scalar payloads and invalid native strings are not coerced.</summary>
    public bool TryGetRegistryValue(out RegistryValue? value)
    {
        value = null;

        try
        {
            switch (Type)
            {
                case 0:
                    value = new RegistryValue(RegistryValueKind.None, payload);
                    break;
                case 3:
                    value = RegistryValue.Binary(payload);
                    break;
                case 1:
                    if (Decode() is string text)
                        value = RegistryValue.String(text);

                    break;
                case 2:
                    if (Decode() is string expand)
                        value = RegistryValue.ExpandString(expand);

                    break;
                case 4:
                case 5:
                    if (Decode() is uint number)
                        value = RegistryValue.DWord(number);

                    break;
                case 7:
                    if (Decode() is string[] strings)
                        value = RegistryValue.MultiString(strings);

                    break;
                case 11:
                    if (Decode() is ulong large)
                        value = RegistryValue.QWord(large);

                    break;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }

        return value != null;
    }

    public static RegistryPolicyRecord FromRegistryValue(string key, RegistryValueEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        var type = entry.Value.Kind == RegistryValueKind.None ? 0u : (uint)entry.Value.Kind;
        object data = entry.Value.Data;

        if (type == 4)
            data = unchecked((uint)(int)data);

        if (type == 11)
            data = unchecked((ulong)(long)data);

        return FromDecoded(key, entry.Name, type, data);
    }

    /// <remarks>Decoded integer payloads must be unsigned whole numbers; use FromRegistryValue to preserve signed CLR registry integer bits.</remarks>
    public static RegistryPolicyRecord FromDecoded(
        string key,
        string valueName,
        uint type,
        object? data
    ) => new RegistryPolicyRecord(key, valueName, type, EncodePayload(type, data));

    internal static byte[] EncodePayload(uint type, object? data)
    {
        if (data == null)
            return Array.Empty<byte>();

        if (type != 1 && type != 2 && type != 4 && type != 5 && type != 7 && type != 11)
        {
            if (!(data is byte[] opaque))
                throw new ArgumentException("Raw and opaque policy data must be a byte array.");

            if (opaque.Length > RegistryPolicyCodec.MaximumPayloadSize)
                throw new ArgumentException("Policy payload exceeds 65535 bytes.");

            return (byte[])opaque.Clone();
        }

        if (type == 1 || type == 2)
        {
            if (!(data is string text) || text.IndexOf('\0') >= 0)
                throw new ArgumentException(
                    "SZ and EXPAND_SZ data must be a string without embedded nulls."
                );

            if (text.Length > 32766)
                throw new ArgumentException("Policy payload exceeds 65535 bytes.");

            return Encoding.Unicode.GetBytes(text + '\0');
        }

        if (type == 7)
        {
            var items =
                data is string text ? new object[] { text }
                : data is IEnumerable collection ? collection
                : new[] { data };
            var strings = new List<string>();
            var characters = 1;

            foreach (var item in items)
            {
                if (!(item is string value) || value.Length == 0 || value.IndexOf('\0') >= 0)
                    throw new ArgumentException(
                        "MULTI_SZ data must contain nonempty strings without embedded nulls."
                    );

                if (value.Length > 32766 - characters)
                    throw new ArgumentException("Policy payload exceeds 65535 bytes.");

                characters += value.Length + 1;
                strings.Add(value);
            }

            return Encoding.Unicode.GetBytes(string.Join("\0", strings) + "\0\0");
        }

        var integer = Convert.ToString(data, CultureInfo.InvariantCulture) ?? "";

        if (!Regex.IsMatch(integer, @"^\d+$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Integer policy data must be an unsigned whole number.");

        var number =
            type == 11
                ? ulong.Parse(integer, CultureInfo.InvariantCulture)
                : uint.Parse(integer, CultureInfo.InvariantCulture);
        var bytes = new byte[type == 11 ? 8 : 4];

        for (var index = 0; index < bytes.Length; index++)
            bytes[type == 5 ? bytes.Length - index - 1 : index] = (byte)(number >> (index * 8));

        return bytes;
    }

    private static ulong ReadNumber(byte[] bytes, bool bigEndian)
    {
        ulong result = 0;

        for (var index = 0; index < bytes.Length; index++)
            result |= (ulong)bytes[bigEndian ? bytes.Length - index - 1 : index] << (index * 8);

        return result;
    }
}
