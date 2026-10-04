using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace PSFoundation.Networking.Platform;

internal sealed class CimNetworkReader : IDisposable
{
    internal readonly List<CimInstance> Adapters = new List<CimInstance>();
    internal readonly List<CimInstance> Configurations = new List<CimInstance>();
    internal readonly List<CimInstance> Routes = new List<CimInstance>();

    internal static CimNetworkReader Read(TimeSpan timeout, CancellationToken token)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new PlatformNotSupportedException("Adapter inventory requires Windows.");
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        token.ThrowIfCancellationRequested();
        var result = new CimNetworkReader();
        try
        {
            using (var session = CimSession.Create(null))
            using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = token })
            {
                void Query(string ns, string query, List<CimInstance> output)
                {
                    foreach (var item in session.QueryInstances(ns, "WQL", query, options))
                    { output.Add(item); token.ThrowIfCancellationRequested(); }
                }
                Query(@"root\StandardCimv2", "SELECT * FROM MSFT_NetAdapter", result.Adapters);
                Query(@"root\cimv2", "SELECT * FROM Win32_NetworkAdapterConfiguration", result.Configurations);
                Query(@"root\StandardCimv2", "SELECT * FROM MSFT_NetRoute WHERE DestinationPrefix = '0.0.0.0/0' OR DestinationPrefix = '::/0'", result.Routes);
            }
            return result;
        }
        catch { result.Dispose(); token.ThrowIfCancellationRequested(); throw; }
    }
    internal static object? Value(CimInstance instance, string name) => instance.CimInstanceProperties[name]?.Value;
    internal static uint Number(CimInstance instance, string name) => Convert.ToUInt32(Value(instance, name), CultureInfo.InvariantCulture);
    internal static string Text(CimInstance instance, string name) => Convert.ToString(Value(instance, name), CultureInfo.InvariantCulture) ?? "";
    internal static string[] Strings(CimInstance instance, string name) => Value(instance, name) as string[] ?? Array.Empty<string>();
    internal CimInstance? SelectDefault(AddressFamily family)
    {
        var route = SelectRoute(family);
        return route == null ? null : Adapters.FirstOrDefault(adapter => Number(adapter, "InterfaceIndex") == Number(route, "InterfaceIndex"));
    }
    internal CimInstance? SelectRoute(AddressFamily family) => Routes.Where(route => Text(route, "DestinationPrefix") == (family == AddressFamily.InterNetwork ? "0.0.0.0/0" : "::/0")
        && Text(route, "NextHop") != (family == AddressFamily.InterNetwork ? "0.0.0.0" : "::")).OrderBy(route => Number(route, "RouteMetric")).FirstOrDefault();
    internal CimInstance? Configuration(CimInstance adapter) => Configurations.FirstOrDefault(config => Number(config, "InterfaceIndex") == Number(adapter, "InterfaceIndex"));
    internal NetworkAdapterSnapshot ToSnapshot(CimInstance adapter)
    {
        var config = Configuration(adapter);
        var addresses = new List<NetworkInterfaceAddress>();
        if (config != null)
        {
            var ips = Strings(config, "IPAddress");
            var subnets = Strings(config, "IPSubnet");
            for (var index = 0; index < ips.Length; index++)
            {
                var address = IPAddressParser.Parse(ips[index]);
                int? prefix = null;
                if (index < subnets.Length && !string.IsNullOrEmpty(subnets[index]))
                    prefix = int.TryParse(subnets[index], NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length
                        : IPNetwork.FromSubnetMask(new IPAddress(address.GetAddressBytes()), IPAddressParser.Parse(subnets[index])).PrefixLength;
                addresses.Add(new NetworkInterfaceAddress(address, prefix));
            }
        }
        var medium = Number(adapter, "NdisPhysicalMedium");
        var kind = medium == 0 ? NetworkAdapterKind.Unspecified : medium == 14 ? NetworkAdapterKind.Ethernet : medium == 1 || medium == 9 ? NetworkAdapterKind.WiFi : NetworkAdapterKind.Other;
        return new NetworkAdapterSnapshot(Text(adapter, "Name"), Number(adapter, "InterfaceIndex"), kind, addresses,
            config == null ? null : Strings(config, "DefaultIPGateway").Select(IPAddressParser.Parse),
            config == null ? null : Strings(config, "DNSServerSearchOrder").Select(IPAddressParser.Parse),
            config == null ? null : Value(config, "MACAddress") as string);
    }
    public void Dispose() { foreach (var item in Adapters.Concat(Configurations).Concat(Routes)) item.Dispose(); }
}
