using System.Diagnostics;
using PZ_Mapper_Converter;

namespace PZ_Mapper_Studio;

internal sealed class MainForm : Form
{
    private const string AppName = "PZ Reverse Mapper";

    private readonly ComboBox _workflowPresetBox = new();
    private readonly ComboBox _sourceProfileBox = new();
    private readonly ComboBox _targetGridBox = new();
    private readonly TextBox _inputBox = new();
    private readonly TextBox _outputBox = new();
    private readonly TextBox _tilesBox = new();
    private readonly CheckBox _useModTilesBox = new();
    private readonly TextBox _modTilesPathBox = new();
    private readonly TextBox _projectNameBox = new();
    private readonly TextBox _cellsBox = new();
    private readonly CheckBox _cleanBox = new();
    private readonly CheckBox _imagesBox = new();
    private readonly CheckBox _tilePacksBox = new();
    private readonly CheckBox _objectsBox = new();
    private readonly CheckBox _roomTbxBox = new();
    private readonly CheckBox _buildingTbxBox = new();
    private readonly CheckBox _tbxOnlyBox = new();
    private readonly TextBox _commandBox = new();
    private readonly TextBox _logBox = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly Button _validateButton = new();
    private readonly Button _modTilesBrowseButton = new();
    private readonly Button _copyCommandButton = new();
    private readonly Button _helpButton = new();
    private readonly Button _aboutButton = new();
    private readonly Button _exportButton = new();
    private readonly Button _openOutputButton = new();
    private readonly ToolTip _toolTip = new();

    private bool _isApplyingPreset;
    private bool _isUpdatingProjectNameFromInput;
    private bool _projectNameEditedByUser;
    private string? _lastProgressStage;
    private int _lastLoggedPercent = -1;

    public MainForm()
    {
        Text = AppName;
        MinimumSize = new Size(1120, 900);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(244, 246, 249);

        Controls.Add(BuildLayout());
        ConfigureDefaults();
        WireEvents();
        ConfigureTooltips();
        UpdateCommandPreview();
    }

    private Control BuildLayout()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(18),
            BackColor = BackColor
        };

        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        shell.Controls.Add(BuildHeader(), 0, 0);
        shell.Controls.Add(BuildWorkflowGroup(), 0, 1);
        shell.Controls.Add(BuildPathsGroup(), 0, 2);
        shell.Controls.Add(BuildCommandGroup(), 0, 3);
        shell.Controls.Add(BuildLogGroup(), 0, 4);
        shell.Controls.Add(BuildActions(), 0, 5);

        return shell;
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = Color.FromArgb(31, 38, 52),
            Padding = new Padding(18, 13, 18, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = AppName
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(211, 218, 228),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Reverse compiled Project Zomboid maps into editable TMX, PZW, TBX, images and tilesheets."
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildWorkflowGroup()
    {
        var group = CreateGroup("Workflow");
        var grid = CreateGrid(4);
        grid.RowCount = 3;
        SetFixedRows(grid, 3, 34);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _workflowPresetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _workflowPresetBox.Items.AddRange(new object[]
        {
            "Initial export - auto to 300",
            "B42 source 256 -> TMX 300",
            "B42 source 256 -> TMX 256",
            "v41 source 300 -> TMX 300",
            "Map only - no TBX",
            "TBX only - rooms/buildings",
            "Custom"
        });
        ConfigureComboBox(_workflowPresetBox, 420);

        _sourceProfileBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sourceProfileBox.Items.AddRange(new object[]
        {
            "Auto detect",
            "v41 and older - source 300",
            "Build 42 POT - source 256"
        });
        ConfigureComboBox(_sourceProfileBox, 360);

        _targetGridBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _targetGridBox.Items.AddRange(new object[]
        {
            "WorldEd TMX 300 x 300",
            "Native TMX 256 x 256"
        });
        ConfigureComboBox(_targetGridBox, 320);

        grid.Controls.Add(CreateLabel("Preset"), 0, 0);
        grid.SetColumnSpan(_workflowPresetBox, 3);
        grid.Controls.Add(_workflowPresetBox, 1, 0);

        grid.Controls.Add(CreateLabel("Input profile"), 0, 1);
        grid.Controls.Add(_sourceProfileBox, 1, 1);
        grid.Controls.Add(CreateLabel("Output grid"), 2, 1);
        grid.Controls.Add(_targetGridBox, 3, 1);

        _cleanBox.Text = "Clean output folder";
        _imagesBox.Text = "Images";
        _tilePacksBox.Text = "Tile sheets";
        _objectsBox.Text = "Objects";
        _roomTbxBox.Text = "RoomDef TBX";
        _buildingTbxBox.Text = "Building TBX pack";
        _tbxOnlyBox.Text = "TBX only";

        var checks = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            Margin = new Padding(0, 3, 0, 0)
        };
        foreach (var checkBox in new[] { _cleanBox, _imagesBox, _tilePacksBox, _objectsBox, _roomTbxBox, _buildingTbxBox, _tbxOnlyBox })
        {
            checkBox.AutoSize = true;
            checkBox.Margin = new Padding(0, 4, 18, 0);
        }
        checks.Controls.AddRange(new Control[] { _cleanBox, _imagesBox, _tilePacksBox, _objectsBox, _roomTbxBox, _buildingTbxBox, _tbxOnlyBox });

        grid.Controls.Add(CreateLabel("Outputs"), 0, 2);
        grid.SetColumnSpan(checks, 3);
        grid.Controls.Add(checks, 1, 2);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildPathsGroup()
    {
        var group = CreateGroup("Project paths");
        var grid = CreateGrid(3);
        grid.RowCount = 6;
        SetFixedRows(grid, 6, 34);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));

        AddPathRow(grid, 0, "Map folder", _inputBox, () => BrowseFolder(_inputBox, "Compiled map folder"));
        AddPathRow(grid, 1, "Output folder", _outputBox, () => BrowseFolder(_outputBox, "Export output folder"));
        AddPathRow(grid, 2, "Tiles/media", _tilesBox, () => BrowseFolder(_tilesBox, "Project Zomboid media folder"));

        _useModTilesBox.Text = "Enable";
        _useModTilesBox.Dock = DockStyle.Fill;
        _useModTilesBox.AutoSize = true;
        _useModTilesBox.TextAlign = ContentAlignment.MiddleLeft;
        _useModTilesBox.Margin = new Padding(0, 5, 8, 4);

        _modTilesPathBox.PlaceholderText = "Optional mod assets folder with .tiles and texturepacks/*.pack";
        ConfigureTextBox(_modTilesPathBox);

        var modTilesPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = Color.White
        };
        modTilesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        modTilesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modTilesPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        modTilesPanel.Controls.Add(_useModTilesBox, 0, 0);
        modTilesPanel.Controls.Add(_modTilesPathBox, 1, 0);

        _modTilesBrowseButton.Text = "Browse";
        _modTilesBrowseButton.Dock = DockStyle.Fill;
        _modTilesBrowseButton.Margin = new Padding(0, 4, 0, 4);
        _modTilesBrowseButton.Click += (_, _) => BrowseFolder(_modTilesPathBox, "Prepared mod tiles/assets folder");

        grid.Controls.Add(CreateLabel("Mod assets"), 0, 3);
        grid.Controls.Add(modTilesPanel, 1, 3);
        grid.Controls.Add(_modTilesBrowseButton, 2, 3);

        _projectNameBox.PlaceholderText = "Auto from selected map folder";
        _cellsBox.PlaceholderText = "Optional, for example 46_26,46_27";
        ConfigureTextBox(_projectNameBox);
        ConfigureTextBox(_cellsBox);

        grid.Controls.Add(CreateLabel("Project name"), 0, 4);
        grid.Controls.Add(_projectNameBox, 1, 4);
        grid.Controls.Add(CreateSpacer(), 2, 4);

        grid.Controls.Add(CreateLabel("Source cells"), 0, 5);
        grid.Controls.Add(_cellsBox, 1, 5);
        grid.Controls.Add(CreateSpacer(), 2, 5);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildCommandGroup()
    {
        var group = CreateGroup("Reproducible command");
        var grid = CreateGrid(2);
        grid.RowCount = 1;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));

        _commandBox.Dock = DockStyle.Fill;
        _commandBox.Multiline = true;
        _commandBox.ReadOnly = true;
        _commandBox.ScrollBars = ScrollBars.Vertical;
        _commandBox.BackColor = Color.FromArgb(250, 251, 253);
        _commandBox.BorderStyle = BorderStyle.FixedSingle;
        _commandBox.Font = new Font("Consolas", 9f);
        _commandBox.MinimumSize = new Size(0, 72);

        _copyCommandButton.Text = "Copy";
        _copyCommandButton.Dock = DockStyle.Fill;
        _copyCommandButton.Margin = new Padding(8, 0, 0, 5);
        _copyCommandButton.Click += (_, _) => CopyCommand();

        grid.Controls.Add(_commandBox, 0, 0);
        grid.Controls.Add(_copyCommandButton, 1, 0);
        group.Controls.Add(grid);
        return group;
    }

    private Control BuildLogGroup()
    {
        var group = CreateGroup("Run log");
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.BackColor = Color.White;
        _logBox.BorderStyle = BorderStyle.FixedSingle;
        _logBox.Font = new Font("Consolas", 9f);
        group.Controls.Add(_logBox);
        return group;
    }

    private Control BuildActions()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 0)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 166));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.FromArgb(66, 75, 92);
        _statusLabel.Text = "Ready.";

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Style = ProgressBarStyle.Blocks;

        _validateButton.Text = "Validate";
        _validateButton.Dock = DockStyle.Fill;
        _validateButton.Margin = new Padding(8, 0, 0, 0);
        _validateButton.Click += (_, _) => ValidateWorkflow();

        _aboutButton.Text = "About";
        _aboutButton.Dock = DockStyle.Fill;
        _aboutButton.Margin = new Padding(8, 0, 0, 0);
        _aboutButton.Click += (_, _) => ShowAbout();

        _helpButton.Text = "Guide";
        _helpButton.Dock = DockStyle.Fill;
        _helpButton.Margin = new Padding(8, 0, 0, 0);
        _helpButton.Click += (_, _) => ShowGuide();

        _openOutputButton.Text = "Open output";
        _openOutputButton.Enabled = false;
        _openOutputButton.Dock = DockStyle.Fill;
        _openOutputButton.Margin = new Padding(8, 0, 0, 0);
        _openOutputButton.Click += (_, _) => OpenOutputFolder();

        _exportButton.Text = "Run export";
        _exportButton.Dock = DockStyle.Fill;
        _exportButton.Margin = new Padding(8, 0, 0, 0);
        _exportButton.BackColor = Color.FromArgb(42, 115, 204);
        _exportButton.ForeColor = Color.White;
        _exportButton.FlatStyle = FlatStyle.Flat;
        _exportButton.FlatAppearance.BorderSize = 0;
        _exportButton.Click += async (_, _) => await RunExportAsync();

        panel.Controls.Add(_statusLabel, 0, 0);
        panel.Controls.Add(_progressBar, 1, 0);
        panel.Controls.Add(_aboutButton, 2, 0);
        panel.Controls.Add(_helpButton, 3, 0);
        panel.Controls.Add(_validateButton, 4, 0);
        panel.Controls.Add(_openOutputButton, 5, 0);
        panel.Controls.Add(_exportButton, 6, 0);
        return panel;
    }

    private void ConfigureDefaults()
    {
        _isApplyingPreset = true;
        _workflowPresetBox.SelectedIndex = 0;
        _sourceProfileBox.SelectedIndex = 0;
        _targetGridBox.SelectedIndex = 0;
        _cleanBox.Checked = true;
        _imagesBox.Checked = true;
        _tilePacksBox.Checked = false;
        _objectsBox.Checked = true;
        _roomTbxBox.Checked = true;
        _buildingTbxBox.Checked = true;
        _tbxOnlyBox.Checked = false;
        _useModTilesBox.Checked = false;
        _outputBox.Text = Path.Combine(GetApplicationDirectory(), "export");
        _projectNameBox.Text = string.Empty;
        _projectNameEditedByUser = false;
        _isApplyingPreset = false;
        ApplyTbxOnlyState();
        ApplyModTilesState();

        AppendLog("Ready. Select a preset, validate, then run the export.");
    }

    private void WireEvents()
    {
        _workflowPresetBox.SelectedIndexChanged += (_, _) => ApplyWorkflowPreset();

        _sourceProfileBox.SelectedIndexChanged += (_, _) => MarkCustomAndRefresh();
        _targetGridBox.SelectedIndexChanged += (_, _) => MarkCustomAndRefresh();
        _cleanBox.CheckedChanged += (_, _) => MarkCustomAndRefresh();
        _imagesBox.CheckedChanged += (_, _) => MarkCustomAndRefresh();
        _tilePacksBox.CheckedChanged += (_, _) => MarkCustomAndRefresh();
        _objectsBox.CheckedChanged += (_, _) => MarkCustomAndRefresh();
        _roomTbxBox.CheckedChanged += (_, _) => MarkCustomAndRefresh();
        _buildingTbxBox.CheckedChanged += (_, _) => MarkCustomAndRefresh();
        _tbxOnlyBox.CheckedChanged += (_, _) =>
        {
            ApplyTbxOnlyState();
            MarkCustomAndRefresh();
        };
        _useModTilesBox.CheckedChanged += (_, _) =>
        {
            ApplyModTilesState();
            MarkCustomAndRefresh();
        };

        _inputBox.TextChanged += (_, _) =>
        {
            UpdateProjectNameFromInput();
            UpdateCommandPreview();
        };
        _outputBox.TextChanged += (_, _) => UpdateCommandPreview();
        _tilesBox.TextChanged += (_, _) => UpdateCommandPreview();
        _modTilesPathBox.TextChanged += (_, _) => UpdateCommandPreview();
        _projectNameBox.TextChanged += (_, _) =>
        {
            if (!_isUpdatingProjectNameFromInput)
            {
                _projectNameEditedByUser = !string.IsNullOrWhiteSpace(_projectNameBox.Text);
            }

            UpdateCommandPreview();
        };
        _cellsBox.TextChanged += (_, _) => UpdateCommandPreview();
    }

    private void ConfigureTooltips()
    {
        _toolTip.AutoPopDelay = 16000;
        _toolTip.InitialDelay = 350;
        _toolTip.ReshowDelay = 150;
        _toolTip.ShowAlways = true;

        _toolTip.SetToolTip(_workflowPresetBox, "Chooses a coherent set of source, output grid and extra exports.");
        _toolTip.SetToolTip(_sourceProfileBox, "Auto reads the lotheader size. v41 validates 300. B42 validates 256.");
        _toolTip.SetToolTip(_targetGridBox, "300 keeps the classic WorldEd grid. 256 writes native Build 42-sized TMX cells.");
        _toolTip.SetToolTip(_cleanBox, "Asks for confirmation, then moves existing output contents to the Recycle Bin.");
        _toolTip.SetToolTip(_imagesBox, "Writes per-cell and merged PNG map previews used by the map workflow.");
        _toolTip.SetToolTip(_tilePacksBox, "When selected alone, extracts tiles without reading lotheaders or lotpacks.");
        _toolTip.SetToolTip(_objectsBox, "Reads objects.lua and writes objects into the PZW project.");
        _toolTip.SetToolTip(_roomTbxBox, "Writes one TBX per RoomDef under tmx/tbx/<cell>.");
        _toolTip.SetToolTip(_buildingTbxBox, "Writes supplemental building TBX files under tbx_buildings/<source-cell>.");
        _toolTip.SetToolTip(_tbxOnlyBox, "Writes only TBX outputs. TMX, PZW and objects.lua are skipped.");
        _toolTip.SetToolTip(_cellsBox, "Optional filter such as 46_26,46_27. Empty exports all source cells.");
        _toolTip.SetToolTip(_tilesBox, "Vanilla Project Zomboid media folder. This input is read only.");
        _toolTip.SetToolTip(_useModTilesBox, "Enable a second read-only asset source for modded .tiles and .pack files.");
        _toolTip.SetToolTip(_modTilesPathBox, "Prepared mod asset folder. Put .tiles here and .pack files under texturepacks or texturespack.");
        _toolTip.SetToolTip(_modTilesBrowseButton, "Select the prepared mod asset folder.");
        _toolTip.SetToolTip(_validateButton, "Reads headers and checks the workflow before writing files.");
        _toolTip.SetToolTip(_aboutButton, "About PZ Reverse Mapper.");
        _toolTip.SetToolTip(_helpButton, "Open the workflow guide.");
        _toolTip.SetToolTip(_copyCommandButton, "Copy the equivalent CLI command.");
    }

    private void ApplyWorkflowPreset()
    {
        if (_isApplyingPreset)
        {
            return;
        }

        _isApplyingPreset = true;
        switch (_workflowPresetBox.SelectedIndex)
        {
            case 0:
                _sourceProfileBox.SelectedIndex = 0;
                _targetGridBox.SelectedIndex = 0;
                _imagesBox.Checked = true;
                _tilePacksBox.Checked = false;
                _objectsBox.Checked = true;
                _roomTbxBox.Checked = true;
                _buildingTbxBox.Checked = true;
                _tbxOnlyBox.Checked = false;
                break;
            case 1:
                _sourceProfileBox.SelectedIndex = 2;
                _targetGridBox.SelectedIndex = 0;
                _imagesBox.Checked = true;
                _tilePacksBox.Checked = false;
                _objectsBox.Checked = true;
                _roomTbxBox.Checked = true;
                _buildingTbxBox.Checked = true;
                _tbxOnlyBox.Checked = false;
                break;
            case 2:
                _sourceProfileBox.SelectedIndex = 2;
                _targetGridBox.SelectedIndex = 1;
                _imagesBox.Checked = true;
                _tilePacksBox.Checked = false;
                _objectsBox.Checked = true;
                _roomTbxBox.Checked = true;
                _buildingTbxBox.Checked = true;
                _tbxOnlyBox.Checked = false;
                break;
            case 3:
                _sourceProfileBox.SelectedIndex = 1;
                _targetGridBox.SelectedIndex = 0;
                _imagesBox.Checked = true;
                _tilePacksBox.Checked = false;
                _objectsBox.Checked = true;
                _roomTbxBox.Checked = true;
                _buildingTbxBox.Checked = true;
                _tbxOnlyBox.Checked = false;
                break;
            case 4:
                _sourceProfileBox.SelectedIndex = 0;
                _targetGridBox.SelectedIndex = 0;
                _imagesBox.Checked = true;
                _tilePacksBox.Checked = false;
                _objectsBox.Checked = true;
                _roomTbxBox.Checked = false;
                _buildingTbxBox.Checked = false;
                _tbxOnlyBox.Checked = false;
                break;
            case 5:
                _sourceProfileBox.SelectedIndex = 0;
                _targetGridBox.SelectedIndex = 0;
                _imagesBox.Checked = false;
                _tilePacksBox.Checked = false;
                _objectsBox.Checked = false;
                _roomTbxBox.Checked = true;
                _buildingTbxBox.Checked = true;
                _tbxOnlyBox.Checked = true;
                break;
        }

        _cleanBox.Checked = true;
        _isApplyingPreset = false;
        ApplyTbxOnlyState();
        UpdateCommandPreview();
    }

    private void MarkCustomAndRefresh()
    {
        if (!_isApplyingPreset && _workflowPresetBox.SelectedIndex != 6)
        {
            _isApplyingPreset = true;
            _workflowPresetBox.SelectedIndex = 6;
            _isApplyingPreset = false;
        }

        UpdateCommandPreview();
    }

    private void ApplyTbxOnlyState()
    {
        if (_tbxOnlyBox.Checked)
        {
            _imagesBox.Checked = false;
            _imagesBox.Enabled = false;
            _objectsBox.Checked = false;
            _objectsBox.Enabled = false;
            return;
        }

        _imagesBox.Enabled = true;
        _objectsBox.Enabled = true;
    }

    private void ApplyModTilesState()
    {
        var enabled = _useModTilesBox.Checked;
        _modTilesPathBox.Enabled = enabled;
        _modTilesBrowseButton.Enabled = enabled;
        _modTilesPathBox.BackColor = enabled ? Color.White : Color.FromArgb(246, 248, 251);
    }

    private void ValidateWorkflow()
    {
        try
        {
            var options = BuildOptions();
            if (options.TilesOnly)
            {
                AppendLog("");
                AppendLog("Validation OK.");
                AppendLog("Mode:           tiles only");
                AppendLog("Map data:       not read");
                AppendLog($"Tiles/media:    {(string.IsNullOrWhiteSpace(options.TilesPath) ? "not set" : options.TilesPath)}");
                AppendLog($"Mod assets:     {(string.IsNullOrWhiteSpace(options.ModTilesPath) ? "disabled" : options.ModTilesPath)}");
                _statusLabel.Text = "Validation OK - tiles-only mode, map data will not be read.";
                return;
            }

            var inspection = InspectWorkflow(options);

            AppendLog("");
            AppendLog("Validation OK.");
            AppendLog($"Source cells:   {inspection.SourceCellCount}");
            AppendLog($"Source sizes:   {inspection.SourceSizes}");
            AppendLog($"Levels:         {inspection.LevelRange}");
            AppendLog($"TMX cells:      {inspection.EstimatedTargetCellCount}");
            AppendLog($"Missing packs:  {inspection.MissingLotPackCount}");
            AppendLog($"objects.lua:    {(inspection.HasObjectsLua ? "found" : "not found")}");
            _statusLabel.Text = $"Validation OK - {inspection.SourceCellCount} source cell(s), {inspection.EstimatedTargetCellCount} target TMX cell(s).";
        }
        catch (Exception ex)
        {
            AppendLog($"Validation failed: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Validation failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RunExportAsync()
    {
        try
        {
            var options = BuildOptions();
            if (!ConfirmCleanOutputIfNeeded(options))
            {
                return;
            }

            SetBusy(true);
            _openOutputButton.Enabled = false;
            _lastProgressStage = null;
            _lastLoggedPercent = -1;
            AppendLog("");
            AppendLog($"Preset:         {_workflowPresetBox.Text}");
            AppendLog($"Input profile:  {_sourceProfileBox.Text}");
            AppendLog($"Output grid:    {_targetGridBox.Text}");
            AppendLog($"Input:          {(options.TilesOnly ? "not used (tiles only)" : options.InputDirectory)}");
            AppendLog($"Output:         {options.OutputDirectory}");
            AppendLog($"Tiles/media:    {(string.IsNullOrWhiteSpace(options.TilesPath) ? "not set" : options.TilesPath)}");
            AppendLog($"Mod assets:     {(string.IsNullOrWhiteSpace(options.ModTilesPath) ? "disabled" : options.ModTilesPath)}");
            AppendLog("Export started...");

            var progress = new Progress<ConversionProgress>(ReportProgress);
            var result = await Task.Run(() => new MapConverter(options, progress).Run());

            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 100;
            _statusLabel.Text = "Export finished.";
            AppendLog("Export finished.");
            AppendLog($"Source cells:   {result.SourceCellCount}");
            AppendLog($"TMX cells:      {result.TargetCellCount}");
            AppendLog($"Objects:        {result.ObjectCount}");
            AppendLog($"Images:         {result.ImageCount}");
            AppendLog($"Tile images:    {result.TileImageCount}");
            AppendLog($"RoomDef TBX:    {result.TbxCount}");
            AppendLog($"Building TBX:   {result.BuildingTbxCount}");
            AppendLog($"Tilesets:       {result.TileSetCount}");
            AppendLog($"Elapsed:        {MapConverter.FormatElapsed(result.Elapsed)}");
            AppendLog($"Project:        {(string.IsNullOrWhiteSpace(result.ProjectFile) ? "not written" : result.ProjectFile)}");
            _openOutputButton.Enabled = true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Export failed.";
            AppendLog($"Error: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool ConfirmCleanOutputIfNeeded(ConverterOptions options)
    {
        if (!options.CleanOutput || !OutputCleaner.HasContent(options.OutputDirectory))
        {
            return true;
        }

        OutputCleaner.EnsureSafeCleanTarget(options.OutputDirectory);

        var message =
            "Clean output folder is enabled.\r\n\r\n" +
            "Existing files and folders in this output directory will be moved to the Recycle Bin before export:\r\n\r\n" +
            options.OutputDirectory + "\r\n\r\n" +
            "Continue?";

        var result = MessageBox.Show(
            this,
            message,
            "Confirm output cleanup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            _statusLabel.Text = "Export canceled.";
            AppendLog("Export canceled: output cleanup was not confirmed.");
            return false;
        }

        options.CleanOutputConfirmed = true;
        return true;
    }

    private void ReportProgress(ConversionProgress progress)
    {
        _statusLabel.Text = $"{progress.Stage}: {progress.Message}";

        if (progress.Percent is int percent)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = percent;

            var isNewStage = !string.Equals(progress.Stage, _lastProgressStage, StringComparison.Ordinal);
            if (isNewStage || percent == 100 || percent - _lastLoggedPercent >= 10)
            {
                AppendLog($"{progress.Stage}: {progress.Message} ({percent}%)");
                _lastProgressStage = progress.Stage;
                _lastLoggedPercent = percent;
            }

            return;
        }

        if (!string.Equals(progress.Stage, _lastProgressStage, StringComparison.Ordinal))
        {
            AppendLog($"{progress.Stage}: {progress.Message}");
            _lastProgressStage = progress.Stage;
            _lastLoggedPercent = -1;
        }
    }

    private ConverterOptions BuildOptions()
    {
        var tilesOnly = IsTilesOnlySelection();
        var input = _inputBox.Text.Trim();
        var output = _outputBox.Text.Trim();
        if (!tilesOnly && string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Select a compiled map input folder.");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("Select an output folder.");
        }

        var fullInput = string.IsNullOrWhiteSpace(input) ? string.Empty : Path.GetFullPath(input);
        var fullOutput = Path.GetFullPath(output);
        if (!tilesOnly && !Directory.Exists(fullInput))
        {
            throw new DirectoryNotFoundException($"Input folder not found: {fullInput}");
        }

        if (!string.IsNullOrWhiteSpace(fullInput)
            && string.Equals(TrimPath(fullInput), TrimPath(fullOutput), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Input and output folders must be different.");
        }

        if (_tbxOnlyBox.Checked && !_roomTbxBox.Checked && !_buildingTbxBox.Checked)
        {
            throw new InvalidOperationException("TBX only requires RoomDef TBX and/or Building TBX to be enabled.");
        }

        var tiles = _tilesBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(tiles) && !Directory.Exists(tiles) && !File.Exists(tiles))
        {
            throw new FileNotFoundException($"Tiles/media path not found: {tiles}");
        }

        var modTiles = _modTilesPathBox.Text.Trim();
        string? fullModTiles = null;
        if (_useModTilesBox.Checked)
        {
            if (string.IsNullOrWhiteSpace(modTiles))
            {
                throw new InvalidOperationException("Select the mod assets folder or disable the Mod assets option.");
            }

            if (!Directory.Exists(modTiles) && !File.Exists(modTiles))
            {
                throw new FileNotFoundException($"Mod assets path not found: {modTiles}");
            }

            fullModTiles = Path.GetFullPath(modTiles);
        }

        if (tilesOnly && string.IsNullOrWhiteSpace(tiles) && string.IsNullOrWhiteSpace(fullModTiles))
        {
            throw new InvalidOperationException("Tiles-only mode requires a Tiles/media or Mod assets path.");
        }

        var projectName = ResolveProjectName(_projectNameBox.Text, fullInput);

        return new ConverterOptions
        {
            InputDirectory = fullInput,
            OutputDirectory = fullOutput,
            ProjectName = projectName,
            TilesPath = string.IsNullOrWhiteSpace(tiles) ? null : Path.GetFullPath(tiles),
            ModTilesPath = fullModTiles,
            ExpectedSourceCellSize = GetExpectedSourceCellSize(),
            TargetCellSize = _targetGridBox.SelectedIndex == 1 ? 256 : 300,
            CleanOutput = _cleanBox.Checked,
            ExportImages = _imagesBox.Checked && !_tbxOnlyBox.Checked,
            ExportTilePacks = _tilePacksBox.Checked,
            ExportObjects = _objectsBox.Checked && !_tbxOnlyBox.Checked,
            ExportRoomTbx = _roomTbxBox.Checked,
            ExportBuildingTbx = _buildingTbxBox.Checked,
            TbxOnly = _tbxOnlyBox.Checked,
            TilesOnly = tilesOnly,
            IncludeCells = ParseCells(_cellsBox.Text)
        };
    }

    private bool IsTilesOnlySelection()
    {
        return _tilePacksBox.Checked
            && !_imagesBox.Checked
            && !_objectsBox.Checked
            && !_roomTbxBox.Checked
            && !_buildingTbxBox.Checked
            && !_tbxOnlyBox.Checked;
    }

    private WorkflowInspection InspectWorkflow(ConverterOptions options)
    {
        var headers = LotHeaderReader.ReadAll(options.InputDirectory);
        if (options.IncludeCells is not null)
        {
            headers = headers
                .Where(pair => options.IncludeCells.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        if (headers.Count == 0)
        {
            throw new InvalidDataException($"No matching .lotheader files found in {options.InputDirectory}");
        }

        ValidateExpectedSourceSize(options, headers.Values);

        var missingLotPacks = headers.Values.Count(header =>
            !File.Exists(Path.Combine(options.InputDirectory, $"world_{header.CellX}_{header.CellY}.lotpack")));

        var sourceSizes = string.Join(", ", headers.Values
            .GroupBy(header => header.CellDim)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}x{group.Key} ({group.Count()})"));

        var minLevel = headers.Values.Min(header => header.MinLevel);
        var maxLevel = headers.Values.Max(header => header.MaxLevel);

        return new WorkflowInspection
        {
            SourceCellCount = headers.Count,
            SourceSizes = sourceSizes,
            LevelRange = $"{minLevel}..{maxLevel}",
            EstimatedTargetCellCount = EstimateTargetCellCount(headers.Values, options.TargetCellSize),
            MissingLotPackCount = missingLotPacks,
            HasObjectsLua = File.Exists(Path.Combine(options.InputDirectory, "objects.lua"))
        };
    }

    private static void ValidateExpectedSourceSize(ConverterOptions options, IEnumerable<LotHeaderData> headers)
    {
        if (options.ExpectedSourceCellSize is null)
        {
            return;
        }

        var unexpected = headers
            .Where(header => header.CellDim != options.ExpectedSourceCellSize.Value)
            .Select(header => $"{header.CellX}_{header.CellY}={header.CellDim}")
            .ToArray();

        if (unexpected.Length > 0)
        {
            throw new InvalidDataException(
                $"Source profile expected {options.ExpectedSourceCellSize.Value}x{options.ExpectedSourceCellSize.Value} cells, " +
                $"but these headers differ: {string.Join(", ", unexpected.Take(12))}");
        }
    }

    private static int EstimateTargetCellCount(IEnumerable<LotHeaderData> headers, int targetCellSize)
    {
        var list = headers.ToArray();
        var minWorldX = list.Min(header => header.MinSquareX);
        var minWorldY = list.Min(header => header.MinSquareY);
        var maxWorldX = list.Max(header => header.MaxSquareX);
        var maxWorldY = list.Max(header => header.MaxSquareY);

        var minTargetX = BinaryHelpers.FloorDiv(minWorldX, targetCellSize);
        var minTargetY = BinaryHelpers.FloorDiv(minWorldY, targetCellSize);
        var maxTargetX = BinaryHelpers.FloorDiv(maxWorldX, targetCellSize);
        var maxTargetY = BinaryHelpers.FloorDiv(maxWorldY, targetCellSize);

        return (maxTargetX - minTargetX + 1) * (maxTargetY - minTargetY + 1);
    }

    private void UpdateCommandPreview()
    {
        _commandBox.Text = BuildCommandPreview();
    }

    private string BuildCommandPreview()
    {
        var tilesOnly = IsTilesOnlySelection();
        var args = new List<string> { "PZReverseMapper.Cli.exe" };

        if (!tilesOnly)
        {
            AddArg(args, "--input", _inputBox.Text.Trim());
        }
        AddArg(args, "--output", _outputBox.Text.Trim());
        AddArg(args, "--tiles", _tilesBox.Text.Trim());
        if (_useModTilesBox.Checked)
        {
            AddArg(args, "--mod-tiles", _modTilesPathBox.Text.Trim());
        }

        if (!tilesOnly)
        {
            var projectName = ResolveProjectName(_projectNameBox.Text, _inputBox.Text);
            AddArg(args, "--name", projectName);

            if (GetExpectedSourceCellSize() is int sourceCellSize)
            {
                AddArg(args, "--source-cell-size", sourceCellSize.ToString());
            }

            AddArg(args, "--target-cell-size", (_targetGridBox.SelectedIndex == 1 ? 256 : 300).ToString());

            if (!string.IsNullOrWhiteSpace(_cellsBox.Text))
            {
                AddArg(args, "--cells", _cellsBox.Text.Trim());
            }
        }

        if (_cleanBox.Checked)
        {
            args.Add("--clean");
        }

        if (tilesOnly)
        {
            args.Add("--tiles-only");
            return string.Join(" ", args);
        }

        if (_tbxOnlyBox.Checked)
        {
            args.Add("--tbx-only");
        }

        if (!_imagesBox.Checked && !_tbxOnlyBox.Checked)
        {
            args.Add("--no-images");
        }

        if (_tilePacksBox.Checked)
        {
            args.Add("--extract-tiles");
        }

        if (!_objectsBox.Checked && !_tbxOnlyBox.Checked)
        {
            args.Add("--no-objects");
        }

        if (!_roomTbxBox.Checked)
        {
            args.Add("--no-room-tbx");
        }

        if (!_buildingTbxBox.Checked)
        {
            args.Add("--no-building-tbx");
        }

        return string.Join(" ", args);
    }

    private static void AddArg(ICollection<string> args, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add(name);
        args.Add(QuoteArgument(value));
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private void CopyCommand()
    {
        if (string.IsNullOrWhiteSpace(_commandBox.Text))
        {
            return;
        }

        Clipboard.SetText(_commandBox.Text);
        AppendLog("Command copied to clipboard.");
    }

    private void ShowGuide()
    {
        using var dialog = new Form
        {
            Text = $"{AppName} - Workflow guide",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(900, 680),
            MinimumSize = new Size(760, 560),
            Font = Font,
            BackColor = Color.FromArgb(244, 246, 249)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var guide = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            DetectUrls = false
        };
        PopulateGuide(guide);

        var closeButton = new Button
        {
            Text = "Close",
            Dock = DockStyle.Right,
            Width = 112
        };
        closeButton.Click += (_, _) => dialog.Close();

        layout.Controls.Add(guide, 0, 0);
        layout.Controls.Add(closeButton, 0, 1);
        dialog.Controls.Add(layout);
        dialog.ShowDialog(this);
    }

    private void ShowAbout()
    {
        using var dialog = new AboutForm();
        dialog.ShowDialog(this);
    }

    private static void PopulateGuide(RichTextBox guide)
    {
        guide.SuspendLayout();
        guide.Clear();

        AppendGuideTitle(guide, "PZ Reverse Mapper workflow");
        AppendGuideBody(guide, "This tool reverses compiled Project Zomboid map data back into editable mapping assets. It does not create a new map from scratch; it rebuilds TMX/PZW/TBX/image outputs from lotheader, lotpack, objects.lua and tilespack data.");

        AppendGuideSection(guide, "1. Source profile");
        AppendGuideBullet(guide, "Auto detect reads each .lotheader and accepts both legacy 300x300 cells and Build 42 256x256 POT cells.");
        AppendGuideBullet(guide, "v41 source 300 and B42 source 256 are validation modes. They stop the export if the selected source does not match the expected compiled-cell size.");

        AppendGuideSection(guide, "2. Output grid");
        AppendGuideBullet(guide, "WorldEd TMX 300x300 is the classic editable map workflow and remains the default.");
        AppendGuideBullet(guide, "Native TMX 256x256 keeps output cells aligned to Build 42 boundaries when that is what you want to inspect.");

        AppendGuideSection(guide, "3. Reprojection rule");
        AppendGuideBody(guide, "Tiles, rooms and objects are reprojected through world coordinates before they are written to the target grid.");
        AppendGuideCode(guide, "worldX = sourceCellX * sourceCellSize + localX\r\ntargetCellX = floor(worldX / targetCellSize)\r\ntargetLocalX = worldX mod targetCellSize");
        AppendGuideBody(guide, "That is what allows compiled 256x256 Build 42 cells to be rebuilt into 300x300 TMX cells.");

        AppendGuideSection(guide, "4. Presets");
        AppendGuideBullet(guide, "Initial export - auto to 300: safest default; preserves the original 300x300 editable workflow.");
        AppendGuideBullet(guide, "B42 source 256 -> TMX 300: recommended for rebuilding compiled Build 42 maps into the classic WorldEd grid.");
        AppendGuideBullet(guide, "B42 source 256 -> TMX 256: keeps the output grid native to Build 42.");
        AppendGuideBullet(guide, "v41 source 300 -> TMX 300: legacy workflow with source-size validation.");
        AppendGuideBullet(guide, "Map only - no TBX: writes TMX/PZW and objects, but skips RoomDef and building TBX extras.");
        AppendGuideBullet(guide, "TBX only - rooms/buildings: writes only RoomDef TBX and supplemental building TBX outputs.");

        AppendGuideSection(guide, "5. Asset paths");
        AppendGuideBullet(guide, "Folder pickers start in the application folder instead of the Desktop. The default output path is an export subfolder beside the app.");
        AppendGuideBullet(guide, "Project name is generated from the selected map folder name until you type a custom value.");
        AppendGuideBullet(guide, "Tiles/media points to the vanilla Project Zomboid media folder. It is read only and is never modified by the export.");
        AppendGuideBullet(guide, "Mod assets is optional. Enable it when you prepared a separate folder containing modded .tiles files and the required texture packs.");
        AppendGuideBullet(guide, "The mod asset folder is loaded after Tiles/media, so it can add custom sheets or replace matching sheet metadata without polluting the game media folder.");
        AppendGuideCode(guide, "MyModAssets\\\r\n  my_tiles.tiles\r\n  texturepacks\\\r\n    my_pack.pack");
        AppendGuideBody(guide, "A texture pack folder named texturespack is also accepted for existing prepared folders.");

        AppendGuideSection(guide, "6. Output options");
        AppendGuideBullet(guide, "Clean output folder: asks for confirmation, then moves existing output contents to the Recycle Bin. Protected folders such as Desktop, Documents, Downloads and drive roots are refused.");
        AppendGuideBullet(guide, "Images: writes maps_img cell previews plus Map.png, Map_veg.png, world.png, optional biomemap.png, and Map_ZombieSpawnMap.png when zombie density exists.");
        AppendGuideBullet(guide, "Map.png is the base terrain image used by the world workflow. It does not draw RoomDefs visually.");
        AppendGuideBullet(guide, "Tile sheets: keeps every physical pack atlas separate under TilesRaw/<physical-pack>, merges only cut tiles under TilesSingle/<logical-pack>/<tileset>, then rebuilds eight-column sheets under Tiles/<logical-pack>. Matching .floor.pack and .pack files never share raw atlas files.");
        AppendGuideBullet(guide, "When Tile sheets is the only selected output, Studio uses tiles-only mode and does not read lotheaders or lotpacks.");
        AppendGuideBullet(guide, "Objects: reads objects.lua and writes visible WorldEd objects into the PZW project.");
        AppendGuideBullet(guide, "RoomDef TBX: writes one TBX per room under tmx/tbx/<cell>.");
        AppendGuideBullet(guide, "Building TBX pack: writes reconstructed supplemental buildings under tbx_buildings/<source-cell>. These are useful for inspection or a separate building pack, but they are not original source TBX files.");

        AppendGuideSection(guide, "7. Testing and validation");
        AppendGuideBullet(guide, "Use Source cells such as 46_26,46_27 to test a small area before launching a full export.");
        AppendGuideBullet(guide, "Validate reads headers only. It reports source sizes, levels, estimated target cells, missing lotpacks and objects.lua presence without writing the full export.");

        guide.SelectionStart = 0;
        guide.SelectionLength = 0;
        guide.ResumeLayout();
    }

    private static void AppendGuideTitle(RichTextBox guide, string text)
    {
        guide.SelectionFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        guide.SelectionColor = Color.FromArgb(31, 38, 52);
        guide.AppendText(text + Environment.NewLine + Environment.NewLine);
    }

    private static void AppendGuideSection(RichTextBox guide, string text)
    {
        guide.SelectionFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        guide.SelectionColor = Color.FromArgb(42, 86, 145);
        guide.AppendText(Environment.NewLine + text + Environment.NewLine);
    }

    private static void AppendGuideBody(RichTextBox guide, string text)
    {
        guide.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        guide.SelectionColor = Color.FromArgb(43, 49, 61);
        guide.AppendText(text + Environment.NewLine);
    }

    private static void AppendGuideBullet(RichTextBox guide, string text)
    {
        guide.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        guide.SelectionColor = Color.FromArgb(43, 49, 61);
        guide.AppendText("  - " + text + Environment.NewLine);
    }

    private static void AppendGuideCode(RichTextBox guide, string text)
    {
        guide.SelectionFont = new Font("Consolas", 9.5f, FontStyle.Regular);
        guide.SelectionColor = Color.FromArgb(25, 33, 45);
        guide.SelectionBackColor = Color.FromArgb(240, 243, 247);
        guide.AppendText(text + Environment.NewLine);
        guide.SelectionBackColor = Color.White;
    }

    private void UpdateProjectNameFromInput()
    {
        if (_projectNameEditedByUser)
        {
            return;
        }

        _isUpdatingProjectNameFromInput = true;
        _projectNameBox.Text = ResolveProjectName(string.Empty, _inputBox.Text);
        _projectNameEditedByUser = false;
        _isUpdatingProjectNameFromInput = false;
    }

    private static string ResolveProjectName(string enteredName, string inputPath)
    {
        if (!string.IsNullOrWhiteSpace(enteredName))
        {
            return SanitizeProjectName(enteredName.Trim());
        }

        var trimmedInput = inputPath.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedInput))
        {
            var folderName = Path.GetFileName(TrimPath(trimmedInput));
            if (!string.IsNullOrWhiteSpace(folderName))
            {
                return SanitizeProjectName(folderName);
            }
        }

        return "ConvertedMap";
    }

    private static string SanitizeProjectName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "ConvertedMap" : value;
    }

    private int? GetExpectedSourceCellSize()
    {
        return _sourceProfileBox.SelectedIndex switch
        {
            1 => 300,
            2 => 256,
            _ => null
        };
    }

    private static IReadOnlySet<CellCoord>? ParseCells(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cells = new HashSet<CellCoord>();
        var tokens = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                throw new InvalidOperationException($"Invalid source cell: {token}. Expected X_Y.");
            }

            cells.Add(new CellCoord(x, y));
        }

        return cells.Count == 0 ? null : cells;
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _validateButton.Enabled = !busy;
        _copyCommandButton.Enabled = !busy;
        _aboutButton.Enabled = !busy;
        _helpButton.Enabled = !busy;
        _exportButton.Enabled = !busy;
        _modTilesPathBox.Enabled = !busy && _useModTilesBox.Checked;
        _modTilesBrowseButton.Enabled = !busy && _useModTilesBox.Checked;
        _progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        if (!busy && _progressBar.Value < 100)
        {
            _progressBar.Value = 0;
        }
    }

    private void AppendLog(string message)
    {
        _logBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private static string TrimPath(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static GroupBox CreateGroup(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 12, 0, 0),
            BackColor = Color.White
        };
    }

    private static TableLayoutPanel CreateGrid(int columns)
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            AutoSize = false,
            Padding = new Padding(0),
            BackColor = Color.White
        };
    }

    private static void SetFixedRows(TableLayoutPanel grid, int rowCount, int height)
    {
        grid.RowStyles.Clear();
        for (var i = 0; i < rowCount; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
    }

    private static void ConfigureComboBox(ComboBox comboBox, int dropDownWidth)
    {
        comboBox.Dock = DockStyle.Fill;
        comboBox.DropDownWidth = dropDownWidth;
        comboBox.IntegralHeight = false;
        comboBox.MaxDropDownItems = 12;
        comboBox.Margin = new Padding(0, 4, 8, 4);
    }

    private static void ConfigureTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 8, 4);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 6, 8, 6)
        };
    }

    private static Control CreateSpacer()
    {
        return new Panel { Dock = DockStyle.Fill };
    }

    private static void AddPathRow(TableLayoutPanel grid, int row, string label, TextBox textBox, Action browse)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 8, 4);

        var button = new Button
        {
            Text = "Browse",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4)
        };
        button.Click += (_, _) => browse();

        grid.Controls.Add(CreateLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(button, 2, row);
    }

    private void BrowseFolder(TextBox target, string description)
    {
        var selectedPath = Directory.Exists(target.Text)
            ? target.Text
            : GetApplicationDirectory();

        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            SelectedPath = selectedPath
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private static string GetApplicationDirectory()
    {
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void OpenOutputFolder()
    {
        var output = _outputBox.Text.Trim();
        if (!Directory.Exists(output))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = output,
            UseShellExecute = true
        });
    }

    private sealed class WorkflowInspection
    {
        public required int SourceCellCount { get; init; }
        public required string SourceSizes { get; init; }
        public required string LevelRange { get; init; }
        public required int EstimatedTargetCellCount { get; init; }
        public required int MissingLotPackCount { get; init; }
        public required bool HasObjectsLua { get; init; }
    }
}
