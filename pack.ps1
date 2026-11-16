param([string]$OutDir = (Join-Path $PSScriptRoot 'dist'))

$ErrorActionPreference = 'Stop'
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$chromeDir = Join-Path $PSScriptRoot 'chrome-extension'
$manifest = Get-Content (Join-Path $chromeDir 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.manifest_version -ne 3 -or $manifest.version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Expected a Manifest V3 extension with a three-part version.'
}

$chromeZip = Join-Path $OutDir 'Mause-Seri-Tik-Chrome.zip'
$chromeEntries = @('manifest.json', 'popup.html', 'popup.css', 'popup.js', 'settings.js', 'aim.js', 'content.js', 'icons')
$chromePaths = @($chromeEntries | ForEach-Object { Join-Path $chromeDir $_ })
$chromePaths += Join-Path $PSScriptRoot 'LICENSE'
Compress-Archive -LiteralPath $chromePaths -DestinationPath $chromeZip -Force

& (Join-Path $PSScriptRoot 'windows-app/build.ps1') -OutDir $OutDir
$windowsExe = Join-Path $OutDir 'Mause-Seri-Tik.exe'
if (-not (Test-Path -LiteralPath $windowsExe -PathType Leaf)) {
    throw 'The Windows build did not produce Mause-Seri-Tik.exe.'
}

$hashes = foreach ($file in @($chromeZip, $windowsExe)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
$hashes | Set-Content -LiteralPath (Join-Path $OutDir 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Mause Seri Tik $($manifest.version) packages: $OutDir"
