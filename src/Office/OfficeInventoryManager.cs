using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Office;

/// <summary>Observes and classifies Office registrations without launching Office, Windows Installer or setup.exe.</summary>
public sealed class OfficeInventoryManager
{
    private const string OfficeCodePattern = @"^\{9[01](12|14|15|16)0000-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{7}FF1CE\}$";
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;
    private static readonly Dictionary<string, OfficeChannel> Channels = new Dictionary<string, OfficeChannel>(Names)
    {
        ["492350f6-3a01-4f97-b9c0-c7c6ddf67d60"] = OfficeChannel.Current,
        ["55336b82-a18d-4dd6-b5f6-9e5095c314a6"] = OfficeChannel.MonthlyEnterprise,
        ["7ffbc6bf-bc32-4f92-8982-f9dd17fd3114"] = OfficeChannel.SemiAnnual,
        ["f2e724c1-748f-4b47-8fb8-8e0d210e9208"] = OfficeChannel.PerpetualVL2019,
        ["5030841d-c919-4594-8d2d-84ae4f96e58e"] = OfficeChannel.PerpetualVL2021,
        ["7983bac0-e531-40cf-be00-fd24fe66619c"] = OfficeChannel.PerpetualVL2024
    };
    public OfficeInventory Read(CancellationToken cancellationToken = default)
    {
        var snapshot = new OfficeRegistryReader().Read(continueOnError: true, cancellationToken: cancellationToken);
        var inspector = new OfficeAppPathInspector();
        var paths = snapshot.Records.Where(IsAppPath).Select(record => inspector.Inspect(record, cancellationToken)).ToArray();
        return Analyze(snapshot.MachineId, snapshot.Records, paths, snapshot.Errors);
    }
    public Task<OfficeInventory> ReadAsync(CancellationToken cancellationToken = default) => Task.Run(() => Read(cancellationToken), cancellationToken);
    /// <summary>Classifies supplied evidence without I/O. Unprobed executable registrations remain uncertain; no absence is inferred.</summary>
    public OfficeInventory Analyze(string machineId, IEnumerable<OfficeRegistryRecord> records, IEnumerable<OfficeAppPathEvidence>? appPaths = null,
        IEnumerable<OfficeRegistryReadError>? readErrors = null)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            throw new ArgumentException("A machine identity is required.", nameof(machineId));
        var rows = (records ?? throw new ArgumentNullException(nameof(records))).ToArray();
        if (rows.Any(row => row == null))
            throw new ArgumentException("Records cannot contain null.", nameof(records));
        var errors = (readErrors ?? Array.Empty<OfficeRegistryReadError>()).ToArray();
        var providedPaths = (appPaths ?? Array.Empty<OfficeAppPathEvidence>()).ToArray();
        var paths = rows.Where(IsAppPath).Select(record => providedPaths.SingleOrDefault(path => path.RegistryView == record.View && Names.Equals(path.RegistryPath, record.Path.SubKey))
            ?? new OfficeAppPathEvidence(record.View, record.Path.SubKey, record.GetValue(""), null, OfficeAppPathState.Uncertain, OfficeAppPathReason.ProbeFailed)).ToArray();
        var products = new List<OfficeProductState>();
        var msi = new List<OfficeMsiRegistration>();
        var related = new List<OfficeRelatedComponent>();
        var unknowns = new List<string>();
        if (errors.Length != 0)
            unknowns.Add("RegistryDiscoveryFailed");
        var configured = rows.Where(record => Names.Equals(record.Path.SubKey, OfficeRegistryReader.ConfigurationPath)).ToArray();
        var residue = rows.Any(record => record.Path.SubKey.StartsWith(OfficeRegistryReader.ResourceRoot, StringComparison.OrdinalIgnoreCase) || Names.Equals(record.Path.SubKey, OfficeRegistryReader.InstalledPath));
        var appResidue = paths.Any(path => path.State != OfficeAppPathState.Missing && (Match(path.RawTarget as string, "Office16|ClickToRun") || Match(path.ResolvedTarget, "Office16|ClickToRun")
            || path.State == OfficeAppPathState.Uncertain && !Match(path.ResolvedTarget, @"\\Office(?:11|12|14|15)\\(?:WINWORD|EXCEL|OUTLOOK)\.EXE$")));
        if (configured.Length == 0 && (residue || appResidue))
            unknowns.Add("OfficeResidueWithoutConfiguration");
        foreach (var record in rows)
        {
            if (Names.Equals(record.Path.SubKey, OfficeRegistryReader.ConfigurationPath))
            {
                var ids = OfficeVersionResolver.SplitIds(record.GetString("ProductReleaseIds"), ',');
                if (ids.Length == 0)
                    unknowns.Add("IncompleteClickToRunRegistration");
                foreach (var id in ids)
                {
                    var product = ReadProduct(rows, record, id, ids);
                    products.Add(product);
                    if (product.InstalledVersion.Issue != null)
                        unknowns.Add(product.InstalledVersion.Issue);
                }
            }
            else if (record.Path.SubKey.StartsWith(OfficeRegistryReader.UninstallRoot + "\\", StringComparison.OrdinalIgnoreCase))
                ClassifyRegistration(record, configured.Length != 0, msi, related, unknowns);
        }
        var codes = new HashSet<string>(msi.Select(item => item.ProductCode), Names);
        foreach (var patch in related.Where(item => item.Role == OfficeRelatedRole.PatchRegistration))
            if (!codes.Contains(patch.ParentProductCode!))
                unknowns.Add("OrphanedOfficePatchRegistration:" + patch.ProductCode);
        var unique = new List<OfficeProductState>();
        foreach (var group in products.GroupBy(product => product.ProductId, Names))
        {
            // Preserve the existing view conflict boundary; retain full raw records for further diagnosis.
            if (group.Select(product => product.Architecture + "|" + product.Version + "|" + product.Channel).Distinct(Names).Count() > 1)
                unknowns.Add("ConflictingRegistryViews");
            unique.Add(group.First());
        }
        var limitations = new List<string>();
        foreach (var product in unique)
        { if (product.Languages == null) limitations.Add("Languages"); if (string.IsNullOrEmpty(product.PrimaryLanguage)) limitations.Add("PrimaryLanguage"); }
        return new OfficeInventory(machineId, unique.OrderBy(product => product.ProductId, Names), msi.GroupBy(item => item.ProductCode, Names).Select(group => group.First()).OrderBy(item => item.ProductCode, Names),
            related.OrderBy(item => item.ProductCode, Names).ThenBy(item => item.RegistryView.ToString(), Names), Sorted(unknowns), Sorted(limitations), rows, paths, errors);
    }
    private static OfficeProductState ReadProduct(OfficeRegistryRecord[] rows, OfficeRegistryRecord record, string id, string[] ids)
    {
        var installed = new OfficeVersionResolver().Resolve(rows, record.View, id, ids);
        OfficeArchitecture? architecture = Names.Equals(record.GetString("Platform"), "x64") ? OfficeArchitecture.X64 : Names.Equals(record.GetString("Platform"), "x86") ? OfficeArchitecture.X86 : (OfficeArchitecture?)null;
        var channels = Channels.Where(pair => (record.GetString("CDNBaseUrl") ?? "").IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0).Select(pair => pair.Value).ToArray();
        OfficeChannel? channel = channels.Length == 1 ? channels[0] : (OfficeChannel?)null;
        string[]? excluded = null;
        var exclusionEvidence = "ExcludeApp unknown: registration is missing or not a string";
        if (record.GetValue(id + ".ExcludedApps") is string excludedText)
        { excluded = Sorted(OfficeVersionResolver.SplitIds(excludedText, ',').Select(value => value.ToLowerInvariant())); exclusionEvidence = "ExcludeApp from " + record.View + ":" + record.Path.SubKey + " value " + id + ".ExcludedApps"; }
        var registered = Array.Empty<string>();
        string? productPath = null;
        var active = rows.Where(row => row.View == record.View && Names.Equals(row.Path.SubKey, OfficeRegistryReader.ResourceRoot)).ToArray();
        if (active.Length == 1 && Match(active[0].GetString("ActiveConfiguration"), "^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$"))
        {
            productPath = OfficeRegistryReader.ResourceRoot + "\\" + active[0].GetString("ActiveConfiguration") + "\\" + id + ".16";
            var resources = rows.Where(row => row.View == record.View && Names.Equals(row.Path.SubKey, productPath)).ToArray();
            if (resources.Length == 1)
                registered = Sorted(resources[0].SubKeys.Where(value => Match(value, "^[a-z]{2,3}-[a-z]{2,4}$") && !Names.Equals(value, "x-none")).Select(value => value.ToLowerInvariant()));
        }
        string[]? languages = null;
        string? primary = null;
        var languageEvidence = "Languages unknown: no active per-product resource registration";
        var primaryEvidence = "PrimaryLanguage unknown: no corroborated shell language";
        var culture = (record.GetString("ClientCulture") ?? "").ToLowerInvariant();
        if (registered.Length != 0)
        {
            languages = registered;
            languageEvidence = "Languages from " + record.View + ":" + productPath + " subkeys [" + string.Join(",", languages) + "]";
            if (languages.Length == 1)
            { primary = languages[0]; primaryEvidence = "PrimaryLanguage from a single registered language [" + primary + "]"; }
            else if (culture.Length != 0 && languages.Contains(culture, Names))
            { primary = culture; primaryEvidence = "PrimaryLanguage from ClientCulture=" + culture + " corroborated by the resource registration"; }
            else if (culture.Length != 0)
                primaryEvidence = "PrimaryLanguage unknown: ClientCulture=" + culture + " is not in the registered languages [" + string.Join(",", languages) + "]";
        }
        return new OfficeProductState(id, architecture, installed, channel, languages, primary, registered, excluded, new[]
        {
            record.View + ":" + record.Path.SubKey, installed.Evidence, "Telemetry VersionToReport=" + record.GetString("VersionToReport") + "; not installation evidence",
            "ClientCulture=" + record.GetString("ClientCulture") + "; not proof of complete languages or shell UI", languageEvidence, primaryEvidence, exclusionEvidence
        });
    }
    private static void ClassifyRegistration(OfficeRegistryRecord record, bool configured, List<OfficeMsiRegistration> msi, List<OfficeRelatedComponent> related, List<string> unknowns)
    {
        var code = record.Path.SubKey.Substring(record.Path.SubKey.LastIndexOf('\\') + 1);
        var name = record.GetString("DisplayName");
        var uninstall = record.GetString("UninstallString");
        var microsoft = Match(record.GetString("Publisher"), "^Microsoft(?: Corporation)?$");
        var officeCode = Match(code, OfficeCodePattern);
        var officeName = Match(name, "Office|Visio|Project|Access|SharePoint Designer|InfoPath|Lync");
        var controller = Match(uninstall, @"\\OFFICE(12|14|15|16)\\Office Setup Controller\\setup\.exe" + "\"?\\s+/uninstall\\s");
        var infrastructure = Match(code, @"^\{9[01]160000-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{7}FF1CE\}$")
            && Match(name, "^Office 16 Click-to-Run (Licensing|Extensibility|Localization) Component(?: 64-bit Registration)?$");
        bool IsOne(string field) => Convert.ToString(record.GetValue(field), CultureInfo.InvariantCulture) == "1";
        var addIn = IsOne("WindowsInstaller") && new[] { "Microsoft Teams Meeting Add-in for Microsoft Office", "Microsoft Office Live Add-in 1.5" }.Contains(name, Names);
        if (microsoft && (infrastructure || addIn))
        {
            related.Add(new OfficeRelatedComponent(code, record, infrastructure ? OfficeRelatedRole.ClickToRunInfrastructure : OfficeRelatedRole.AddIn));
            if (infrastructure && !configured)
                unknowns.Add("ClickToRunInfrastructureWithoutConfiguration");
            return;
        }
        string? parent = null;
        var match = Regex.Match(code, @"^(\{[0-9A-F-]{36}\})_.+_\{[0-9A-F-]{36}\}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (match.Success)
            parent = match.Groups[1].Value;
        else
        {
            match = Regex.Match(uninstall ?? "", @"/package\s+(\{[0-9A-F-]{36}\})\s+/uninstall\s+\{[0-9A-F-]{36}\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (match.Success)
                parent = match.Groups[1].Value;
        }
        if (!Match(parent, OfficeCodePattern))
            parent = null;
        if (microsoft && parent != null)
        { related.Add(new OfficeRelatedComponent(code, record, OfficeRelatedRole.PatchRegistration, parent, IsOne("SystemComponent"))); return; }
        if (microsoft && (officeCode || officeName && (IsOne("WindowsInstaller") || controller)))
        {
            var kind = OfficeMsiResourceKind.ProductOrComponent;
            if (officeCode && Match(code, @"^\{[^-]+-(001F|002C)-") || Match(name, @"\bProof(?:ing)?\b"))
                kind = OfficeMsiResourceKind.Proofing;
            else if (Match(name, "Language Interface Pack"))
                kind = OfficeMsiResourceKind.LanguageInterfacePack;
            else if (Match(name, "Language Pack|MUI"))
                kind = OfficeMsiResourceKind.LanguageResource;
            string? language = null;
            match = Regex.Match(code, @"^\{[^-]+-[^-]+-([0-9A-F]{4})-", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (officeCode && match.Success)
                try
                { language = CultureInfo.GetCultureInfo(int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).Name.ToLowerInvariant(); }
                catch (CultureNotFoundException) { }
            msi.Add(new OfficeMsiRegistration(code, record, language, kind));
        }
        else if (microsoft && officeName && !Match(name, "Update|Hotfix|Security|Language|Proofing") && !Match(uninstall, @"OfficeClickToRun\.exe"))
            unknowns.Add("UnclassifiedOfficeRegistration:" + code);
    }
    internal static bool IsAppPath(OfficeRegistryRecord record) => record.Path.SubKey.IndexOf(@"\App Paths\", StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool Match(string? value, string pattern) => OfficeVersionResolver.IsMatch(value, pattern);
    private static string[] Sorted(IEnumerable<string> values) => values.Distinct(Names).OrderBy(value => value, Names).ToArray();
}
