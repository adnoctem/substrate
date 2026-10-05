using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace AdNoctem.Substrate.Packages.Runtime;

[DataContract]
internal sealed class RuntimeRequest
{
    [DataMember] public string Operation { get; set; } = "";
    [DataMember] public string Target { get; set; } = "";
    [DataMember] public string[] Dependencies { get; set; } = Array.Empty<string>();
    [DataMember] public bool AllUsers { get; set; }
    [DataMember] public bool ForceUpdate { get; set; }
    [DataMember] public string CancellationEvent { get; set; } = "";
}
[DataContract]
internal sealed class RuntimePackage
{
    [DataMember] public string Name { get; set; } = "";
    [DataMember] public string FullName { get; set; } = "";
    [DataMember] public string FamilyName { get; set; } = "";
    [DataMember] public string Publisher { get; set; } = "";
    [DataMember] public string Version { get; set; } = "";
    [DataMember] public string Architecture { get; set; } = "";
    [DataMember] public string InstallLocation { get; set; } = "";
    [DataMember] public bool IsFramework { get; set; }
    [DataMember] public bool IsResource { get; set; }
    [DataMember] public bool IsBundle { get; set; }
    [DataMember] public bool NonRemovable { get; set; }
}
[DataContract]
internal sealed class RuntimeStoreUpdate
{
    [DataMember] public string PackageFamilyName { get; set; } = "";
    [DataMember] public string ProductId { get; set; } = "";
    [DataMember] public string InstallType { get; set; } = "";
    [DataMember] public string? ItemKind { get; set; }
    [DataMember] public int ErrorCode { get; set; }
    [DataMember] public uint? CompletedInstallCount { get; set; }
    [DataMember] public uint? TotalInstallCount { get; set; }
}
[DataContract]
internal sealed class RuntimeResponse
{
    [DataMember] public int Schema { get; set; } = 1;
    [DataMember] public bool Succeeded { get; set; }
    [DataMember] public bool Cancelled { get; set; }
    [DataMember] public bool NoUpdate { get; set; }
    [DataMember] public int ErrorCode { get; set; }
    [DataMember] public string Error { get; set; } = "";
    [DataMember] public RuntimePackage[] Packages { get; set; } = Array.Empty<RuntimePackage>();
    [DataMember] public RuntimeStoreUpdate[] Updates { get; set; } = Array.Empty<RuntimeStoreUpdate>();
}
internal static class PackageRuntimeProtocol
{
    internal static byte[] Serialize<T>(T value)
    { using var stream = new MemoryStream(); new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return stream.ToArray(); }
    internal static T Deserialize<T>(byte[] data)
    {
        if (data.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Package runtime response exceeds the size limit.");
        using var stream = new MemoryStream(data, false);
        return (T)(new DataContractJsonSerializer(typeof(T)).ReadObject(stream) ?? throw new InvalidDataException("Empty package runtime document."));
    }
}
