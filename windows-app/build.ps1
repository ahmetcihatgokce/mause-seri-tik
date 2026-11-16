param(
    [string]$OutDir = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Microsoft .NET Framework C# compiler was not found.'
}

$output = (New-Item -ItemType Directory -Path $OutDir -Force).FullName
$executable = Join-Path $output 'Mause-Seri-Tik.exe'
$manifest = Join-Path $PSScriptRoot 'app.manifest'
$license = Join-Path (Split-Path -Parent $PSScriptRoot) 'LICENSE'

$compilerArguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warnaserror+', '/codepage:65001',
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', "/win32manifest:$manifest",
    "/out:$executable"
)
if (Test-Path -LiteralPath $license) {
    $compilerArguments += "/resource:$license,MauseSeriTik.LICENSE"
}

$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Sort-Object Name
& $compiler @compilerArguments @($sources.FullName)
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed (exit $LASTEXITCODE)."
}
Get-Item -LiteralPath $executable
