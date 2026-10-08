namespace ImageOptimizer.NO;

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
        ClientSize = new Size(600, 650);
        Text = "Bildeoptimalisering";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        AllowDrop = true;
        DragEnter += Form1_DragEnter;
        DragDrop += Form1_DragDrop;

        // Header label
        lblHeader = new Label();
        lblHeader.Text = "Bildeoptimalisering for nett";
        lblHeader.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblHeader.Location = new Point(20, 15);
        lblHeader.Size = new Size(560, 35);
        lblHeader.ForeColor = Color.FromArgb(0, 120, 212);
        Controls.Add(lblHeader);

        // Drag drop hint
        lblDragHint = new Label();
        lblDragHint.Text = "Tips: dra og slipp en mappe hit for å velge den";
        lblDragHint.Location = new Point(20, 50);
        lblDragHint.Size = new Size(560, 20);
        lblDragHint.ForeColor = Color.Gray;
        lblDragHint.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
        Controls.Add(lblDragHint);

        // Source folder group
        grpSource = new GroupBox();
        grpSource.Text = "Originalmappe (bilder å komprimere)";
        grpSource.Location = new Point(20, 75);
        grpSource.Size = new Size(560, 70);
        grpSource.AllowDrop = true;
        grpSource.DragEnter += Form1_DragEnter;
        grpSource.DragDrop += Form1_DragDrop;
        Controls.Add(grpSource);

        txtSourceFolder = new TextBox();
        txtSourceFolder.Location = new Point(15, 28);
        txtSourceFolder.Size = new Size(415, 23);
        txtSourceFolder.ReadOnly = true;
        txtSourceFolder.AllowDrop = true;
        txtSourceFolder.DragEnter += Form1_DragEnter;
        txtSourceFolder.DragDrop += Form1_DragDrop;
        grpSource.Controls.Add(txtSourceFolder);

        btnBrowseSource = new Button();
        btnBrowseSource.Text = "Bla gjennom...";
        btnBrowseSource.Location = new Point(437, 26);
        btnBrowseSource.Size = new Size(108, 27);
        btnBrowseSource.Click += BtnBrowseSource_Click;
        grpSource.Controls.Add(btnBrowseSource);

        // Destination folder group
        grpDest = new GroupBox();
        grpDest.Text = "Målmappe (komprimerte bilder)";
        grpDest.Location = new Point(20, 155);
        grpDest.Size = new Size(560, 70);
        Controls.Add(grpDest);

        txtDestFolder = new TextBox();
        txtDestFolder.Location = new Point(15, 28);
        txtDestFolder.Size = new Size(415, 23);
        txtDestFolder.ReadOnly = true;
        grpDest.Controls.Add(txtDestFolder);

        btnBrowseDest = new Button();
        btnBrowseDest.Text = "Bla gjennom...";
        btnBrowseDest.Location = new Point(437, 26);
        btnBrowseDest.Size = new Size(108, 27);
        btnBrowseDest.Click += BtnBrowseDest_Click;
        grpDest.Controls.Add(btnBrowseDest);

        // Settings group
        grpSettings = new GroupBox();
        grpSettings.Text = "Innstillinger";
        grpSettings.Location = new Point(20, 235);
        grpSettings.Size = new Size(560, 110);
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
        lblQualityHint.Text = "(lavere = mindre filer)";
        lblQualityHint.Location = new Point(410, 30);
        lblQualityHint.Size = new Size(140, 23);
        lblQualityHint.ForeColor = Color.Gray;
        grpSettings.Controls.Add(lblQualityHint);

        lblMinGain = new Label();
        lblMinGain.Text = "Minste gevinst:";
        lblMinGain.Location = new Point(15, 78);
        lblMinGain.Size = new Size(90, 23);
        grpSettings.Controls.Add(lblMinGain);

        numMinGain = new NumericUpDown();
        numMinGain.Location = new Point(110, 75);
        numMinGain.Size = new Size(55, 23);
        numMinGain.Minimum = 1;
        numMinGain.Maximum = 90;
        numMinGain.Value = 10;
        grpSettings.Controls.Add(numMinGain);

        lblMinGainHint = new Label();
        lblMinGainHint.Text = "% mindre fil kreves, ellers forblir bildet urørt";
        lblMinGainHint.Location = new Point(172, 78);
        lblMinGainHint.Size = new Size(280, 23);
        lblMinGainHint.ForeColor = Color.Gray;
        grpSettings.Controls.Add(lblMinGainHint);

        chkDryRun = new CheckBox();
        chkDryRun.Text = "Testkjøring";
        chkDryRun.Location = new Point(455, 77);
        chkDryRun.Size = new Size(95, 23);
        chkDryRun.CheckedChanged += ModeChanged;
        grpSettings.Controls.Add(chkDryRun);

        // Overwrite group
        grpOverwrite = new GroupBox();
        grpOverwrite.Text = "Overskriving";
        grpOverwrite.Location = new Point(20, 355);
        grpOverwrite.Size = new Size(560, 120);
        Controls.Add(grpOverwrite);

        chkOverwrite = new CheckBox();
        chkOverwrite.Text = "Skriv over originalene i originalmappen";
        chkOverwrite.Location = new Point(15, 25);
        chkOverwrite.Size = new Size(450, 22);
        chkOverwrite.CheckedChanged += ModeChanged;
        grpOverwrite.Controls.Add(chkOverwrite);

        chkBackup = new CheckBox();
        chkBackup.Text = "Ta sikkerhetskopi før endring til:";
        chkBackup.Location = new Point(35, 52);
        chkBackup.Size = new Size(450, 22);
        chkBackup.Checked = true;
        chkBackup.Enabled = false;
        chkBackup.CheckedChanged += ModeChanged;
        grpOverwrite.Controls.Add(chkBackup);

        txtBackupFolder = new TextBox();
        txtBackupFolder.Location = new Point(35, 78);
        txtBackupFolder.Size = new Size(385, 23);
        txtBackupFolder.ReadOnly = true;
        txtBackupFolder.Enabled = false;
        grpOverwrite.Controls.Add(txtBackupFolder);

        btnBrowseBackup = new Button();
        btnBrowseBackup.Text = "Bla gjennom...";
        btnBrowseBackup.Location = new Point(437, 76);
        btnBrowseBackup.Size = new Size(108, 27);
        btnBrowseBackup.Enabled = false;
        btnBrowseBackup.Click += BtnBrowseBackup_Click;
        grpOverwrite.Controls.Add(btnBrowseBackup);

        // Progress group
        grpProgress = new GroupBox();
        grpProgress.Text = "Fremgang";
        grpProgress.Location = new Point(20, 485);
        grpProgress.Size = new Size(560, 90);
        Controls.Add(grpProgress);

        progressBar = new ProgressBar();
        progressBar.Location = new Point(15, 28);
        progressBar.Size = new Size(530, 25);
        progressBar.Style = ProgressBarStyle.Continuous;
        grpProgress.Controls.Add(progressBar);

        lblStatus = new Label();
        lblStatus.Text = "Klar til å starte...";
        lblStatus.Location = new Point(15, 58);
        lblStatus.Size = new Size(530, 23);
        lblStatus.ForeColor = Color.Gray;
        grpProgress.Controls.Add(lblStatus);

        // Buttons
        btnStart = new Button();
        btnStart.Text = "Start komprimering";
        btnStart.Location = new Point(20, 590);
        btnStart.Size = new Size(170, 45);
        btnStart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnStart.BackColor = Color.FromArgb(0, 120, 212);
        btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Click += BtnStart_Click;
        Controls.Add(btnStart);

        btnCancel = new Button();
        btnCancel.Text = "Avbryt";
        btnCancel.Location = new Point(200, 590);
        btnCancel.Size = new Size(100, 45);
        btnCancel.Enabled = false;
        btnCancel.Click += BtnCancel_Click;
        Controls.Add(btnCancel);

        btnOpenDest = new Button();
        btnOpenDest.Text = "Åpne målmappe";
        btnOpenDest.Location = new Point(400, 590);
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
    private Label lblMinGain;
    private NumericUpDown numMinGain;
    private Label lblMinGainHint;
    private CheckBox chkDryRun;
    private GroupBox grpOverwrite;
    private CheckBox chkOverwrite;
    private CheckBox chkBackup;
    private TextBox txtBackupFolder;
    private Button btnBrowseBackup;
    private GroupBox grpProgress;
    private ProgressBar progressBar;
    private Label lblStatus;
    private Button btnStart;
    private Button btnCancel;
    private Button btnOpenDest;
}
