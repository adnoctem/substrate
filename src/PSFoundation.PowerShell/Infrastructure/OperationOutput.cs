using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using PSFoundation.Core.Compatibility;

namespace PSFoundation.PowerShell.Infrastructure;

internal static class OperationOutput
{
    internal static PSObject Create(string target, string source, string action, string status,
        IDictionary<string, object?>? fields = null, Hashtable? properties = null)
    {
        var supplied = fields == null ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(fields, StringComparer.OrdinalIgnoreCase);
        supplied["Source"] = source;
        var value = new PSObject();
        foreach (var pair in OperationResult.Create(target, action, status, supplied, properties).Fields)
            value.Properties.Add(new PSNoteProperty(pair.Key, pair.Value));
        return value;
    }
}
