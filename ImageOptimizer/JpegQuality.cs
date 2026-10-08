namespace ImageOptimizer;

/// <summary>
/// Uppskattar vilken kvalitet en JPEG redan är sparad med, genom att läsa dess
/// kvantiseringstabeller (DQT) och jämföra med standardtabellerna skalade per kvalitetsnivå.
/// Behövs för att slippa koda om en bild som redan ligger på eller under målkvaliteten —
/// en sådan omkodning ger bara ännu en generation av förluster.
/// </summary>
internal static class JpegQuality
{
    // Standardtabeller ur JPEG-specen (Annex K), i naturlig ordning.
    private static readonly int[] StandardLuminance =
    {
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77,
        24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103, 99
    };

    private static readonly int[] StandardChrominance =
    {
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
    };

    // Tabellen i filen ligger i sicksackordning; posten på plats k hör hemma på Zigzag[k].
    private static readonly int[] Zigzag =
    {
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    };

    // Avvikelse per koefficient (RMS) över detta betyder att tabellerna inte är
    // skalade standardtabeller — t.ex. Photoshops egna. Då går kvaliteten inte att läsa ut.
    private const double MaxRootMeanSquareError = 12.0;

    /// <summary>Uppskattad kvalitet 1-100, eller null när den inte går att avgöra.</summary>
    public static int? Estimate(byte[] jpeg)
    {
        var tables = ReadQuantizationTables(jpeg);
        if (tables.Count == 0)
        {
            return null;
        }

        var bestQuality = 0;
        var bestError = double.MaxValue;

        for (var quality = 1; quality <= 100; quality++)
        {
            var error = 0.0;

            foreach (var (id, table) in tables)
            {
                var reference = id == 0 ? StandardLuminance : StandardChrominance;
                for (var i = 0; i < 64; i++)
                {
                    var difference = ScaleCoefficient(reference[i], quality) - table[i];
                    error += (double)difference * difference;
                }
            }

            if (error < bestError)
            {
                bestError = error;
                bestQuality = quality;
            }
        }

        var meanSquaredError = bestError / (tables.Count * 64);
        if (Math.Sqrt(meanSquaredError) > MaxRootMeanSquareError)
        {
            return null;
        }

        return bestQuality;
    }

    /// <summary>Samma skalning som libjpeg (och därmed GDI+) gör av standardtabellerna.</summary>
    private static int ScaleCoefficient(int baseValue, int quality)
    {
        var scale = quality < 50 ? 5000 / quality : 200 - quality * 2;
        return Math.Clamp((baseValue * scale + 50) / 100, 1, 255);
    }

    private static List<(int Id, int[] Table)> ReadQuantizationTables(byte[] data)
    {
        var tables = new List<(int, int[])>();

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return tables;
        }

        var position = 2;

        while (position + 1 < data.Length)
        {
            if (data[position] != 0xFF)
            {
                position++;
                continue;
            }

            var marker = data[position + 1];
            position += 2;

            // Fyllnadsbyte: nästa byte är markören.
            if (marker == 0xFF)
            {
                position--;
                continue;
            }

            // Markörer utan längdfält.
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                continue;
            }

            // Slut på bilden, eller bilddata tar vid — inga fler tabeller att hämta.
            if (marker == 0xD9 || marker == 0xDA)
            {
                break;
            }

            if (position + 1 >= data.Length)
            {
                break;
            }

            var length = (data[position] << 8) | data[position + 1];
            if (length < 2 || position + length > data.Length)
            {
                break;
            }

            if (marker == 0xDB)
            {
                ReadSegment(data, position + 2, position + length, tables);
            }

            position += length;
        }

        return tables;
    }

    private static void ReadSegment(byte[] data, int start, int end, List<(int, int[])> tables)
    {
        var cursor = start;

        while (cursor < end)
        {
            var precision = data[cursor] >> 4;
            var id = data[cursor] & 0x0F;
            cursor++;

            var size = precision == 0 ? 64 : 128;
            if (cursor + size > end)
            {
                return;
            }

            var table = new int[64];
            for (var i = 0; i < 64; i++)
            {
                table[Zigzag[i]] = precision == 0
                    ? data[cursor + i]
                    : (data[cursor + i * 2] << 8) | data[cursor + i * 2 + 1];
            }

            tables.Add((id, table));
            cursor += size;
        }
    }
}
