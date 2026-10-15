# Get-PngTools.ps1
# Henter verktoyene Compress-Images.ps1 bruker pa PNG, og legger exe-ene ved siden av skriptet.
# Kjores en gang pa en maskin med nett; deretter kopieres mappen til serveren.
#
#   .\Get-PngTools.ps1
#   .\Get-PngTools.ps1 -IncludePngQuant

[CmdletBinding()]
param(
    # pngquant trengs bare for -LossyPng. Den er GPL-lisensiert, og lastes derfor
    # ned ved behov i stedet for a ligge i repoet.
    [switch]$IncludePngQuant,

    [string]$OxipngVersion = '10.2.1'
)

$ErrorActionPreference = 'Stop'

# Eldre Windows Server slar ikke pa TLS 1.2 av seg selv.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$targetFolder = $PSScriptRoot
$workFolder = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $workFolder -Force | Out-Null

function Install-Tool {
    param([string]$Name, [string]$Url, [string]$ExeName)

    $archive = Join-Path $workFolder "$Name.zip"
    $unpacked = Join-Path $workFolder $Name

    Write-Host "Henter $Name ..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $Url -OutFile $archive -UseBasicParsing
    Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force

    $exe = Get-ChildItem -LiteralPath $unpacked -Recurse -Filter $ExeName | Select-Object -First 1
    if (-not $exe) { throw "Fant ikke $ExeName i nedlastingen av $Name." }

    Copy-Item -LiteralPath $exe.FullName -Destination (Join-Path $targetFolder $ExeName) -Force

    # Lisensen folger med verktoyet.
    Get-ChildItem -LiteralPath $unpacked -Recurse -File |
        Where-Object { $_.Name -match '^(LICENSE|COPYRIGHT|COPYING)' } |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetFolder "$Name-$($_.Name)") -Force }

    Write-Host "  $ExeName lagt i $targetFolder" -ForegroundColor Green
}

try {
    Install-Tool -Name 'oxipng' -ExeName 'oxipng.exe' `
        -Url "https://github.com/oxipng/oxipng/releases/download/v$OxipngVersion/oxipng-$OxipngVersion-x86_64-pc-windows-msvc.zip"

    if ($IncludePngQuant) {
        Install-Tool -Name 'pngquant' -ExeName 'pngquant.exe' `
            -Url 'https://pngquant.org/pngquant-windows.zip'
    }

    Write-Host ""
    Write-Host "Ferdig. Compress-Images.ps1 finner verktoyene av seg selv." -ForegroundColor Cyan
}
finally {
    Remove-Item -LiteralPath $workFolder -Recurse -Force -ErrorAction SilentlyContinue
}
