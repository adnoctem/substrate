#Requires -Version 7.0
# Data validation and bounded downloads shared by scheduled checks and offline tests.
function Assert-MicrosoftDownloadUri {
  param ([Parameter(Mandatory = $true)][uri]$Uri)

  if (-not $Uri.IsAbsoluteUri -or $Uri.Scheme -cne 'https' -or -not $Uri.IsDefaultPort -or $Uri.UserInfo -or $Uri.Fragment -or $Uri.Host -ne 'download.microsoft.com') {
    throw 'Vendor candidates must use HTTPS on download.microsoft.com without credentials, a custom port, or a fragment.'
  }
}

function Assert-VendorArchivePath {
  param ([Parameter(Mandatory = $true)][string]$Path)

  if ($Path -match '[\\:\x00-\x1f]' -or $Path.StartsWith('/') -or @($Path.TrimEnd('/') -split '/' | Where-Object { $_ -in @('', '.', '..') }).Count) {
    throw "Unsafe vendor archive entry: $Path"
  }
}

function Save-MicrosoftDownload {
  param ([uri]$Uri, [string]$Destination, [long]$MaximumBytes = 67108864)

  Assert-MicrosoftDownloadUri $Uri
  $handler = [Net.Http.HttpClientHandler]::new()
  $handler.AllowAutoRedirect = $false
  $client = [Net.Http.HttpClient]::new($handler)
  $stopping = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(3))

  try {
    for ($redirect = 0; $redirect -le 5; $redirect++) {
      Assert-MicrosoftDownloadUri $Uri
      $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $Uri)
      $response = $null

      try {
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $stopping.Token).GetAwaiter().GetResult()
        $status = [int]$response.StatusCode

        if ($status -in @(301, 302, 303, 307, 308)) {
          if (-not $response.Headers.Location) { throw 'Redirect without a location.' }

          $Uri = [uri]::new($Uri, $response.Headers.Location)
          continue
        }

        $null = $response.EnsureSuccessStatusCode()

        if ($response.Content.Headers.ContentLength -gt $MaximumBytes) { throw 'Vendor download exceeds its size limit.' }

        $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $output = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)

        try {
          $buffer = [byte[]]::new(81920)
          $total = 0L

          while (($count = $inputStream.ReadAsync($buffer, 0, $buffer.Length, $stopping.Token).GetAwaiter().GetResult()) -gt 0) {
            $total += $count

            if ($total -gt $MaximumBytes) { throw 'Vendor download exceeds its size limit.' }

            $output.Write($buffer, 0, $count)
          }
        }
        finally { $output.Dispose(); $inputStream.Dispose() }

        return
      }
      finally { if ($response) { $response.Dispose() }; $request.Dispose() }
    }

    throw 'Too many vendor download redirects.'
  }
  finally { $stopping.Dispose(); $client.Dispose() }
}

function Get-MicrosoftSignatureEvidence {
  param ([Parameter(Mandatory = $true)][string]$Path)

  $signature = Get-AuthenticodeSignature -LiteralPath $Path

  if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)' -or -not $signature.TimeStamperCertificate) {
    throw 'Vendor candidate must have a valid, timestamped Microsoft Authenticode signature.'
  }

  $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
  [PSCustomObject]@{
    Sha256           = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    Version          = [version]::new($info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart, $info.FilePrivatePart)
    Signer           = $signature.SignerCertificate.Subject
    SignerThumbprint = $signature.SignerCertificate.Thumbprint
    TimestampSigner  = $signature.TimeStamperCertificate.Subject
    SignatureStatus  = [string]$signature.Status
    OriginalFilename = $info.OriginalFilename
  }
}
