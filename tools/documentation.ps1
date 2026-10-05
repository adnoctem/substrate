#Requires -Version 7.0
<#
.SYNOPSIS
  Builds documentation from C# XML comments and reviewed PowerShell Markdown.
.PARAMETER Mode
  Help validates and stages MAML; Site builds DocFX; Update refreshes command metadata and the API catalog.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Docs
#>
[CmdletBinding()]
param ([ValidateSet('Help', 'Site', 'Update')][string]$Mode = 'Help')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'environment.ps1')
Import-DevelopmentModule Microsoft.PowerShell.PlatyPS
$stage = Join-Path $repositoryRoot 'build/module/PSFoundation'
$commandRoot = Join-Path $repositoryRoot 'docs/commands/PSFoundation'
$compiled = @((Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compiled-commands.psd1')).Values | ForEach-Object { $_ })
$compatibility = @((Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compatibility-commands.psd1')).Values | ForEach-Object { $_ })
$expected = @($compiled + $compatibility | Sort-Object)
$files = @(Get-ChildItem -LiteralPath $commandRoot -Filter '*.md' | Sort-Object BaseName)
if ($Mode -eq 'Update') {
  Import-Module (Join-Path $stage 'PSFoundation.psd1') -Force
  foreach ($name in $expected) {
    if ($name -notin $files.BaseName) {
      $newHelp = New-CommandHelp -CommandInfo (Get-Command -Name ("PSFoundation\" + $name))
      $null = $newHelp | Export-MarkdownCommandHelp -OutputFolder (Split-Path $commandRoot -Parent) -Force
    }
  }
  $files = @(Get-ChildItem -LiteralPath $commandRoot -Filter '*.md' | Sort-Object BaseName)
}
if (@(Compare-Object $expected @($files.BaseName)).Count) { throw 'Command help pages must match the exported command catalogs.' }

if ($Mode -eq 'Update') {
  Import-Module (Join-Path $stage 'PSFoundation.psd1') -Force
  foreach ($file in $files) {
    $previous = Import-MarkdownCommandHelp -LiteralPath $file.FullName
    $command = Get-Command -Name ("PSFoundation\" + $previous.Title)
    $help = New-CommandHelp -CommandInfo $command
    foreach ($property in @('Synopsis', 'Description', 'Notes')) { $help.$property = $previous.$property }
    foreach ($property in @('Examples', 'Inputs', 'Outputs', 'RelatedLinks')) {
      $help.$property.Clear()
      foreach ($entry in $previous.$property) { $help.$property.Add($entry) }
    }
    foreach ($parameter in $help.Parameters) {
      $old = @($previous.Parameters | Where-Object Name -EQ $parameter.Name)
      if ($old.Count -eq 1) { $parameter.Description = $old[0].Description; $parameter.DefaultValue = $old[0].DefaultValue }
      # Dynamic ValidateSet values can contain private machine-specific stores. Document the binding, never snapshot those values.
      if ($command.Parameters[$parameter.Name].IsDynamic) { $parameter.AcceptedValues.Clear() }
    }
    $help.ExternalHelpFile = 'PSFoundation-help.xml'
    $help.Metadata['external help file'] = 'PSFoundation-help.xml'
    $help.Metadata['Locale'] = 'en-US'
    $null = $help.Metadata.Remove('ms.date')
    $null = $help | Export-MarkdownCommandHelp -OutputFolder (Split-Path $commandRoot -Parent) -Force
    $text = [IO.File]::ReadAllText($file.FullName) -replace '\{\{ Fill in [^\r\n]*\}\}|\{\{Insert list of aliases\}\}', ''
    $aliases = @(Get-Alias | Where-Object { $_.ModuleName -eq 'PSFoundation' -and $_.ResolvedCommand.Name -eq $command.Name } | Select-Object -ExpandProperty Name)
    $aliasText = if ($aliases.Count) { $aliases -join ', ' } else { 'None.' }
    $text = [regex]::Replace($text, '(?s)(## ALIASES\r?\n).*?(?=\r?\n## DESCRIPTION)', ('$1' + "`r`n$aliasText`r`n"))
    $text = $text -replace '(?m)[ \t]+\r?$', ''
    [IO.File]::WriteAllText($file.FullName, (($text.TrimEnd() -replace '\r?\n', "`r`n") + "`r`n"), [Text.UTF8Encoding]::new($false))
  }
}

if ($Mode -in @('Help', 'Update')) {
  Import-Module (Join-Path $stage 'PSFoundation.psd1') -Force
  $helpObjects = foreach ($file in $files) {
    if ([IO.File]::ReadAllText($file.FullName) -match '\{\{[^\r\n]*\}\}') { throw "Unfinished help placeholder: $($file.Name)" }
    $help = Import-MarkdownCommandHelp -LiteralPath $file.FullName
    if ([string]::IsNullOrWhiteSpace($help.Synopsis) -or $help.Synopsis -match '\{\{.*\}\}') { throw "Missing synopsis: $($file.Name)" }
    if ([string]::IsNullOrWhiteSpace($help.Description)) { throw "Missing description: $($file.Name)" }
    if ($help.Examples.Count -eq 0) { throw "Missing examples: $($file.Name)" }
    $actual = New-CommandHelp -CommandInfo (Get-Command -Name ("PSFoundation\" + $help.Title))
    $expectedSyntax = @($help.Syntax | ForEach-Object { $_.ToString() }) -join "`n"
    $actualSyntax = @($actual.Syntax | ForEach-Object { $_.ToString() }) -join "`n"
    if ($expectedSyntax -cne $actualSyntax) { throw "Command syntax changed: $($help.Title). Expected: $expectedSyntax. Actual: $actualSyntax. Run UpdateHelp and review the result." }
    foreach ($parameter in $actual.Parameters) {
      $documented = @($help.Parameters | Where-Object Name -EQ $parameter.Name)
      if ($documented.Count -ne 1 -or $documented[0].Type -cne $parameter.Type) { throw "Parameter metadata changed: $($help.Title) -$($parameter.Name). Run UpdateHelp." }
    }
    $help
  }
  $helpRoot = Join-Path $stage 'en-US'
  $generated = Join-Path $repositoryRoot 'build/docs/help'
  $generatedMaml = Join-Path $generated 'PSFoundation/PSFoundation-help.xml'
  # The pinned exporter does not truncate an existing longer file, even with Force.
  if (Test-Path -LiteralPath $generatedMaml) { Remove-Item -LiteralPath $generatedMaml -Force }
  $null = $helpObjects | Export-MamlCommandHelp -OutputFolder $generated -Encoding ([Text.UTF8Encoding]::new($true)) -Force
  # PlatyPS 1.0.1 exports each example's entire Markdown as its MAML introduction.
  # Promote the fenced sample into dev:code so both PowerShell hosts render real examples.
  $xml = [Xml.XmlDocument]::new()
  $xml.Load($generatedMaml)
  $namespaces = [Xml.XmlNamespaceManager]::new($xml.NameTable)
  $namespaces.AddNamespace('command', 'http://schemas.microsoft.com/maml/dev/command/2004/10')
  $namespaces.AddNamespace('maml', 'http://schemas.microsoft.com/maml/2004/10')
  $namespaces.AddNamespace('dev', 'http://schemas.microsoft.com/maml/dev/2004/10')
  foreach ($example in $xml.SelectNodes('//command:example', $namespaces)) {
    $intro = $example.SelectSingleNode('maml:introduction/maml:para', $namespaces)
    $parts = [regex]::Match($intro.InnerText, '(?s)\A(.*?)```(?:powershell|pwsh)\r?\n(.*?)\r?\n```(.*)\z')
    if (-not $parts.Success -or [string]::IsNullOrWhiteSpace($parts.Groups[2].Value)) { throw 'Each help example needs a fenced PowerShell code block.' }
    $intro.InnerText = $parts.Groups[1].Value.Trim()
    $example.SelectSingleNode('dev:code', $namespaces).InnerText = $parts.Groups[2].Value.Trim()
    $remarks = $example.SelectSingleNode('dev:remarks', $namespaces)
    $remarks.RemoveAll()
    $paragraph = $xml.CreateElement('maml', 'para', $namespaces.LookupNamespace('maml'))
    $paragraph.InnerText = $parts.Groups[3].Value.Trim()
    $null = $remarks.AppendChild($paragraph)
  }
  $xml.Save($generatedMaml)
  $null = [IO.Directory]::CreateDirectory($helpRoot)
  Copy-Item -LiteralPath $generatedMaml -Destination $helpRoot -Force
  $maml = Join-Path $helpRoot 'PSFoundation-help.xml'
  if (-not (Test-Path -LiteralPath $maml)) { throw 'PlatyPS did not generate the expected help file.' }
  foreach ($edition in @('Desktop', 'Core')) {
    $destination = Join-Path $stage "lib/$edition/en-US"
    $null = [IO.Directory]::CreateDirectory($destination)
    Copy-Item -LiteralPath $maml -Destination (Join-Path $destination 'PSFoundation.PowerShell.dll-Help.xml') -Force
  }
  Write-Output "Generated help for $($helpObjects.Count) commands."
}

if ($Mode -in @('Site', 'Update')) {
  Push-Location $repositoryRoot
  try {
    $metadataRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'build/docs/api'))
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'build')) + [IO.Path]::DirectorySeparatorChar
    if (-not $metadataRoot.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Metadata must remain inside build.' }
    if (Test-Path -LiteralPath $metadataRoot) { Remove-Item -LiteralPath $metadataRoot -Recurse -Force }
    & dotnet tool run docfx metadata tools/docfx.json --warningsAsErrors
    if ($LASTEXITCODE) { throw 'DocFX metadata generation failed.' }
    & (Join-Path $PSScriptRoot 'api-catalog.ps1') -Update:($Mode -eq 'Update')
    & dotnet tool run docfx build tools/docfx.json --warningsAsErrors
    if ($LASTEXITCODE) { throw 'DocFX site generation failed.' }
  }
  finally { Pop-Location }
}
