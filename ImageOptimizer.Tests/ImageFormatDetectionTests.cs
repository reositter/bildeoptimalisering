using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Tests;

[TestClass]
public class ImageFormatDetectionTests
{
    private const int DefaultMinGain = 10;

    [TestMethod]
    public void Detect_ReadsTheFormatOutOfTheFirstBytes()
    {
        Assert.AreEqual(DetectedFormat.Jpeg, Format(Headers.Jpeg));
        Assert.AreEqual(DetectedFormat.Png, Format(Headers.Png));
        Assert.AreEqual(DetectedFormat.Gif, Format(Headers.Gif));
        Assert.AreEqual(DetectedFormat.Bmp, Format(Headers.Bmp));
        Assert.AreEqual(DetectedFormat.Tiff, Format(Headers.TiffLittleEndian));
        Assert.AreEqual(DetectedFormat.Tiff, Format(Headers.TiffBigEndian));
        Assert.AreEqual(DetectedFormat.WebP, Format(Headers.WebP));
        Assert.AreEqual(DetectedFormat.Avif, Format(Headers.Avif));
        Assert.AreEqual(DetectedFormat.Heic, Format(Headers.Heic));
        Assert.AreEqual(DetectedFormat.Html, Format(Headers.Html));
        Assert.AreEqual(DetectedFormat.Html, Format(Headers.HtmlWithoutDoctype));
        Assert.AreEqual(DetectedFormat.Html, Format(Headers.HtmlComment));
        Assert.AreEqual(DetectedFormat.Xml, Format(Headers.Xml));
        Assert.AreEqual(DetectedFormat.Mhtml, Format(Headers.Mhtml));
    }

    [TestMethod]
    public void Detect_NamesTheBrandForOtherIsoBaseMediaFiles()
    {
        var detected = ImageFormatDetection.Detect(Headers.IsoBaseMedia);

        Assert.AreEqual(DetectedFormat.IsoBaseMedia, detected.Format);
        Assert.AreEqual("ISO-BMFF (qt  )", detected.Name);
    }

    [TestMethod]
    public void Detect_GivesUpOnTooShortAndUnrecognisedFiles()
    {
        Assert.AreEqual(DetectedFormat.Unknown, Format(new byte[] { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0 }));
        Assert.AreEqual(DetectedFormat.Unknown, Format(Headers.Noise));
    }

    [TestMethod]
    public void Detect_RequiresBothTheFirstHeaderAndTheEnvelopeForMhtml()
    {
        // Ett mejlhuvud utan multipart-kuvert är ingen sparad webbsida.
        var header = Encoding.ASCII.GetBytes("Subject: ingen webbsida\r\nContent-Length: 12\r\n\r\n");

        Assert.AreEqual(DetectedFormat.Unknown, Format(header));
    }

    [TestMethod]
    public void Analyze_TellsTheUserWhenTheFileIsNotAnImage()
    {
        var cases = new[] { (Headers.Html, "HTML"), (Headers.Xml, "XML"), (Headers.Mhtml, "MHTML") };

        foreach (var (data, name) in cases)
        {
            var result = ImageOptimization.Analyze(data, ".jpg", 75, DefaultMinGain);

            Assert.AreEqual(OptimizationOutcome.Unsupported, result.Outcome, $"Gällde {name}");
            Assert.IsNull(result.Data);
            StringAssert.Contains(result.Error!, name);
            StringAssert.Contains(result.Error!, "inte en bild");
        }
    }

    [TestMethod]
    public void Analyze_ReportsFormatsGdiPlusCannotRead()
    {
        var cases = new[]
        {
            (Headers.Avif, "AVIF"), (Headers.Heic, "HEIC"), (Headers.WebP, "WebP"),
            (Headers.IsoBaseMedia, "ISO-BMFF")
        };

        foreach (var (data, name) in cases)
        {
            var result = ImageOptimization.Analyze(data, ".jpg", 75, DefaultMinGain);

            Assert.AreEqual(OptimizationOutcome.Unsupported, result.Outcome, $"Gällde {name}");
            Assert.IsNull(result.Data);
            StringAssert.Contains(result.Error!, name);
            StringAssert.Contains(result.Error!, "GDI+");
        }
    }

    [TestMethod]
    public void Analyze_LeavesFramesAndLayersAlone()
    {
        var cases = new[] { (Headers.Gif, "GIF"), (Headers.TiffLittleEndian, "TIFF") };

        foreach (var (data, name) in cases)
        {
            var result = ImageOptimization.Analyze(data, ".jpg", 75, DefaultMinGain);

            Assert.AreEqual(OptimizationOutcome.Unsupported, result.Outcome, $"Gällde {name}");
            StringAssert.Contains(result.Error!, name);
        }
    }

    [TestMethod]
    public void Analyze_StillFailsOnFilesThatAreBrokenRatherThanAnotherFormat()
    {
        // Okänt innehåll är inte samma sak som ett format vi avstår från: en trasig JPEG
        // ska räknas som misslyckad, inte som format utan stöd.
        var result = ImageOptimization.Analyze(Headers.Noise, ".jpg", 75, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.Failed, result.Outcome);
    }

    [TestMethod]
    public void Analyze_LetsTheContentDecideTheEncodingNotTheExtension()
    {
        var result = ImageOptimization.Analyze(TestImages.Jpeg(98), ".png", targetQuality: 50, DefaultMinGain);

        Assert.AreEqual(OptimizationOutcome.Optimized, result.Outcome);
        Assert.IsTrue(result.Data![0] == 0xFF && result.Data[1] == 0xD8,
            "En JPEG som heter .png ska kodas om till JPEG.");
        StringAssert.Contains(result.Error!, "ändelsen säger något annat");
    }

    [TestMethod]
    public void Analyze_KeepsAPngCalledJpgAsPng()
    {
        var result = ImageOptimization.Analyze(TestImages.PngWithTransparency(), ".jpg", 75, DefaultMinGain);

        Assert.AreNotEqual(OptimizationOutcome.Failed, result.Outcome);
        StringAssert.Contains(result.Error!, "PNG");

        if (result.Data != null)
        {
            Assert.IsTrue(result.Data[0] == 0x89 && result.Data[1] == 0x50, "Den skulle ha skrivits som PNG.");
        }
    }

    private static DetectedFormat Format(byte[] data) => ImageFormatDetection.Detect(data).Format;
}

/// <summary>Filhuvuden räcker: detekteringen läser bara de första bytena.</summary>
internal static class Headers
{
    public static byte[] Jpeg => Pad(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
    public static byte[] Png => Pad(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    public static byte[] Gif => Pad(Ascii("GIF89a"));
    public static byte[] Bmp => Pad(Ascii("BM"));
    public static byte[] TiffLittleEndian => Pad(new byte[] { 0x49, 0x49, 0x2A, 0x00 });
    public static byte[] TiffBigEndian => Pad(new byte[] { 0x4D, 0x4D, 0x00, 0x2A });
    public static byte[] WebP => Pad(Concat(Ascii("RIFF"), new byte[] { 0x24, 0x10, 0, 0 }, Ascii("WEBP")));
    public static byte[] Avif => IsoBaseMediaWith("avif");
    public static byte[] Heic => IsoBaseMediaWith("heic");
    public static byte[] IsoBaseMedia => IsoBaseMediaWith("qt  ");

    public static byte[] Html => Ascii("<!DOCTYPE html>\r\n<html><head><title>Fel</title></head></html>");
    public static byte[] HtmlWithoutDoctype => Ascii("\r\n  <html lang=\"sv\"><body>Hittades inte</body></html>");
    public static byte[] HtmlComment => Ascii("<!-- saved from url=(0040)http://example.com/bild.jpg -->\r\n<html>");
    public static byte[] Xml => Ascii("<?xml version=\"1.0\" encoding=\"utf-8\"?><error>404</error>");

    public static byte[] Mhtml => Ascii(
        "From: <Saved by Blink>\r\n" +
        "Snapshot-Content-Location: http://example.com/artikel/123\r\n" +
        "Subject: Produktsida\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: multipart/related; boundary=\"----MultipartBoundary----\"\r\n\r\n" +
        "------MultipartBoundary----\r\n");

    /// <summary>Långt nog för att alla regler ska prövas, och inget format.</summary>
    public static byte[] Noise => new byte[] { 7, 3, 9, 1, 4, 8, 2, 6, 5, 0, 11, 13, 17, 19, 23 };

    private static byte[] IsoBaseMediaWith(string brand) =>
        Pad(Concat(new byte[] { 0, 0, 0, 0x20 }, Ascii("ftyp"), Ascii(brand)));

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static byte[] Pad(byte[] header)
    {
        if (header.Length >= 16)
        {
            return header;
        }

        var padded = new byte[16];
        header.CopyTo(padded, 0);
        return padded;
    }
}
