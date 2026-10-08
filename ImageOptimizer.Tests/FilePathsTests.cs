using ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Tests;

[TestClass]
public class FilePathsTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "imageoptimizer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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

    [TestMethod]
    public void BackupPathFor_KeepsFileNameAndFolderStructure()
    {
        var backupPath = FilePaths.BackupPathFor(
            @"W:\Images",
            @"W:\Images_backup",
            @"W:\Images\912-1022921_files\b_32057_default_1.jpg");

        Assert.AreEqual(
            Path.Combine(@"W:\Images_backup", @"912-1022921_files", "b_32057_default_1.jpg"),
            backupPath);
    }

    [TestMethod]
    public void BackupPathFor_HandlesImageDirectlyInTheRoot()
    {
        var backupPath = FilePaths.BackupPathFor(@"W:\Images", @"W:\Images_backup", @"W:\Images\logo.png");

        Assert.AreEqual(Path.Combine(@"W:\Images_backup", "logo.png"), backupPath);
    }

    [TestMethod]
    public void IsInside_DetectsFoldersUnderTheRoot()
    {
        Assert.IsTrue(FilePaths.IsInside(@"W:\Images", @"W:\Images\under"));
        Assert.IsTrue(FilePaths.IsInside(@"W:\Images", @"W:\Images"));
        Assert.IsTrue(FilePaths.IsInside(@"W:\Images\", @"W:\Images\djupt\ner"));
    }

    [TestMethod]
    public void IsInside_DoesNotMatchSiblingWithSharedPrefix()
    {
        Assert.IsFalse(FilePaths.IsInside(@"W:\Images", @"W:\Images_backup"));
        Assert.IsFalse(FilePaths.IsInside(@"W:\Images", @"W:\Annat"));
    }

    [TestMethod]
    public void Backup_CopiesTheFileIntoTheMirroredFolder()
    {
        var source = Path.Combine(_root, "source", "sub", "bild.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });

        var backupPath = FilePaths.BackupPathFor(Path.Combine(_root, "source"), Path.Combine(_root, "backup"), source);
        FilePaths.Backup(source, backupPath);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(backupPath));
    }

    [TestMethod]
    public void Backup_KeepsTheOriginalFromAnEarlierRun()
    {
        // En andra körning får inte ersätta den äkta originalkopian med en redan komprimerad bild.
        var source = Path.Combine(_root, "bild.jpg");
        var backupPath = Path.Combine(_root, "backup", "bild.jpg");

        File.WriteAllBytes(source, new byte[] { 9, 9, 9 });
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.WriteAllBytes(backupPath, new byte[] { 1, 1, 1 });

        FilePaths.Backup(source, backupPath);

        CollectionAssert.AreEqual(new byte[] { 1, 1, 1 }, File.ReadAllBytes(backupPath));
    }

    [TestMethod]
    public void WriteAtomic_ReplacesTheFileAndLeavesNoTemporaries()
    {
        var path = Path.Combine(_root, "bild.jpg");
        File.WriteAllBytes(path, new byte[] { 1, 1, 1, 1 });

        FilePaths.WriteAtomic(path, new byte[] { 2, 2 });

        CollectionAssert.AreEqual(new byte[] { 2, 2 }, File.ReadAllBytes(path));
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length);
    }

    [TestMethod]
    public void WriteAtomic_CreatesMissingFolders()
    {
        var path = Path.Combine(_root, "ny", "mapp", "bild.jpg");

        FilePaths.WriteAtomic(path, new byte[] { 7 });

        Assert.IsTrue(File.Exists(path));
    }
}
