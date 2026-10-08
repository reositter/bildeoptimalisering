# Fix-Failed-Images.ps1
# Kör bara de 3 bilder som misslyckades (CMYK-bilder)

param(
    [int]$JpegQuality = 75
)

Add-Type -AssemblyName System.Drawing

$failedImages = @(
    @{ Source = "C:\jamo\ProductPictures org\701 7519.jpg"; Dest = "C:\jamo\ProductPictures\701 7519.jpg" },
    @{ Source = "C:\jamo\ProductPictures org\701 1758.jpg"; Dest = "C:\jamo\ProductPictures\701 1758.jpg" },
    @{ Source = "C:\jamo\ProductPictures org\701 3506.jpg"; Dest = "C:\jamo\ProductPictures\701 3506.jpg" }
)

function Get-ImageEncoder {
    param([string]$MimeType)
    $codecs = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()
    return $codecs | Where-Object { $_.MimeType -eq $MimeType }
}

function Format-FileSize {
    param([long]$Size)
    if ($Size -ge 1MB) { return "{0:N2} MB" -f ($Size / 1MB) }
    if ($Size -ge 1KB) { return "{0:N2} KB" -f ($Size / 1KB) }
    return "$Size bytes"
}

Write-Host "Fixar 3 CMYK-bilder som misslyckades..." -ForegroundColor Cyan
Write-Host ""

foreach ($item in $failedImages) {
    $sourcePath = $item.Source
    $destPath = $item.Dest
    $fileName = [System.IO.Path]::GetFileName($sourcePath)

    Write-Host "Bearbetar: $fileName" -ForegroundColor Yellow

    try {
        # Ladda original
        $image = [System.Drawing.Image]::FromFile($sourcePath)
        Write-Host "  Original: $($image.Width) x $($image.Height), PixelFormat: $($image.PixelFormat)"

        # Behåll originalstorlek för dessa bilder (ingen resize)
        $newWidth = $image.Width
        $newHeight = $image.Height

        Write-Host "  Ny storlek: $newWidth x $newHeight"

        # Skapa destinationsmapp
        $destDir = [System.IO.Path]::GetDirectoryName($destPath)
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        # Skapa ny bitmap med explicit RGB-format (löser CMYK-problemet)
        $bitmap = New-Object System.Drawing.Bitmap($newWidth, $newHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        # Fyll med vit bakgrund
        $graphics.Clear([System.Drawing.Color]::White)

        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($image, 0, 0, $newWidth, $newHeight)

        # Spara som JPEG
        $encoder = Get-ImageEncoder -MimeType "image/jpeg"
        $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
        $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
            [System.Drawing.Imaging.Encoder]::Quality, $JpegQuality
        )
        $bitmap.Save($destPath, $encoder, $encoderParams)

        # Städa upp
        $graphics.Dispose()
        $bitmap.Dispose()
        $image.Dispose()

        # Visa resultat
        $originalSize = (Get-Item $sourcePath).Length
        $newSize = (Get-Item $destPath).Length
        $savings = [math]::Round((1 - ($newSize / $originalSize)) * 100, 1)

        Write-Host "  OK! $(Format-FileSize $originalSize) -> $(Format-FileSize $newSize) (-$savings%)" -ForegroundColor Green
    }
    catch {
        Write-Host "  FEL: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
}

Write-Host "Klart!" -ForegroundColor Cyan
