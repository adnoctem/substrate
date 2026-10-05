using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Core;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Core;

public static class DataCompatibility
{
    public static void Merge(Array Base, Array Overrides)
    {
        var originals = Base.Cast<object>().Select(value => PSObject.AsPSObject(value).BaseObject).OfType<IDictionary>().ToArray();
        var records = originals.Select(value => value.Cast<DictionaryEntry>().ToDictionary(item => LanguagePrimitives.ConvertTo<string>(item.Key), item => (object?)item.Value, StringComparer.OrdinalIgnoreCase)).ToArray();
        var changes = Overrides.Cast<object>().Select(value => (IReadOnlyDictionary<string, object?>)PSObject.AsPSObject(value).Properties
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase)).ToArray();
        new RecordMerger().ApplyOverrides(records, changes);
        for (var index = 0; index < records.Length; index++)
            foreach (var key in originals[index].Keys.Cast<object>().ToArray())
                originals[index][key] = records[index][LanguagePrimitives.ConvertTo<string>(key)];
    }
}
