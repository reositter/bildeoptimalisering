using System.Text;
using System.Text.RegularExpressions;

namespace ImageOptimizer;

/// <summary>Vad filen verkligen innehåller, oavsett vad den heter.</summary>
public enum DetectedFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Bmp,
    Tiff,
    WebP,
    Avif,
    Heic,

    /// <summary>ISO-BMFF med ett märke vi inte känner igen — samma behållare som AVIF och HEIC.</summary>
    IsoBaseMedia,

    Html,
    Xml,
    Mhtml
}

/// <summary>
/// Formatet och dess namn för rapportering. Namnet är tomt för <see cref="DetectedFormat.Unknown"/>,
/// som aldrig ger någon orsakstext.
/// </summary>
public readonly record struct ImageFormatInfo(DetectedFormat Format, string Name);

/// <summary>
/// Läser formatet ur de första bytena. Filändelsen ljuger titt och ofta: en AVIF som heter
/// .jpg får GDI+ att svara "Parameter is not valid", vilket inte säger användaren någonting.
/// Samma regler som Get-ActualFormat i Compress-Images.ps1.
/// </summary>
public static class ImageFormatDetection
{
    private static readonly string[] HeicBrands = { "heic", "heix", "hevc", "mif1", "msf1" };

    private static readonly Regex HtmlStart =
        new(@"^(<!DOCTYPE\s+html|<html|<!--)", RegexOptions.IgnoreCase);

    private static readonly Regex XmlStart = new(@"^<\?xml", RegexOptions.IgnoreCase);

    private static readonly Regex MhtmlFirstHeader =
        new(@"^(From|MIME-Version|Snapshot-Content-Location|Subject|Content-Type):", RegexOptions.IgnoreCase);

    private static readonly Regex MhtmlEnvelope =
        new(@"^(MIME-Version:|Content-Type:\s*multipart/)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static ImageFormatInfo Detect(byte[] data)
    {
        if (data.Length < 12)
        {
            return new ImageFormatInfo(DetectedFormat.Unknown, string.Empty);
        }

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return new ImageFormatInfo(DetectedFormat.Jpeg, "JPEG");
        }

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return new ImageFormatInfo(DetectedFormat.Png, "PNG");
        }

        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
        {
            return new ImageFormatInfo(DetectedFormat.Gif, "GIF");
        }

        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return new ImageFormatInfo(DetectedFormat.Bmp, "BMP");
        }

        if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A) ||
            (data[0] == 0x4D && data[1] == 0x4D && data[3] == 0x2A))
        {
            return new ImageFormatInfo(DetectedFormat.Tiff, "TIFF");
        }

        var text = Encoding.ASCII.GetString(data, 0, 12);

        if (Matches(text, 0, "RIFF") && Matches(text, 8, "WEBP"))
        {
            return new ImageFormatInfo(DetectedFormat.WebP, "WebP");
        }

        // ISO-BMFF: ftyp-boxen med märket direkt efter.
        if (Matches(text, 4, "ftyp"))
        {
            var brand = text.Substring(8, 4);

            if (brand.StartsWith("avi", StringComparison.OrdinalIgnoreCase))
            {
                return new ImageFormatInfo(DetectedFormat.Avif, "AVIF");
            }

            if (HeicBrands.Contains(brand, StringComparer.OrdinalIgnoreCase))
            {
                return new ImageFormatInfo(DetectedFormat.Heic, "HEIC");
            }

            return new ImageFormatInfo(DetectedFormat.IsoBaseMedia, $"ISO-BMFF ({brand})");
        }

        // Somliga filer är inte bilder alls: en nedladdning som gav en felsida eller en
        // produktsida i stället för bilden, sparad med bildens filnamn.
        var head = Text(data, 256).TrimStart(' ', '\t', '\r', '\n');

        if (HtmlStart.IsMatch(head))
        {
            return new ImageFormatInfo(DetectedFormat.Html, "HTML");
        }

        if (XmlStart.IsMatch(head))
        {
            return new ImageFormatInfo(DetectedFormat.Xml, "XML");
        }

        // En hel webbsida sparad som en fil: Chrome och IE skriver MIME-huvuden först, och
        // bilderna ligger base64-kodade inuti.
        var envelope = Text(data, 2048);

        if (MhtmlFirstHeader.IsMatch(envelope) && MhtmlEnvelope.IsMatch(envelope))
        {
            return new ImageFormatInfo(DetectedFormat.Mhtml, "MHTML");
        }

        return new ImageFormatInfo(DetectedFormat.Unknown, string.Empty);
    }

    private static bool Matches(string text, int offset, string marker) =>
        string.Equals(text.Substring(offset, marker.Length), marker, StringComparison.OrdinalIgnoreCase);

    private static string Text(byte[] data, int length) =>
        Encoding.ASCII.GetString(data, 0, Math.Min(length, data.Length));
}
