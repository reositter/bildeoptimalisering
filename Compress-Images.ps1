# Compress-Images.ps1
# Komprimerer bilder for web, med samme regler som ImageOptimizer.exe:
# bare bilder som faktisk blir mindre uten a bli darligere endres.
#
#   .\Compress-Images.ps1 -Source "W:\Images" -Destination "W:\Images_optimized"
#   .\Compress-Images.ps1 -Source "W:\Images" -Overwrite -BackupFolder "W:\Images_backup"
#   .\Compress-Images.ps1 -Source "W:\Images" -Overwrite -WhatIf

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    # Malmappe. Kreves nar -Overwrite ikke er satt.
    [string]$Destination,

    # Erstatt originalene pa plass i stedet for a skrive til en malmappe.
    [switch]$Overwrite,

    # Mappe for sikkerhetskopier. Speiler filnavn og mappestruktur fra -Source.
    [string]$BackupFolder,

    [ValidateRange(30, 100)]
    [int]$Quality = 75,

    # Hvor mye mindre filen ma bli for at omkodingen skal vaere verdt en generasjon tap.
    [ValidateRange(1, 90)]
    [int]$MinGainPercent = 10,

    # Analyser og rapporter uten a rore en eneste fil.
    [switch]$WhatIf,

    # Skriv en CSV med hver enkelt fil og hva den ville gitt. Virker i alle moduser,
    # men er mest nyttig sammen med -WhatIf.
    [string]$ReportPath
)

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$script:ImageExtensions = @('.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.tif')
$script:SupportedExtensions = @('.jpg', '.jpeg', '.png')

# Standardtabeller fra JPEG-spesifikasjonen (Annex K), i naturlig rekkefolge.
$script:StandardLuminance = @(
    16, 11, 10, 16, 24, 40, 51, 61,
    12, 12, 14, 19, 26, 58, 60, 55,
    14, 13, 16, 24, 40, 57, 69, 56,
    14, 17, 22, 29, 51, 87, 80, 62,
    18, 22, 37, 56, 68, 109, 103, 77,
    24, 35, 55, 64, 81, 104, 113, 92,
    49, 64, 78, 87, 103, 121, 120, 101,
    72, 92, 95, 98, 112, 100, 103, 99
)

$script:StandardChrominance = @(
    17, 18, 24, 47, 99, 99, 99, 99,
    18, 21, 26, 66, 99, 99, 99, 99,
    24, 26, 56, 99, 99, 99, 99, 99,
    47, 66, 99, 99, 99, 99, 99, 99,
    99, 99, 99, 99, 99, 99, 99, 99,
    99, 99, 99, 99, 99, 99, 99, 99,
    99, 99, 99, 99, 99, 99, 99, 99,
    99, 99, 99, 99, 99, 99, 99, 99
)

# Tabellen i filen ligger i siksakrekkefolge; posten pa plass k horer hjemme pa Zigzag[k].
$script:Zigzag = @(
    0, 1, 8, 16, 9, 2, 3, 10,
    17, 24, 32, 25, 18, 11, 4, 5,
    12, 19, 26, 33, 40, 48, 41, 34,
    27, 20, 13, 6, 7, 14, 21, 28,
    35, 42, 49, 56, 57, 50, 43, 36,
    29, 22, 15, 23, 30, 37, 44, 51,
    58, 59, 52, 45, 38, 31, 39, 46,
    53, 60, 61, 54, 47, 55, 62, 63
)

function Get-QuantizationTables {
    param([byte[]]$Data)

    $tables = @()
    if ($Data.Length -lt 4 -or $Data[0] -ne 0xFF -or $Data[1] -ne 0xD8) { return $tables }

    $position = 2
    while ($position + 1 -lt $Data.Length) {
        if ($Data[$position] -ne 0xFF) { $position++; continue }

        $marker = $Data[$position + 1]
        $position += 2

        # Fyllbyte: neste byte er markoren.
        if ($marker -eq 0xFF) { $position--; continue }

        # Markorer uten lengdefelt.
        if ($marker -eq 0xD8 -or $marker -eq 0x01 -or ($marker -ge 0xD0 -and $marker -le 0xD7)) { continue }

        # Slutt pa bildet, eller bildedata tar over.
        if ($marker -eq 0xD9 -or $marker -eq 0xDA) { break }

        if ($position + 1 -ge $Data.Length) { break }
        $length = ($Data[$position] -shl 8) -bor $Data[$position + 1]
        if ($length -lt 2 -or $position + $length -gt $Data.Length) { break }

        if ($marker -eq 0xDB) {
            $end = $position + $length
            $cursor = $position + 2

            while ($cursor -lt $end) {
                $precision = $Data[$cursor] -shr 4
                $id = $Data[$cursor] -band 0x0F
                $cursor++

                $size = if ($precision -eq 0) { 64 } else { 128 }
                if ($cursor + $size -gt $end) { break }

                $table = New-Object int[] 64
                for ($i = 0; $i -lt 64; $i++) {
                    $table[$script:Zigzag[$i]] = if ($precision -eq 0) {
                        $Data[$cursor + $i]
                    } else {
                        ($Data[$cursor + $i * 2] -shl 8) -bor $Data[$cursor + $i * 2 + 1]
                    }
                }

                $tables += , @{ Id = $id; Table = $table }
                $cursor += $size
            }
        }

        $position += $length
    }

    return $tables
}

function Get-JpegQuality {
    <#
        .SYNOPSIS
        Anslar hvilken kvalitet en JPEG allerede er lagret med. $null nar det ikke lar seg lese ut.
    #>
    param([byte[]]$Data)

    $tables = Get-QuantizationTables -Data $Data
    if ($tables.Count -eq 0) { return $null }

    $bestQuality = 0
    $bestError = [double]::MaxValue

    for ($quality = 1; $quality -le 100; $quality++) {
        $scale = if ($quality -lt 50) { [int](5000 / $quality) } else { 200 - $quality * 2 }
        $totalError = 0.0

        foreach ($entry in $tables) {
            $reference = if ($entry.Id -eq 0) { $script:StandardLuminance } else { $script:StandardChrominance }
            $table = $entry.Table

            for ($i = 0; $i -lt 64; $i++) {
                $expected = [Math]::Max(1, [Math]::Min(255, [int](($reference[$i] * $scale + 50) / 100)))
                $difference = $expected - $table[$i]
                $totalError += [double]$difference * $difference
            }
        }

        if ($totalError -lt $bestError) {
            $bestError = $totalError
            $bestQuality = $quality
        }
    }

    # Avvik over dette betyr at tabellene ikke er skalerte standardtabeller.
    $rootMeanSquareError = [Math]::Sqrt($bestError / ($tables.Count * 64))
    if ($rootMeanSquareError -gt 12.0) { return $null }

    return $bestQuality
}

function Get-ImageEncoder {
    param([string]$MimeType)
    return [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq $MimeType }
}

function Get-ExifOrientation {
    param($Image)

    try {
        if ($Image.PropertyIdList -notcontains 0x0112) { return 1 }
        $property = $Image.GetPropertyItem(0x0112)
        if ($null -eq $property -or $property.Value.Length -lt 2) { return 1 }
        return [BitConverter]::ToUInt16($property.Value, 0)
    }
    catch {
        return 1
    }
}

function Invoke-ImageAnalysis {
    <#
        .SYNOPSIS
        Avgjor om bildet kan gjores mindre uten a bli darligere, og koder det om i sa fall.
        Returnerer Outcome, OriginalBytes, NewBytes, Data og SourceQuality.
    #>
    param(
        [byte[]]$Original,
        [string]$Extension,
        [int]$TargetQuality,
        [int]$MinGain
    )

    $result = @{
        Outcome       = 'Failed'
        OriginalBytes = $Original.Length
        NewBytes      = $Original.Length
        Data          = $null
        SourceQuality = $null
        Error         = $null
    }

    $extension = $Extension.ToLowerInvariant()
    if ($script:SupportedExtensions -notcontains $extension) {
        $result.Outcome = 'Unsupported'
        return $result
    }

    $isJpeg = $extension -eq '.jpg' -or $extension -eq '.jpeg'

    $stream = $null; $image = $null; $bitmap = $null; $graphics = $null; $output = $null
    try {
        $stream = New-Object System.IO.MemoryStream (, $Original)
        $image = [System.Drawing.Image]::FromStream($stream, $false, $true)

        # Animerte bilder mister alle rammer unntatt den forste ved omkoding.
        try {
            $frames = $image.GetFrameCount([System.Drawing.Imaging.FrameDimension]::Time)
        }
        catch {
            $frames = 1
        }
        if ($frames -gt 1) { $result.Outcome = 'Animated'; return $result }

        # EXIF-rotasjon folger ikke med, og bildet ville blitt vist dreid.
        if ((Get-ExifOrientation -Image $image) -gt 1) { $result.Outcome = 'Rotated'; return $result }

        if ($isJpeg) {
            $sourceQuality = Get-JpegQuality -Data $Original
            $result.SourceQuality = $sourceQuality
            if ($null -ne $sourceQuality -and $sourceQuality -le $TargetQuality) {
                $result.Outcome = 'AlreadyOptimal'
                return $result
            }
        }

        $bitmap = New-Object System.Drawing.Bitmap $image.Width, $image.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        # Hvit bunn bare for JPEG, som mangler alfakanal. En gjennomsiktig PNG skal forbli gjennomsiktig.
        if ($isJpeg) { $graphics.Clear([System.Drawing.Color]::White) }

        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
        $graphics.Dispose(); $graphics = $null

        $output = New-Object System.IO.MemoryStream

        if ($isJpeg) {
            $encoder = Get-ImageEncoder -MimeType 'image/jpeg'
            $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters 1
            $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter (
                [System.Drawing.Imaging.Encoder]::Quality, [long]$TargetQuality)
            $bitmap.Save($output, $encoder, $encoderParams)
            $encoderParams.Dispose()
        }
        else {
            $bitmap.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
        }

        $encoded = $output.ToArray()

        # Gevinsten ma overstige terskelen, ellers er originalet det bedre valget.
        $limit = $Original.Length * (100.0 - $MinGain) / 100.0
        if ($encoded.Length -ge $limit) {
            $result.Outcome = 'NoGain'
            return $result
        }

        $result.Outcome = 'Optimized'
        $result.NewBytes = $encoded.Length
        $result.Data = $encoded
        return $result
    }
    catch {
        $result.Outcome = 'Failed'
        $result.Error = $_.Exception.Message
        return $result
    }
    finally {
        if ($graphics) { $graphics.Dispose() }
        if ($bitmap) { $bitmap.Dispose() }
        if ($image) { $image.Dispose() }
        if ($stream) { $stream.Dispose() }
        if ($output) { $output.Dispose() }
    }
}

function Get-RelativePath {
    # .NET Framework, som Windows PowerShell 5.1 kjorer pa, har ingen Path.GetRelativePath.
    param([string]$Root, [string]$Path)

    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('')
    $fullPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $fullPath.StartsWith($normalizedRoot + '', [StringComparison]::OrdinalIgnoreCase)) {
        return [System.IO.Path]::GetFileName($fullPath)
    }

    return $fullPath.Substring($normalizedRoot.Length + 1)
}

function Write-FileAtomic {
    <#
        .SYNOPSIS
        Skriver via en midlertidig fil i samme mappe og bytter navn, slik at et avbrudd
        aldri etterlater et halvskrevet bilde.
    #>
    param([string]$Path, [byte[]]$Data)

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $temporaryPath = "$Path.optimizing.tmp"
    try {
        [System.IO.File]::WriteAllBytes($temporaryPath, $Data)

        if (Test-Path -LiteralPath $Path) {
            # Replace bytter innholdet i ett steg; Move uten overskriving finnes ikke her.
            [System.IO.File]::Replace($temporaryPath, $Path, [NullString]::Value)
        }
        else {
            [System.IO.File]::Move($temporaryPath, $Path)
        }
    }
    catch {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}

function Backup-Original {
    <#
        .SYNOPSIS
        Kopierer originalet til backupmappen. En kopi som alt finnes rores ikke - den er
        originalet fra en tidligere kjoring, og skal ikke erstattes av et komprimert bilde.
    #>
    param([string]$SourcePath, [string]$BackupPath)

    if (Test-Path -LiteralPath $BackupPath) { return }

    $directory = Split-Path -Parent $BackupPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Copy-Item -LiteralPath $SourcePath -Destination $BackupPath
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

function Test-IsInside {
    param([string]$Root, [string]$Path)

    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')

    if ($normalizedRoot -eq $normalizedPath) { return $true }
    return $normalizedPath.StartsWith($normalizedRoot + '\', [StringComparison]::OrdinalIgnoreCase)
}

# ---------------------------------------------------------------- validering

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Originalmappen finnes ikke: $Source"
}

$Source = [System.IO.Path]::GetFullPath($Source)

if (-not $Overwrite) {
    if (-not $Destination) {
        throw "Angi -Destination, eller bruk -Overwrite for a erstatte originalene."
    }
    $Destination = [System.IO.Path]::GetFullPath($Destination)
    if (Test-IsInside -Root $Source -Path $Destination) {
        throw "Malmappen kan ikke ligge i originalmappen: $Destination"
    }
}

$useBackup = $false
if ($Overwrite -and $BackupFolder) {
    $BackupFolder = [System.IO.Path]::GetFullPath($BackupFolder)
    if (Test-IsInside -Root $Source -Path $BackupFolder) {
        throw "Backupmappen kan ikke ligge i originalmappen: $BackupFolder"
    }
    $useBackup = $true
}

if ($Overwrite -and -not $useBackup -and -not $WhatIf) {
    Write-Host ""
    Write-Host "ADVARSEL: originalene i $Source skrives over uten sikkerhetskopi." -ForegroundColor Red
    Write-Host "Angi -BackupFolder for a ta vare pa dem. Endringen kan ikke angres." -ForegroundColor Red
    $answer = Read-Host "Skriv JA for a fortsette"
    if ($answer -ne 'JA') {
        Write-Host "Avbrutt." -ForegroundColor Yellow
        return
    }
}

# ---------------------------------------------------------------- kjoring

Write-Host ""
Write-Host "Bildeoptimalisering" -ForegroundColor Cyan
Write-Host "  Kilde:       $Source"
if ($Overwrite) {
    Write-Host "  Modus:       skriver over originalene" -ForegroundColor Yellow
    if ($useBackup) { Write-Host "  Backup:      $BackupFolder" }
} else {
    Write-Host "  Modus:       kopierer til $Destination"
}
Write-Host "  Kvalitet:    $Quality"
Write-Host "  Min gevinst: $MinGainPercent %"
if ($WhatIf) { Write-Host "  TESTKJORING - ingen filer endres" -ForegroundColor Magenta }
Write-Host ""

$images = Get-ChildItem -LiteralPath $Source -Recurse -File |
    Where-Object { $script:ImageExtensions -contains $_.Extension.ToLowerInvariant() }

if ($images.Count -eq 0) {
    Write-Host "Ingen bilder ble funnet i originalmappen." -ForegroundColor Yellow
    return
}

$counts = @{
    Optimized = 0; AlreadyOptimal = 0; NoGain = 0
    Animated = 0; Rotated = 0; Unsupported = 0
    CopiedUnchanged = 0; Failed = 0
}
$originalBytes = 0L
$newBytes = 0L
$firstError = $null
$processed = 0
$report = New-Object System.Collections.Generic.List[object]

foreach ($image in $images) {
    $processed++
    $relativePath = Get-RelativePath -Root $Source -Path $image.FullName

    Write-Progress -Activity "Komprimerer bilder" -Status "$processed/$($images.Count): $relativePath" `
        -PercentComplete ($processed * 100 / $images.Count)

    try {
        $original = [System.IO.File]::ReadAllBytes($image.FullName)
        $analysis = Invoke-ImageAnalysis -Original $original -Extension $image.Extension `
            -TargetQuality $Quality -MinGain $MinGainPercent

        $report.Add([PSCustomObject]@{
            Fil            = Protect-CsvValue $relativePath
            Status         = $analysis.Outcome
            Kvalitet       = $analysis.SourceQuality
            NuvarendeBytes = $analysis.OriginalBytes
            MuligeBytes    = $analysis.NewBytes
            SparteBytes    = $analysis.OriginalBytes - $analysis.NewBytes
            SpartProsent   = if ($analysis.OriginalBytes -gt 0) {
                [math]::Round((1 - $analysis.NewBytes / $analysis.OriginalBytes) * 100, 1)
            } else { 0 }
        })

        if ($analysis.Outcome -eq 'Optimized') {
            if (-not $WhatIf) {
                if ($Overwrite) {
                    # Kopien ma ligge pa plass for originalet rores.
                    if ($useBackup) {
                        Backup-Original -SourcePath $image.FullName `
                            -BackupPath (Join-Path $BackupFolder $relativePath)
                    }
                    Write-FileAtomic -Path $image.FullName -Data $analysis.Data
                }
                else {
                    Write-FileAtomic -Path (Join-Path $Destination $relativePath) -Data $analysis.Data
                }
            }

            $counts.Optimized++
            $originalBytes += $analysis.OriginalBytes
            $newBytes += $analysis.NewBytes

            $savedPercent = (1 - $analysis.NewBytes / $analysis.OriginalBytes) * 100
            Write-Host ("  {0,-55} {1,6:F1} % mindre" -f $relativePath, $savedPercent) -ForegroundColor Green

            continue
        }

        $counts[$analysis.Outcome]++
        if ($analysis.Outcome -eq 'Failed' -and -not $firstError) {
            $firstError = "$relativePath : $($analysis.Error)"
        }

        # Originalet er det beste vi har. Ved kopiering blir det likevel med,
        # slik at malmappen blir en komplett speiling.
        if (-not $Overwrite -and -not $WhatIf) {
            $destinationPath = Join-Path $Destination $relativePath
            $directory = Split-Path -Parent $destinationPath
            if ($directory -and -not (Test-Path -LiteralPath $directory)) {
                New-Item -ItemType Directory -Path $directory -Force | Out-Null
            }
            Copy-Item -LiteralPath $image.FullName -Destination $destinationPath -Force
            $counts.CopiedUnchanged++
        }
    }
    catch {
        $counts.Failed++
        if (-not $firstError) { $firstError = "$relativePath : $($_.Exception.Message)" }
    }
}

if ($ReportPath) {
    $report | Sort-Object SparteBytes -Descending |
        Export-Csv -LiteralPath $ReportPath -NoTypeInformation -Encoding UTF8 -UseCulture
}

Write-Progress -Activity "Komprimerer bilder" -Completed

$saved = $originalBytes - $newBytes
$savedMb = [math]::Round($saved / 1MB, 2)
$savedPercent = if ($originalBytes -gt 0) { (1 - $newBytes / $originalBytes) * 100 } else { 0 }

Write-Host ""
Write-Host "Resultat" -ForegroundColor Cyan
if ($WhatIf) { Write-Host "  Testkjoring - ingen filer er endret." -ForegroundColor Magenta }
Write-Host ("  Optimaliserte bilder: {0}" -f $counts.Optimized)
Write-Host ("  Spart:                {0} MB ({1:F1} % av disse)" -f $savedMb, $savedPercent)
Write-Host ""
Write-Host "  Lot sta urort:"
Write-Host ("    Allerede pa eller under malkvaliteten: {0}" -f $counts.AlreadyOptimal)
Write-Host ("    For liten gevinst (under {0} %):        {1}" -f $MinGainPercent, $counts.NoGain)
if ($counts.Animated -gt 0) { Write-Host ("    Animerte:                              {0}" -f $counts.Animated) }
if ($counts.Rotated -gt 0) { Write-Host ("    EXIF-roterte:                          {0}" -f $counts.Rotated) }
if ($counts.Unsupported -gt 0) { Write-Host ("    Formater som ikke kan komprimeres:     {0}" -f $counts.Unsupported) }

if (-not $Overwrite -and -not $WhatIf) {
    Write-Host ""
    Write-Host ("  Kopiert uendret til malmappen: {0}" -f $counts.CopiedUnchanged)
}

if ($Overwrite -and $useBackup -and -not $WhatIf -and $counts.Optimized -gt 0) {
    Write-Host ""
    Write-Host "  Sikkerhetskopier: $BackupFolder"
}

if ($ReportPath) {
    Write-Host ""
    Write-Host "  Rapport: $ReportPath"
}

if ($counts.Failed -gt 0) {
    Write-Host ""
    Write-Host ("  Mislyktes: {0}" -f $counts.Failed) -ForegroundColor Red
    if ($firstError) { Write-Host "  Forste feil: $firstError" -ForegroundColor Red }
}

Write-Host ""
