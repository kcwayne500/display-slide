$ErrorActionPreference = 'Stop'

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The Windows .NET Framework C# compiler was not found.'
}

$source = Join-Path $PSScriptRoot 'src'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$application = Join-Path $dist 'Display Slide.exe'
$manifest = Join-Path $source 'DisplaySlide.manifest'
$icon = Join-Path $source 'DisplaySlide.ico'
$sources = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)

& $compiler /nologo /warnaserror /optimize+ /target:winexe "/out:$application" "/win32manifest:$manifest" "/win32icon:$icon" @sources
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Output "Built $application"
