#Requires -Version 7.0
<#
.SYNOPSIS
  Removes generated outputs while retaining restored tools and packages.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Clean
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param ()

$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent)) + [IO.Path]::DirectorySeparatorChar

foreach ($relative in @('build/bin', 'build/obj', 'build/module', 'build/docs', 'build/test-results', 'dist')) {
  $target = [IO.Path]::GetFullPath((Join-Path $root $relative))

  if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Clean target escaped the repository.' }

  if ((Test-Path -LiteralPath $target) -and $PSCmdlet.ShouldProcess($target, 'Remove generated output')) { Remove-Item -LiteralPath $target -Recurse -Force }
}
