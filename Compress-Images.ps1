# Compress-Images.ps1
# Komprimerer bilder for web og bevarer filstruktur

param(
    [int]$JpegQuality = 75,        # JPEG kvalitet (1-100, lavere = mindre fil)
    [int]$MaxWidth = 1920,          # Maks bredde i piksler (0 = ingen resize)
    [int]$MaxHeight = 1080,         # Maks høyde i piksler (0 = ingen resize)
    [switch]$WhatIf                 # Simuler uten å gjøre endringer
)

Add-Type -AssemblyName System.Drawing

# Konfigurasjon - kilde og destinasjonsmapper
$mappings = @(
    @{
        Source = "C:\jamo\ONIT.Images org"
        Destination = "C:\jamo\ONIT.Images"
    },
    @{
        Source = "C:\jamo\ProductPictures org"
        Destination = "C:\jamo\ProductPictures"
    }
)

# Bildeutvidelser som skal behandles
$imageExtensions = @('.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.tif')

function Get-ImageEncoder {
    param([string]$MimeType)
    $codecs = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()
    return $codecs | Where-Object { $_.MimeType -eq $MimeType }
}

function Compress-Image {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [int]$Quality,
        [int]$MaxW,
        [int]$MaxH
    )

    try {
        # Last inn original bilde
        $image = [System.Drawing.Image]::FromFile($SourcePath)

        # Beregn ny størrelse hvis nødvendig
        $newWidth = $image.Width
        $newHeight = $image.Height

        if ($MaxW -gt 0 -and $MaxH -gt 0) {
            if ($image.Width -gt $MaxW -or $image.Height -gt $MaxH) {
                $ratioX = $MaxW / $image.Width
                $ratioY = $MaxH / $image.Height
                $ratio = [Math]::Min($ratioX, $ratioY)

                $newWidth = [int]($image.Width * $ratio)
                $newHeight = [int]($image.Height * $ratio)

                # Säkerställ minst 1 pixel (undvik 0x0)
                if ($newWidth -lt 1) { $newWidth = 1 }
                if ($newHeight -lt 1) { $newHeight = 1 }
            }
        }

        # Opprett destinasjonsmappe hvis den ikke finnes
        $destDir = [System.IO.Path]::GetDirectoryName($DestinationPath)
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        # Opprett nytt bilde med riktig størrelse (använd RGB-format för kompatibilitet med CMYK)
        $bitmap = New-Object System.Drawing.Bitmap($newWidth, $newHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        # Fyll med vit bakgrund (viktigt för CMYK-bilder och transparens)
        $graphics.Clear([System.Drawing.Color]::White)

        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($image, 0, 0, $newWidth, $newHeight)

        # Hent filutvidelse
        $extension = [System.IO.Path]::GetExtension($DestinationPath).ToLower()

        if ($extension -eq '.jpg' -or $extension -eq '.jpeg') {
            # Komprimert JPEG
            $encoder = Get-ImageEncoder -MimeType "image/jpeg"
            $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
            $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
                [System.Drawing.Imaging.Encoder]::Quality, $Quality
            )
            $bitmap.Save($DestinationPath, $encoder, $encoderParams)
        }
        elseif ($extension -eq '.png') {
            # PNG med komprimering
            $bitmap.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        else {
            # Andre formater - konverter til JPEG for bedre komprimering
            $jpegPath = [System.IO.Path]::ChangeExtension($DestinationPath, ".jpg")
            $encoder = Get-ImageEncoder -MimeType "image/jpeg"
            $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
            $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
                [System.Drawing.Imaging.Encoder]::Quality, $Quality
            )
            $bitmap.Save($jpegPath, $encoder, $encoderParams)
            $DestinationPath = $jpegPath
        }

        # Rydd opp
        $graphics.Dispose()
        $bitmap.Dispose()
        $image.Dispose()

        # Hent filstørrelser
        $originalSize = (Get-Item $SourcePath).Length
        $newSize = (Get-Item $DestinationPath).Length
        $savings = [math]::Round((1 - ($newSize / $originalSize)) * 100, 1)

        return @{
            Success = $true
            OriginalSize = $originalSize
            NewSize = $newSize
            Savings = $savings
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

function Format-FileSize {
    param([long]$Size)
    if ($Size -ge 1MB) { return "{0:N2} MB" -f ($Size / 1MB) }
    if ($Size -ge 1KB) { return "{0:N2} KB" -f ($Size / 1KB) }
    return "$Size bytes"
}

# Hovedlogikk
$totalOriginal = 0
$totalNew = 0
$processedCount = 0
$errorCount = 0

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Bildekomprimering for Web" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Innstillinger:" -ForegroundColor Yellow
Write-Host "  JPEG Kvalitet: $JpegQuality%"
Write-Host "  Maks bredde:   $(if($MaxWidth -eq 0){'Ingen grense'}else{"$MaxWidth px"})"
Write-Host "  Maks hoyde:    $(if($MaxHeight -eq 0){'Ingen grense'}else{"$MaxHeight px"})"
Write-Host ""

foreach ($mapping in $mappings) {
    $source = $mapping.Source
    $destination = $mapping.Destination

    Write-Host "Behandler: $source" -ForegroundColor Green
    Write-Host "       ->  $destination" -ForegroundColor Green
    Write-Host ""

    if (-not (Test-Path $source)) {
        Write-Host "  ADVARSEL: Kildemappen finnes ikke: $source" -ForegroundColor Yellow
        continue
    }

    # Finn alle bilder rekursivt
    $images = Get-ChildItem -Path $source -Recurse -File |
        Where-Object { $imageExtensions -contains $_.Extension.ToLower() }

    $imageCount = ($images | Measure-Object).Count
    Write-Host "  Fant $imageCount bilder" -ForegroundColor Cyan

    $i = 0
    foreach ($img in $images) {
        $i++
        $relativePath = $img.FullName.Substring($source.Length)
        $destPath = Join-Path $destination $relativePath

        Write-Progress -Activity "Komprimerer bilder" -Status "$i av $imageCount - $($img.Name)" -PercentComplete (($i / $imageCount) * 100)

        if ($WhatIf) {
            Write-Host "  [WhatIf] Ville komprimert: $($img.Name)" -ForegroundColor Gray
            continue
        }

        $result = Compress-Image -SourcePath $img.FullName -DestinationPath $destPath -Quality $JpegQuality -MaxW $MaxWidth -MaxH $MaxHeight

        if ($result.Success) {
            $totalOriginal += $result.OriginalSize
            $totalNew += $result.NewSize
            $processedCount++

            $origSize = Format-FileSize $result.OriginalSize
            $newSize = Format-FileSize $result.NewSize
            Write-Host "  OK: $($img.Name) - $origSize -> $newSize (-$($result.Savings)%)" -ForegroundColor White
        }
        else {
            $errorCount++
            Write-Host "  FEIL: $($img.Name) - $($result.Error)" -ForegroundColor Red
        }
    }

    Write-Progress -Activity "Komprimerer bilder" -Completed
    Write-Host ""
}

# Oppsummering
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Oppsummering" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Behandlede bilder: $processedCount" -ForegroundColor White
Write-Host "  Feil:              $errorCount" -ForegroundColor $(if($errorCount -gt 0){'Red'}else{'White'})
Write-Host ""

if ($processedCount -gt 0) {
    $totalSavings = [math]::Round((1 - ($totalNew / $totalOriginal)) * 100, 1)
    Write-Host "  Original storrelse:  $(Format-FileSize $totalOriginal)" -ForegroundColor White
    Write-Host "  Ny storrelse:        $(Format-FileSize $totalNew)" -ForegroundColor Green
    Write-Host "  Spart:               $(Format-FileSize ($totalOriginal - $totalNew)) ($totalSavings%)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Ferdig!" -ForegroundColor Cyan
