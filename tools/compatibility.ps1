#Requires -Version 5.1

function Get-SubstrateApiContract {
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$ModulePath)

  $module = Import-Module -Name $ModulePath -Force -PassThru -ErrorAction Stop

  $commands = foreach ($command in @($module.ExportedCommands.Values | Sort-Object Name)) {
    if ($command.CommandType -eq 'Alias') {
      [ordered]@{ Name = $command.Name; Kind = 'Alias'; Definition = $command.Definition }
      continue
    }

    $metadata = New-Object Management.Automation.CommandMetadata($command)

    $sets = foreach ($set in @($command.ParameterSets | Sort-Object Name)) {
      $parameters = foreach ($parameter in @($set.Parameters | Sort-Object Name)) {
        $attributes = foreach ($attribute in $parameter.Attributes) {
          if ($attribute -is [Management.Automation.ParameterAttribute] -or $attribute -is [Management.Automation.AliasAttribute]) { continue }

          $values = [ordered]@{}

          foreach ($property in @($attribute.GetType().GetProperties() | Where-Object Name -NE TypeId | Sort-Object Name)) {
            $value = $property.GetValue($attribute, $null)

            if ($value -is [scriptblock]) {
              # Compare validator code without indentation or extra blank lines. Keep statement
              # separators and literal token text, including whitespace inside quoted strings.
              $tokens = $null
              $null = [Management.Automation.Language.Parser]::ParseInput([string]$value, [ref]$tokens, [ref]$null)
              $normalized = New-Object 'Collections.Generic.List[object]'

              foreach ($token in $tokens) {
                if ($token.Kind -eq 'EndOfInput') { continue }

                if ($token.Kind -eq 'NewLine') {
                  if ($normalized.Count -eq 0 -or $normalized[$normalized.Count - 1].Kind -eq 'NewLine') { continue }

                  $normalized.Add([ordered]@{ Kind = 'NewLine'; Text = "`n" })
                }
                else {
                  $normalized.Add([ordered]@{ Kind = [string]$token.Kind; Text = $token.Text })
                }
              }

              if ($normalized.Count -gt 0 -and $normalized[$normalized.Count - 1].Kind -eq 'NewLine') { $normalized.RemoveAt($normalized.Count - 1) }

              $value = @($normalized.ToArray())
            }
            elseif ($value -is [type]) { $value = [string]$value }

            $values[$property.Name] = $value
          }

          [ordered]@{ Type = $attribute.GetType().FullName; Values = $values }
        }

        [ordered]@{
          Name = $parameter.Name; Type = $parameter.ParameterType.FullName
          Aliases = @($parameter.Aliases | Sort-Object); Mandatory = $parameter.IsMandatory; Position = $parameter.Position
          Pipeline = $parameter.ValueFromPipeline; PropertyPipeline = $parameter.ValueFromPipelineByPropertyName
          Remaining = $parameter.ValueFromRemainingArguments; Attributes = @($attributes | Sort-Object Type)
        }
      }

      [ordered]@{ Name = $set.Name; Default = $set.IsDefault; Parameters = @($parameters) }
    }

    [ordered]@{
      Name = $command.Name; Kind = [string]$command.CommandType
      ShouldProcess = $metadata.SupportsShouldProcess; ConfirmImpact = [string]$metadata.ConfirmImpact
      OutputTypes = @($command.OutputType | ForEach-Object Name); ParameterSets = @($sets)
    }
  }

  [ordered]@{ Module = $module.Name; Guid = [string]$module.Guid; Commands = @($commands) }
}
