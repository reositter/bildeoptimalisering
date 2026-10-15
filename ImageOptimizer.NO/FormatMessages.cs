namespace ImageOptimizer;

/// <summary>Norske arsakstekster. Den svenske utgaven har sin egen kopi i ImageOptimizer.</summary>
internal static class FormatMessages
{
    public static string NotAnImage(string format) => $"filen er {format}, ikke et bilde";

    public static string NotReadByGdiPlus(string format) => $"innholdet er {format}, som GDI+ ikke leser";

    public static string ContentIs(string format) => $"innholdet er {format}";

    public static string ExtensionDisagrees(string format) => $"innholdet er {format}, endelsen sier noe annet";
}
