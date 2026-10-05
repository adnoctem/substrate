using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Management.Infrastructure;
using AdNoctem.Substrate.Networking.Compatibility;
using AdNoctem.Substrate.Networking.Platform;

namespace AdNoctem.Substrate.PowerShell.Networking;

/// <summary>PowerShell-only bridge used by compat.ps1. Other applications use NetworkManager and NetworkValidator.</summary>
public static class NetworkCompatibility
{
    public static NetworkCompatibilityResult Invoke(string operation, PSObject? adapter, string family, bool required, string type)
    {
        var result = new NetworkCompatibilityResult();
        var ownsAdapter = adapter == null && operation != "Get-DefaultNetworkAdapter";
        try
        {
            if (adapter == null)
                adapter = GetDefaultAdapter(type, result, required);
            if (adapter == null || result.Failure != null)
                return result;
            if (operation == "Get-DefaultNetworkAdapter")
            { result.Value = adapter; return result; }
            var configurationValue = Property(adapter, "CimConfig");
            var configuration = configurationValue == null ? null : PSObject.AsPSObject(configurationValue);
            var data = new LegacyNetworkAdapter
            {
                Required = required,
                ReadName = () => LanguagePrimitives.ConvertTo<string>(Property(adapter, "Name")),
                ReadValues = name => Strings(Property(configuration, name)),
                ReadMACAddress = () => Property(configuration, "MACAddress") is object mac ? LanguagePrimitives.ConvertTo<string>(mac) : null
            };
            result.Value = data.Get(operation, string.Equals(family, "IPv6", StringComparison.OrdinalIgnoreCase));
            result.Messages.AddRange(data.Messages);
            result.Failure = data.Failure;
        }
        catch (PropertyNotFoundException error)
        { result.Error = new ErrorRecord(error, "PropertyNotFoundStrict", ErrorCategory.NotSpecified, null); }
        catch (LegacyNetworkMaskException error)
        {
            try
            { _ = LanguagePrimitives.ConvertTo(error.Value, typeof(byte), CultureInfo.InvariantCulture); }
            catch (PSInvalidCastException converted)
            { result.Error = new ErrorRecord(new RuntimeException(converted.Message, converted), converted.ErrorRecord.FullyQualifiedErrorId, ErrorCategory.InvalidArgument, null); }
        }
        catch (Exception error)
        {
            var exception = error is FormatException ? new MethodInvocationException("Exception calling \"Parse\" with \"1\" argument(s): \"" + error.Message + "\"", error) : error;
            result.Error = new ErrorRecord(exception, error.GetType().Name, ErrorCategory.NotSpecified, null);
        }
        finally
        {
            if (ownsAdapter && adapter != null)
            {
                (adapter.Properties["NetAdapter"]?.Value as IDisposable)?.Dispose();
                (adapter.Properties["CimConfig"]?.Value as IDisposable)?.Dispose();
            }
        }
        return result;
    }

    private static object? Property(PSObject? value, string name)
    {
        var property = value?.Properties[name];
        if (property == null)
            throw new PropertyNotFoundException("The property '" + name + "' cannot be found on this object. Verify that the property exists.");
        return property.Value;
    }

    private static string?[] Strings(object? value)
    {
        if (value == null)
            return new string?[] { null };
        var iterator = LanguagePrimitives.GetEnumerator(value);
        if (iterator == null)
            return new[] { LanguagePrimitives.ConvertTo<string>(value) };
        var output = new List<string?>();
        try
        { while (iterator.MoveNext()) output.Add(iterator.Current == null ? null : LanguagePrimitives.ConvertTo<string>(iterator.Current)); }
        finally { (iterator as IDisposable)?.Dispose(); }
        return output.ToArray();
    }
    private static PSObject? GetDefaultAdapter(string type, NetworkCompatibilityResult result, bool required)
    {
        PSObject? Missing(string message)
        { if (required) result.Failure = message; else result.Messages.Add(message); return null; }
        using (var data = CimNetworkReader.Read(TimeSpan.FromSeconds(30), CancellationToken.None))
        {
            var route = data.SelectRoute(AddressFamily.InterNetwork);
            if (route == null)
                return Missing("No default IPv4 route found.");
            var index = CimNetworkReader.Number(route, "InterfaceIndex");
            var selected = data.Adapters.FirstOrDefault(item => CimNetworkReader.Number(item, "InterfaceIndex") == index);
            if (selected == null)
                return Missing("Could not resolve adapter for interface index " + index + ".");
            var name = CimNetworkReader.Text(selected, "Name");
            var media = PhysicalMedium(CimNetworkReader.Number(selected, "NdisPhysicalMedium"));
            if (!string.Equals(type, "Any", StringComparison.OrdinalIgnoreCase))
            {
                var match = string.Equals(type, "WiFi", StringComparison.OrdinalIgnoreCase) ? media.Contains("802.11") :
                    string.Equals(type, "Ethernet", StringComparison.OrdinalIgnoreCase) ? media == "802.3" : media == "Unspecified";
                if (!match)
                    return Missing("Default adapter '" + name + "' (type: " + media + ") does not match the requested type '" + type + "'.");
            }
            var config = data.Configuration(selected);
            if (config == null)
                return Missing("Could not retrieve CIM configuration for adapter '" + name + "'.");
            // Detached CIM copies preserve the v1 returned object types. PowerShell callers own these returned objects.
            var adapterCopy = new CimInstance(selected);
            CimInstance? configurationCopy = null;
            try
            {
                configurationCopy = new CimInstance(config);
                var output = new PSObject();
                output.Properties.Add(new PSNoteProperty("Name", name));
                output.Properties.Add(new PSNoteProperty("ifIndex", index));
                output.Properties.Add(new PSNoteProperty("PhysicalMedia", media));
                output.Properties.Add(new PSNoteProperty("NetAdapter", adapterCopy));
                output.Properties.Add(new PSNoteProperty("CimConfig", configurationCopy));
                return output;
            }
            catch
            {
                configurationCopy?.Dispose();
                adapterCopy.Dispose();
                throw;
            }
        }
    }
    private static string PhysicalMedium(uint value)
    {
        switch (value)
        {
            case 0:
                return "Unspecified";
            case 1:
                return "Wireless LAN";
            case 2:
                return "Cable Modem";
            case 8:
                return "Wireless WAN";
            case 9:
                return "Native 802.11";
            case 10:
                return "BlueTooth";
            case 11:
                return "Infiniband";
            case 12:
                return "WiMAX";
            case 13:
                return "UWB";
            case 14:
                return "802.3";
            case 16:
                return "IRDA";
            case 17:
                return "Wired WAN";
            case 18:
                return "Wired Connection Oriented WAN";
            case 19:
                return "Other";
            default:
                return "Unknown";
        }
    }
}

public sealed class NetworkCompatibilityResult
{
    public object? Value { get; internal set; }
    public List<string> Messages { get; } = new List<string>();
    public string? Failure { get; internal set; }
    public ErrorRecord? Error { get; internal set; }
}
