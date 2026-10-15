# Find-FakeImages.ps1
# Finner filer med bildeendelse som ikke er bilder - nedlastede feilsider, lagrede
# websider og halve filer - og filer der innholdet er et annet format enn endelsen sier.
# Rorer ingen fil. Skriver en CSV og en ren liste over kandidater for sletting.
#
#   .\Find-FakeImages.ps1 -Source "C:\zpiderdata\Images"
#   .\Find-FakeImages.ps1 -Source "W:\Images" -ReportPath "C:\temp\falske.csv" -DeleteListPath "C:\temp\slett.txt"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    # Ta bare filene som ligger rett i kildemappen. Undermapper hoppes over.
    [switch]$NoRecurse,

    [string]$ReportPath,

    # Ren tekstfil med en full sti per linje, bare filene som ikke er bilder.
    # Ikke-bilder er det eneste som havner her; feil endelse er ikke sletteverdig.
    [string]$DeleteListPath
)

$ErrorActionPreference = 'Stop'

# Endelsene vi ser pa. .webp og .avif er med fordi de er gyldige bilder i dag, og
# ikke skal rapporteres som feil nar de faktisk heter det de er.
$script:ImageExtensions = @(
    '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tiff', '.tif', '.webp', '.avif', '.heic'
)

# Hva endelsen lover. Brukes til a se om innholdet er noe annet.
$script:ExtensionFormat = @{
    '.jpg' = 'JPEG'; '.jpeg' = 'JPEG'; '.png' = 'PNG'; '.gif' = 'GIF'
    '.bmp' = 'BMP'; '.tiff' = 'TIFF'; '.tif' = 'TIFF'
    '.webp' = 'WebP'; '.avif' = 'AVIF'; '.heic' = 'HEIC'
}

# Samme regler som Get-ActualFormat i Compress-Images.ps1. Kopien star her med vilje:
# hvert skript skal kunne kopieres alene til en kundemaskin.
function Get-ActualFormat {
    param([byte[]]$Data)

    if ($Data.Length -lt 12) { return 'ukjent' }

    if ($Data[0] -eq 0xFF -and $Data[1] -eq 0xD8 -and $Data[2] -eq 0xFF) { return 'JPEG' }
    if ($Data[0] -eq 0x89 -and $Data[1] -eq 0x50 -and $Data[2] -eq 0x4E -and $Data[3] -eq 0x47) { return 'PNG' }
    if ($Data[0] -eq 0x47 -and $Data[1] -eq 0x49 -and $Data[2] -eq 0x46) { return 'GIF' }
    if ($Data[0] -eq 0x42 -and $Data[1] -eq 0x4D) { return 'BMP' }
    if (($Data[0] -eq 0x49 -and $Data[1] -eq 0x49 -and $Data[2] -eq 0x2A) -or
        ($Data[0] -eq 0x4D -and $Data[1] -eq 0x4D -and $Data[3] -eq 0x2A)) { return 'TIFF' }

    $text = [System.Text.Encoding]::ASCII.GetString($Data, 0, 12)
    if ($text.Substring(0, 4) -eq 'RIFF' -and $text.Substring(8, 4) -eq 'WEBP') { return 'WebP' }

    if ($text.Substring(4, 4) -eq 'ftyp') {
        $brand = $text.Substring(8, 4)
        if ($brand -like 'avi*') { return 'AVIF' }
        if ($brand -in @('heic', 'heix', 'hevc', 'mif1', 'msf1')) { return 'HEIC' }
        return "ISO-BMFF ($brand)"
    }

    $head = [System.Text.Encoding]::ASCII.GetString($Data, 0, [Math]::Min(256, $Data.Length))
    $head = $head.TrimStart([char]0xEF, [char]0xBB, [char]0xBF, ' ', [char]9, [char]13, [char]10)
    if ($head -match '^<!DOCTYPE\s+html' -or $head -match '^<html' -or $head -match '^<!--') { return 'HTML' }
    if ($head -match '^<\?xml') { return 'XML' }

    $mime = [System.Text.Encoding]::ASCII.GetString($Data, 0, [Math]::Min(2048, $Data.Length))
    if ($mime -match '^(From|MIME-Version|Snapshot-Content-Location|Subject|Content-Type):' -and
        $mime -match '(?im)^(MIME-Version:|Content-Type:\s*multipart/)') { return 'MHTML' }

    return 'ukjent'
}

function Get-SourceUrl {
    <#
        .SYNOPSIS
        Henter adressen ut av en lagret webside. Da vet man hvilken side som ble lagret
        i stedet for bildet, og hvilket produkt som mangler et bilde.
    #>
    param([byte[]]$Data)

    $text = [System.Text.Encoding]::ASCII.GetString($Data, 0, [Math]::Min(4096, $Data.Length))

    if ($text -match '(?im)^Snapshot-Content-Location:\s*(\S+)') { return $Matches[1] }
    if ($text -match '(?im)^Content-Location:\s*(https?://\S+)') { return $Matches[1] }

    # En tittel kan ga over flere linjer. Alt pa en linje, ellers blir CSV-en uleselig.
    if ($text -match '(?is)<title[^>]*>(.*?)</title>') {
        $title = ($Matches[1] -replace '\s+', ' ').Trim()
        if ($title.Length -gt 120) { $title = $title.Substring(0, 120) }
        return $title
    }

    return ''
}

function Test-Truncated {
    <#
        .SYNOPSIS
        Ser etter sluttmarkoren. Et avbrutt nedlasting gir en fil som apner seg halvveis
        eller ikke i det hele tatt. Vi leter i de siste bytene, for en del filer har
        harmlost sogl etter markoren.
    #>
    param([string]$Path, [string]$Format, [long]$Length)

    if ($Format -notin @('JPEG', 'PNG')) { return $false }
    if ($Length -lt 32) { return $true }

    $tailSize = [int][Math]::Min(64, $Length)
    $tail = New-Object byte[] $tailSize

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        [void]$stream.Seek(-$tailSize, [System.IO.SeekOrigin]::End)
        [void]$stream.Read($tail, 0, $tailSize)
    }
    finally {
        $stream.Dispose()
    }

    if ($Format -eq 'JPEG') {
        for ($i = 0; $i -lt $tail.Length - 1; $i++) {
            if ($tail[$i] -eq 0xFF -and $tail[$i + 1] -eq 0xD9) { return $false }
        }
        return $true
    }

    $text = [System.Text.Encoding]::ASCII.GetString($tail)
    return ($text -notmatch 'IEND')
}

function Protect-CsvValue {
    <#
        .SYNOPSIS
        Et filnavn som begynner med = + - @ blir tolket som en formel nar CSV-en apnes
        i Excel. En ledende apostrof gjor at verdien vises som ren tekst.
    #>
    param([string]$Value)

    if ($Value -match "^[=+\-@`t`r]") { return "'" + $Value }
    return $Value
}

# ---------------------------------------------------------------- kjoring

if (-not (Test-Path -LiteralPath $Source)) { throw "Fant ikke kildemappen: $Source" }
$Source = (Resolve-Path -LiteralPath $Source).Path

Write-Host ""
Write-Host "Leter etter filer som ikke er bilder" -ForegroundColor Cyan
Write-Host "  Kilde: $Source"
Write-Host ""

$searchParameters = @{ LiteralPath = $Source; File = $true }
if (-not $NoRecurse) { $searchParameters.Recurse = $true }

$files = Get-ChildItem @searchParameters |
    Where-Object { $script:ImageExtensions -contains $_.Extension.ToLowerInvariant() }

if ($files.Count -eq 0) {
    Write-Host "Ingen filer med bildeendelse i mappen." -ForegroundColor Yellow
    return
}

$report = New-Object System.Collections.Generic.List[object]
$notImages = New-Object System.Collections.Generic.List[string]
$counts = @{ NotImage = 0; WrongExtension = 0; Truncated = 0; Ok = 0 }
$processed = 0

foreach ($file in $files) {
    $processed++
    if ($processed % 200 -eq 0) {
        Write-Progress -Activity "Leser filhoder" -Status "$processed av $($files.Count)" `
            -PercentComplete ($processed * 100.0 / $files.Count)
    }

    $extension = $file.Extension.ToLowerInvariant()
    $expected = $script:ExtensionFormat[$extension]

    # Bare starten av filen trengs. Pa en mappe med titusen bilder er det forskjellen
    # pa noen sekunder og en kaffepause.
    $headSize = [int][Math]::Min(4096, $file.Length)
    $head = New-Object byte[] $headSize
    if ($headSize -gt 0) {
        $stream = [System.IO.File]::OpenRead($file.FullName)
        try { [void]$stream.Read($head, 0, $headSize) } finally { $stream.Dispose() }
    }

    $actual = Get-ActualFormat -Data $head
    $url = ''
    $verdict = 'OK'

    if ($file.Length -eq 0) {
        $verdict = 'Ikke bilde'
        $actual = 'tom fil'
    }
    elseif ($actual -in @('HTML', 'XML', 'MHTML')) {
        $verdict = 'Ikke bilde'
        $url = Get-SourceUrl -Data $head
    }
    elseif ($actual -eq 'ukjent') {
        $verdict = 'Ikke bilde'
    }
    elseif (Test-Truncated -Path $file.FullName -Format $actual -Length $file.Length) {
        $verdict = 'Avkuttet'
    }
    elseif ($expected -and $actual -ne $expected) {
        $verdict = 'Feil endelse'
    }

    switch ($verdict) {
        'Ikke bilde'   { $counts.NotImage++;       [void]$notImages.Add($file.FullName) }
        'Avkuttet'     { $counts.Truncated++ }
        'Feil endelse' { $counts.WrongExtension++ }
        default        { $counts.Ok++ }
    }

    if ($verdict -ne 'OK') {
        $relativePath = $file.FullName.Substring($Source.Length).TrimStart('\')
        Write-Host ("  {0,-13} {1,-45} {2}" -f $verdict, $relativePath, $actual) -ForegroundColor Yellow

        $report.Add([pscustomobject]@{
            Fil       = Protect-CsvValue $relativePath
            Vurdering = $verdict
            Innhold   = $actual
            Endelse   = $extension
            Bytes     = $file.Length
            Endret    = $file.LastWriteTime.ToString('yyyy-MM-dd')
            Kilde     = Protect-CsvValue $url
        })
    }
}

Write-Progress -Activity "Leser filhoder" -Completed

Write-Host ""
Write-Host "Resultat" -ForegroundColor Cyan
Write-Host ("  Gjennomgatt:    {0}" -f $files.Count)
Write-Host ("  I orden:        {0}" -f $counts.Ok)
Write-Host ("  Ikke bilder:    {0}" -f $counts.NotImage) -ForegroundColor $(if ($counts.NotImage) { 'Red' } else { 'Gray' })
Write-Host ("  Avkuttede:      {0}" -f $counts.Truncated) -ForegroundColor $(if ($counts.Truncated) { 'Red' } else { 'Gray' })
Write-Host ("  Feil endelse:   {0}" -f $counts.WrongExtension)

if ($counts.WrongExtension -gt 0) {
    Write-Host ""
    Write-Host "  Feil endelse betyr bare at navnet lyver - filen er et bilde og vises" -ForegroundColor Gray
    Write-Host "  i nettleseren. Den skal ikke slettes." -ForegroundColor Gray
}

if ($ReportPath) {
    $directory = Split-Path -Parent $ReportPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $report | Export-Csv -LiteralPath $ReportPath -NoTypeInformation -Encoding UTF8 -UseCulture
    Write-Host ""
    Write-Host "  Rapport: $ReportPath"
}

if ($DeleteListPath) {
    $directory = Split-Path -Parent $DeleteListPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    Set-Content -LiteralPath $DeleteListPath -Value $notImages -Encoding UTF8
    Write-Host "  Slettelist: $DeleteListPath ($($notImages.Count) filer)"
    Write-Host ""
    Write-Host "  Slett dem senere med (ta bort -WhatIf nar lista er gatt gjennom):" -ForegroundColor Gray
    Write-Host "    Get-Content '$DeleteListPath' | ForEach-Object { Remove-Item -LiteralPath `$_ -WhatIf }" -ForegroundColor Gray
}

Write-Host ""
