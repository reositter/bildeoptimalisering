# Slett-IkkeBilder.ps1
# Fjerner filene under ProductPictures som ikke er bilder:
#   23 tomme filer (0 byte)
#    6 lagrede websider med bildeendelse (5 HTML, 1 MHTML)
#
# Listene er hentet ut av productpictures.csv 2026-10-15. Hver fil kontrolleres pa nytt
# for den slettes, sa en fil som er byttet ut siden rapporten blir staende.
#
#   .\Slett-IkkeBilder.ps1           viser hva som ville blitt slettet
#   .\Slett-IkkeBilder.ps1 -Delete   sletter

[CmdletBinding()]
param(
    [string]$Root = 'C:\ZpiderWebs\1_F0026\ProductPictures',
    [switch]$Delete
)

$ErrorActionPreference = 'Stop'

# Tomme filer. Kontroll for sletting: lengden skal fortsatt vaere 0.
$emptyFiles = @(
    '956-78248-930.jpg'
    '956-78345.jpg'
    '956-79264.jpg'
    '953-1111550-600-l.jpg'
    '950-32707.jpg'
    '968-20136-2.jpg'
    '71208-1-s.jpg'
    '01_oilye50mygo.jpg'
    '01_duk5a0b3r1e.jpg'
    '01_tiiy2kmrmav.jpg'
    '01_ymponqk33sm.jpg'
    '01_2xbv0trb2it.jpg'
    '01_1utn0c3nitw.jpg'
    '01_cnrrgjiruva.jpg'
    '01_drhclrbwv2v.jpg'
    '20134.jpg'
    '20135.jpg'
    '938-8855-49.jpg'
    '940-45800-125-80-42.jpg'
    '938-442513.jpg'
    '938-442514.jpg'
    '938-40256.jpg'
    '938-1230517.jpg'
)

# Lagrede websider. Kontroll for sletting: innholdet skal ikke vaere et bilde.
$webPages = @(
    'Scansafe __ Jobmaster JM 106Synlighet.jpg'
    '71221-m.jpg'
    '71678.jpg'
    '71018bS.jpg'
    '25615-4.jpg'
    '948-1902645.jpg'
)

function Test-IsImage {
    <#
        .SYNOPSIS
        Ser etter en kjent bildesignatur i starten av filen. Treffer den, star filen igjen.
    #>
    param([string]$Path)

    $head = New-Object byte[] 12
    $stream = [System.IO.File]::OpenRead($Path)
    try { $read = $stream.Read($head, 0, 12) } finally { $stream.Dispose() }
    if ($read -lt 12) { return $false }

    if ($head[0] -eq 0xFF -and $head[1] -eq 0xD8 -and $head[2] -eq 0xFF) { return $true }
    if ($head[0] -eq 0x89 -and $head[1] -eq 0x50 -and $head[2] -eq 0x4E -and $head[3] -eq 0x47) { return $true }
    if ($head[0] -eq 0x47 -and $head[1] -eq 0x49 -and $head[2] -eq 0x46) { return $true }
    if ($head[0] -eq 0x42 -and $head[1] -eq 0x4D) { return $true }
    if (($head[0] -eq 0x49 -and $head[1] -eq 0x49 -and $head[2] -eq 0x2A) -or
        ($head[0] -eq 0x4D -and $head[1] -eq 0x4D -and $head[3] -eq 0x2A)) { return $true }

    $text = [System.Text.Encoding]::ASCII.GetString($head, 0, 12)
    if ($text.Substring(0, 4) -eq 'RIFF' -and $text.Substring(8, 4) -eq 'WEBP') { return $true }
    if ($text.Substring(4, 4) -eq 'ftyp') { return $true }

    return $false
}

if (-not (Test-Path -LiteralPath $Root)) { throw "Fant ikke mappen: $Root" }

$toDelete = New-Object System.Collections.Generic.List[string]
$skipped = 0
$missing = 0

Write-Host ""
Write-Host "Tomme filer" -ForegroundColor Cyan
foreach ($name in $emptyFiles) {
    $path = Join-Path $Root $name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host ("  mangler   {0}" -f $name) -ForegroundColor DarkGray
        $missing++
        continue
    }

    $length = (Get-Item -LiteralPath $path).Length
    if ($length -ne 0) {
        Write-Host ("  HOPPER    {0} - har {1} byte, ikke tom lenger" -f $name, $length) -ForegroundColor Yellow
        $skipped++
        continue
    }

    Write-Host ("  slettes   {0}" -f $name)
    $toDelete.Add($path)
}

Write-Host ""
Write-Host "Lagrede websider" -ForegroundColor Cyan
foreach ($name in $webPages) {
    $path = Join-Path $Root $name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host ("  mangler   {0}" -f $name) -ForegroundColor DarkGray
        $missing++
        continue
    }

    if (Test-IsImage -Path $path) {
        Write-Host ("  HOPPER    {0} - er et bilde na" -f $name) -ForegroundColor Yellow
        $skipped++
        continue
    }

    $size = [math]::Round((Get-Item -LiteralPath $path).Length / 1KB)
    Write-Host ("  slettes   {0} ({1} KB)" -f $name, $size)
    $toDelete.Add($path)
}

Write-Host ""
Write-Host ("  Til sletting: {0}" -f $toDelete.Count)
if ($skipped -gt 0) { Write-Host ("  Hoppet over: {0}" -f $skipped) -ForegroundColor Yellow }
if ($missing -gt 0) { Write-Host ("  Fantes ikke: {0}" -f $missing) -ForegroundColor DarkGray }

if (-not $Delete) {
    Write-Host ""
    Write-Host "  Ingen fil er rort. Kjor med -Delete for a slette." -ForegroundColor Magenta
    Write-Host ""
    return
}

$removed = 0
$failed = 0
foreach ($path in $toDelete) {
    try {
        Remove-Item -LiteralPath $path -Force
        $removed++
    }
    catch {
        Write-Host ("  FEIL  {0} : {1}" -f $path, $_.Exception.Message) -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host ("  Slettet: {0}" -f $removed) -ForegroundColor Green
if ($failed -gt 0) { Write-Host ("  Mislyktes: {0}" -f $failed) -ForegroundColor Red }
Write-Host ""
