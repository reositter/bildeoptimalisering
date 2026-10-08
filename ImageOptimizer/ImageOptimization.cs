using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageOptimizer;

public enum OptimizationOutcome
{
    /// <summary>Omkodningen gav en mindre fil utan att något gick förlorat.</summary>
    Optimized,

    /// <summary>JPEG:en ligger redan på eller under målkvaliteten.</summary>
    AlreadyOptimal,

    /// <summary>Omkodningen gav ingen vinst, eller för liten för att vara värd förlusten.</summary>
    NoGain,

    /// <summary>Animerad bild — en omkodning skulle kasta alla bildrutor utom den första.</summary>
    Animated,

    /// <summary>EXIF-rotation som omkodningen inte bär med sig; bilden skulle visas vriden.</summary>
    Rotated,

    /// <summary>Format som inte går att komprimera bättre utan att riskera kvaliteten.</summary>
    Unsupported,

    Failed
}

public sealed class OptimizationResult
{
    public required OptimizationOutcome Outcome { get; init; }
    public long OriginalBytes { get; init; }
    public long NewBytes { get; init; }

    /// <summary>Den omkodade filen. Satt enbart när <see cref="Outcome"/> är Optimized.</summary>
    public byte[]? Data { get; init; }

    public int? SourceQuality { get; init; }
    public string? Error { get; init; }

    public long SavedBytes => Outcome == OptimizationOutcome.Optimized ? OriginalBytes - NewBytes : 0;
}

/// <summary>
/// Avgör om en bild går att göra mindre utan att bli sämre, och kodar om den i så fall.
/// Formatet och filnamnet ändras aldrig — en bild som refereras som .png förblir .png.
/// </summary>
public static class ImageOptimization
{
    private const int ExifOrientationId = 0x0112;

    /// <summary>Format vi kan koda om utan att riskera synlig försämring.</summary>
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };

    /// <summary>Alla format vi letar upp; de som inte stöds lämnas orörda respektive kopieras oförändrade.</summary>
    public static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };

    public static bool IsSupported(string extension) =>
        SupportedExtensions.Contains(extension.ToLowerInvariant());

    /// <param name="minGainPercent">
    /// Hur mycket mindre filen måste bli för att omkodningen ska vara värd en generation av förluster.
    /// </param>
    public static OptimizationResult Analyze(byte[] original, string extension, int targetQuality, int minGainPercent)
    {
        extension = extension.ToLowerInvariant();

        if (!IsSupported(extension))
        {
            return Skip(OptimizationOutcome.Unsupported, original.Length);
        }

        var isJpeg = extension is ".jpg" or ".jpeg";

        try
        {
            using var input = new MemoryStream(original, writable: false);
            using var image = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: true);

            if (GetFrameCount(image) > 1)
            {
                return Skip(OptimizationOutcome.Animated, original.Length);
            }

            if (GetExifOrientation(image) > 1)
            {
                return Skip(OptimizationOutcome.Rotated, original.Length);
            }

            int? sourceQuality = null;
            if (isJpeg)
            {
                sourceQuality = JpegQuality.Estimate(original);
                if (sourceQuality.HasValue && sourceQuality.Value <= targetQuality)
                {
                    return Skip(OptimizationOutcome.AlreadyOptimal, original.Length, sourceQuality);
                }
            }

            var encoded = Encode(image, isJpeg, targetQuality);

            // Vinsten måste överstiga tröskeln, annars är originalet det bättre valet.
            var limit = original.Length * (100.0 - minGainPercent) / 100.0;
            if (encoded.Length >= limit)
            {
                return Skip(OptimizationOutcome.NoGain, original.Length, sourceQuality);
            }

            return new OptimizationResult
            {
                Outcome = OptimizationOutcome.Optimized,
                OriginalBytes = original.Length,
                NewBytes = encoded.Length,
                Data = encoded,
                SourceQuality = sourceQuality
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                Outcome = OptimizationOutcome.Failed,
                OriginalBytes = original.Length,
                NewBytes = original.Length,
                Error = ex.Message
            };
        }
    }

    private static OptimizationResult Skip(OptimizationOutcome outcome, long size, int? sourceQuality = null) =>
        new()
        {
            Outcome = outcome,
            OriginalBytes = size,
            NewBytes = size,
            SourceQuality = sourceQuality
        };

    private static byte[] Encode(Image image, bool asJpeg, int quality)
    {
        using var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            // Vit botten bara för JPEG, som saknar alfakanal — annars skulle en genomskinlig
            // PNG plattas mot vitt och bli en annan bild.
            if (asJpeg)
            {
                graphics.Clear(Color.White);
            }

            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        }

        using var output = new MemoryStream();

        if (asJpeg)
        {
            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            bitmap.Save(output, encoder, parameters);
        }
        else
        {
            bitmap.Save(output, ImageFormat.Png);
        }

        return output.ToArray();
    }

    private static int GetFrameCount(Image image)
    {
        try
        {
            return image.GetFrameCount(FrameDimension.Time);
        }
        catch
        {
            // Formatet saknar tidsdimension, alltså en enda bildruta.
            return 1;
        }
    }

    private static int GetExifOrientation(Image image)
    {
        try
        {
            if (!image.PropertyIdList.Contains(ExifOrientationId))
            {
                return 1;
            }

            var property = image.GetPropertyItem(ExifOrientationId);
            if (property?.Value == null || property.Value.Length < 2)
            {
                return 1;
            }

            return BitConverter.ToUInt16(property.Value, 0);
        }
        catch
        {
            return 1;
        }
    }
}
