namespace ImageOptimizer;

public sealed class ProcessingOptions
{
    public required string SourceFolder { get; init; }

    /// <summary>Används bara när <see cref="Overwrite"/> är false.</summary>
    public string DestinationFolder { get; init; } = string.Empty;

    public string BackupFolder { get; init; } = string.Empty;

    /// <summary>Ersätt originalen på plats i stället för att skriva till en destinationsmapp.</summary>
    public bool Overwrite { get; init; }

    public bool Backup { get; init; }

    /// <summary>Analysera och rapportera, men rör ingen fil.</summary>
    public bool DryRun { get; init; }

    public int Quality { get; init; } = 75;
    public int MinGainPercent { get; init; } = 10;
}

public sealed class ProcessingResult
{
    public int Optimized { get; set; }
    public int AlreadyOptimal { get; set; }
    public int NoGain { get; set; }
    public int Animated { get; set; }
    public int Rotated { get; set; }
    public int Unsupported { get; set; }
    public int CopiedUnchanged { get; set; }
    public int Failed { get; set; }
    public int TotalImages { get; set; }
    public string? FirstError { get; set; }
    public bool Cancelled { get; set; }

    /// <summary>Summerar enbart de bilder som faktiskt kodades om.</summary>
    public long OriginalBytes { get; set; }

    public long NewBytes { get; set; }

    public int LeftUntouched => AlreadyOptimal + NoGain + Animated + Rotated + Unsupported;
    public long SavedBytes => OriginalBytes - NewBytes;
    public double SavingsPercent => OriginalBytes > 0 ? (1 - (double)NewBytes / OriginalBytes) * 100 : 0;
}

public sealed record ProgressUpdate(string FileName, int Completed, int Total);

/// <summary>
/// Går igenom en mapp med undermappar och optimerar de bilder som faktiskt blir bättre av det.
/// Bilder som inte kan förbättras lämnas orörda vid överskrivning, och kopieras oförändrade
/// vid kopiering, så att destinationen blir en komplett spegling.
/// </summary>
public static class BatchProcessor
{
    public static List<string> FindImages(string folder) =>
        Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => ImageOptimization.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static ProcessingResult Run(
        ProcessingOptions options,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default) =>
        Run(options, FindImages(options.SourceFolder), progress, cancellationToken);

    public static ProcessingResult Run(
        ProcessingOptions options,
        IReadOnlyList<string> images,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessingResult { TotalImages = images.Count };
        var completed = 0;

        foreach (var sourcePath in images)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Cancelled = true;
                break;
            }

            completed++;
            progress?.Report(new ProgressUpdate(Path.GetFileName(sourcePath), completed, images.Count));

            try
            {
                ProcessImage(sourcePath, options, result);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.FirstError ??= $"{Path.GetFileName(sourcePath)}: {ex.Message}";
            }
        }

        return result;
    }

    private static void ProcessImage(string sourcePath, ProcessingOptions options, ProcessingResult result)
    {
        var original = File.ReadAllBytes(sourcePath);
        var analysis = ImageOptimization.Analyze(
            original, Path.GetExtension(sourcePath), options.Quality, options.MinGainPercent);

        if (analysis.Outcome == OptimizationOutcome.Optimized && analysis.Data != null)
        {
            if (options.DryRun)
            {
                CountOptimized(analysis, result);
                return;
            }

            if (options.Overwrite)
            {
                // Säkerhetskopian måste ligga på plats innan originalet rörs.
                if (options.Backup)
                {
                    FilePaths.Backup(sourcePath,
                        FilePaths.BackupPathFor(options.SourceFolder, options.BackupFolder, sourcePath));
                }

                FilePaths.WriteAtomic(sourcePath, analysis.Data);
            }
            else
            {
                FilePaths.WriteAtomic(DestinationPathFor(sourcePath, options), analysis.Data);
            }

            // Räknas först när skrivningen är gjord, annars räknas en fil som både
            // optimerad och misslyckad om skrivningen fallerar.
            CountOptimized(analysis, result);
            return;
        }

        Count(analysis, sourcePath, result);

        // Originalet är det bästa vi har. Vid kopiering följer det ändå med.
        if (!options.Overwrite && !options.DryRun)
        {
            var destinationPath = DestinationPathFor(sourcePath, options);
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            result.CopiedUnchanged++;
        }
    }

    private static void CountOptimized(OptimizationResult analysis, ProcessingResult result)
    {
        result.Optimized++;
        result.OriginalBytes += analysis.OriginalBytes;
        result.NewBytes += analysis.NewBytes;
    }

    private static void Count(OptimizationResult analysis, string sourcePath, ProcessingResult result)
    {
        switch (analysis.Outcome)
        {
            case OptimizationOutcome.AlreadyOptimal:
                result.AlreadyOptimal++;
                break;
            case OptimizationOutcome.NoGain:
                result.NoGain++;
                break;
            case OptimizationOutcome.Animated:
                result.Animated++;
                break;
            case OptimizationOutcome.Rotated:
                result.Rotated++;
                break;
            case OptimizationOutcome.Unsupported:
                result.Unsupported++;
                break;
            case OptimizationOutcome.Failed:
                result.Failed++;
                result.FirstError ??= $"{Path.GetFileName(sourcePath)}: {analysis.Error}";
                break;
        }
    }

    private static string DestinationPathFor(string sourcePath, ProcessingOptions options)
    {
        var relativePath = Path.GetRelativePath(options.SourceFolder, sourcePath);
        return Path.Combine(options.DestinationFolder, relativePath);
    }
}
