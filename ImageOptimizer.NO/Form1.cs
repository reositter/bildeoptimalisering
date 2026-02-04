using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

namespace ImageOptimizer.NO;

public partial class Form1 : Form
{
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly string[] _imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };
    private readonly string _settingsPath;

    public Form1()
    {
        InitializeComponent();

        // Settings file in same folder as exe
        var exeFolder = AppDomain.CurrentDomain.BaseDirectory;
        _settingsPath = Path.Combine(exeFolder, "settings.json");

        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    if (!string.IsNullOrEmpty(settings.LastSourceFolder) && Directory.Exists(settings.LastSourceFolder))
                    {
                        txtSourceFolder.Text = settings.LastSourceFolder;
                    }
                    if (!string.IsNullOrEmpty(settings.LastDestFolder))
                    {
                        txtDestFolder.Text = settings.LastDestFolder;
                    }
                    if (settings.Quality >= 30 && settings.Quality <= 100)
                    {
                        trackQuality.Value = settings.Quality;
                        lblQualityValue.Text = $"{settings.Quality}%";
                    }
                }
            }
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
                Quality = trackQuality.Value
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
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
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

        // Auto-suggest destination folder
        var parent = Path.GetDirectoryName(path);
        var folderName = Path.GetFileName(path);
        if (parent != null)
        {
            // Remove " org" suffix if present, otherwise add "_optimized"
            if (folderName.EndsWith(" org", StringComparison.OrdinalIgnoreCase))
            {
                txtDestFolder.Text = Path.Combine(parent, folderName[..^4]);
            }
            else
            {
                txtDestFolder.Text = Path.Combine(parent, folderName + "_optimalisert");
            }
        }
    }

    private void BtnBrowseSource_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Velg mappe med originalbilder";
        dialog.UseDescriptionForTitle = true;

        // Start from last used folder
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
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Velg malmappe for komprimerte bilder";
        dialog.UseDescriptionForTitle = true;

        // Start from last used folder
        if (!string.IsNullOrEmpty(txtDestFolder.Text))
        {
            var parent = Path.GetDirectoryName(txtDestFolder.Text);
            if (parent != null && Directory.Exists(parent))
            {
                dialog.InitialDirectory = parent;
            }
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtDestFolder.Text = dialog.SelectedPath;
        }
    }

    private void TrackQuality_ValueChanged(object? sender, EventArgs e)
    {
        lblQualityValue.Text = $"{trackQuality.Value}%";
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        // Validate
        if (string.IsNullOrEmpty(txtSourceFolder.Text))
        {
            MessageBox.Show("Velg en originalmappe forst!", "Feil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrEmpty(txtDestFolder.Text))
        {
            MessageBox.Show("Velg en malmappe forst!", "Feil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!Directory.Exists(txtSourceFolder.Text))
        {
            MessageBox.Show("Kildemappen finnes ikke!", "Feil", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (txtSourceFolder.Text.Equals(txtDestFolder.Text, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Kilde- og malmappe kan ikke vaere den samme!", "Feil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Save settings
        SaveSettings();

        // Setup UI for processing
        SetProcessingState(true);
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var result = await ProcessImagesAsync(
                txtSourceFolder.Text,
                txtDestFolder.Text,
                trackQuality.Value,
                _cancellationTokenSource.Token
            );

            if (result.Cancelled)
            {
                lblStatus.Text = "Avbrutt av brukeren.";
                lblStatus.ForeColor = Color.Orange;
            }
            else
            {
                lblStatus.Text = $"Ferdig! {result.ProcessedCount} bilder komprimert. Spart {FormatFileSize(result.SavedBytes)} ({result.SavingsPercent:F1}%)";
                lblStatus.ForeColor = Color.Green;
                btnOpenDest.Enabled = true;

                MessageBox.Show(
                    $"Komprimering ferdig!\n\n" +
                    $"Behandlede bilder: {result.ProcessedCount}\n" +
                    $"Mislyktes: {result.ErrorCount}\n" +
                    $"Original storrelse: {FormatFileSize(result.OriginalBytes)}\n" +
                    $"Ny storrelse: {FormatFileSize(result.NewBytes)}\n" +
                    $"Spart: {FormatFileSize(result.SavedBytes)} ({result.SavingsPercent:F1}%)",
                    "Ferdig!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Feil: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"En feil oppstod:\n{ex.Message}", "Feil", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetProcessingState(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        lblStatus.Text = "Avbryter...";
    }

    private void BtnOpenDest_Click(object? sender, EventArgs e)
    {
        if (Directory.Exists(txtDestFolder.Text))
        {
            Process.Start("explorer.exe", txtDestFolder.Text);
        }
    }

    private void SetProcessingState(bool processing)
    {
        btnStart.Enabled = !processing;
        btnBrowseSource.Enabled = !processing;
        btnBrowseDest.Enabled = !processing;
        trackQuality.Enabled = !processing;
        btnCancel.Enabled = processing;

        if (processing)
        {
            progressBar.Value = 0;
            lblStatus.Text = "Forbereder...";
            lblStatus.ForeColor = Color.Black;
            btnOpenDest.Enabled = false;
        }
    }

    private async Task<ProcessingResult> ProcessImagesAsync(string sourceFolder, string destFolder, int quality, CancellationToken cancellationToken)
    {
        var result = new ProcessingResult();

        // Find all images
        var images = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => _imageExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        if (images.Count == 0)
        {
            throw new Exception("Ingen bilder ble funnet i originalmappen!");
        }

        progressBar.Maximum = images.Count;
        var processed = 0;

        foreach (var sourcePath in images)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Cancelled = true;
                break;
            }

            var relativePath = Path.GetRelativePath(sourceFolder, sourcePath);
            var destPath = Path.Combine(destFolder, relativePath);
            var fileName = Path.GetFileName(sourcePath);

            lblStatus.Text = $"Behandler: {fileName} ({processed + 1}/{images.Count})";

            try
            {
                var (originalSize, newSize) = await Task.Run(() => CompressImage(sourcePath, destPath, quality), cancellationToken);
                result.OriginalBytes += originalSize;
                result.NewBytes += newSize;
                result.ProcessedCount++;
            }
            catch
            {
                result.ErrorCount++;
            }

            processed++;
            progressBar.Value = processed;
            Application.DoEvents();
        }

        return result;
    }

    private (long originalSize, long newSize) CompressImage(string sourcePath, string destPath, int quality)
    {
        // Ensure destination directory exists
        var destDir = Path.GetDirectoryName(destPath);
        if (destDir != null && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        using var image = Image.FromFile(sourcePath);

        var width = image.Width;
        var height = image.Height;

        // Create new bitmap with RGB format (handles CMYK images)
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        // Fill with white background (important for CMYK and transparency)
        graphics.Clear(Color.White);

        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.DrawImage(image, 0, 0, width, height);

        // Get JPEG encoder
        var encoder = GetEncoder(ImageFormat.Jpeg);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

        // Determine output path (convert non-JPEG to JPEG)
        var extension = Path.GetExtension(destPath).ToLower();
        if (extension != ".jpg" && extension != ".jpeg")
        {
            destPath = Path.ChangeExtension(destPath, ".jpg");
        }

        bitmap.Save(destPath, encoder, encoderParams);

        var originalSize = new FileInfo(sourcePath).Length;
        var newSize = new FileInfo(destPath).Length;

        return (originalSize, newSize);
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.First(codec => codec.FormatID == format.Guid);
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

    private class ProcessingResult
    {
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
        public long OriginalBytes { get; set; }
        public long NewBytes { get; set; }
        public bool Cancelled { get; set; }
        public long SavedBytes => OriginalBytes - NewBytes;
        public double SavingsPercent => OriginalBytes > 0 ? (1 - (double)NewBytes / OriginalBytes) * 100 : 0;
    }

    private class AppSettings
    {
        public string? LastSourceFolder { get; set; }
        public string? LastDestFolder { get; set; }
        public int Quality { get; set; } = 75;
    }
}
