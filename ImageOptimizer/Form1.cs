using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ImageOptimizer;

public partial class Form1 : Form
{
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly string _settingsPath;
    private string _lastOpenedFolder = string.Empty;

    public Form1()
    {
        InitializeComponent();

        // Settings file in same folder as exe
        var exeFolder = AppDomain.CurrentDomain.BaseDirectory;
        _settingsPath = Path.Combine(exeFolder, "settings.json");

        LoadSettings();
        UpdateMode();
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(settings.LastSourceFolder) && Directory.Exists(settings.LastSourceFolder))
            {
                txtSourceFolder.Text = settings.LastSourceFolder;
            }

            if (!string.IsNullOrEmpty(settings.LastDestFolder))
            {
                txtDestFolder.Text = settings.LastDestFolder;
            }

            if (!string.IsNullOrEmpty(settings.BackupFolder))
            {
                txtBackupFolder.Text = settings.BackupFolder;
            }

            if (settings.Quality >= trackQuality.Minimum && settings.Quality <= trackQuality.Maximum)
            {
                trackQuality.Value = settings.Quality;
                lblQualityValue.Text = $"{settings.Quality}%";
            }

            if (settings.MinGainPercent >= numMinGain.Minimum && settings.MinGainPercent <= numMinGain.Maximum)
            {
                numMinGain.Value = settings.MinGainPercent;
            }

            chkOverwrite.Checked = settings.Overwrite;
            chkBackup.Checked = settings.Backup;
        }
        catch
        {
            // Ignore settings errors
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                LastSourceFolder = txtSourceFolder.Text,
                LastDestFolder = txtDestFolder.Text,
                BackupFolder = txtBackupFolder.Text,
                Quality = trackQuality.Value,
                MinGainPercent = (int)numMinGain.Value,
                Overwrite = chkOverwrite.Checked,
                Backup = chkBackup.Checked
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore settings errors
        }
    }

    private void Form1_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void Form1_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var path = files[0];

            // If it's a file, get its directory
            if (File.Exists(path))
            {
                path = Path.GetDirectoryName(path) ?? path;
            }

            if (Directory.Exists(path))
            {
                SetSourceFolder(path);
            }
        }
    }

    private void SetSourceFolder(string path)
    {
        txtSourceFolder.Text = path;

        var parent = Path.GetDirectoryName(path);
        var folderName = Path.GetFileName(path);
        if (parent == null)
        {
            return;
        }

        // Remove " org" suffix if present, otherwise add "_optimized"
        txtDestFolder.Text = folderName.EndsWith(" org", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(parent, folderName[..^4])
            : Path.Combine(parent, folderName + "_optimized");

        if (string.IsNullOrEmpty(txtBackupFolder.Text))
        {
            txtBackupFolder.Text = Path.Combine(parent, folderName + "_backup");
        }
    }

    private void BtnBrowseSource_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Välj mapp med originalbilder";
        dialog.UseDescriptionForTitle = true;

        if (!string.IsNullOrEmpty(txtSourceFolder.Text) && Directory.Exists(txtSourceFolder.Text))
        {
            dialog.InitialDirectory = txtSourceFolder.Text;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            SetSourceFolder(dialog.SelectedPath);
        }
    }

    private void BtnBrowseDest_Click(object? sender, EventArgs e)
    {
        var selected = BrowseFolder("Välj destinationsmapp för komprimerade bilder", txtDestFolder.Text);
        if (selected != null)
        {
            txtDestFolder.Text = selected;
        }
    }

    private void BtnBrowseBackup_Click(object? sender, EventArgs e)
    {
        var selected = BrowseFolder("Välj mapp för säkerhetskopior", txtBackupFolder.Text);
        if (selected != null)
        {
            txtBackupFolder.Text = selected;
        }
    }

    private static string? BrowseFolder(string description, string current)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = description;
        dialog.UseDescriptionForTitle = true;

        if (!string.IsNullOrEmpty(current))
        {
            var parent = Path.GetDirectoryName(current);
            if (parent != null && Directory.Exists(parent))
            {
                dialog.InitialDirectory = parent;
            }
        }

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private void TrackQuality_ValueChanged(object? sender, EventArgs e)
    {
        lblQualityValue.Text = $"{trackQuality.Value}%";
    }

    private void ModeChanged(object? sender, EventArgs e) => UpdateMode();

    private void UpdateMode()
    {
        var overwrite = chkOverwrite.Checked;

        grpDest.Enabled = !overwrite;
        chkBackup.Enabled = overwrite;
        txtBackupFolder.Enabled = overwrite && chkBackup.Checked;
        btnBrowseBackup.Enabled = overwrite && chkBackup.Checked;

        if (chkDryRun.Checked)
        {
            btnStart.Text = "Analysera (ändrar inget)";
            btnStart.BackColor = Color.FromArgb(96, 96, 96);
        }
        else if (overwrite)
        {
            btnStart.Text = "Skriv över originalen";
            btnStart.BackColor = Color.FromArgb(196, 89, 17);
        }
        else
        {
            btnStart.Text = "Starta komprimering";
            btnStart.BackColor = Color.FromArgb(0, 120, 212);
        }
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        var options = BuildOptions();
        if (options == null)
        {
            return;
        }

        SaveSettings();

        SetProcessingState(true);
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var progress = new Progress<ProgressUpdate>(update =>
            {
                progressBar.Maximum = update.Total;
                progressBar.Value = Math.Min(update.Completed, update.Total);
                lblStatus.Text = $"Bearbetar: {update.FileName} ({update.Completed}/{update.Total})";
            });

            var result = await Task.Run(
                () => BatchProcessor.Run(options, progress, _cancellationTokenSource.Token),
                _cancellationTokenSource.Token);

            if (result.Cancelled)
            {
                lblStatus.Text = "Avbruten av användaren.";
                lblStatus.ForeColor = Color.Orange;
            }
            else
            {
                lblStatus.Text = options.DryRun
                    ? $"Analys klar: {result.Optimized} bilder kan optimeras, {FormatFileSize(result.SavedBytes)} att spara."
                    : $"Klart! {result.Optimized} bilder optimerade. Sparat {FormatFileSize(result.SavedBytes)}.";
                lblStatus.ForeColor = Color.Green;
            }

            _lastOpenedFolder = options.Overwrite ? options.SourceFolder : options.DestinationFolder;
            btnOpenDest.Enabled = Directory.Exists(_lastOpenedFolder);

            MessageBox.Show(BuildSummary(result, options), options.DryRun ? "Analys" : "Resultat",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Avbruten av användaren.";
            lblStatus.ForeColor = Color.Orange;
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Fel uppstod.";
            lblStatus.ForeColor = Color.Red;
            MessageBox.Show(ex.Message, "Fel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetProcessingState(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>Validerar valen och bekräftar en överskrivning. Null betyder att körningen inte ska starta.</summary>
    private ProcessingOptions? BuildOptions()
    {
        var source = txtSourceFolder.Text;

        if (string.IsNullOrEmpty(source))
        {
            Warn("Välj en originalmapp först!");
            return null;
        }

        if (!Directory.Exists(source))
        {
            Warn("Originalmappen finns inte!");
            return null;
        }

        var overwrite = chkOverwrite.Checked;
        var dryRun = chkDryRun.Checked;
        var destination = txtDestFolder.Text;
        var backupFolder = txtBackupFolder.Text;
        var backup = overwrite && chkBackup.Checked;

        if (!overwrite)
        {
            if (string.IsNullOrEmpty(destination))
            {
                Warn("Välj en destinationsmapp först!");
                return null;
            }

            if (FilePaths.IsInside(source, destination))
            {
                Warn("Destinationsmappen får inte ligga i originalmappen.\n\n" +
                     "Välj en mapp vid sidan av, eller kryssa i \"Skriv över originalen\".");
                return null;
            }
        }

        if (backup)
        {
            if (string.IsNullOrEmpty(backupFolder))
            {
                Warn("Välj en mapp för säkerhetskopiorna först!");
                return null;
            }

            if (FilePaths.IsInside(source, backupFolder))
            {
                Warn("Backupmappen får inte ligga i originalmappen.");
                return null;
            }
        }

        if (overwrite && !dryRun)
        {
            var warning = new StringBuilder();
            warning.AppendLine($"Originalbilderna i {source} skrivs över, inklusive alla undermappar.");
            warning.AppendLine();
            warning.AppendLine(backup
                ? $"Säkerhetskopia tas först till:\n{backupFolder}"
                : "INGEN säkerhetskopia tas. Ändringen går inte att ångra.");
            warning.AppendLine();
            warning.Append("Fortsätt?");

            var answer = MessageBox.Show(warning.ToString(), "Bekräfta överskrivning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                return null;
            }
        }

        return new ProcessingOptions
        {
            SourceFolder = source,
            DestinationFolder = destination,
            BackupFolder = backupFolder,
            Overwrite = overwrite,
            Backup = backup,
            DryRun = dryRun,
            Quality = trackQuality.Value,
            MinGainPercent = (int)numMinGain.Value
        };
    }

    private static void Warn(string message) =>
        MessageBox.Show(message, "Fel", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static string BuildSummary(ProcessingResult result, ProcessingOptions options)
    {
        var summary = new StringBuilder();

        if (options.DryRun)
        {
            summary.AppendLine("Testkörning — inga filer har ändrats.");
            summary.AppendLine();
        }

        summary.AppendLine($"Optimerade bilder: {result.Optimized}");
        summary.AppendLine($"Sparat: {FormatFileSize(result.SavedBytes)} ({result.SavingsPercent:F1}% av dessa)");
        summary.AppendLine();
        summary.AppendLine("Lämnade orörda:");
        summary.AppendLine($"  Redan på eller under målkvaliteten: {result.AlreadyOptimal}");
        summary.AppendLine($"  För liten vinst (under {options.MinGainPercent}%): {result.NoGain}");

        if (result.Animated > 0)
        {
            summary.AppendLine($"  Animerade: {result.Animated}");
        }

        if (result.Rotated > 0)
        {
            summary.AppendLine($"  EXIF-roterade: {result.Rotated}");
        }

        if (result.Unsupported > 0)
        {
            summary.AppendLine($"  Format som inte kan komprimeras säkert: {result.Unsupported}");
        }

        if (!options.Overwrite && !options.DryRun)
        {
            summary.AppendLine();
            summary.AppendLine($"Kopierade oförändrade till destinationen: {result.CopiedUnchanged}");
        }

        if (options.Overwrite && options.Backup && !options.DryRun && result.Optimized > 0)
        {
            summary.AppendLine();
            summary.AppendLine($"Säkerhetskopior: {options.BackupFolder}");
        }

        if (result.Failed > 0)
        {
            summary.AppendLine();
            summary.AppendLine($"Misslyckade: {result.Failed}");
            if (result.FirstError != null)
            {
                summary.AppendLine($"Första felet: {result.FirstError}");
            }
        }

        return summary.ToString();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        lblStatus.Text = "Avbryter...";
    }

    private void BtnOpenDest_Click(object? sender, EventArgs e)
    {
        if (Directory.Exists(_lastOpenedFolder))
        {
            Process.Start("explorer.exe", _lastOpenedFolder);
        }
    }

    private void SetProcessingState(bool processing)
    {
        btnStart.Enabled = !processing;
        btnCancel.Enabled = processing;
        grpSource.Enabled = !processing;
        grpDest.Enabled = !processing && !chkOverwrite.Checked;
        grpSettings.Enabled = !processing;
        grpOverwrite.Enabled = !processing;

        if (processing)
        {
            progressBar.Value = 0;
            lblStatus.ForeColor = Color.Black;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} bytes";
    }

    private sealed class AppSettings
    {
        public string? LastSourceFolder { get; set; }
        public string? LastDestFolder { get; set; }
        public string? BackupFolder { get; set; }
        public int Quality { get; set; } = 75;
        public int MinGainPercent { get; set; } = 10;
        public bool Overwrite { get; set; }
        public bool Backup { get; set; } = true;
    }
}
