namespace ImageOptimizer;

/// <summary>
/// Orsakstexter om vad filen innehåller. Skärmtexter är egna per utgåva — norska utgåvan
/// har sin egen kopia i ImageOptimizer.NO.
/// </summary>
internal static class FormatMessages
{
    public static string NotAnImage(string format) => $"filen är {format}, inte en bild";

    public static string NotReadByGdiPlus(string format) => $"innehållet är {format}, som GDI+ inte läser";

    public static string ContentIs(string format) => $"innehållet är {format}";

    public static string ExtensionDisagrees(string format) => $"innehållet är {format}, ändelsen säger något annat";
}
