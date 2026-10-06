using System;
using System.Collections;
using System.Management.Automation;

namespace AdNoctem.Substrate.PowerShell.Infrastructure;

/// <summary>Preserves the caller's collection identity across the script-to-compiled binder boundary.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class UnwrapListAttribute : ArgumentTransformationAttribute
{
    public override object Transform(EngineIntrinsics engineIntrinsics, object inputData) =>
        LanguagePrimitives.ConvertTo(inputData, typeof(IList));
}
