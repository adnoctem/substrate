using System;
using System.Collections;
using System.Management.Automation;

namespace AdNoctem.Substrate.PowerShell.Infrastructure;

/// <summary>Unwraps enumerable arguments before the compiled parameter binder attempts collection conversion.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class UnwrapEnumerableAttribute : ArgumentTransformationAttribute
{
    public override object Transform(EngineIntrinsics engineIntrinsics, object inputData) =>
        LanguagePrimitives.ConvertTo(inputData, typeof(IEnumerable));
}
