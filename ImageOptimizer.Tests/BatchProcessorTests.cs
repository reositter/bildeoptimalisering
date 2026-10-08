using ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Tests;

[TestClass]
public class BatchProcessorTests
{
    private string _root = string.Empty;
    private string _source = string.Empty;
    private string _destination = string.Empty;
    private string _backup = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "imageoptimizer-tests", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "Images");
        _destination = Path.Combine(_root, "Images_optimized");
        _backup = Path.Combine(_root, "Images_backup");

        // Samma form som Zpiders bildmapp: bilder i undermappar per artikel.
        Write(@"912-1022921_files\stor.jpg", TestImages.Jpeg(98));
        Write(@"912-1022921_files\liten.jpg", TestImages.Jpeg(40));
        Write(@"938-87123_files\stor.jpg", TestImages.Jpeg(98));
        Write(@"logo.png", TestImages.PngWithTransparency());
        Write(@"ikon.gif", TestImages.Jpeg(90));
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Städningen får inte fälla ett test.
        }
    }

    private void Write(string relativePath, byte[] data)
    {
        var path = Path.Combine(_source, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    private ProcessingOptions Options(bool overwrite, bool backup = false, bool dryRun = false, int quality = 50) =>
        new()
        {
            SourceFolder = _source,
            DestinationFolder = _destination,
            BackupFolder = _backup,
            Overwrite = overwrite,
            Backup = backup,
            DryRun = dryRun,
            Quality = quality,
            MinGainPercent = 10
        };

    [TestMethod]
    public void FindImages_ReachesEverySubfolder()
    {
        var images = BatchProcessor.FindImages(_source);

        Assert.AreEqual(5, images.Count);
        Assert.IsTrue(images.Any(p => p.EndsWith(Path.Combine("938-87123_files", "stor.jpg"))));
    }

    [TestMethod]
    public void Run_Overwrite_ReplacesOnlyTheImagesThatGetSmaller()
    {
        var before = Snapshot();

        var result = BatchProcessor.Run(Options(overwrite: true));

        var after = Snapshot();

        Assert.IsTrue(result.Optimized > 0, "Minst en bild borde ha gått att optimera.");

        foreach (var (path, originalBytes) in before)
        {
            var newBytes = after[path];

            if (newBytes.Length == originalBytes.Length)
            {
                CollectionAssert.AreEqual(originalBytes, newBytes, $"{path} ändrades trots samma storlek.");
            }
            else
            {
                Assert.IsTrue(newBytes.Length < originalBytes.Length,
                    $"{path} blev större: {originalBytes.Length} -> {newBytes.Length}.");
            }
        }
    }

    [TestMethod]
    public void Run_Overwrite_KeepsFileNamesAndFolderStructure()
    {
        var before = Snapshot().Keys.OrderBy(k => k).ToList();

        BatchProcessor.Run(Options(overwrite: true));

        var after = Snapshot().Keys.OrderBy(k => k).ToList();

        CollectionAssert.AreEqual(before, after, "Inga filer får byta namn, plats eller tillkomma.");
    }

    [TestMethod]
    public void Run_Overwrite_BacksUpEveryImageItChangesWithTheSameStructure()
    {
        var before = Snapshot();

        var result = BatchProcessor.Run(Options(overwrite: true, backup: true));

        Assert.IsTrue(result.Optimized > 0);

        var backedUp = Snapshot(_backup);
        Assert.AreEqual(result.Optimized, backedUp.Count,
            "Exakt de ändrade bilderna ska finnas i backupmappen.");

        foreach (var (path, data) in backedUp)
        {
            CollectionAssert.AreEqual(before[path], data,
                $"Säkerhetskopian av {path} är inte originalet.");
        }
    }

    [TestMethod]
    public void Run_Overwrite_SecondRunDoesNotReplaceTheOriginalBackup()
    {
        var before = Snapshot();

        BatchProcessor.Run(Options(overwrite: true, backup: true));
        BatchProcessor.Run(Options(overwrite: true, backup: true, quality: 30));

        foreach (var (path, data) in Snapshot(_backup))
        {
            CollectionAssert.AreEqual(before[path], data,
                $"Andra körningen skrev över säkerhetskopian av {path}.");
        }
    }

    [TestMethod]
    public void Run_DryRun_ChangesNothingOnDisk()
    {
        var before = Snapshot();

        var result = BatchProcessor.Run(Options(overwrite: true, backup: true, dryRun: true));

        var after = Snapshot();
        CollectionAssert.AreEqual(before.Keys.OrderBy(k => k).ToList(), after.Keys.OrderBy(k => k).ToList());

        foreach (var (path, data) in before)
        {
            CollectionAssert.AreEqual(data, after[path], $"{path} ändrades under en testkörning.");
        }

        Assert.IsFalse(Directory.Exists(_backup), "Testkörningen skapade en backupmapp.");
        Assert.IsTrue(result.Optimized > 0, "Testkörningen ska ändå rapportera vad som kan optimeras.");
    }

    [TestMethod]
    public void Run_Copy_ProducesACompleteMirrorIncludingUnchangedImages()
    {
        var before = Snapshot();

        var result = BatchProcessor.Run(Options(overwrite: false));

        var copied = Snapshot(_destination);

        CollectionAssert.AreEqual(
            before.Keys.OrderBy(k => k).ToList(),
            copied.Keys.OrderBy(k => k).ToList(),
            "Destinationen ska innehålla varje bild, med samma namn och mappstruktur.");

        Assert.AreEqual(before.Count, result.Optimized + result.CopiedUnchanged);
    }

    [TestMethod]
    public void Run_Copy_LeavesTheOriginalsUntouched()
    {
        var before = Snapshot();

        BatchProcessor.Run(Options(overwrite: false));

        foreach (var (path, data) in Snapshot())
        {
            CollectionAssert.AreEqual(before[path], data, $"{path} ändrades i originalmappen.");
        }
    }

    [TestMethod]
    public void Run_Copy_NeverWritesABiggerFileThanTheOriginal()
    {
        var before = Snapshot();

        BatchProcessor.Run(Options(overwrite: false));

        foreach (var (path, data) in Snapshot(_destination))
        {
            Assert.IsTrue(data.Length <= before[path].Length,
                $"{path} blev större i destinationen: {before[path].Length} -> {data.Length}.");
        }
    }

    [TestMethod]
    public void Run_CountsEveryImageExactlyOnce()
    {
        var result = BatchProcessor.Run(Options(overwrite: true, dryRun: true));

        Assert.AreEqual(5, result.Optimized + result.LeftUntouched + result.Failed);
    }

    /// <summary>Alla filer under en mapp, nycklade på sökvägen relativt mappen.</summary>
    private Dictionary<string, byte[]> Snapshot(string? folder = null)
    {
        folder ??= _source;

        if (!Directory.Exists(folder))
        {
            return new Dictionary<string, byte[]>();
        }

        return Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(folder, path),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);
    }
}
