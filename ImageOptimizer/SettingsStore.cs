namespace ImageOptimizer;

/// <summary>
/// Var inställningsfilen hamnar. Bredvid exe:n när mappen går att skriva i — då följer
/// inställningarna med när exe:n kopieras till en kunddator. Annars under AppData, eftersom
/// t.ex. Program Files inte är skrivbart för en vanlig användare.
/// </summary>
public static class SettingsStore
{
    public static string ResolvePath(string applicationName)
    {
        var exeFolder = AppContext.BaseDirectory;

        if (IsWritable(exeFolder))
        {
            return Path.Combine(exeFolder, "settings.json");
        }

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            applicationName);

        try
        {
            Directory.CreateDirectory(appDataFolder);
        }
        catch
        {
            // Går inte heller det sparas inga inställningar; läsning och skrivning fångar felet.
        }

        return Path.Combine(appDataFolder, "settings.json");
    }

    private static bool IsWritable(string folder)
    {
        try
        {
            var probePath = Path.Combine(folder, $".write-probe-{Guid.NewGuid():N}");
            using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
