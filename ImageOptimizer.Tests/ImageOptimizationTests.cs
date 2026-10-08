using System.Drawing;
using System.Drawing.Imaging;
using ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Tests;

[TestClass]
public class ImageOptimizationTests
{
    private const int DefaultMinGain = 10;

    [TestMethod]
    public void Analyze_SkipsJpegAlreadyAtOrBelowTargetQuality()
    {
        var jpeg = TestImages.Jpeg(quality: 55);

        var result = ImageOptimization.Analyze(jpeg, ".jpg", targetQuality: 75, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.AlreadyOptimal, result.Outcome);
        Assert.IsNull(result.Data, "En bild som lämnas orörd får inte leverera nya bytes.");
    }

    [TestMethod]
    public void Analyze_OptimizesJpegSavedWithHigherQualityThanTarget()
    {
        var jpeg = TestImages.Jpeg(quality: 98);

        var result = ImageOptimization.Analyze(jpeg, ".jpg", targetQuality: 50, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.Optimized, result.Outcome);
        Assert.IsNotNull(result.Data);
        Assert.IsTrue(result.NewBytes < result.OriginalBytes,
            $"Den nya filen ({result.NewBytes}) måste vara mindre än originalet ({result.OriginalBytes}).");
    }

    [TestMethod]
    public void Analyze_NeverProducesLargerFile()
    {
        // Hela poängen med verktyget: blir resultatet inte mindre ska originalet behållas.
        foreach (var sourceQuality in new[] { 40, 60, 75, 90, 100 })
        {
            foreach (var targetQuality in new[] { 30, 50, 75, 95 })
            {
                var jpeg = TestImages.Jpeg(sourceQuality);
                var result = ImageOptimization.Analyze(jpeg, ".jpg", targetQuality, DefaultMinGain);

                if (result.Outcome == OptimizationOutcome.Optimized)
                {
                    Assert.IsTrue(result.NewBytes < result.OriginalBytes,
                        $"Källkvalitet {sourceQuality} mot mål {targetQuality} gav en större fil.");
                }
                else
                {
                    Assert.IsNull(result.Data);
                    Assert.AreEqual(result.OriginalBytes, result.NewBytes);
                }
            }
        }
    }

    [TestMethod]
    public void Analyze_SkipsWhenGainIsBelowThreshold()
    {
        var jpeg = TestImages.Jpeg(quality: 98);

        // Kräv 95 % mindre fil — det klarar ingen omkodning, alltså ska bilden lämnas orörd.
        var result = ImageOptimization.Analyze(jpeg, ".jpg", targetQuality: 50, minGainPercent: 95);

        Assert.AreEqual(OptimizationOutcome.NoGain, result.Outcome);
        Assert.IsNull(result.Data);
    }

    [TestMethod]
    public void Analyze_NeverFlattensTransparencyInAPng()
    {
        // System.Drawing kan inte komprimera PNG bättre än originalet, så en PNG ska lämnas
        // orörd. Det viktiga är att den aldrig plattas mot vit botten på vägen.
        var png = TestImages.PngWithTransparency();

        var result = ImageOptimization.Analyze(png, ".png", targetQuality: 75, DefaultMinGain);

        if (result.Outcome != OptimizationOutcome.Optimized)
        {
            Assert.IsNull(result.Data);
            return;
        }

        using var stream = new MemoryStream(result.Data!);
        using var image = new Bitmap(stream);
        Assert.AreEqual(0, image.GetPixel(0, 0).A, "Genomskinligheten fick inte plattas mot vitt.");
    }

    [TestMethod]
    public void Analyze_EstimatesQualityCloselyEnoughToProtectAlreadyCompressedImages()
    {
        // Gränsfallet som avgör allt: en bild sparad på exakt målkvaliteten ska inte kodas om.
        var result = ImageOptimization.Analyze(TestImages.Jpeg(75), ".jpg", targetQuality: 75, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.AlreadyOptimal, result.Outcome);
    }

    [TestMethod]
    public void Analyze_LeavesFormatsItCannotImproveAlone()
    {
        foreach (var extension in new[] { ".gif", ".bmp", ".tif", ".tiff" })
        {
            var result = ImageOptimization.Analyze(TestImages.Jpeg(90), extension, 75, DefaultMinGain);

            Assert.AreEqual(OptimizationOutcome.Unsupported, result.Outcome, $"Gällde {extension}");
            Assert.IsNull(result.Data);
        }
    }

    [TestMethod]
    public void Analyze_ReportsFailureForBrokenFileWithoutThrowing()
    {
        var garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var result = ImageOptimization.Analyze(garbage, ".jpg", 75, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.Failed, result.Outcome);
        Assert.IsNull(result.Data);
    }

    [TestMethod]
    public void Analyze_KeepsImageVisuallyUnchangedApartFromCompression()
    {
        var jpeg = TestImages.Jpeg(quality: 98);
        var result = ImageOptimization.Analyze(jpeg, ".jpg", targetQuality: 50, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.Optimized, result.Outcome);

        using var originalStream = new MemoryStream(jpeg);
        using var original = new Bitmap(originalStream);
        using var optimizedStream = new MemoryStream(result.Data!);
        using var optimized = new Bitmap(optimizedStream);

        Assert.AreEqual(original.Width, optimized.Width);
        Assert.AreEqual(original.Height, optimized.Height);
    }

    [TestMethod]
    public void JpegQuality_MatchesTheQualityTheEncoderUsed()
    {
        foreach (var quality in new[] { 40, 55, 70, 85 })
        {
            var estimate = JpegQuality.Estimate(TestImages.Jpeg(quality));

            Assert.IsNotNull(estimate, $"Kvaliteten gick inte att läsa ut för {quality}.");
            Assert.IsTrue(Math.Abs(estimate.Value - quality) <= 3,
                $"Uppskattade {estimate} för en bild sparad med {quality}.");
        }
    }

    [TestMethod]
    public void JpegQuality_ReturnsNullWhenThereAreNoTables()
    {
        Assert.IsNull(JpegQuality.Estimate(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }));
        Assert.IsNull(JpegQuality.Estimate(Array.Empty<byte>()));
    }
}

internal static class TestImages
{
    /// <summary>Ett motiv med både mjuka ytor och brus, så att komprimeringen beter sig som på riktiga foton.</summary>
    private static Bitmap CreatePhotoLike(int width = 320, int height = 240)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var random = new Random(1234);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = Math.Clamp(x * 255 / width + random.Next(-20, 20), 0, 255);
                var green = Math.Clamp(y * 255 / height + random.Next(-20, 20), 0, 255);
                var blue = Math.Clamp((x + y) * 255 / (width + height) + random.Next(-20, 20), 0, 255);
                bitmap.SetPixel(x, y, Color.FromArgb(255, red, green, blue));
            }
        }

        return bitmap;
    }

    public static byte[] Jpeg(int quality)
    {
        using var bitmap = CreatePhotoLike();
        using var stream = new MemoryStream();

        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        bitmap.Save(stream, encoder, parameters);

        return stream.ToArray();
    }

    public static byte[] PngWithTransparency()
    {
        using var bitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb);

        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                bitmap.SetPixel(x, y, x < 32
                    ? Color.FromArgb(0, 0, 0, 0)
                    : Color.FromArgb(255, 10, 200, 90));
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
