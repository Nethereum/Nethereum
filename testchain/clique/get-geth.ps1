# Downloads the pinned geth build used by the integration test fixtures.
# The binary is intentionally not committed (too large); this script is the source of truth
# for the exact version and its checksum. Run it once per machine (or after a version bump).
param(
    [string]$Version = "1.17.4",
    [string]$Commit = "36a7dc72",
    # Azure Blob Content-MD5 (base64) of the official zip; verified against both the
    # blob's stored hash and the downloaded bytes.
    [string]$ExpectedMd5Base64 = "DauvEqCjMnYLecvnR6DjpA=="
)
$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $dir "geth.exe"

if (Test-Path $exe) {
    try {
        $existing = (& $exe version 2>$null | Select-String "^Version: (.+)-stable")
        if ($existing -and $existing.Matches[0].Groups[1].Value -eq $Version) {
            Write-Host "geth $Version already present at $exe"
            exit 0
        }
    }
    catch {
        Write-Host "Existing geth.exe is not usable; re-downloading."
    }
}

$zipName = "geth-windows-amd64-$Version-$Commit.zip"
$url = "https://gethstore.blob.core.windows.net/builds/$zipName"
$tmpZip = Join-Path $env:TEMP $zipName

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $tmpZip

$hex = (Get-FileHash $tmpZip -Algorithm MD5).Hash
$bytes = for ($i = 0; $i -lt $hex.Length; $i += 2) { [Convert]::ToByte($hex.Substring($i, 2), 16) }
$localMd5 = [Convert]::ToBase64String($bytes)
$blobMd5 = (Invoke-WebRequest -Uri $url -Method Head).Headers["Content-MD5"]
if ($localMd5 -ne $ExpectedMd5Base64 -or $localMd5 -ne $blobMd5) {
    throw "Checksum mismatch for $zipName. local=$localMd5 pinned=$ExpectedMd5Base64 blob=$blobMd5"
}

$extractDir = Join-Path $env:TEMP "geth-extract-$Version"
Expand-Archive $tmpZip -DestinationPath $extractDir -Force
$downloaded = Get-ChildItem $extractDir -Recurse -Filter geth.exe | Select-Object -First 1
Copy-Item $downloaded.FullName $exe -Force
Set-Content (Join-Path $dir "version.txt") "v$Version"
Remove-Item $tmpZip -Force
Remove-Item $extractDir -Recurse -Force

Write-Host "geth $Version installed at $exe"
