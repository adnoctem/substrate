#Requires -Version 7.4
<#
.SYNOPSIS
  Regenerates the Word-saved press-release fixtures with desktop Word.
.DESCRIPTION
  Builds the synthetic authoring template with the Documents library, creates one document per scenario from it in desktop Word
  through COM, saves each document as .docx and removes machine and user metadata. The fixtures contain only synthetic text.
  Requires desktop Word on Windows and a prior Build. Word runs hidden and is closed afterwards; no other document is opened.
.PARAMETER Binaries
  Directory containing AdNoctem.Substrate.Documents.dll and its dependencies.
.EXAMPLE
  ./tests/fixtures/documents/New-WordFixtures.ps1
#>
[CmdletBinding()]
param (
  [string]$Binaries = (Join-Path $PSScriptRoot '../../../build/bin/PowerShell/Release/netstandard2.0')
)

$ErrorActionPreference = 'Stop'
$null = [Reflection.Assembly]::LoadFrom((Join-Path $Binaries 'AdNoctem.Substrate.Documents.dll'))
$null = [Reflection.Assembly]::LoadFrom((Join-Path $Binaries 'AdNoctem.Substrate.IO.dll'))
$tags = [AdNoctem.Substrate.Documents.PressReleaseTags]
$field = [AdNoctem.Substrate.Documents.AuthoringField]

function New-Text {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Builds a synthetic inline in memory.')]
  param ([string]$Text, [string]$Marks = 'None')

  [AdNoctem.Substrate.Documents.TextInline]::new($Text, [AdNoctem.Substrate.Documents.TextMarks]$Marks)
}

function New-Paragraph {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Builds a synthetic paragraph in memory.')]
  param ([object[]]$Inline)

  [AdNoctem.Substrate.Documents.ParagraphBlock]::new([AdNoctem.Substrate.Documents.DocumentInline[]]$Inline)
}

function ConvertTo-Block([object[]]$Block) {
  , ([AdNoctem.Substrate.Documents.DocumentBlock[]]$Block)
}

function ConvertTo-Element([object[]]$Element) {
  , ([AdNoctem.Substrate.Documents.AuthoringElement[]]$Element)
}

function New-SyntheticTemplate {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Writes the synthetic template to a temporary path.')]
  param ([string]$Path)

  $mail = [AdNoctem.Substrate.Documents.HyperlinkInline]::new(
    [Uri]'mailto:press@example.com',
    [AdNoctem.Substrate.Documents.DocumentInline[]]@(New-Text 'press@example.com')
  )
  $content = ConvertTo-Element @(
    [AdNoctem.Substrate.Documents.AuthoringInstruction]::new($tags::Instruction, 'Fill the fields; delete unused optional sections completely.')
    $field::Text($tags::Headline, 'Headline', 'Enter the headline.', $null, $true, $null)
    $field::Text($tags::Dateline, 'Dateline', 'Place, month year', $null, $true, $null)
    $field::Rich($tags::Body, 'Body', 'Write the release text.', $null, $true, $null)
    $field::Group($tags::Specifications, 'Technical data (optional)', $false, (ConvertTo-Element @(
          $field::Text($tags::SpecificationsHeading, 'Heading', $null, 'TECHNICAL DATA', $false, $null)
          $field::Table($tags::SpecificationsTable, 'Rows', [string[]]@('Label', 'Value'), $null, 2, $false))))
    $field::Group($tags::FactBox, 'Fact box (optional)', $false, (ConvertTo-Element @(
          $field::Text($tags::FactBoxHeading, 'Heading', 'Fact box heading', $null, $false, $null)
          $field::Rich($tags::FactBoxBody, 'Text', 'Fact box text', $null, $false, $null))))
    $field::Rich($tags::CompanyDescription('example'), 'About Example (optional)', $null,
      (ConvertTo-Block @(New-Paragraph @((New-Text 'About Example:' 'Bold'), (New-Text ' Default wording.')))), $false, $null)
    $field::Rich($tags::Editorial, 'Editorial note (optional)', $null, (ConvertTo-Block @(New-Paragraph @(New-Text 'Free for editorial use.'))), $false, $null)
    $field::Group($tags::Contact, 'Contact', $true, (ConvertTo-Element @(
          $field::Text($tags::ContactHeading, 'Heading', $null, 'Contact:', $true, $null)
          $field::Rich($tags::ContactDetails, 'Details', $null,
            (ConvertTo-Block @(New-Paragraph @((New-Text 'Press Office'), [AdNoctem.Substrate.Documents.LineBreakInline]::Instance, $mail))), $true, $null))))
  )
  $properties = [Collections.Generic.Dictionary[string, string]]::new()
  $properties[$tags::SchemaProperty] = $tags::SchemaId
  $properties[$tags::LanguageProperty] = 'en'
  $properties[$tags::TemplateVersionProperty] = '1.0'
  $template = [AdNoctem.Substrate.Documents.AuthoringTemplate]::new($content, $properties, 'en-GB', $null, $null)
  $stream = [IO.File]::Create($Path)

  try { [AdNoctem.Substrate.Documents.AuthoringTemplateBuilder]::new().Write($template, $stream, $true, [Threading.CancellationToken]::None) }
  finally { $stream.Dispose() }
}

# Removes the author name, the attached-template path and the preview image that Word adds when saving.
function Clear-WordMetadata {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Metadata is a mass noun.')]
  param ([string]$Path)

  $document = [DocumentFormat.OpenXml.Packaging.WordprocessingDocument]::Open($Path, $true)

  try {
    $settings = $document.MainDocumentPart.DocumentSettingsPart

    foreach ($element in @($settings.Settings.ChildElements | Where-Object LocalName -EQ 'attachedTemplate')) { $null = $element.Remove() }

    foreach ($relationship in @($settings.ExternalRelationships)) { $settings.DeleteExternalRelationship($relationship) }

    if ($document.ThumbnailPart) { $null = $document.DeletePart($document.ThumbnailPart) }

    $document.PackageProperties.Creator = $null
    $document.PackageProperties.LastModifiedBy = $null
    $settings.Settings.Save()
  }
  finally { $document.Dispose() }
}

function Get-Control($Document, [string]$Tag) {
  $items = $Document.SelectContentControlsByTag($Tag)

  if ($items.Count -ne 1) { throw "Expected one control $Tag, found $($items.Count)." }

  $items.Item(1)
}

# Equivalent to clicking the control's title tab and pressing Delete.
function Remove-Block {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Deletes a block from the generated fixture document.')]
  param ($Word, $Document, [string]$Tag)

  $control = Get-Control $Document $Tag
  $null = $Document.Range($control.Range.Start - 1, $control.Range.End + 1).Select()
  $null = $Word.Selection.Delete()
}

function Set-Field {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Fills a field in the generated fixture document.')]
  param ($Document, [string]$Tag, [string]$Text)

  (Get-Control $Document $Tag).Range.Text = $Text
}

function Set-Required {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Fills required fields in the generated fixture document.')]
  param ($Document)

  Set-Field $Document $tags::Headline 'Headline'
  Set-Field $Document $tags::Dateline 'Town, May 2026'
  Set-Field $Document $tags::Body 'Body.'
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('substrate word fixtures ' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $work
$template = Join-Path $work 'Synthetic Template.dotx'
New-SyntheticTemplate $template
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
Write-Verbose "Word $($word.Version) build $($word.Build)"

$scenarios = [ordered]@{
  'untouched'        = {}
  'complete'         = {
    param($Document)
    Set-Field $Document $tags::Headline "Word headline $([char]0x2013) $([char]0x201E)Gr$([char]0xF6)$([char]0xDF)e$([char]0x201C) 37$([char]0x2033)"
    Set-Field $Document $tags::Dateline "Sampletown,$([char]0xA0)November 2026"
    $body = Get-Control $Document $tags::Body
    $body.Range.Text = "First paragraph with bold and italic words.`rBullet one`rBullet two`rNumber one`rNumber two`rLevel two`rVisit the example site."
    $range = $body.Range.Duplicate; $null = $range.Find.Execute('bold'); $range.Bold = 1
    $range = $body.Range.Duplicate; $null = $range.Find.Execute('italic'); $range.Italic = 1
    $paragraphs = $body.Range.Paragraphs
    # Built-in style numbers avoid localized names: List Bullet -49, List Number -50, List Number 2 -59.
    foreach ($index in 2, 3) { $paragraphs.Item($index).Style = -49 }
    foreach ($index in 4, 5) { $paragraphs.Item($index).Style = -50 }
    $paragraphs.Item(6).Style = -59
    $range = $body.Range.Duplicate; $null = $range.Find.Execute('example site')
    $null = $Document.Hyperlinks.Add($range, 'https://example.com/de/video?id=1&lang=de')
    $table = (Get-Control $Document $tags::SpecificationsTable).Range.Tables.Item(1)
    $table.Cell(2, 1).Range.Text = 'Size:'; $table.Cell(2, 2).Range.Text = '275/70 R18'
    $table.Cell(3, 1).Range.Text = 'Load:'; $table.Cell(3, 2).Range.Text = '2,000 kg'
    $null = $table.Rows.Add(); $table.Cell(4, 1).Range.Text = 'Speed:'; $table.Cell(4, 2).Range.Text = '180 km/h'
    Set-Field $Document $tags::FactBoxHeading 'WHY IT MATTERS'
    Set-Field $Document $tags::FactBoxBody 'Fact box text.'
  }
  'deleted-optional' = {
    param($Document)
    Set-Required $Document

    foreach ($tag in $tags::Specifications, $tags::FactBox, $tags::CompanyDescription('example'), $tags::Editorial, $tags::Instruction) {
      Remove-Block $word $Document $tag
    }
  }
  'wrapper-removed'  = {
    param($Document)
    Set-Required $Document
    Remove-Block $word $Document $tags::Specifications
    Remove-Block $word $Document $tags::FactBox
    (Get-Control $Document $tags::Editorial).Delete($false)
  }
  'heading-only'     = {
    param($Document)
    Set-Required $Document
    Remove-Block $word $Document $tags::Specifications
    Set-Field $Document $tags::FactBoxHeading 'Heading without text'
  }
}

try {
  foreach ($name in $scenarios.Keys) {
    $document = $word.Documents.Add($template)
    & $scenarios[$name] $document
    $path = Join-Path $PSScriptRoot "$name.docx"
    $document.SaveAs2($path, 16)
    $document.Close(0)
    Clear-WordMetadata $path
    Write-Output "Saved $name.docx"
  }
}
finally {
  $word.Quit(0)
  $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($word)
  Remove-Item -LiteralPath $work -Recurse -Force
}
