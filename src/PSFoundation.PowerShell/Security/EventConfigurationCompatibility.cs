using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using PSFoundation.Security;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Security;

public static class EventConfigurationCompatibility
{
    /// <summary>Reads a data-only PowerShell hashtable AST. No expressions are invoked as code.</summary>
    public static Hashtable Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Security event configuration file not found: " + path, path);
        Token[] tokens;
        ParseError[] errors;
        var ast = Parser.ParseFile(path, out tokens, out errors);
        if (errors.Length != 0)
            throw new InvalidDataException("Invalid security event configuration: " + errors[0].Message);
        if (ast.BeginBlock != null || ast.ProcessBlock != null || ast.ParamBlock != null || ast.EndBlock == null || ast.EndBlock.Statements.Count != 1
            || !(ast.EndBlock.Statements[0] is PipelineAst pipeline) || pipeline.PipelineElements.Count != 1
            || !(pipeline.PipelineElements[0] is CommandExpressionAst expression) || !(expression.Expression is HashtableAst table))
            throw new InvalidDataException("Security event configuration must contain one literal hashtable.");
        return (Hashtable)table.SafeGetValue();
    }
    public static object Group(string name, Hashtable configuration)
    {
        var groups = (Hashtable)configuration["Groups"];
        if (!groups.ContainsKey(name))
            throw new ArgumentException("Unknown security event group: " + name);
        return groups[name];
    }
    public static Hashtable[] Definitions(Hashtable configuration, string? group, string? logName, string? providerName, int[]? ids, string? name)
    {
        var events = (Hashtable)configuration["Events"];
        var originals = new Dictionary<SecurityEventDefinition, Hashtable>();
        foreach (var log in events.Keys)
            foreach (DictionaryEntry section in (Hashtable)events[log])
                foreach (var item in LanguagePrimitives.GetEnumerable(section.Value) ?? new[] { section.Value })
                {
                    var value = (Hashtable)item;
                    string? Text(string key) => value[key] == null ? null : LanguagePrimitives.ConvertTo<string>(value[key]);
                    var definition = new SecurityEventDefinition(LanguagePrimitives.ConvertTo<int>(value["Id"]), Text("LogName")!, Text("ProviderName"), Text("Name"), Text("UtilityGroup"));
                    originals.Add(definition, value);
                }
        return new SecurityEventCatalog(originals.Keys).Find(group, logName, providerName, ids, name).Select(definition => originals[definition]).ToArray();
    }
    public static object? MappedField(string name, object value, Hashtable configuration)
    {
        var maps = (Hashtable)configuration["FieldMaps"];
        if (!maps.ContainsKey(name))
            return null;
        var map = (Hashtable)maps[name];
        if (map.ContainsKey(value))
            return map[value];
        var text = LanguagePrimitives.ConvertTo<string>(value);
        return map.ContainsKey(text) ? map[text] : null;
    }
    public static PSObject ConvertEvent(PSObject value, Hashtable configuration)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        var method = value.Methods["ToXml"] ?? throw new ArgumentException("An event with a ToXml method is required.", nameof(value));
        var payload = WindowsEventPayload.Parse(LanguagePrimitives.ConvertTo<string>(method.Invoke()));
        var fields = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
        foreach (var field in payload.Fields)
            if (!string.IsNullOrWhiteSpace(field.Name))
                fields[field.Name!] = field.Value.Length == 0 ? null : field.Value;
        object? logonType = null, logonName = null, impersonationName = null;
        if (fields.Contains("LogonType"))
        {
            try
            {
                logonType = LanguagePrimitives.ConvertTo<int>(fields["LogonType"]);
                logonName = (MappedField("LogonType", logonType, configuration) as Hashtable)?["Name"];
            }
            catch (PSInvalidCastException) { logonType = fields["LogonType"]; }
        }
        if (fields.Contains("ImpersonationLevel") && fields["ImpersonationLevel"] != null)
            impersonationName = (MappedField("ImpersonationLevel", fields["ImpersonationLevel"]!, configuration) as Hashtable)?["Name"];
        object? Read(string name) => value.Properties[name]?.Value;
        var output = SystemOutput.Object("TimeCreated", Read("TimeCreated"), "Id", Read("Id"), "ProviderName", Read("ProviderName"), "LogName", Read("LogName"),
            "MachineName", Read("MachineName"), "RecordId", Read("RecordId"), "LevelDisplayName", Read("LevelDisplayName"),
            "TargetUserName", fields["TargetUserName"], "TargetDomainName", fields["TargetDomainName"], "SubjectUserName", fields["SubjectUserName"],
            "SubjectDomainName", fields["SubjectDomainName"], "LogonType", logonType, "LogonTypeName", logonName,
            "ImpersonationLevel", fields["ImpersonationLevel"], "ImpersonationLevelName", impersonationName);
        foreach (var name in new[] { "IpAddress", "IpPort", "WorkstationName", "ProcessName", "ProcessId", "LogonProcessName", "AuthenticationPackageName", "Status", "SubStatus",
            "TargetLogonId", "ServiceName", "ImagePath", "ServiceFileName" })
            output.Properties.Add(new PSNoteProperty(name, fields[name]));
        output.Properties.Add(new PSNoteProperty("RawData", fields));
        output.Properties.Add(new PSNoteProperty("EventRecord", value));
        return output;
    }
}
