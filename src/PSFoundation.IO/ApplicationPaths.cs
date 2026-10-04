using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PSFoundation.IO;

/// <summary>Conventional paths only; construction does not create directories or change access permissions.</summary>
public sealed class ApplicationPaths
{
    public FileSystemPath Home { get; }
    public FileSystemPath Config { get; }
    public FileSystemPath Cache { get; }
    public FileSystemPath Data { get; }
    public FileSystemPath Logs { get; }
    public ApplicationPaths(string name, FileSystemPath userProfile, FileSystemPath roamingData, FileSystemPath localData, FileSystemPath sharedData)
    {
        if (name == null || !Regex.IsMatch(name, @"\A[A-Za-z0-9._-]+\z") || name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal))
            throw new ArgumentException("Use a single product directory name without traversal or trailing dots.", nameof(name));
        Home = (userProfile ?? throw new ArgumentNullException(nameof(userProfile))).Combine(name);
        Config = (roamingData ?? throw new ArgumentNullException(nameof(roamingData))).Combine(name);
        Cache = (localData ?? throw new ArgumentNullException(nameof(localData))).Combine(name);
        Data = (sharedData ?? throw new ArgumentNullException(nameof(sharedData))).Combine(name);
        Logs = Cache.Combine("logs");
    }
    public static ApplicationPaths FromEnvironment(string name)
    {
        FileSystemPath Read(string key) => FileSystemPath.Parse(Environment.GetEnvironmentVariable(key)
            ?? throw new IOException("Required environment variable is missing: " + key));
        return new ApplicationPaths(name, Read("USERPROFILE"), Read("APPDATA"), Read("LOCALAPPDATA"), Read("ProgramData"));
    }
}
