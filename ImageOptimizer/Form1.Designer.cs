namespace ImageOptimizer;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // Form settings
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(600, 520);
        Text = "Bildoptimerare";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        AllowDrop = true;
        DragEnter += Form1_DragEnter;
        DragDrop += Form1_DragDrop;

        // Header label
        lblHeader = new Label();
        lblHeader.Text = "Bildoptimerare for Webben";
        lblHeader.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblHeader.Location = new Point(20, 15);
        lblHeader.Size = new Size(560, 35);
        lblHeader.ForeColor = Color.FromArgb(0, 120, 212);
        Controls.Add(lblHeader);

        // Drag drop hint
        lblDragHint = new Label();
        lblDragHint.Text = "Tips: Dra och slapp en mapp hit for att valja den";
        lblDragHint.Location = new Point(20, 50);
        lblDragHint.Size = new Size(560, 20);
        lblDragHint.ForeColor = Color.Gray;
        lblDragHint.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
        Controls.Add(lblDragHint);

        // Source folder group
        grpSource = new GroupBox();
        grpSource.Text = "Originalmapp (bilder att komprimera)";
        grpSource.Location = new Point(20, 75);
        grpSource.Size = new Size(560, 70);
        grpSource.AllowDrop = true;
        grpSource.DragEnter += Form1_DragEnter;
        grpSource.DragDrop += Form1_DragDrop;
        Controls.Add(grpSource);

        txtSourceFolder = new TextBox();
        txtSourceFolder.Location = new Point(15, 28);
        txtSourceFolder.Size = new Size(440, 23);
        txtSourceFolder.ReadOnly = true;
        txtSourceFolder.AllowDrop = true;
        txtSourceFolder.DragEnter += Form1_DragEnter;
        txtSourceFolder.DragDrop += Form1_DragDrop;
        grpSource.Controls.Add(txtSourceFolder);

        btnBrowseSource = new Button();
        btnBrowseSource.Text = "Bläddra...";
        btnBrowseSource.Location = new Point(460, 26);
        btnBrowseSource.Size = new Size(85, 27);
        btnBrowseSource.Click += BtnBrowseSource_Click;
        grpSource.Controls.Add(btnBrowseSource);

        // Destination folder group
        grpDest = new GroupBox();
        grpDest.Text = "Destinationsmapp (komprimerade bilder)";
        grpDest.Location = new Point(20, 155);
        grpDest.Size = new Size(560, 70);
        Controls.Add(grpDest);

        txtDestFolder = new TextBox();
        txtDestFolder.Location = new Point(15, 28);
        txtDestFolder.Size = new Size(440, 23);
        txtDestFolder.ReadOnly = true;
        grpDest.Controls.Add(txtDestFolder);

        btnBrowseDest = new Button();
        btnBrowseDest.Text = "Bläddra...";
        btnBrowseDest.Location = new Point(460, 26);
        btnBrowseDest.Size = new Size(85, 27);
        btnBrowseDest.Click += BtnBrowseDest_Click;
        grpDest.Controls.Add(btnBrowseDest);

        // Settings group
        grpSettings = new GroupBox();
        grpSettings.Text = "Installningar";
        grpSettings.Location = new Point(20, 235);
        grpSettings.Size = new Size(560, 80);
        Controls.Add(grpSettings);

        lblQuality = new Label();
        lblQuality.Text = "JPEG-kvalitet:";
        lblQuality.Location = new Point(15, 30);
        lblQuality.Size = new Size(90, 23);
        grpSettings.Controls.Add(lblQuality);

        trackQuality = new TrackBar();
        trackQuality.Location = new Point(110, 25);
        trackQuality.Size = new Size(250, 45);
        trackQuality.Minimum = 30;
        trackQuality.Maximum = 100;
        trackQuality.Value = 75;
        trackQuality.TickFrequency = 10;
        trackQuality.ValueChanged += TrackQuality_ValueChanged;
        grpSettings.Controls.Add(trackQuality);

        lblQualityValue = new Label();
        lblQualityValue.Text = "75%";
        lblQualityValue.Location = new Point(365, 30);
        lblQualityValue.Size = new Size(45, 23);
        lblQualityValue.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grpSettings.Controls.Add(lblQualityValue);

        lblQualityHint = new Label();
        lblQualityHint.Text = "(lagre = mindre filer)";
        lblQualityHint.Location = new Point(410, 30);
        lblQualityHint.Size = new Size(130, 23);
        lblQualityHint.ForeColor = Color.Gray;
        grpSettings.Controls.Add(lblQualityHint);

        // Progress group
        grpProgress = new GroupBox();
        grpProgress.Text = "Framsteg";
        grpProgress.Location = new Point(20, 325);
        grpProgress.Size = new Size(560, 90);
        Controls.Add(grpProgress);

        progressBar = new ProgressBar();
        progressBar.Location = new Point(15, 28);
        progressBar.Size = new Size(530, 25);
        progressBar.Style = ProgressBarStyle.Continuous;
        grpProgress.Controls.Add(progressBar);

        lblStatus = new Label();
        lblStatus.Text = "Klar att starta...";
        lblStatus.Location = new Point(15, 58);
        lblStatus.Size = new Size(530, 23);
        lblStatus.ForeColor = Color.Gray;
        grpProgress.Controls.Add(lblStatus);

        // Buttons
        btnStart = new Button();
        btnStart.Text = "Starta komprimering";
        btnStart.Location = new Point(20, 430);
        btnStart.Size = new Size(170, 45);
        btnStart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnStart.BackColor = Color.FromArgb(0, 120, 212);
        btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Click += BtnStart_Click;
        Controls.Add(btnStart);

        btnCancel = new Button();
        btnCancel.Text = "Avbryt";
        btnCancel.Location = new Point(200, 430);
        btnCancel.Size = new Size(100, 45);
        btnCancel.Enabled = false;
        btnCancel.Click += BtnCancel_Click;
        Controls.Add(btnCancel);

        btnOpenDest = new Button();
        btnOpenDest.Text = "Öppna destinationsmapp";
        btnOpenDest.Location = new Point(400, 430);
        btnOpenDest.Size = new Size(180, 45);
        btnOpenDest.Enabled = false;
        btnOpenDest.Click += BtnOpenDest_Click;
        Controls.Add(btnOpenDest);
    }

    #endregion

    private Label lblHeader;
    private Label lblDragHint;
    private GroupBox grpSource;
    private TextBox txtSourceFolder;
    private Button btnBrowseSource;
    private GroupBox grpDest;
    private TextBox txtDestFolder;
    private Button btnBrowseDest;
    private GroupBox grpSettings;
    private Label lblQuality;
    private TrackBar trackQuality;
    private Label lblQualityValue;
    private Label lblQualityHint;
    private GroupBox grpProgress;
    private ProgressBar progressBar;
    private Label lblStatus;
    private Button btnStart;
    private Button btnCancel;
    private Button btnOpenDest;
}
