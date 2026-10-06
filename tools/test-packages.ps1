#Requires -Version 7.0
<#
.SYNOPSIS
  Restores packaged libraries into isolated consumers and checks transitive runtime assets.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:TestPackages
#>
[CmdletBinding()]
param ()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$version = if ($env:SUBSTRATE_VERSION) { $env:SUBSTRATE_VERSION } else { '1.0.0' }

$domains = @('Core', 'Diagnostics', 'IO', 'Interop', 'Networking', 'Office', 'Packages', 'Policies', 'Registry', 'Security', 'Windows')
$work = Join-Path $root ('build/package-tests/' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path "$work/library", "$work/app" -Force
# Keep consumer projects independent of the repository's assembly names, output paths and packing metadata.
Set-Content -LiteralPath "$work/Directory.Build.props" -Value '<Project />'
$references = $domains | ForEach-Object { '<PackageReference Include="AdNoctem.Substrate.' + $_ + '" Version="[' + $version + ']" />' }
Set-Content -LiteralPath "$work/library/ConsumerLibrary.csproj" -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFrameworks>net48;net10.0-windows</TargetFrameworks></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
</Project>
"@
Set-Content -LiteralPath "$work/library/Library.cs" -Value 'public static class ConsumerLibrary { public static string Name => typeof(AdNoctem.Substrate.Registry.RegistryPath).Assembly.GetName().Name; }'
Set-Content -LiteralPath "$work/app/Consumer.csproj" -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFrameworks>net48;net10.0-windows</TargetFrameworks><OutputType>Exe</OutputType><PlatformTarget>x64</PlatformTarget></PropertyGroup>
  <ItemGroup><ProjectReference Include="../library/ConsumerLibrary.csproj" /></ItemGroup>
</Project>
'@
Set-Content -LiteralPath "$work/app/Program.cs" -Value @'
using System;
using System.IO;
using System.Reflection;
class Program
{
    static void Main()
    {
        if (ConsumerLibrary.Name != "AdNoctem.Substrate.Registry") throw new Exception("Wrong library identity.");
        foreach (var domain in "Core Diagnostics IO Interop Networking Office Packages Policies Registry Security Windows".Split(' '))
            Assembly.Load("AdNoctem.Substrate." + domain);
        foreach (var file in new[] { "AdNoctem.Substrate.WinRtHost.exe", "AdNoctem.Substrate.WinRtHost.exe.config" })
            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file))) throw new Exception("Missing transitive runtime asset: " + file);
        Console.WriteLine("Loaded eleven packaged libraries and found the Windows Runtime helper.");
    }
}
'@
$project = Join-Path $work 'app/Consumer.csproj'
# A fresh cache prevents a prior package of the same version from masking today's output.
# External dependencies come from Restore's local cache; substrate packages come only from dist.
& dotnet restore $project --source (Join-Path $root 'dist/nuget') --source (Join-Path $root 'build/packages') --packages "$work/packages" --nologo

if ($LASTEXITCODE) { throw 'Packaged consumer restore failed.' }

foreach ($framework in @('net48', 'net10.0-windows')) {
  $output = Join-Path $work "publish/$framework"
  & dotnet publish $project --no-restore --configuration Release --framework $framework --output $output --nologo

  if ($LASTEXITCODE) { throw "Packaged consumer publish failed: $framework" }

  if ($framework -eq 'net48') { & "$output/Consumer.exe" }
  else { & dotnet "$output/Consumer.dll" }

  if ($LASTEXITCODE) { throw "Packaged consumer execution failed: $framework" }
}

Write-Output "Verified $version NuGet consumers on .NET Framework and modern .NET."
