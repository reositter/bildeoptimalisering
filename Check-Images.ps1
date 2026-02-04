Add-Type -AssemblyName System.Drawing

$files = @(
    "C:\jamo\ProductPictures org\701 7519.jpg",
    "C:\jamo\ProductPictures org\701 1758.jpg",
    "C:\jamo\ProductPictures org\701 3506.jpg"
)

foreach($f in $files) {
    Write-Host "Fil: $f" -ForegroundColor Cyan
    if(Test-Path $f) {
        try {
            $img = [System.Drawing.Image]::FromFile($f)
            Write-Host "  Storlek: $($img.Width) x $($img.Height)"
            Write-Host "  PixelFormat: $($img.PixelFormat)"
            Write-Host "  Filstorlek: $([math]::Round((Get-Item $f).Length / 1KB, 2)) KB"
            $img.Dispose()
        }
        catch {
            Write-Host "  FEL vid inlasning: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "  Hittar inte filen" -ForegroundColor Yellow
    }
    Write-Host ""
}
