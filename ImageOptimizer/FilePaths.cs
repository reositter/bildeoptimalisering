namespace ImageOptimizer;

/// <summary>Sökvägsregler för spegling av mappstruktur och säker skrivning över en befintlig fil.</summary>
public static class FilePaths
{
    /// <summary>
    /// Säkerhetskopians plats: samma filnamn och samma mappstruktur under backupmappen
    /// som bilden har under originalmappen.
    /// </summary>
    public static string BackupPathFor(string sourceRoot, string backupRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        return Path.Combine(backupRoot, relativePath);
    }

    /// <summary>Ligger <paramref name="path"/> i eller under <paramref name="root"/>?</summary>
    public static bool IsInside(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        if (string.Equals(normalizedRoot, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kopierar originalet till backupmappen. En kopia som redan finns lämnas orörd —
    /// den är originalet från en tidigare körning, och får inte skrivas över av en
    /// redan komprimerad bild.
    /// </summary>
    public static void Backup(string sourcePath, string backupPath)
    {
        if (File.Exists(backupPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(backupPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, backupPath);
    }

    /// <summary>
    /// Skriver via en temporärfil i samma mapp och byter sedan namn, så att ett avbrott
    /// aldrig lämnar en halvskriven bild efter sig.
    /// </summary>
    public static void WriteAtomic(string path, byte[] data)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".optimizing.tmp";

        try
        {
            File.WriteAllBytes(temporaryPath, data);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Temporärfilen får ligga kvar hellre än att dölja det verkliga felet.
                }
            }

            throw;
        }
    }
}
