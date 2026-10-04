#Requires -Version 5.1

function Write-PSFCompiledHelp {
  [CmdletBinding()]
  param ([string]$SourceRoot, [string]$Stage, [string[]]$Commands)

  $reference = Get-Content (Join-Path (Split-Path $SourceRoot -Parent) 'tests/fixtures/Api/desktop.json') -Raw | ConvertFrom-Json
  $maml = New-Object Text.StringBuilder
  $null = $maml.Append('<helpItems schema="maml" xmlns:maml="http://schemas.microsoft.com/maml/2004/10" xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10" xmlns:dev="http://schemas.microsoft.com/maml/dev/2004/10">')
  foreach ($file in Get-ChildItem -LiteralPath $SourceRoot -Filter '*.ps1') {
    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
    foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
      if ($function.Name -notin $Commands) { continue }
      $help = $function.GetHelpContent()
      if ($null -eq $help) {
        # A leading .NET in prose is mistaken for a help directive by the v1 parser.
        $helpText = $function.Extent.Text -replace '(?im)^(\s*)\.NET\b', '$1The .NET'
        $helpAst = [Management.Automation.Language.Parser]::ParseInput($helpText, [ref]$null, [ref]$null)
        $help = $helpAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true).GetHelpContent()
      }
      if ($null -eq $help) {
        # Some v1 functions put attributes before their help block. Parse the actual
        # comment in a normal help position without changing the frozen source.
        $comment = @($tokens | Where-Object { $_.Kind -eq 'Comment' -and $_.Text -match '(?im)^\s*\.SYNOPSIS' -and $_.Extent.StartOffset -ge $function.Extent.StartOffset -and $_.Extent.EndOffset -le $function.Extent.EndOffset } | Select-Object -First 1)
        if ($comment.Count -ne 0) {
          $helpAst = [Management.Automation.Language.Parser]::ParseInput(("function HelpCapture {`n" + $comment[0].Text + "`nparam()`n}"), [ref]$null, [ref]$null)
          $help = $helpAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true).GetHelpContent()
        }
      }
      $name = [Security.SecurityElement]::Escape($function.Name)
      $synopsis = [Security.SecurityElement]::Escape($help.Synopsis)
      $description = [Security.SecurityElement]::Escape($help.Description)
      $metadata = $reference.Contract.Commands | Where-Object Name -EQ $function.Name
      $parameterXml = New-Object Text.StringBuilder
      $previousEnd = $function.Body.ParamBlock.Extent.StartOffset
      foreach ($parameter in $function.Body.ParamBlock.Parameters) {
        $parameterName = $parameter.Name.VariablePath.UserPath
        $parameterHelp = ''
        if ($null -ne $help -and $null -ne $help.Parameters) {
          $parameterHelp = [Security.SecurityElement]::Escape($help.Parameters[$parameterName.ToUpperInvariant()])
        }
        if (-not $parameterHelp) {
          $comments = @($tokens | Where-Object { $_.Kind -eq 'Comment' -and $_.Extent.StartOffset -ge $previousEnd -and $_.Extent.EndOffset -le $parameter.Extent.StartOffset })
          $parameterHelp = [Security.SecurityElement]::Escape((($comments | ForEach-Object { $_.Text -replace '^#\s*', '' }) -join ' '))
        }
        $previousEnd = $parameter.Extent.EndOffset
        $contract = $metadata.ParameterSets[0].Parameters | Where-Object Name -EQ $parameterName
        $required = ([string]$contract.Mandatory).ToLowerInvariant()
        $pipeline = if ($contract.Pipeline) { 'true (ByValue)' } else { 'false' }
        $position = if ($contract.Position -ge 0) { [string]$contract.Position } else { 'named' }
        $type = [Security.SecurityElement]::Escape($contract.Type)
        $defaultValue = [Security.SecurityElement]::Escape([string]$parameter.DefaultValue.Extent.Text)
        $null = $parameterXml.Append("<command:parameter required='$required' globbing='false' pipelineInput='$pipeline' position='$position'><maml:name>$parameterName</maml:name><maml:description><maml:para>$parameterHelp</maml:para></maml:description><command:parameterValue required='true'>$type</command:parameterValue><dev:type><maml:name>$type</maml:name></dev:type><dev:defaultValue>$defaultValue</dev:defaultValue></command:parameter>")
      }
      $null = $maml.Append("<command:command><command:details><command:name>$name</command:name><maml:description><maml:para>$synopsis</maml:para></maml:description></command:details><maml:description><maml:para>$description</maml:para></maml:description><command:syntax><command:syntaxItem><maml:name>$name</maml:name>$parameterXml</command:syntaxItem></command:syntax><command:parameters>$parameterXml</command:parameters><command:examples>")
      $index = 0
      foreach ($example in $help.Examples) {
        $index++
        $exampleText = [Security.SecurityElement]::Escape($example)
        $null = $maml.Append("<command:example><maml:title>Example $index</maml:title><dev:code>$exampleText</dev:code></command:example>")
      }
      $null = $maml.Append('</command:examples><command:returnValues>')
      foreach ($outputType in $metadata.OutputTypes) {
        $type = [Security.SecurityElement]::Escape($outputType)
        $null = $maml.Append("<command:returnValue><dev:type><maml:name>$type</maml:name></dev:type></command:returnValue>")
      }
      $notes = [Security.SecurityElement]::Escape($help.Notes)
      $null = $maml.Append("</command:returnValues><maml:alertSet><maml:alert><maml:para>$notes</maml:para></maml:alert></maml:alertSet><maml:relatedLinks>")
      foreach ($link in $help.Links) {
        $uri = [Security.SecurityElement]::Escape($link)
        $null = $maml.Append("<maml:navigationLink><maml:uri>$uri</maml:uri></maml:navigationLink>")
      }
      $null = $maml.Append('</maml:relatedLinks></command:command>')
    }
  }
  $null = $maml.Append('</helpItems>')
  foreach ($edition in @('Desktop', 'Core')) {
    $helpRoot = Join-Path $Stage "lib/$edition/en-US"
    $null = [IO.Directory]::CreateDirectory($helpRoot)
    [IO.File]::WriteAllText((Join-Path $helpRoot 'PSFoundation.PowerShell.dll-Help.xml'), $maml.ToString(), (New-Object Text.UTF8Encoding($true)))
  }
}
