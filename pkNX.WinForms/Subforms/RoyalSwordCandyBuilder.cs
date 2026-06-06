using pkNX.Containers;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers;
using pkNX.Structures.FlatBuffers.SWSH;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwShShopInventory = pkNX.Structures.FlatBuffers.SWSH.ShopInventory;

namespace pkNX.WinForms;

public sealed class RoyalSwordCandyBuilderForm : Form
{
    private const int DefaultRoyalCandyItemId = 1128;
    private const int DefaultTemplateItemId = 50;
    private const int DefaultVirtualCount = 999;
    private const int CandyDescriptionMaxCharacters = 120;
    private const int CandyDescriptionWrapColumn = 48;
    private const int CandyDescriptionMaxLines = 3;
    private const string DefaultCandyDescriptionText = "Raises a Pokemon's level up to the current allowed cap";
    private const int DefaultStartingCap = 10;
    private static readonly Color StatusBlockedColor = Color.FromArgb(255, 142, 142);

    private readonly string RomFsPath;
    private readonly string ExeFsPath;
    private readonly GameVersion DetectedGame;
    private RoyalCandyGameFlavor SelectedGame = RoyalCandyGameFlavor.Sword;
    private bool ProjectReady;
    private readonly Label ProjectStatusLabel = new();
    private readonly Button UnlimitedButton = new();
    private readonly Button CustomizeButton = new();
    private readonly Button UninstallButton = new();
    private readonly DataGridView ResultGrid = new();
    private readonly TextBox LogText = new();
    private readonly ToolTip ButtonToolTips = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 450,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    public RoyalSwordCandyBuilderForm(string romFsPath, string exefsPath, GameVersion detectedGame)
    {
        RomFsPath = romFsPath;
        ExeFsPath = exefsPath;
        DetectedGame = detectedGame;

        Text = "Royal Candy";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 740);
        Size = new Size(1280, 820);

        InitializeLayout();
        ApplyTheme();
        LoadDefaults();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ButtonToolTips.Dispose();

        base.Dispose(disposing);
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        ProjectStatusLabel.Dock = DockStyle.Fill;
        ProjectStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        ProjectStatusLabel.Padding = new Padding(8, 0, 8, 0);
        ProjectStatusLabel.AutoEllipsis = true;

        var actionPanel = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 12),
            RowCount = 1,
        };
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        ConfigurePrimaryActionButton(UnlimitedButton, "Give me an infinite Royal Candy without limits", "Builds Royal Candy with the fresh-new-game Bag grant and no custom story cap ladder.", BuildUnlimited);
        ConfigurePrimaryActionButton(CustomizeButton, "Customize Royal Candy limits", "Opens milestone level cap customization before building Royal Candy.", CustomizeLimits);
        ConfigurePrimaryActionButton(UninstallButton, "Uninstall Royal Candy", "Removes a Royal Candy LayeredFS output only when exefs/main matches a registered Royal Candy signature.", UninstallRoyalCandy);
        actionPanel.Controls.Add(UnlimitedButton, 0, 0);
        actionPanel.Controls.Add(CustomizeButton, 1, 0);
        actionPanel.Controls.Add(UninstallButton, 2, 0);

        ConfigureGrid(ResultGrid);
        ResultGrid.Columns.Add(CreateTextColumn("Status", 86));
        ResultGrid.Columns.Add(CreateTextColumn("Area", 100));
        ResultGrid.Columns.Add(CreateTextColumn("Output", 250));
        ResultGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Message",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 430,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        LogText.Dock = DockStyle.Fill;
        LogText.Multiline = true;
        LogText.ReadOnly = true;
        LogText.ScrollBars = ScrollBars.Vertical;
        LogText.WordWrap = true;

        root.Controls.Add(ProjectStatusLabel, 0, 0);
        root.Controls.Add(actionPanel, 0, 1);
        root.Controls.Add(ResultGrid, 0, 2);
        root.Controls.Add(LogText, 0, 3);
        Controls.Add(root);
    }

    private void LoadDefaults()
    {
        var status = AnalyzeProject();
        ProjectReady = status.Ready;
        SetProjectStatus(status.Message, status.Ready);

        if (status.Ready)
        {
            if (!AskGameVersion(status.DetectedFlavor))
            {
                ProjectReady = false;
                SetProjectStatus("Royal Candy output cancelled: choose Sword or Shield before building.", false);
            }
            else
            {
                RefreshInstalledRoyalCandyStatus();
            }
        }

        ToggleActions(ProjectReady);
        SetResults(status.Results);
        LogText.Text = string.Join(Environment.NewLine, status.LogLines);
    }

    private string GetDefaultOutputPath()
    {
        var root = Directory.GetParent(RomFsPath)?.FullName ?? RomFsPath;
        return Path.Combine(root, GetTitleId(SelectedGame));
    }

    private RoyalCandyProjectStatus AnalyzeProject()
    {
        var results = new List<BuildResult>();
        var log = new List<string>
        {
            "Royal Candy project validation",
            "==============================",
            "",
            $"RomFS: {RomFsPath}",
            $"ExeFS: {ExeFsPath}",
            $"pkNX detected game: {DetectedGame}",
            "",
        };

        if (!Directory.Exists(RomFsPath))
            return Fail("Project", "RomFS folder was not found.", results, log);
        results.Add(new("Pass", "Project", "romfs", "RomFS folder found."));

        if (string.IsNullOrWhiteSpace(ExeFsPath) || !Directory.Exists(ExeFsPath))
            return Fail("Project", "ExeFS folder was not found. Royal Candy needs a fresh ExeFS dump.", results, log);
        results.Add(new("Pass", "Project", "exefs", "ExeFS folder found."));

        var mainPath = Path.Combine(ExeFsPath, "main");
        var npdmPath = Path.Combine(ExeFsPath, "main.npdm");
        if (!File.Exists(mainPath))
            return Fail("Project", "Missing exefs/main. ExeFS patching cannot be validated.", results, log);
        if (!File.Exists(npdmPath))
            return Fail("Project", "Missing exefs/main.npdm. Sword/Shield version cannot be detected.", results, log);
        results.Add(new("Pass", "Project", "exefs/main", "ExeFS main and main.npdm found."));

        var fileCount = Directory.GetFiles(RomFsPath, "*", SearchOption.AllDirectories).Length;
        log.Add($"RomFS file count: {fileCount:N0} (informational only)");

        if (!ValidateRequiredRoyalCandyInputs(results, log, out var inputFailure))
            return Fail("Project", inputFailure, results, log);

        var detectedFlavor = DetectedGame switch
        {
            GameVersion.SW => RoyalCandyGameFlavor.Sword,
            GameVersion.SH => RoyalCandyGameFlavor.Shield,
            _ => ReadGameFlavorFromNpdm(npdmPath),
        };
        if (detectedFlavor is { } flavor)
        {
            results.Add(new("Pass", "Project", "main.npdm", $"Detected {flavor}."));
            log.Add($"Detected version: {flavor}");
        }
        else
        {
            results.Add(new("Warning", "Project", "main.npdm", "Could not prove Sword or Shield from title ID; user selection is required."));
            log.Add("Detected version: unknown");
        }

        return new(true, detectedFlavor, "Ready: required Sword/Shield Royal Candy inputs detected. Choose a Royal Candy mode.", results, log);
    }

    private bool ValidateRequiredRoyalCandyInputs(List<BuildResult> results, List<string> log, out string failure)
    {
        foreach (var relativePath in GetRequiredRomFsFiles())
        {
            var sourcePath = GetRomFsPath(relativePath);
            if (!File.Exists(sourcePath))
            {
                failure = $"Missing romfs/{relativePath}. Royal Candy cannot be built without this source file.";
                return false;
            }

            log.Add($"Required file found: romfs/{relativePath}");
        }
        results.Add(new("Pass", "Project", "romfs", "Required Royal Candy RomFS files found."));

        var messageRoot = GetRomFsPath(RoyalCandyLayeredFsBuilder.MessageRoot);
        if (!Directory.Exists(messageRoot))
        {
            failure = $"Missing romfs/{RoyalCandyLayeredFsBuilder.MessageRoot}. Royal Candy text cannot be patched.";
            return false;
        }

        var languageTextSetCount = Directory.EnumerateDirectories(messageRoot)
            .Select(directory => Path.Combine(directory, "common"))
            .Count(commonDirectory =>
                Directory.Exists(commonDirectory)
                && File.Exists(Path.Combine(commonDirectory, RoyalCandyLayeredFsBuilder.ItemInfoFile))
                && Directory.EnumerateFiles(commonDirectory, "itemname*.dat").Any());
        if (languageTextSetCount == 0)
        {
            failure = $"Missing item text files under romfs/{RoyalCandyLayeredFsBuilder.MessageRoot}. Royal Candy needs at least one common/iteminfo.dat and itemname*.dat set.";
            return false;
        }

        results.Add(new("Pass", "Project", "romfs/bin/message", $"Found {languageTextSetCount:N0} Royal Candy item text language set(s)."));
        log.Add($"Royal Candy item text language sets found: {languageTextSetCount:N0}");

        failure = string.Empty;
        return true;
    }

    private static string[] GetRequiredRomFsFiles() =>
    [
        RoyalCandyLayeredFsBuilder.ItemPath,
        RoyalCandyLayeredFsBuilder.ItemHashPath,
        RoyalCandyLayeredFsBuilder.ShopPath,
        RoyalCandyLayeredFsBuilder.NestDataPath,
        RoyalCandyLayeredFsBuilder.PlacementPath,
        RoyalSwordScriptAmxPatcher.BagEventScriptPath,
    ];

    private string GetRomFsPath(string relativePath) =>
        Path.Combine(RomFsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static RoyalCandyProjectStatus Fail(string area, string message, List<BuildResult> results, List<string> log)
    {
        results.Add(new("Fail", area, string.Empty, message));
        log.Add(message);
        return new(false, null, message, results, log);
    }

    private bool AskGameVersion(RoyalCandyGameFlavor? detectedFlavor)
    {
        using var dialog = new RoyalCandyGameVersionDialog(detectedFlavor);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return false;

        SelectedGame = dialog.SelectedGame;
        ProjectStatusLabel.Text = $"Ready: {SelectedGame} Royal Candy inputs selected. Choose a Royal Candy mode.";
        return true;
    }

    private static RoyalCandyGameFlavor? ReadGameFlavorFromNpdm(string npdmPath)
    {
        try
        {
            var data = File.ReadAllBytes(npdmPath);
            if (data.Length < 0x298)
                return null;

            return BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(0x290, 8)) switch
            {
                0x0100ABF008968000 => RoyalCandyGameFlavor.Sword,
                0x01008DB008C2C000 => RoyalCandyGameFlavor.Shield,
                _ => null,
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void BuildUnlimited()
    {
        var options = CreateOptions(RoyalCandyBuildMode.Unlimited, null);
        if (!CheckBuildPreflight(options))
            return;

        if (!ConfirmDangerousBuild())
            return;

        RunBuild(options);
    }

    private void CustomizeLimits()
    {
        if (!CheckBuildPreflight(CreateOptions(RoyalCandyBuildMode.Unlimited, null)))
            return;

        using var dialog = new RoyalCandyLimitDialog(SelectedGame, DefaultStartingCap, RoyalCandyLayeredFsBuilder.GetDefaultCapMilestoneDefinitions());
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var options = CreateOptions(RoyalCandyBuildMode.CustomLimits, dialog.Milestones);
        if (!CheckBuildPreflight(options))
            return;

        if (!ConfirmDangerousBuild())
            return;

        RunBuild(options);
    }

    private void UninstallRoyalCandy()
    {
        var options = CreateOptions(RoyalCandyBuildMode.Unlimited, null);
        if (!CheckUninstallPreflight(options))
            return;

        if (!ConfirmUninstall())
            return;

        RunUninstall(options);
    }

    private bool CheckBuildPreflight(RoyalCandyBuildOptions options)
    {
        ToggleActions(false);
        try
        {
            var preflight = RoyalCandyLayeredFsBuilder.AnalyzeBuildPreflight(options);
            SetResults(preflight.Results);
            LogText.Text = string.Join(Environment.NewLine, preflight.Notes);
            if (preflight.Passed)
            {
                SetReadyStatus();
                return true;
            }

            SetProjectStatus(GetSimplePreflightStatus(preflight), false);
            MessageBox.Show(this, preflight.Message, "Royal Candy Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            SetResults([
                new("Fail", "Preflight", string.Empty, ex.Message),
            ]);
            LogText.Text = ex.ToString();
            SetProjectStatus("Royal Candy cannot be installed until the preflight issue is fixed.", false);
            MessageBox.Show(this, ex.Message, "Royal Candy Preflight", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            ToggleActions(true);
        }
    }

    private bool CheckUninstallPreflight(RoyalCandyBuildOptions options)
    {
        ToggleActions(false);
        try
        {
            var preflight = RoyalCandyLayeredFsBuilder.AnalyzeUninstallPreflight(options);
            SetResults(preflight.Results);
            LogText.Text = string.Join(Environment.NewLine, preflight.Notes);
            if (preflight.Passed)
            {
                SetProjectStatus("Ready to uninstall the detected Royal Candy output.", true);
                return true;
            }

            SetProjectStatus(GetSimpleUninstallStatus(preflight), false);
            MessageBox.Show(this, preflight.Message, "Royal Candy Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            SetResults([
                new("Fail", "Uninstall", string.Empty, ex.Message),
            ]);
            LogText.Text = ex.ToString();
            SetProjectStatus("Royal Candy cannot be uninstalled until the preflight issue is fixed.", false);
            MessageBox.Show(this, ex.Message, "Royal Candy Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            ToggleActions(true);
        }
    }

    private void RunBuild(RoyalCandyBuildOptions options)
    {
        ToggleActions(false);
        try
        {
            var summary = RoyalCandyLayeredFsBuilder.Build(options);
            SetResults(summary.Results);
            LogText.Text = string.Join(Environment.NewLine, summary.Notes);
            SetProjectStatus(GetInstalledStatusText(options.Mode), false);
            MessageBox.Show(this, "Royal Candy output was generated.", "Royal Candy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            SetResults([
                new("Fail", "Build", string.Empty, ex.Message),
            ]);
            LogText.Text = ex.ToString();
            SetProjectStatus("Royal Candy output was not generated.", false);
        }
        finally
        {
            ToggleActions(true);
        }
    }

    private void RunUninstall(RoyalCandyBuildOptions options)
    {
        ToggleActions(false);
        try
        {
            var summary = RoyalCandyLayeredFsBuilder.Uninstall(options);
            SetResults(summary.Results);
            LogText.Text = string.Join(Environment.NewLine, summary.Notes);
            SetReadyStatus();
            MessageBox.Show(this, "Royal Candy output was uninstalled.", "Royal Candy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            SetResults([
                new("Fail", "Uninstall", string.Empty, ex.Message),
            ]);
            LogText.Text = ex.ToString();
            SetProjectStatus("Royal Candy output was not uninstalled.", false);
        }
        finally
        {
            ToggleActions(true);
        }
    }

    private RoyalCandyBuildOptions CreateOptions(RoyalCandyBuildMode mode, IReadOnlyList<RoyalCandyCapMilestone>? customMilestones)
    {
        var itemDescription = CompileCandyDescription(DefaultCandyDescriptionText);
        var useStoryCaps = mode == RoyalCandyBuildMode.CustomLimits;
        if (useStoryCaps && (customMilestones is null || customMilestones.Count == 0))
            throw new InvalidOperationException("Custom Royal Candy limits were not provided.");

        return new(
            RomFsPath,
            ExeFsPath,
            GetDefaultOutputPath(),
            DefaultRoyalCandyItemId,
            DefaultTemplateItemId,
            true,
            true,
            useStoryCaps,
            true,
            DefaultVirtualCount,
            null,
            true,
            itemDescription,
            SelectedGame,
            DefaultStartingCap,
            customMilestones,
            mode);
    }

    private static string GetTitleId(RoyalCandyGameFlavor gameFlavor) => gameFlavor switch
    {
        RoyalCandyGameFlavor.Sword => "0100ABF008968000",
        RoyalCandyGameFlavor.Shield => "01008DB008C2C000",
        _ => throw new ArgumentOutOfRangeException(nameof(gameFlavor), gameFlavor, null),
    };

    private bool ConfirmDangerousBuild()
    {
        const string warning = "Royal Candy output edits RomFS data, a story AMX script, and ExeFS main. ExeFS patches are build-specific: if the executable is not the supported Sword/Shield 1.3.2 main, the builder should stop instead of writing, but a bad source dump or already-modified executable can still produce broken game behavior.\n\nUse a fresh RomFS and ExeFS dump. The Bag-event grant only runs during a fresh new game when the player receives the Bag, so testing from an existing save may not receive the item.\n\nContinue and build the LayeredFS output?";
        return MessageBox.Show(this, warning, "Royal Candy ExeFS Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private bool ConfirmUninstall()
    {
        const string warning = "Royal Candy uninstall confirms that the selected exefs/main matches a registered Royal Candy signature, removes that ExeFS overlay, and restores only the RomFS records normally modified by Royal Candy back to the base dump's vanilla values.\n\nUnrelated custom RomFS edits in the same files are preserved when their records can be identified safely. Royal Candy-owned item data, item text, source pickup/shop/raid data, and the clean generated Bag-event script are restored to vanilla, not to any previous custom value.\n\nContinue and uninstall Royal Candy from the selected LayeredFS output?";
        return MessageBox.Show(this, warning, "Royal Candy Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void SetResults(IEnumerable<BuildResult> results)
    {
        ResultGrid.Rows.Clear();
        foreach (var result in results)
            ResultGrid.Rows.Add(result.Status, result.Area, result.Output, result.Message);
    }

    private void ToggleActions(bool enabled)
    {
        var actionEnabled = enabled && ProjectReady;
        UnlimitedButton.Enabled = actionEnabled;
        CustomizeButton.Enabled = actionEnabled;
        UninstallButton.Enabled = actionEnabled;
    }

    private void RefreshInstalledRoyalCandyStatus()
    {
        try
        {
            var scan = RoyalCandyLayeredFsBuilder.AnalyzeLayeredExeFsMain(CreateOptions(RoyalCandyBuildMode.Unlimited, null));
            if (scan is null)
            {
                SetReadyStatus();
                return;
            }

            if (scan.InstalledRoyalCandy is { } installed)
            {
                SetProjectStatus(GetInstalledStatusText(installed.Mode), false);
                return;
            }

            if (scan.HasUnknownChanges)
            {
                SetProjectStatus("Unknown ExeFS mod detected.", false);
                return;
            }

            SetReadyStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            SetProjectStatus("Royal Candy cannot check the selected output folder yet.", false);
            SetResults([
                new("Fail", "Project", "output", ex.Message),
            ]);
        }
    }

    private void SetReadyStatus() =>
        SetProjectStatus($"Ready: {SelectedGame} Royal Candy inputs selected. Choose a Royal Candy mode.", true);

    private void SetProjectStatus(string message, bool ready)
    {
        ProjectStatusLabel.Text = message;
        ProjectStatusLabel.ForeColor = ready ? WinFormsTheme.Text : StatusBlockedColor;
    }

    private static string GetSimplePreflightStatus(RoyalCandyBuildPreflight preflight)
    {
        if (preflight.InstalledRoyalCandy is { } installed)
            return GetInstalledStatusText(installed.Mode);

        if (preflight.ExeFsSignatureScan is { HasUnknownChanges: true })
            return "Unknown ExeFS mod detected.";

        return "Royal Candy cannot be installed until the preflight issue is fixed.";
    }

    private static string GetSimpleUninstallStatus(RoyalCandyBuildPreflight preflight)
    {
        if (preflight.ExeFsSignatureScan is { HasUnknownChanges: true })
            return "Unknown ExeFS mod detected.";

        return "No Royal Candy install detected.";
    }

    private static string GetInstalledStatusText(RoyalCandyBuildMode mode) =>
        mode == RoyalCandyBuildMode.CustomLimits
            ? "Custom Royal Candy already installed."
            : "Unlimited Royal Candy already installed.";

    private void ConfigurePrimaryActionButton(Button button, string text, string tooltip, Action action)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(8);
        button.Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
        button.Click += (_, _) => action();
        ButtonToolTips.SetToolTip(button, tooltip);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.Dock = DockStyle.Fill;
        grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    private static string CompileCandyDescription(string text)
    {
        var paragraphs = GetDescriptionParagraphs(text).ToArray();
        if (paragraphs.Length == 0)
            throw new InvalidOperationException("Description cannot be empty.");

        var plainLength = CountDescriptionCharacters(text);
        if (plainLength > CandyDescriptionMaxCharacters)
            throw new InvalidOperationException($"Description is {plainLength} characters; maximum is {CandyDescriptionMaxCharacters}.");

        var lines = new List<string>();
        foreach (var paragraph in paragraphs)
            lines.AddRange(WrapDescriptionParagraph(paragraph));

        if (lines.Count > CandyDescriptionMaxLines)
            throw new InvalidOperationException($"Description wraps to {lines.Count} lines; maximum is {CandyDescriptionMaxLines}.");

        return string.Join("\\n", lines);
    }

    private static int CountDescriptionCharacters(string text) =>
        string.Join(" ", GetDescriptionParagraphs(text)).Length;

    private static IEnumerable<string> GetDescriptionParagraphs(string text)
    {
        var normalized = text
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var paragraph in normalized.Split('\n'))
        {
            var cleaned = string.Join(" ", paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (cleaned.Length != 0)
                yield return cleaned;
        }
    }

    private static IEnumerable<string> WrapDescriptionParagraph(string paragraph)
    {
        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = string.Empty;
        foreach (var word in words)
        {
            if (line.Length == 0)
            {
                line = word;
                continue;
            }

            if (line.Length + 1 + word.Length <= CandyDescriptionWrapColumn)
            {
                line += " " + word;
                continue;
            }

            yield return line;
            line = word;
        }

        if (line.Length != 0)
            yield return line;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, int width) => new()
    {
        HeaderText = header,
        Width = width,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    private void ApplyTheme()
    {
        WinFormsTheme.Apply(this);
    }
}

internal sealed class RoyalCandyGameVersionDialog : Form
{
    private readonly RadioButton SwordButton = new();
    private readonly RadioButton ShieldButton = new();

    public RoyalCandyGameFlavor SelectedGame { get; private set; }

    public RoyalCandyGameVersionDialog(RoyalCandyGameFlavor? detectedFlavor)
    {
        Text = "Royal Candy Game Version";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 170);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var prompt = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = detectedFlavor is { } flavor
                ? $"pkNX detected {flavor}. Confirm the game version for version-specific Royal Candy labels."
                : "Choose the game version for version-specific Royal Candy labels.",
        };

        SwordButton.Text = "Pokemon Sword";
        SwordButton.Checked = detectedFlavor != RoyalCandyGameFlavor.Shield;
        SwordButton.Dock = DockStyle.Fill;
        ShieldButton.Text = "Pokemon Shield";
        ShieldButton.Checked = detectedFlavor == RoyalCandyGameFlavor.Shield;
        ShieldButton.Dock = DockStyle.Fill;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 86 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 86 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) => SelectedGame = ShieldButton.Checked ? RoyalCandyGameFlavor.Shield : RoyalCandyGameFlavor.Sword;

        root.Controls.Add(prompt, 0, 0);
        root.Controls.Add(SwordButton, 0, 1);
        root.Controls.Add(ShieldButton, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        WinFormsTheme.Apply(this);
    }
}

internal sealed class RoyalCandyLimitDialog : Form
{
    private readonly RoyalCandyGameFlavor GameFlavor;
    private readonly int DefaultCap;
    private readonly List<(RoyalCandyCapMilestoneDefinition Definition, NumericUpDown CapBox)> Rows = [];

    public IReadOnlyList<RoyalCandyCapMilestone> Milestones { get; private set; } = [];

    public RoyalCandyLimitDialog(RoyalCandyGameFlavor gameFlavor, int defaultCap, IReadOnlyList<RoyalCandyCapMilestoneDefinition> definitions)
    {
        GameFlavor = gameFlavor;
        DefaultCap = defaultCap;

        Text = "Customize Royal Candy Limits";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(680, 640);
        Size = new Size(760, 760);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var header = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"Default cap before these milestones is {defaultCap}. Each later cap must be equal to or higher than the previous cap.",
        };

        var table = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        foreach (var definition in definitions)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"Level after defeating: {definition.GetDisplayName(gameFlavor)}",
            };
            var capBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = definition.DefaultCap,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
            };
            table.Controls.Add(label, 0, row);
            table.Controls.Add(capBox, 1, row);
            Rows.Add((definition, capBox));
        }

        var scroll = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
        };
        scroll.Controls.Add(table);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var confirm = new Button { Text = "Confirm", Width = 96, Height = 32 };
        var cancel = new Button { Text = "Cancel", Width = 96, Height = 32, DialogResult = DialogResult.Cancel };
        confirm.Click += (_, _) => Confirm();
        actions.Controls.Add(confirm);
        actions.Controls.Add(cancel);

        CancelButton = cancel;
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(scroll, 0, 1);
        root.Controls.Add(actions, 0, 2);
        Controls.Add(root);
        WinFormsTheme.Apply(this);
    }

    private void Confirm()
    {
        var previous = DefaultCap;
        var milestones = new List<RoyalCandyCapMilestone>(Rows.Count);
        foreach (var (definition, capBox) in Rows)
        {
            var cap = (int)capBox.Value;
            if (cap < previous)
            {
                MessageBox.Show(this, $"Level after defeating {definition.GetDisplayName(GameFlavor)} is {cap}, but it must be at least {previous}.", "Royal Candy Limits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            milestones.Add(new(cap, definition));
            previous = cap;
        }

        Milestones = milestones;
        DialogResult = DialogResult.OK;
        Close();
    }
}

internal static class RoyalCandyLayeredFsBuilder
{
    private const int RareCandyItemId = 50;
    private const int RoyalCandyItemId = 1128;
    private const int RareCandyUiHookCodeCaveSearchStart = 0x007BC338;
    private const int KeyItemType = 9;
    private const byte KeyItemTypeByte = KeyItemType;
    private const ulong SceneMainMasterWorkHash = 0x00188D41BB7B57FB;
    private const ulong HopEndorsementFlagHash = 0x005A329212277F11;
    internal const string ItemPath = "bin/pml/item/item.dat";
    internal const string ItemHashPath = "bin/pml/item/item_hash_to_index.dat";
    internal const string ShopPath = "bin/appli/shop/bin/shop_data.bin";
    internal const string NestDataPath = "bin/archive/field/resident/data_table.gfpak";
    internal const string PlacementPath = "bin/archive/field/resident/placement.gfpak";
    internal const string MessageRoot = "bin/message";
    internal const string ItemInfoFile = "iteminfo.dat";
    private const string RoyalCandyName = "Royal Candy";
    private const string RoyalCandyPluralName = "Royal Candies";
    private const string MarkerFileName = "RoyalSword_RoyalCandy.txt";
    private const string MarkerHeader = "Royal Sword - Royal Candy";
    private const int ItemRawRowSize = 0x30;

    public static RoyalCandyBuildSummary Build(RoyalCandyBuildOptions options)
    {
        var results = new List<BuildResult>();
        var notes = new List<string>
        {
            "Royal Sword Candy Builder",
            "=========================",
            "",
            $"Item id: {options.ItemId}",
            $"Template item id: {options.TemplateItemId}",
            $"Output: {options.OutputPath}",
            $"Mode: {options.Mode}",
            $"Game: {options.GameFlavor}",
            $"Description: {options.ItemDescription}",
            $"Default cap: {options.DefaultCap}",
            $"Max story cap: {(options.MaxStoryCap is { } cap ? cap.ToString(CultureInfo.InvariantCulture) : "full ladder")}",
            $"Bag pickup grant: {(options.GrantOnBagEvent ? "enabled" : "disabled")}",
            "",
        };

        var preflight = AnalyzeBuildPreflight(options);
        if (!preflight.Passed)
            throw new InvalidOperationException(preflight.Message);

        results.AddRange(preflight.Results);
        notes.AddRange(preflight.Notes.Where(z => z.StartsWith("- ", StringComparison.Ordinal)));
        Directory.CreateDirectory(options.OutputPath);

        if (options.BuildRomFs)
        {
            PatchItemData(options, results, notes);
            PatchItemText(options, results, notes);
            PatchSourceItemAcquisitionData(options, results, notes);
        }

        if (options.GrantOnBagEvent)
            PatchBagEventScript(options, results, notes);

        if (options.BuildExeFs)
            PatchExeFsMain(options, results, notes);

        WriteMarkerFile(options);
        results.Add(new("Pass", "Output", MarkerFileName, "Generated Royal Sword marker file."));
        return new(results, notes);
    }

    public static RoyalCandyBuildSummary Uninstall(RoyalCandyBuildOptions options)
    {
        var results = new List<BuildResult>();
        var notes = new List<string>
        {
            "Royal Sword Candy Uninstall",
            "===========================",
            "",
            $"Output: {options.OutputPath}",
            $"Game: {options.GameFlavor}",
            "",
        };

        var preflight = AnalyzeUninstallPreflight(options);
        if (!preflight.Passed)
            throw new InvalidOperationException(preflight.Message);

        results.AddRange(preflight.Results);
        notes.AddRange(preflight.Notes.Where(z => z.StartsWith("- ", StringComparison.Ordinal)));

        var changedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removedFiles = 0;
        foreach (var relativePath in EnumerateRoyalCandyOwnedOutputRelativePaths(options).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!DeleteOutputFile(options.OutputPath, relativePath, changedDirectories))
                continue;

            removedFiles++;
            results.Add(new("Pass", "Uninstall", relativePath, "Removed Royal Candy output file."));
            notes.Add($"- Removed {relativePath}.");
        }

        var restoredRecords = RestoreRoyalCandyRomFsToVanilla(options, results, notes, changedDirectories);
        var identicalFilesRemoved = RemoveLayeredFilesMatchingBaseDump(options, results, notes, changedDirectories);

        var removedDirectories = PruneEmptyOutputDirectories(options.OutputPath, changedDirectories);
        results.Add(new("Pass", "Uninstall", options.OutputPath, $"Removed {removedFiles:N0} Royal Candy-owned file(s), restored {restoredRecords:N0} RomFS record(s), removed {identicalFilesRemoved:N0} vanilla-identical layered file(s), and removed {removedDirectories:N0} empty folder(s)."));
        notes.Add($"- Removed {removedFiles:N0} Royal Candy-owned file(s), restored {restoredRecords:N0} RomFS record(s), removed {identicalFilesRemoved:N0} vanilla-identical layered file(s), and removed {removedDirectories:N0} empty folder(s).");
        return new(results, notes);
    }

    public static RoyalCandyBuildPreflight AnalyzeBuildPreflight(RoyalCandyBuildOptions options)
    {
        var results = new List<BuildResult>();
        var notes = new List<string>
        {
            "Royal Candy preflight",
            "=====================",
            "",
            $"LayeredFS output root: {options.OutputPath}",
            "Existing files in the output root take priority over the base RomFS/ExeFS dump when the builder reads source data.",
            "",
        };

        var signatureScan = AnalyzeLayeredExeFsMain(options);
        if (signatureScan is not null)
        {
            AddExeFsSignatureScanResult(signatureScan, options.BuildExeFs, results, notes);

            if (signatureScan.InstalledRoyalCandy is not null)
            {
                var existingRoyalCandy = signatureScan.InstalledRoyalCandy;
                var message = $"Existing Royal Candy patch detected in the selected mod folder: {existingRoyalCandy.Description}. Remove that Royal Candy output before building a new one.";
                results.Add(new("Fail", "Preflight", "exefs/main", message));
                notes.Add("- " + message);
                notes.AddRange(existingRoyalCandy.Details.Select(detail => "  - " + detail));
                return new(false, message, results, notes, existingRoyalCandy, signatureScan);
            }

            if (options.BuildExeFs && signatureScan.HasUnknownChanges)
            {
                var message = "Unknown ExeFS mod detected in the selected mod folder. Add a signature for this edit or remove the layered exefs/main before building Royal Candy.";
                results.Add(new("Fail", "Preflight", "exefs/main", message));
                notes.Add("- " + message);
                return new(false, message, results, notes, null, signatureScan);
            }
        }

        if (!options.BuildExeFs)
        {
            results.Add(new("Pass", "Preflight", "ExeFS", "ExeFS output is disabled for this build."));
            notes.Add("- ExeFS output disabled; conflict check skipped.");
            return new(true, "Preflight passed.", results, notes, null, signatureScan);
        }

        var overlayMainPath = GetLayeredFsExeFsPath(options, "main");
        var sourceMainPath = ResolveExeFsSourcePath(options, "main");
        var usingOverlayMain = File.Exists(overlayMainPath);
        if (!File.Exists(sourceMainPath))
        {
            var message = "Could not find exefs/main in either the selected mod folder or the base ExeFS dump.";
            results.Add(new("Fail", "Preflight", "exefs/main", message));
            notes.Add("- " + message);
            return new(false, message, results, notes, null, signatureScan);
        }

        try
        {
            _ = CreatePatchedExeFsMain(File.ReadAllBytes(sourceMainPath), options, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            var message = usingOverlayMain
                ? $"Existing exefs/main in the selected mod folder conflicts with the Royal Candy ExeFS patch: {ex.Message}"
                : $"Base exefs/main is not compatible with the Royal Candy ExeFS patch: {ex.Message}";
            results.Add(new("Fail", "Preflight", "exefs/main", message));
            notes.Add("- " + message);
            return new(false, message, results, notes, null, signatureScan);
        }

        var passMessage = usingOverlayMain
            ? "Existing exefs/main overlay found; Royal Candy patch points are still compatible."
            : "No exefs/main overlay found; Royal Candy will patch the base ExeFS main.";
        results.Add(new("Pass", "Preflight", "exefs/main", passMessage));
        notes.Add("- " + passMessage);
        return new(true, "Preflight passed.", results, notes, null, signatureScan);
    }

    public static RoyalCandyBuildPreflight AnalyzeUninstallPreflight(RoyalCandyBuildOptions options)
    {
        var results = new List<BuildResult>();
        var notes = new List<string>
        {
            "Royal Candy uninstall preflight",
            "===============================",
            "",
            $"LayeredFS output root: {options.OutputPath}",
            "Uninstall runs when layered exefs/main matches a registered Royal Candy signature, or when orphaned Royal Candy RomFS output is detected after ExeFS was already removed.",
            "",
        };

        var signatureScan = AnalyzeLayeredExeFsMain(options);
        if (signatureScan is null)
        {
            var orphanedRomFs = DetectRoyalCandyRomFsLeftovers(options, notes);
            if (orphanedRomFs)
            {
                var orphanPassMessage = "No Royal Candy ExeFS remains, but orphaned Royal Candy RomFS output was detected. Uninstall can clean those leftover files.";
                results.Add(new("Pass", "Uninstall", "romfs", orphanPassMessage));
                notes.Add("- " + orphanPassMessage);
                return new(true, orphanPassMessage, results, notes);
            }

            var message = "No layered exefs/main or Royal Candy RomFS leftovers were found in the selected mod folder.";
            results.Add(new("Fail", "Uninstall", "exefs/main", message));
            notes.Add("- " + message);
            return new(false, message, results, notes);
        }

        AddExeFsSignatureScanResult(signatureScan, false, results, notes, failOnInstalledRoyalCandy: false);
        if (!signatureScan.Valid)
        {
            var message = "Layered exefs/main could not be scanned as a valid NSO.";
            results.Add(new("Fail", "Uninstall", "exefs/main", message));
            notes.Add("- " + message);
            return new(false, message, results, notes, null, signatureScan);
        }

        if (signatureScan.InstalledRoyalCandy is not { } installedRoyalCandy)
        {
            var message = signatureScan.HasUnknownChanges
                ? "Unknown ExeFS mod detected in the selected mod folder. Uninstall is blocked until this edit has a registered signature."
                : "No registered Royal Candy ExeFS signature was detected in the selected mod folder.";
            results.Add(new("Fail", "Uninstall", "exefs/main", message));
            notes.Add("- " + message);
            return new(false, message, results, notes, null, signatureScan);
        }

        var passMessage = $"Detected {installedRoyalCandy.Description}. Uninstall can remove the Royal Candy LayeredFS output files.";
        results.Add(new("Pass", "Uninstall", "exefs/main", passMessage));
        notes.Add("- " + passMessage);
        notes.AddRange(installedRoyalCandy.Details.Select(detail => "  - " + detail));
        return new(true, passMessage, results, notes, installedRoyalCandy, signatureScan);
    }

    private static bool DetectRoyalCandyRomFsLeftovers(RoyalCandyBuildOptions options, List<string> notes)
    {
        var detections = new List<string>();
        TryDetectRoyalCandyItemDataLeftover(options, detections);
        TryDetectRoyalCandyTextLeftovers(options, detections);
        TryDetectRoyalCandyShopLeftover(options, detections);
        TryDetectRoyalCandyGfPackLeftover(options, NestDataPath, "raid reward", detections);
        TryDetectRoyalCandyGfPackLeftover(options, PlacementPath, "placement", detections);

        if (detections.Count == 0)
            return false;

        notes.Add("- Orphaned Royal Candy RomFS leftovers detected:");
        notes.AddRange(detections.Select(detection => "  - " + detection));
        return true;
    }

    private static void TryDetectRoyalCandyItemDataLeftover(RoyalCandyBuildOptions options, List<string> detections)
    {
        var layeredPath = GetLayeredFsRomFsPath(options, ItemPath);
        var basePath = GetBaseRomFsPath(options, ItemPath);
        if (!File.Exists(layeredPath) || !File.Exists(basePath) || FilesAreEqual(layeredPath, basePath))
            return;

        try
        {
            var currentBytes = File.ReadAllBytes(layeredPath);
            var baseBytes = File.ReadAllBytes(basePath);
            if (BinaryPrimitives.ReadUInt16LittleEndian(currentBytes.AsSpan(4, 2)) > BinaryPrimitives.ReadUInt16LittleEndian(baseBytes.AsSpan(4, 2)))
            {
                detections.Add("item.dat has an appended raw item row from Royal Candy output.");
                return;
            }

            var currentItems = Item8.GetArray(currentBytes);
            var baseItems = Item8.GetArray(baseBytes);
            if ((uint)options.ItemId >= (uint)currentItems.Length || (uint)options.ItemId >= (uint)baseItems.Length)
                return;

            if (!currentItems[options.ItemId].Data.SequenceEqual(baseItems[options.ItemId].Data)
                && IsRoyalCandyItemShape(currentItems[options.ItemId], baseItems[RareCandyItemId]))
            {
                detections.Add($"item.dat still has Royal Candy-shaped item data for item {options.ItemId}.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            detections.Add($"item.dat exists but could not be fully inspected: {ex.Message}");
        }
    }

    private static bool IsRoyalCandyItemShape(Item8 item, Item8 rareCandy) =>
        item.Price == 1
        && item.Pouch == Item8.PouchID.Key
        && item.EffectField == FieldItemType.Medicine
        && item.CanUseOnPokemon
        && item.ItemType == KeyItemTypeByte
        && item.LevelUp
        && item.ItemSprite == rareCandy.ItemSprite
        && item.GroupType == Item8.GroupIndexType.None;

    private static void TryDetectRoyalCandyTextLeftovers(RoyalCandyBuildOptions options, List<string> detections)
    {
        foreach (var language in EnumerateMessageLanguages(options))
        {
            foreach (var fileName in EnumerateMessageFileNames(options, language, "itemname*.dat"))
                TryDetectRoyalCandyTextLeftover(options, $"{MessageRoot}/{language}/common/{fileName}", options.ItemId, detections);

            TryDetectRoyalCandyTextLeftover(options, $"{MessageRoot}/{language}/common/{ItemInfoFile}", options.ItemId, detections);
        }
    }

    private static void TryDetectRoyalCandyTextLeftover(RoyalCandyBuildOptions options, string relativePath, int lineIndex, List<string> detections)
    {
        var layeredPath = GetLayeredFsRomFsPath(options, relativePath);
        var basePath = GetBaseRomFsPath(options, relativePath);
        if (!File.Exists(layeredPath) || !File.Exists(basePath) || FilesAreEqual(layeredPath, basePath))
            return;

        try
        {
            var current = new TextFile(File.ReadAllBytes(layeredPath), new TextConfig(GameVersion.SW), remapChars: true) { SETEMPTYTEXT = false };
            var baseline = new TextFile(File.ReadAllBytes(basePath), new TextConfig(GameVersion.SW), remapChars: true) { SETEMPTYTEXT = false };
            if ((uint)lineIndex >= (uint)current.Lines.Length || (uint)lineIndex >= (uint)baseline.Lines.Length)
                return;

            var currentLine = current.Lines[lineIndex];
            if (TextContentMatchesVanilla(current.Lines, current.Flags, baseline.Lines, baseline.Flags)
                || string.Equals(currentLine, RoyalCandyName, StringComparison.Ordinal)
                || string.Equals(currentLine, RoyalCandyPluralName, StringComparison.Ordinal)
                || string.Equals(currentLine, options.ItemDescription, StringComparison.Ordinal))
            {
                detections.Add($"{relativePath} has Royal Candy or vanilla-equivalent item text output.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            detections.Add($"{relativePath} exists but could not be fully inspected: {ex.Message}");
        }
    }

    private static void TryDetectRoyalCandyShopLeftover(RoyalCandyBuildOptions options, List<string> detections)
    {
        var layeredPath = GetLayeredFsRomFsPath(options, ShopPath);
        var basePath = GetBaseRomFsPath(options, ShopPath);
        if (!File.Exists(layeredPath) || !File.Exists(basePath) || FilesAreEqual(layeredPath, basePath))
            return;

        try
        {
            var current = FlatBufferConverter.DeserializeFrom<SwShShopInventory>(File.ReadAllBytes(layeredPath));
            var baseline = FlatBufferConverter.DeserializeFrom<SwShShopInventory>(File.ReadAllBytes(basePath));
            if (current.SerializeFrom().SequenceEqual(baseline.SerializeFrom()))
                detections.Add("shop_data.bin is vanilla-equivalent Royal Candy cleanup output.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            detections.Add($"shop_data.bin exists but could not be fully inspected: {ex.Message}");
        }
    }

    private static void TryDetectRoyalCandyGfPackLeftover(RoyalCandyBuildOptions options, string relativePath, string label, List<string> detections)
    {
        var layeredPath = GetLayeredFsRomFsPath(options, relativePath);
        var basePath = GetBaseRomFsPath(options, relativePath);
        if (!File.Exists(layeredPath) || !File.Exists(basePath) || FilesAreEqual(layeredPath, basePath))
            return;

        try
        {
            var currentBytes = File.ReadAllBytes(layeredPath);
            var baseBytes = File.ReadAllBytes(basePath);
            if (relativePath == NestDataPath
                && (currentBytes.SequenceEqual(CreateRoyalCandyRaidCleanupBytes(baseBytes, options.ItemId))
                    || currentBytes.SequenceEqual(CreateRestoredRoyalCandyRaidCleanupBytes(baseBytes, options.ItemId))))
            {
                detections.Add($"{relativePath} matches Royal Candy {label} cleanup output.");
                return;
            }

            if (relativePath == PlacementPath)
            {
                var hashes = ReadItemHashes(options);
                if (hashes.TryGetValue(options.ItemId, out var sourceItemHash) && hashes.TryGetValue(RareCandyItemId, out var rareCandyHash)
                    && (currentBytes.SequenceEqual(CreateRoyalCandyPlacementCleanupBytes(baseBytes, sourceItemHash, rareCandyHash, options.ItemId))
                        || currentBytes.SequenceEqual(CreateRestoredRoyalCandyPlacementCleanupBytes(baseBytes, sourceItemHash, rareCandyHash, options.ItemId))))
                {
                    detections.Add($"{relativePath} matches Royal Candy {label} cleanup output.");
                    return;
                }
            }

            var current = new GFPack(currentBytes);
            var baseline = new GFPack(baseBytes);
            if (current.Write().SequenceEqual(baseline.Write()))
                detections.Add($"{relativePath} is vanilla-equivalent Royal Candy {label} cleanup output.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or IOException or UnauthorizedAccessException)
        {
            detections.Add($"{relativePath} exists but could not be fully inspected: {ex.Message}");
        }
    }

    private static void AddExeFsSignatureScanResult(RoyalSwordExeFsSignatureScan scan, bool buildingExeFs, List<BuildResult> results, List<string> notes, bool failOnInstalledRoyalCandy = true)
    {
        var status = !scan.Valid || failOnInstalledRoyalCandy && scan.InstalledRoyalCandy is not null || buildingExeFs && scan.HasUnknownChanges
            ? "Fail"
            : scan.Matches.Count != 0
                ? "Pass"
                : "Info";
        results.Add(new(status, "Preflight", "exefs/main", "ExeFS signature scan: " + scan.Summary));
        notes.Add("- ExeFS signature scan: " + scan.Summary);
        foreach (var detail in scan.Details)
            notes.Add("  - " + detail.TrimStart());
    }

    private static void PatchItemData(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        var sourcePath = ResolveRomFsSourcePath(options, ItemPath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Could not find Sword/Shield item table.", sourcePath);

        var sourceDescription = DescribeLayeredSource(options, "romfs", ItemPath);
        var itemData = File.ReadAllBytes(sourcePath);
        var items = Item8.GetArray(itemData);
        if ((uint)options.ItemId >= (uint)items.Length)
            throw new ArgumentOutOfRangeException(nameof(options.ItemId), $"Item id {options.ItemId} is outside the item table.");
        if ((uint)options.TemplateItemId >= (uint)items.Length)
            throw new ArgumentOutOfRangeException(nameof(options.TemplateItemId), $"Template item id {options.TemplateItemId} is outside the item table.");

        var template = items[options.TemplateItemId];
        var rareCandy = items[RareCandyItemId];
        var target = items[options.ItemId];
        var originalTarget = target.Data.ToArray();
        var sharedIds = GetIdsSharingEntry(itemData, options.ItemId).ToArray();

        template.Data.CopyTo(target.Data);
        target.Price = 1;
        target.PriceWatts = 0;
        target.PriceAlternate = template.PriceAlternate;
        target.Pouch = Item8.PouchID.Key;
        target.PouchFlags = rareCandy.PouchFlags;
        target.EffectField = FieldItemType.Medicine;
        target.CanUseOnPokemon = true;
        target.ItemType = KeyItemTypeByte;
        target.LevelUp = true;
        target.ItemSprite = rareCandy.ItemSprite;
        target.GroupType = Item8.GroupIndexType.None;
        target.GroupIndex = 0;
        target.SortIndex = template.SortIndex;

        var patched = AppendUniqueItemRow(itemData, options.ItemId, target.Data);
        WriteOutputBytes(options.OutputPath, "romfs/" + ItemPath, patched);

        results.Add(new("Pass", "RomFS", "romfs/" + ItemPath, "Royal Candy item row generated."));
        notes.Add($"- Generated romfs/{ItemPath} from item {options.TemplateItemId} into item {options.ItemId}; source was {sourceDescription}; vanilla candidate bytes {Convert.ToHexString(originalTarget)}; template bytes {Convert.ToHexString(template.Data.ToArray())}; shared row ids {FormatSharedIds(sharedIds, options.ItemId)}.");
    }

    private static void PatchItemText(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        var patchedFiles = 0;
        foreach (var language in EnumerateMessageLanguages(options))
        {
            foreach (var fileName in EnumerateMessageFileNames(options, language, "itemname*.dat"))
            {
                var replacement = fileName.Contains("plural", StringComparison.OrdinalIgnoreCase)
                    ? RoyalCandyPluralName
                    : RoyalCandyName;
                if (PatchOneTextFile(options, $"{MessageRoot}/{language}/common/{fileName}", options.ItemId, replacement))
                    patchedFiles++;
            }

            if (PatchOneTextFile(options, $"{MessageRoot}/{language}/common/{ItemInfoFile}", options.ItemId, options.ItemDescription))
                patchedFiles++;
        }

        results.Add(new(patchedFiles == 0 ? "Warning" : "Pass", "RomFS", "romfs/bin/message", $"Patched {patchedFiles:N0} item text file(s)."));
        notes.Add($"- Patched {patchedFiles:N0} item name/description message files.");
    }

    private static bool PatchOneTextFile(RoyalCandyBuildOptions options, string relativePath, int lineIndex, string replacement)
    {
        var sourcePath = ResolveRomFsSourcePath(options, relativePath);
        if (!File.Exists(sourcePath))
            return false;

        var text = new TextFile(File.ReadAllBytes(sourcePath), new TextConfig(GameVersion.SW), remapChars: true)
        {
            SETEMPTYTEXT = false,
        };
        var lines = text.Lines;
        var flags = text.Flags;
        if ((uint)lineIndex >= (uint)lines.Length)
            throw new IndexOutOfRangeException($"{Path.GetFileName(relativePath)} has {lines.Length} lines; cannot patch line {lineIndex}.");

        lines[lineIndex] = replacement;
        var patched = new TextFile(new TextConfig(GameVersion.SW), remapChars: true)
        {
            SETEMPTYTEXT = false,
            Lines = lines,
            Flags = flags,
        };

        WriteOutputBytes(options.OutputPath, "romfs/" + relativePath, patched.Data);
        return true;
    }

    private static void PatchSourceItemAcquisitionData(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        var cleanupNotes = new List<string>
        {
            "Royal Candy source acquisition cleanup",
            "======================================",
            "",
            $"Repurposed source item id: {options.ItemId}",
            $"Replacement item id: {RareCandyItemId} (regular Rare Candy)",
            "",
        };

        var shopRemovals = PatchSourceShopData(options, results, cleanupNotes);
        var raidReplacements = PatchSourceRaidRewards(options, results, cleanupNotes);
        var placementReplacements = PatchSourcePlacementItems(options, results, cleanupNotes);
        var totalChanges = shopRemovals + raidReplacements + placementReplacements;

        results.Add(new(totalChanges == 0 ? "Warning" : "Pass", "RomFS", "romfs", $"Cleaned {totalChanges:N0} vanilla acquisition entr{(totalChanges == 1 ? "y" : "ies")} for source item {options.ItemId}."));
        notes.Add(totalChanges == 0
            ? $"- Source acquisition cleanup found no vanilla item {options.ItemId} entries to change."
            : $"- Cleaned {totalChanges:N0} vanilla item {options.ItemId} acquisition entr{(totalChanges == 1 ? "y" : "ies")}; hidden pickups and raid rewards become regular Rare Candy.");
        notes.AddRange(cleanupNotes.Where(z => z.StartsWith("- ", StringComparison.Ordinal)));
    }

    private static int PatchSourceShopData(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> cleanupNotes)
    {
        var sourcePath = ResolveRomFsSourcePath(options, ShopPath);
        if (!File.Exists(sourcePath))
        {
            results.Add(new("Warning", "RomFS", "romfs/" + ShopPath, "shop_data.bin was not found; shop cleanup skipped."));
            cleanupNotes.Add("- Shop cleanup skipped because shop_data.bin was not found.");
            return 0;
        }

        var shop = FlatBufferConverter.DeserializeFrom<SwShShopInventory>(File.ReadAllBytes(sourcePath));
        var removals = 0;

        foreach (var single in shop.Single)
            removals += RemoveFromInventory(single.Inventories.Items, options.ItemId, cleanupNotes, $"single shop 0x{single.Hash:X16}");
        foreach (var multi in shop.Multi)
        {
            for (var i = 0; i < multi.Inventories.Count; i++)
                removals += RemoveFromInventory(multi.Inventories[i].Items, options.ItemId, cleanupNotes, $"multi shop 0x{multi.Hash:X16} inventory {i}");
        }

        if (removals != 0)
            WriteOutputBytes(options.OutputPath, "romfs/" + ShopPath, shop.SerializeFrom());

        results.Add(new(removals == 0 ? "Info" : "Pass", "RomFS", "romfs/" + ShopPath, removals == 0 ? "No source item shop entries found." : $"Removed {removals:N0} source item shop entr{(removals == 1 ? "y" : "ies")}."));
        return removals;
    }

    private static int PatchSourceRaidRewards(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> cleanupNotes)
    {
        var sourcePath = ResolveRomFsSourcePath(options, NestDataPath);
        if (!File.Exists(sourcePath))
        {
            results.Add(new("Warning", "RomFS", "romfs/" + NestDataPath, "data_table.gfpak was not found; raid reward cleanup skipped."));
            cleanupNotes.Add("- Raid reward cleanup skipped because data_table.gfpak was not found.");
            return 0;
        }

        var dataTable = new GFPack(File.ReadAllBytes(sourcePath));
        var replacements = 0;
        replacements += ReplaceNestRewardItems(dataTable, "nest_hole_drop_rewards.bin", options.ItemId, RareCandyItemId, cleanupNotes);
        replacements += ReplaceNestRewardItems(dataTable, "nest_hole_bonus_rewards.bin", options.ItemId, RareCandyItemId, cleanupNotes);

        if (replacements != 0)
            WriteOutputBytes(options.OutputPath, "romfs/" + NestDataPath, dataTable.Write());

        results.Add(new(replacements == 0 ? "Info" : "Pass", "RomFS", "romfs/" + NestDataPath, replacements == 0 ? "No source item raid reward entries found." : $"Replaced {replacements:N0} raid reward entr{(replacements == 1 ? "y" : "ies")} with regular Rare Candy."));
        return replacements;
    }

    private static int ReplaceNestRewardItems(GFPack dataTable, string fileName, int sourceItemId, int replacementItemId, List<string> cleanupNotes)
    {
        if (dataTable.GetIndexFileName(fileName) < 0)
        {
            cleanupNotes.Add($"- Raid reward file not found: {fileName}");
            return 0;
        }

        var archive = FlatBufferConverter.DeserializeFrom<NestHoleRewardArchive>(dataTable.GetDataFileName(fileName));
        var replacements = 0;
        foreach (var (table, tableIndex) in archive.Table.Select((table, index) => (table, index)))
        {
            foreach (var reward in table.Entries)
            {
                if (reward.ItemID != sourceItemId)
                    continue;

                reward.ItemID = (uint)replacementItemId;
                replacements++;
                cleanupNotes.Add($"- {fileName}: table {tableIndex} [0x{table.TableID:X16}] entry {reward.EntryID} now awards regular Rare Candy.");
            }
        }

        if (replacements != 0)
            dataTable.SetDataFileName(fileName, archive.SerializeFrom());

        return replacements;
    }

    private static int PatchSourcePlacementItems(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> cleanupNotes)
    {
        var placementPath = ResolveRomFsSourcePath(options, PlacementPath);
        if (!File.Exists(placementPath))
        {
            results.Add(new("Warning", "RomFS", "romfs/" + PlacementPath, "placement.gfpak was not found; placement item cleanup skipped."));
            cleanupNotes.Add("- Placement item cleanup skipped because placement.gfpak was not found.");
            return 0;
        }

        var hashes = ReadItemHashes(options);
        if (!hashes.TryGetValue(options.ItemId, out var sourceItemHash))
            throw new InvalidOperationException($"Could not find item hash for source item {options.ItemId}.");
        if (!hashes.TryGetValue(RareCandyItemId, out var rareCandyHash))
            throw new InvalidOperationException("Could not find item hash for regular Rare Candy.");

        var placement = new GFPack(File.ReadAllBytes(placementPath));
        var replacements = ReplacePlacementItems(placement, sourceItemHash, rareCandyHash, (uint)options.ItemId, cleanupNotes);

        if (replacements != 0)
            WriteOutputBytes(options.OutputPath, "romfs/" + PlacementPath, placement.Write());

        results.Add(new(replacements == 0 ? "Info" : "Pass", "RomFS", "romfs/" + PlacementPath, replacements == 0 ? "No source item placement pickups found." : $"Replaced {replacements:N0} placement pickup entr{(replacements == 1 ? "y" : "ies")} with regular Rare Candy."));
        return replacements;
    }

    private static int ReplacePlacementItems(GFPack placement, ulong sourceItemHash, ulong rareCandyHash, uint itemId, List<string> cleanupNotes)
    {
        var areaNames = new AHTB(placement.GetDataFileName("AreaNameHashTable.tbl")).ToDictionary();
        var zoneNames = TryReadAhtbDictionary(placement, "ZoneNameHashTable.tbl");
        var replacements = 0;
        foreach (var areaName in areaNames.Values.OrderBy(z => z, StringComparer.Ordinal))
        {
            var fileName = $"{areaName}.bin";
            if (placement.GetIndexFileName(fileName) < 0)
                continue;

            var archive = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(placement.GetDataFileName(fileName));
            var areaChanged = false;
            foreach (var (zone, zoneIndex) in archive.Table.Select((zone, index) => (zone, index)))
            {
                var zoneName = zoneNames.TryGetValue(zone.Meta.ZoneID, out var knownZone)
                    ? knownZone
                    : zone.Meta.ZoneID.ToString("X16");

                foreach (var (fieldItem, fieldItemIndex) in zone.FieldItems.Select((item, index) => (item.Field00, index)))
                {
                    var flagReplacements = ReplaceUlongListValue(fieldItem.Flags, sourceItemHash, rareCandyHash);
                    var itemReplacements = ReplaceUintListValue(fieldItem.Items, itemId, RareCandyItemId);
                    var changed = flagReplacements + itemReplacements;
                    if (changed == 0)
                        continue;

                    replacements += changed;
                    areaChanged = true;
                    cleanupNotes.Add($"- {fileName}: {zoneName} field item {fieldItemIndex} now points to regular Rare Candy.");
                }

                foreach (var (hiddenItem, hiddenItemIndex) in zone.HiddenItems.Select((item, index) => (item.Field00, index)))
                {
                    for (var chanceIndex = 0; chanceIndex < hiddenItem.Field02.Count; chanceIndex++)
                    {
                        var chance = hiddenItem.Field02[chanceIndex];
                        if (chance.Hash != sourceItemHash)
                            continue;

                        chance.Hash = rareCandyHash;
                        replacements++;
                        areaChanged = true;
                        cleanupNotes.Add($"- {fileName}: {zoneName} hidden item {hiddenItemIndex} chance {chanceIndex} now points to regular Rare Candy; quantity/chance preserved.");
                    }
                }
            }

            if (areaChanged)
                placement.SetDataFileName(fileName, archive.SerializeFrom());
        }

        return replacements;
    }

    private static int RemoveFromInventory(IList<int> items, int itemId, List<string> cleanupNotes, string label)
    {
        var removals = 0;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] != itemId)
                continue;

            items.RemoveAt(i);
            removals++;
        }

        if (removals != 0)
            cleanupNotes.Add($"- Removed {removals:N0} source item entr{(removals == 1 ? "y" : "ies")} from {label}.");

        return removals;
    }

    private static int ReplaceUlongListValue(IList<ulong> values, ulong source, ulong replacement)
    {
        var replacements = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] != source)
                continue;

            values[i] = replacement;
            replacements++;
        }

        return replacements;
    }

    private static int ReplaceUintListValue(IList<uint> values, uint source, uint replacement)
    {
        var replacements = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] != source)
                continue;

            values[i] = replacement;
            replacements++;
        }

        return replacements;
    }

    private static void PatchBagEventScript(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        if (options.ItemId != RoyalCandyItemId)
            throw new InvalidOperationException("The Royal Candy Bag-event grant currently targets item id 1128 only.");

        var sourcePath = ResolveRomFsSourcePath(options, RoyalSwordScriptAmxPatcher.BagEventScriptPath);
        var patchNotes = RoyalSwordScriptAmxPatcher.PatchBagEventRoyalCandyGrant(sourcePath, options.OutputPath, options.ItemId);

        results.Add(new("Pass", "RomFS", "romfs/bin/script/amx/main_event_0020.amx", "Bag pickup script grant generated."));
        notes.AddRange(patchNotes.Where(z => z.StartsWith("- ", StringComparison.Ordinal)));
    }

    private static int RestoreRoyalCandyRomFsToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        var restored = 0;
        restored += RestoreItemDataToVanilla(options, results, notes, changedDirectories);
        restored += RestoreItemTextToVanilla(options, results, notes, changedDirectories);
        restored += RestoreSourceAcquisitionDataToVanilla(options, results, notes, changedDirectories);
        restored += RestoreBagEventScriptToVanilla(options, results, notes, changedDirectories);
        return restored;
    }

    private static int RestoreItemDataToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        const string outputRelativePath = "romfs/" + ItemPath;
        var layeredPath = GetLayeredFsRomFsPath(options, ItemPath);
        if (!File.Exists(layeredPath))
        {
            results.Add(new("Info", "Uninstall", outputRelativePath, "No layered item table found."));
            return 0;
        }

        var basePath = GetBaseRomFsPath(options, ItemPath);
        if (!File.Exists(basePath))
            throw new FileNotFoundException("Could not find base item table for Royal Candy uninstall.", basePath);

        var current = File.ReadAllBytes(layeredPath);
        var baseline = File.ReadAllBytes(basePath);
        if (current.SequenceEqual(baseline))
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed item table overlay that already matched vanilla."));
            notes.Add($"- Removed {outputRelativePath}; it already matched the base dump.");
            return 0;
        }

        var baseIndex = GetItemEntryIndex(baseline, options.ItemId);
        var currentIndex = GetItemEntryIndex(current, options.ItemId);
        var restoredRecords = 0;
        if (currentIndex != baseIndex)
        {
            var indexOffset = GetItemEntryIndexOffset(options.ItemId);
            BinaryPrimitives.WriteUInt16LittleEndian(current.AsSpan(indexOffset, 2), baseIndex);
            restoredRecords++;
            notes.Add($"- Restored item {options.ItemId} table index from raw row {currentIndex} to vanilla raw row {baseIndex}.");
        }

        if (RestoreItemRawRowToVanilla(current, baseline, baseIndex))
        {
            restoredRecords++;
            notes.Add($"- Restored item {options.ItemId} vanilla raw row {baseIndex} bytes.");
        }

        current = TryTrimRoyalCandyAppendedItemRow(current, baseline, currentIndex, baseIndex, options.ItemId, notes, ref restoredRecords);
        if (restoredRecords == 0)
        {
            results.Add(new("Info", "Uninstall", outputRelativePath, "No Royal Candy item table data needed restoration."));
            return 0;
        }

        CommitLayeredRestore(options.OutputPath, outputRelativePath, current, baseline, changedDirectories, notes);
        results.Add(new("Pass", "Uninstall", outputRelativePath, $"Restored {restoredRecords:N0} Royal Candy item table record(s) to vanilla."));
        return restoredRecords;
    }

    private static bool RestoreItemRawRowToVanilla(byte[] current, byte[] baseline, ushort rowIndex)
    {
        var currentOffset = GetItemRawRowOffset(current, rowIndex);
        var baselineOffset = GetItemRawRowOffset(baseline, rowIndex);
        var currentRow = current.AsSpan(currentOffset, ItemRawRowSize);
        var baselineRow = baseline.AsSpan(baselineOffset, ItemRawRowSize);
        if (currentRow.SequenceEqual(baselineRow))
            return false;

        baselineRow.CopyTo(currentRow);
        return true;
    }

    private static byte[] TryTrimRoyalCandyAppendedItemRow(byte[] current, byte[] baseline, ushort currentIndex, ushort baseIndex, int itemId, List<string> notes, ref int restoredRecords)
    {
        if (currentIndex == baseIndex)
            return current;

        var currentRawCount = BinaryPrimitives.ReadUInt16LittleEndian(current.AsSpan(4, 2));
        var baseRawCount = BinaryPrimitives.ReadUInt16LittleEndian(baseline.AsSpan(4, 2));
        if (currentIndex != currentRawCount - 1 || currentRawCount <= baseRawCount)
            return current;

        var itemCount = BinaryPrimitives.ReadUInt16LittleEndian(current);
        for (var i = 0; i < itemCount; i++)
        {
            if (i == itemId)
                continue;
            if (GetItemEntryIndex(current, i) == currentIndex)
                return current;
        }

        var rowLength = ItemRawRowSize;
        if (rowLength <= 0 || current.Length < baseline.Length + rowLength)
            return current;

        var trimmed = current[..^rowLength];
        BinaryPrimitives.WriteUInt16LittleEndian(trimmed.AsSpan(4, 2), (ushort)(currentRawCount - 1));
        restoredRecords++;
        notes.Add($"- Removed unused Royal Candy raw item row {currentIndex} from the end of the item table.");
        return trimmed;
    }

    private static int RestoreItemTextToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        var restoredFiles = 0;
        foreach (var language in EnumerateMessageLanguages(options))
        {
            foreach (var fileName in EnumerateMessageFileNames(options, language, "itemname*.dat"))
                restoredFiles += RestoreOneTextFileLineToVanilla(options, $"{MessageRoot}/{language}/common/{fileName}", options.ItemId, results, notes, changedDirectories);

            restoredFiles += RestoreOneTextFileLineToVanilla(options, $"{MessageRoot}/{language}/common/{ItemInfoFile}", options.ItemId, results, notes, changedDirectories);
        }

        results.Add(new(restoredFiles == 0 ? "Info" : "Pass", "Uninstall", "romfs/bin/message", restoredFiles == 0 ? "No layered Royal Candy item text needed restoration." : $"Restored {restoredFiles:N0} item text file(s) to vanilla line data."));
        return restoredFiles;
    }

    private static int RestoreOneTextFileLineToVanilla(RoyalCandyBuildOptions options, string relativePath, int lineIndex, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        var outputRelativePath = "romfs/" + relativePath;
        var layeredPath = GetLayeredFsRomFsPath(options, relativePath);
        if (!File.Exists(layeredPath))
            return 0;

        var basePath = GetBaseRomFsPath(options, relativePath);
        if (!File.Exists(basePath))
        {
            results.Add(new("Warning", "Uninstall", outputRelativePath, "Base text file was not found; leaving layered text unchanged."));
            return 0;
        }

        var currentBytes = File.ReadAllBytes(layeredPath);
        var baseBytes = File.ReadAllBytes(basePath);
        if (currentBytes.SequenceEqual(baseBytes))
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed text overlay that already matched vanilla."));
            notes.Add($"- Removed {outputRelativePath}; it already matched the base dump.");
            return 0;
        }

        var current = new TextFile(currentBytes, new TextConfig(GameVersion.SW), remapChars: true) { SETEMPTYTEXT = false };
        var baseline = new TextFile(baseBytes, new TextConfig(GameVersion.SW), remapChars: true) { SETEMPTYTEXT = false };
        var normalizedBaseBytes = CreateTextFileData(baseline.Lines, baseline.Flags);
        if ((uint)lineIndex >= (uint)current.Lines.Length || (uint)lineIndex >= (uint)baseline.Lines.Length)
        {
            results.Add(new("Warning", "Uninstall", outputRelativePath, $"Line {lineIndex} is outside the current or base text file; leaving layered text unchanged."));
            return 0;
        }

        var lines = current.Lines;
        var flags = current.Flags;
        var changed = !string.Equals(lines[lineIndex], baseline.Lines[lineIndex], StringComparison.Ordinal);
        if ((uint)lineIndex < (uint)flags.Length && (uint)lineIndex < (uint)baseline.Flags.Length && flags[lineIndex] != baseline.Flags[lineIndex])
        {
            flags[lineIndex] = baseline.Flags[lineIndex];
            changed = true;
        }

        if (!changed)
        {
            if (TextContentMatchesVanilla(lines, flags, baseline.Lines, baseline.Flags) || currentBytes.SequenceEqual(normalizedBaseBytes))
            {
                DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
                results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed text overlay that matched vanilla line data."));
                notes.Add($"- Removed {outputRelativePath}; parsed text matched vanilla line data.");
            }

            return 0;
        }

        lines[lineIndex] = baseline.Lines[lineIndex];
        if (TextContentMatchesVanilla(lines, flags, baseline.Lines, baseline.Flags))
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            notes.Add($"- Removed {outputRelativePath}; Royal Candy line restore made the file match vanilla line data.");
            return 1;
        }

        var restored = new TextFile(new TextConfig(GameVersion.SW), remapChars: true)
        {
            SETEMPTYTEXT = false,
            Lines = lines,
            Flags = flags,
        };
        CommitLayeredRestore(options.OutputPath, outputRelativePath, restored.Data, baseBytes, changedDirectories, notes, normalizedBaseBytes);
        notes.Add($"- Restored {outputRelativePath} line {lineIndex} to vanilla text.");
        return 1;
    }

    private static byte[] CreateTextFileData(string[] lines, ushort[] flags)
    {
        var text = new TextFile(new TextConfig(GameVersion.SW), remapChars: true)
        {
            SETEMPTYTEXT = false,
            Lines = lines,
            Flags = flags,
        };
        return text.Data;
    }

    private static bool TextContentMatchesVanilla(string[] currentLines, ushort[] currentFlags, string[] vanillaLines, ushort[] vanillaFlags) =>
        currentLines.SequenceEqual(vanillaLines, StringComparer.Ordinal) && currentFlags.SequenceEqual(vanillaFlags);

    private static int RestoreSourceAcquisitionDataToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        var restored = 0;
        restored += RestoreShopDataToVanilla(options, results, notes, changedDirectories);
        restored += RestoreRaidRewardsToVanilla(options, results, notes, changedDirectories);
        restored += RestorePlacementItemsToVanilla(options, results, notes, changedDirectories);
        return restored;
    }

    private static int RestoreShopDataToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        const string outputRelativePath = "romfs/" + ShopPath;
        var layeredPath = GetLayeredFsRomFsPath(options, ShopPath);
        if (!File.Exists(layeredPath))
        {
            results.Add(new("Info", "Uninstall", outputRelativePath, "No layered shop data found."));
            return 0;
        }

        var basePath = GetBaseRomFsPath(options, ShopPath);
        if (!File.Exists(basePath))
            throw new FileNotFoundException("Could not find base shop data for Royal Candy uninstall.", basePath);

        var currentBytes = File.ReadAllBytes(layeredPath);
        var baseBytes = File.ReadAllBytes(basePath);
        var current = FlatBufferConverter.DeserializeFrom<SwShShopInventory>(currentBytes);
        var baseline = FlatBufferConverter.DeserializeFrom<SwShShopInventory>(baseBytes);
        var restored = 0;

        var currentSingles = current.Single.ToDictionary(z => z.Hash);
        foreach (var vanillaSingle in baseline.Single)
        {
            if (currentSingles.TryGetValue(vanillaSingle.Hash, out var currentSingle))
                restored += RestoreInventoryItemsToVanilla(currentSingle.Inventories.Items, vanillaSingle.Inventories.Items, options.ItemId);
        }

        var currentMultis = current.Multi.ToDictionary(z => z.Hash);
        foreach (var vanillaMulti in baseline.Multi)
        {
            if (!currentMultis.TryGetValue(vanillaMulti.Hash, out var currentMulti))
                continue;

            var inventoryCount = Math.Min(currentMulti.Inventories.Count, vanillaMulti.Inventories.Count);
            for (var i = 0; i < inventoryCount; i++)
                restored += RestoreInventoryItemsToVanilla(currentMulti.Inventories[i].Items, vanillaMulti.Inventories[i].Items, options.ItemId);
        }

        var restoredBytes = current.SerializeFrom();
        var normalizedBaseBytes = baseline.SerializeFrom();
        if (restored == 0)
        {
            if (restoredBytes.SequenceEqual(normalizedBaseBytes))
            {
                DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
                results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed shop overlay that matched vanilla inventory data."));
                notes.Add($"- Removed {outputRelativePath}; parsed shop data matched vanilla.");
                return 0;
            }

            results.Add(new("Info", "Uninstall", outputRelativePath, "No Royal Candy shop entries needed restoration."));
            return 0;
        }

        CommitLayeredRestore(options.OutputPath, outputRelativePath, restoredBytes, baseBytes, changedDirectories, notes, normalizedBaseBytes);
        results.Add(new("Pass", "Uninstall", outputRelativePath, $"Restored {restored:N0} vanilla shop entr{(restored == 1 ? "y" : "ies")} for item {options.ItemId}."));
        notes.Add($"- Restored {restored:N0} vanilla shop entr{(restored == 1 ? "y" : "ies")} for item {options.ItemId}.");
        return restored;
    }

    private static int RestoreInventoryItemsToVanilla(IList<int> currentItems, IList<int> vanillaItems, int itemId)
    {
        var restored = 0;
        for (var i = 0; i < vanillaItems.Count; i++)
        {
            if (vanillaItems[i] != itemId || currentItems.Contains(itemId))
                continue;

            currentItems.Insert(Math.Min(i, currentItems.Count), itemId);
            restored++;
        }

        return restored;
    }

    private static int RestoreRaidRewardsToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        const string outputRelativePath = "romfs/" + NestDataPath;
        var layeredPath = GetLayeredFsRomFsPath(options, NestDataPath);
        if (!File.Exists(layeredPath))
        {
            results.Add(new("Info", "Uninstall", outputRelativePath, "No layered raid reward data found."));
            return 0;
        }

        var basePath = GetBaseRomFsPath(options, NestDataPath);
        if (!File.Exists(basePath))
            throw new FileNotFoundException("Could not find base raid reward data for Royal Candy uninstall.", basePath);

        var currentBytes = File.ReadAllBytes(layeredPath);
        var baseBytes = File.ReadAllBytes(basePath);
        var cleanRoyalCandyOverlay = currentBytes.SequenceEqual(CreateRoyalCandyRaidCleanupBytes(baseBytes, options.ItemId));
        var restoredRoyalCandyOverlay = currentBytes.SequenceEqual(CreateRestoredRoyalCandyRaidCleanupBytes(baseBytes, options.ItemId));
        if (restoredRoyalCandyOverlay)
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed raid reward overlay that matched restored Royal Candy cleanup output."));
            notes.Add($"- Removed {outputRelativePath}; it matched restored Royal Candy raid cleanup output.");
            return 0;
        }

        var current = new GFPack(currentBytes);
        var baseline = new GFPack(baseBytes);
        var restored = 0;
        restored += RestoreNestRewardItemsToVanilla(current, baseline, "nest_hole_drop_rewards.bin", options.ItemId);
        restored += RestoreNestRewardItemsToVanilla(current, baseline, "nest_hole_bonus_rewards.bin", options.ItemId);

        var restoredBytes = current.Write();
        var normalizedBaseBytes = baseline.Write();
        if (restored == 0)
        {
            if (restoredBytes.SequenceEqual(normalizedBaseBytes))
            {
                DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
                results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed raid reward overlay that matched vanilla reward data."));
                notes.Add($"- Removed {outputRelativePath}; parsed raid reward data matched vanilla.");
                return 0;
            }

            results.Add(new("Info", "Uninstall", outputRelativePath, "No Royal Candy raid reward entries needed restoration."));
            return 0;
        }

        if (cleanRoyalCandyOverlay)
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, $"Removed clean Royal Candy raid reward cleanup overlay after restoring {restored:N0} entr{(restored == 1 ? "y" : "ies")}."));
            notes.Add($"- Removed {outputRelativePath}; it matched clean Royal Candy raid cleanup output.");
            return restored;
        }

        CommitLayeredRestore(options.OutputPath, outputRelativePath, restoredBytes, baseBytes, changedDirectories, notes, normalizedBaseBytes);
        results.Add(new("Pass", "Uninstall", outputRelativePath, $"Restored {restored:N0} vanilla raid reward entr{(restored == 1 ? "y" : "ies")} for item {options.ItemId}."));
        notes.Add($"- Restored {restored:N0} vanilla raid reward entr{(restored == 1 ? "y" : "ies")} for item {options.ItemId}.");
        return restored;
    }

    private static byte[] CreateRoyalCandyRaidCleanupBytes(byte[] baseBytes, int itemId)
    {
        var pack = new GFPack(baseBytes);
        var notes = new List<string>();
        _ = ReplaceNestRewardItems(pack, "nest_hole_drop_rewards.bin", itemId, RareCandyItemId, notes);
        _ = ReplaceNestRewardItems(pack, "nest_hole_bonus_rewards.bin", itemId, RareCandyItemId, notes);
        return pack.Write();
    }

    private static byte[] CreateRestoredRoyalCandyRaidCleanupBytes(byte[] baseBytes, int itemId)
    {
        var pack = new GFPack(CreateRoyalCandyRaidCleanupBytes(baseBytes, itemId));
        var baseline = new GFPack(baseBytes);
        _ = RestoreNestRewardItemsToVanilla(pack, baseline, "nest_hole_drop_rewards.bin", itemId);
        _ = RestoreNestRewardItemsToVanilla(pack, baseline, "nest_hole_bonus_rewards.bin", itemId);
        return pack.Write();
    }

    private static int RestoreNestRewardItemsToVanilla(GFPack current, GFPack baseline, string fileName, int itemId)
    {
        if (current.GetIndexFileName(fileName) < 0 || baseline.GetIndexFileName(fileName) < 0)
            return 0;

        var currentArchive = FlatBufferConverter.DeserializeFrom<NestHoleRewardArchive>(current.GetDataFileName(fileName));
        var baseArchive = FlatBufferConverter.DeserializeFrom<NestHoleRewardArchive>(baseline.GetDataFileName(fileName));
        var restored = 0;
        var tableCount = Math.Min(currentArchive.Table.Count, baseArchive.Table.Count);
        for (var tableIndex = 0; tableIndex < tableCount; tableIndex++)
        {
            var currentEntries = currentArchive.Table[tableIndex].Entries;
            var baseEntries = baseArchive.Table[tableIndex].Entries;
            var entryCount = Math.Min(currentEntries.Count, baseEntries.Count);
            for (var entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                if (baseEntries[entryIndex].ItemID != itemId || currentEntries[entryIndex].ItemID != RareCandyItemId)
                    continue;

                currentEntries[entryIndex].ItemID = (uint)itemId;
                restored++;
            }
        }

        if (restored != 0)
            current.SetDataFileName(fileName, currentArchive.SerializeFrom());
        return restored;
    }

    private static int RestorePlacementItemsToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        const string outputRelativePath = "romfs/" + PlacementPath;
        var layeredPath = GetLayeredFsRomFsPath(options, PlacementPath);
        if (!File.Exists(layeredPath))
        {
            results.Add(new("Info", "Uninstall", outputRelativePath, "No layered placement data found."));
            return 0;
        }

        var basePath = GetBaseRomFsPath(options, PlacementPath);
        if (!File.Exists(basePath))
            throw new FileNotFoundException("Could not find base placement data for Royal Candy uninstall.", basePath);

        var hashes = ReadItemHashes(options);
        if (!hashes.TryGetValue(options.ItemId, out var sourceItemHash))
            throw new InvalidOperationException($"Could not find item hash for source item {options.ItemId}.");
        if (!hashes.TryGetValue(RareCandyItemId, out var rareCandyHash))
            throw new InvalidOperationException("Could not find item hash for regular Rare Candy.");

        var currentBytes = File.ReadAllBytes(layeredPath);
        var baseBytes = File.ReadAllBytes(basePath);
        var cleanRoyalCandyOverlay = currentBytes.SequenceEqual(CreateRoyalCandyPlacementCleanupBytes(baseBytes, sourceItemHash, rareCandyHash, options.ItemId));
        var restoredRoyalCandyOverlay = currentBytes.SequenceEqual(CreateRestoredRoyalCandyPlacementCleanupBytes(baseBytes, sourceItemHash, rareCandyHash, options.ItemId));
        if (restoredRoyalCandyOverlay)
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed placement overlay that matched restored Royal Candy cleanup output."));
            notes.Add($"- Removed {outputRelativePath}; it matched restored Royal Candy placement cleanup output.");
            return 0;
        }

        var current = new GFPack(currentBytes);
        var baseline = new GFPack(baseBytes);
        var restored = RestorePlacementItemsToVanilla(current, baseline, sourceItemHash, rareCandyHash, (uint)options.ItemId);

        var restoredBytes = current.Write();
        var normalizedBaseBytes = baseline.Write();
        if (restored == 0)
        {
            if (restoredBytes.SequenceEqual(normalizedBaseBytes))
            {
                DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
                results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed placement overlay that matched vanilla placement data."));
                notes.Add($"- Removed {outputRelativePath}; parsed placement data matched vanilla.");
                return 0;
            }

            results.Add(new("Info", "Uninstall", outputRelativePath, "No Royal Candy placement entries needed restoration."));
            return 0;
        }

        if (cleanRoyalCandyOverlay)
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, $"Removed clean Royal Candy placement cleanup overlay after restoring {restored:N0} entr{(restored == 1 ? "y" : "ies")}."));
            notes.Add($"- Removed {outputRelativePath}; it matched clean Royal Candy placement cleanup output.");
            return restored;
        }

        CommitLayeredRestore(options.OutputPath, outputRelativePath, restoredBytes, baseBytes, changedDirectories, notes, normalizedBaseBytes);
        results.Add(new("Pass", "Uninstall", outputRelativePath, $"Restored {restored:N0} vanilla placement pickup entr{(restored == 1 ? "y" : "ies")} for item {options.ItemId}."));
        notes.Add($"- Restored {restored:N0} vanilla placement pickup entr{(restored == 1 ? "y" : "ies")} for item {options.ItemId}.");
        return restored;
    }

    private static byte[] CreateRoyalCandyPlacementCleanupBytes(byte[] baseBytes, ulong sourceItemHash, ulong rareCandyHash, int itemId)
    {
        var pack = new GFPack(baseBytes);
        _ = ReplacePlacementItems(pack, sourceItemHash, rareCandyHash, (uint)itemId, new List<string>());
        return pack.Write();
    }

    private static byte[] CreateRestoredRoyalCandyPlacementCleanupBytes(byte[] baseBytes, ulong sourceItemHash, ulong rareCandyHash, int itemId)
    {
        var pack = new GFPack(CreateRoyalCandyPlacementCleanupBytes(baseBytes, sourceItemHash, rareCandyHash, itemId));
        var baseline = new GFPack(baseBytes);
        RestorePlacementItemsToVanilla(pack, baseline, sourceItemHash, rareCandyHash, (uint)itemId);
        return pack.Write();
    }

    private static int RestorePlacementItemsToVanilla(GFPack current, GFPack baseline, ulong sourceItemHash, ulong rareCandyHash, uint itemId)
    {
        var currentAreas = new AHTB(current.GetDataFileName("AreaNameHashTable.tbl")).ToDictionary();
        var baseAreas = new AHTB(baseline.GetDataFileName("AreaNameHashTable.tbl")).ToDictionary();
        var restored = 0;

        foreach (var areaName in baseAreas.Values.OrderBy(z => z, StringComparer.Ordinal))
        {
            var fileName = $"{areaName}.bin";
            if (baseline.GetIndexFileName(fileName) < 0 || current.GetIndexFileName(fileName) < 0 || !currentAreas.ContainsValue(areaName))
                continue;

            var currentArchive = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(current.GetDataFileName(fileName));
            var baseArchive = FlatBufferConverter.DeserializeFrom<PlacementZoneArchive>(baseline.GetDataFileName(fileName));
            var areaRestored = RestorePlacementArchiveToVanilla(currentArchive, baseArchive, sourceItemHash, rareCandyHash, itemId);
            if (areaRestored == 0)
                continue;

            restored += areaRestored;
            current.SetDataFileName(fileName, currentArchive.SerializeFrom());
        }

        return restored;
    }

    private static int RestorePlacementArchiveToVanilla(PlacementZoneArchive currentArchive, PlacementZoneArchive baseArchive, ulong sourceItemHash, ulong rareCandyHash, uint itemId)
    {
        var restored = 0;
        var zoneCount = Math.Min(currentArchive.Table.Count, baseArchive.Table.Count);
        for (var zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
        {
            var currentZone = currentArchive.Table[zoneIndex];
            var baseZone = baseArchive.Table[zoneIndex];
            var fieldItemCount = Math.Min(currentZone.FieldItems.Count, baseZone.FieldItems.Count);
            for (var fieldItemIndex = 0; fieldItemIndex < fieldItemCount; fieldItemIndex++)
            {
                var currentItem = currentZone.FieldItems[fieldItemIndex].Field00;
                var baseItem = baseZone.FieldItems[fieldItemIndex].Field00;
                restored += RestoreUlongListToVanilla(currentItem.Flags, baseItem.Flags, sourceItemHash, rareCandyHash);
                restored += RestoreUintListToVanilla(currentItem.Items, baseItem.Items, itemId, RareCandyItemId);
            }

            var hiddenItemCount = Math.Min(currentZone.HiddenItems.Count, baseZone.HiddenItems.Count);
            for (var hiddenItemIndex = 0; hiddenItemIndex < hiddenItemCount; hiddenItemIndex++)
            {
                var currentChances = currentZone.HiddenItems[hiddenItemIndex].Field00.Field02;
                var baseChances = baseZone.HiddenItems[hiddenItemIndex].Field00.Field02;
                var chanceCount = Math.Min(currentChances.Count, baseChances.Count);
                for (var chanceIndex = 0; chanceIndex < chanceCount; chanceIndex++)
                {
                    if (baseChances[chanceIndex].Hash != sourceItemHash || currentChances[chanceIndex].Hash != rareCandyHash)
                        continue;

                    currentChances[chanceIndex].Hash = sourceItemHash;
                    restored++;
                }
            }
        }

        return restored;
    }

    private static int RestoreUlongListToVanilla(IList<ulong> current, IList<ulong> baseline, ulong source, ulong replaced)
    {
        var restored = 0;
        var count = Math.Min(current.Count, baseline.Count);
        for (var i = 0; i < count; i++)
        {
            if (baseline[i] != source || current[i] != replaced)
                continue;

            current[i] = source;
            restored++;
        }

        return restored;
    }

    private static int RestoreUintListToVanilla(IList<uint> current, IList<uint> baseline, uint source, uint replaced)
    {
        var restored = 0;
        var count = Math.Min(current.Count, baseline.Count);
        for (var i = 0; i < count; i++)
        {
            if (baseline[i] != source || current[i] != replaced)
                continue;

            current[i] = source;
            restored++;
        }

        return restored;
    }

    private static int RestoreBagEventScriptToVanilla(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        const string outputRelativePath = "romfs/" + RoyalSwordScriptAmxPatcher.BagEventScriptPath;
        var layeredPath = GetLayeredFsRomFsPath(options, RoyalSwordScriptAmxPatcher.BagEventScriptPath);
        if (!File.Exists(layeredPath))
        {
            results.Add(new("Info", "Uninstall", outputRelativePath, "No layered Bag-event script found."));
            return 0;
        }

        var basePath = GetBaseRomFsPath(options, RoyalSwordScriptAmxPatcher.BagEventScriptPath);
        if (!File.Exists(basePath))
            throw new FileNotFoundException("Could not find base Bag-event script for Royal Candy uninstall.", basePath);

        var currentBytes = File.ReadAllBytes(layeredPath);
        var baseBytes = File.ReadAllBytes(basePath);
        if (currentBytes.SequenceEqual(baseBytes))
        {
            DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
            results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed Bag-event script overlay that already matched vanilla."));
            notes.Add($"- Removed {outputRelativePath}; it already matched the base dump.");
            return 0;
        }

        var expectedRoyalCandyPatch = BuildExpectedBagEventPatchForComparison(basePath, options.ItemId);
        if (!currentBytes.SequenceEqual(expectedRoyalCandyPatch))
        {
            results.Add(new("Warning", "Uninstall", outputRelativePath, "Layered Bag-event AMX script is not the clean Royal Candy patch; leaving it unchanged."));
            notes.Add($"- Left {outputRelativePath} unchanged because it does not exactly match the clean generated Royal Candy Bag-event patch.");
            return 0;
        }

        DeleteOutputFile(options.OutputPath, outputRelativePath, changedDirectories);
        results.Add(new("Pass", "Uninstall", outputRelativePath, "Removed clean Royal Candy Bag-event script overlay."));
        notes.Add($"- Removed clean Royal Candy Bag-event script overlay at {outputRelativePath}; base script will be used.");
        return 1;
    }

    private static byte[] BuildExpectedBagEventPatchForComparison(string baseScriptPath, int itemId)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RoyalCandyExpectedBagPatch_" + Guid.NewGuid().ToString("N"));
        try
        {
            _ = RoyalSwordScriptAmxPatcher.PatchBagEventRoyalCandyGrant(baseScriptPath, tempRoot, itemId);
            var patchedPath = Path.Combine(tempRoot, "romfs", RoyalSwordScriptAmxPatcher.BagEventScriptPath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllBytes(patchedPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void PatchExeFsMain(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes)
    {
        if (options.ItemId != RoyalCandyItemId)
            throw new InvalidOperationException("The Royal Candy ExeFS patch currently targets item id 1128 only.");

        var mainPath = ResolveExeFsSourcePath(options, "main");
        if (!File.Exists(mainPath))
            throw new FileNotFoundException("Could not find exefs/main for Royal Candy patching.", mainPath);

        var patchNotes = new List<string>();
        var patched = CreatePatchedExeFsMain(File.ReadAllBytes(mainPath), options, patchNotes);
        WriteOutputBytes(options.OutputPath, "exefs/main", patched);

        results.Add(new("Pass", "ExeFS", "exefs/main", "Royal Candy ExeFS patch generated."));
        notes.AddRange(patchNotes.Where(z => z.StartsWith("- ", StringComparison.Ordinal)));
    }

    private static byte[] CreatePatchedExeFsMain(byte[] sourceMain, RoyalCandyBuildOptions options, List<string> patchNotes)
    {
        var nso = new NSO(sourceMain);
        if (!nso.Header.Valid)
            throw new InvalidOperationException("The selected main file is not a valid NSO.");

        patchNotes.AddRange([
            "Royal Candy ExeFS patch",
            "=======================",
            "",
            $"Build ID: {Convert.ToHexString(nso.Header.DigestBuildID)}",
            $"Royal Candy item id: {options.ItemId}",
            "",
        ]);

        PatchExpCandyFixedAmountBypass(nso.DecompressedText, options.ItemId, patchNotes);
        if (options.InfiniteUse)
            PatchInfiniteCandidateItemUse(nso.DecompressedText, options.ItemId, patchNotes);
        if (options.StoryCapLadder)
            PatchStoryCapLadder(nso.DecompressedText, options, patchNotes);
        if (options.VirtualCount is { } virtualCount)
        {
            PatchCandidateVirtualInventoryOwnership(nso.DecompressedText, options.ItemId, patchNotes);
            PatchCandidateVirtualInventoryCount(nso.DecompressedText, options.ItemId, virtualCount, patchNotes);
        }
        PatchRoyalCandyUiRoute(nso.DecompressedText, options.ItemId, patchNotes);

        return nso.Write();
    }

    private static void PatchRoyalCandyUiRoute(byte[] text, int candidateId, List<string> notes)
    {
        var check = new RareCandyUiCheck(0x007BC1F8, 8, 0x007BC200, 0x007BC2B4);
        var caveOffset = FindZeroRun(text, 0xC, RareCandyUiHookCodeCaveSearchStart);
        if (caveOffset < 0)
            caveOffset = FindZeroRun(text, 0xC, 0);
        if (caveOffset < 0)
        {
            var largest = FindLargestZeroRun(text);
            throw new InvalidOperationException($"Could not find a 12-byte zero-filled code cave for the Royal Candy UI route. Largest run: text+0x{largest.Offset:X} length 0x{largest.Length:X}.");
        }

        var actualCompare = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(check.CompareOffset));
        var expectedCompare = EncodeCmpImmediate(check.ItemRegister, RareCandyItemId);
        if (actualCompare != expectedCompare)
            throw new InvalidOperationException($"Unexpected instruction at text+0x{check.CompareOffset:X}: {actualCompare:X8}; expected {expectedCompare:X8}.");

        var originalBranchOffset = check.CompareOffset + 4;
        var actualBranch = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(originalBranchOffset));
        var expectedBranch = EncodeConditionalBranch(originalBranchOffset, check.FailOffset, Arm64Condition.NE);
        if (actualBranch != expectedBranch)
            throw new InvalidOperationException($"Unexpected branch at text+0x{originalBranchOffset:X}: {actualBranch:X8}; expected {expectedBranch:X8}.");

        WriteInstruction(text, originalBranchOffset, EncodeConditionalBranch(originalBranchOffset, caveOffset, Arm64Condition.NE));
        WriteInstruction(text, caveOffset, EncodeCmpImmediate(check.ItemRegister, candidateId));
        WriteInstruction(text, caveOffset + 4, EncodeConditionalBranch(caveOffset + 4, check.PassOffset, Arm64Condition.EQ));
        WriteInstruction(text, caveOffset + 8, EncodeBranch(caveOffset + 8, check.FailOffset));

        notes.Add($"- text+0x{check.CompareOffset:X}: Royal Candy now enters the confirmed Rare Candy bag UI route through stub text+0x{caveOffset:X}.");
    }

    private static void PatchExpCandyFixedAmountBypass(byte[] text, int candidateId, List<string> notes)
    {
        const int firstRangeCompareOffset = 0x007BC1BC;
        const int secondRangeCompareOffset = 0x007BC1C4;
        const int expCandyIndexRegister = 9;

        foreach (var offset in new[] { firstRangeCompareOffset, secondRangeCompareOffset })
        {
            var actualCompare = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset));
            var expectedCompare = EncodeCmpImmediate(expCandyIndexRegister, 4);
            if (actualCompare != expectedCompare)
                throw new InvalidOperationException($"Unexpected Exp Candy range compare at text+0x{offset:X}: {actualCompare:X8}; expected {expectedCompare:X8}.");

            WriteInstruction(text, offset, EncodeCmpImmediate(expCandyIndexRegister, 3));
        }

        notes.Add("- text+0x7BC1BC/text+0x7BC1C4: item id 1128 no longer enters the fixed Exp Candy XL amount table.");
    }

    private static void PatchInfiniteCandidateItemUse(byte[] text, int candidateId, List<string> notes)
    {
        const int quantityMoveOffset = 0x007B1F20;
        const int resumeOffset = quantityMoveOffset + 4;
        const int itemRegister = 22;
        const uint expectedQuantityMove = 0x2A0003E2;

        var actualQuantityMove = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(quantityMoveOffset));
        if (actualQuantityMove != expectedQuantityMove)
            throw new InvalidOperationException($"Unexpected consume-quantity move at text+0x{quantityMoveOffset:X}: {actualQuantityMove:X8}; expected {expectedQuantityMove:X8}.");

        var caveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy infinite-use patch");
        WriteInstruction(text, quantityMoveOffset, EncodeBranch(quantityMoveOffset, caveOffset));
        WriteInstruction(text, caveOffset, EncodeCmpImmediate(itemRegister, candidateId));
        WriteInstruction(text, caveOffset + 4, EncodeConditionalSelect32(2, 31, 0, Arm64Condition.EQ));
        WriteInstruction(text, caveOffset + 8, EncodeBranch(caveOffset + 8, resumeOffset));

        notes.Add($"- text+0x{quantityMoveOffset:X}: Royal Candy passes zero to the bag decrement call while other items keep the selected quantity.");
    }

    private static void PatchStoryCapLadder(byte[] text, RoyalCandyBuildOptions options, List<string> notes)
    {
        var sourceMilestones = options.CustomCapMilestones ?? GetDefaultCapMilestoneDefinitions()
            .Select(definition => new RoyalCandyCapMilestone(definition.DefaultCap, definition))
            .ToArray();
        var milestones = sourceMilestones
            .Select(z => ToExeFsMilestone(z, options.GameFlavor))
            .Where(milestone => options.MaxStoryCap is null || milestone.Cap <= options.MaxStoryCap)
            .ToArray();
        if (milestones.Length == 0)
            throw new InvalidOperationException("Royal Candy story-cap ladder has no milestones after applying the requested max cap.");

        var capHelperOffset = WriteStoryCapHelper(text, milestones, options.DefaultCap);
        PatchCandidateUseGateDynamicCap(text, options.ItemId, capHelperOffset, notes);
        PatchCandidateQuantityMaxDynamicCap(text, options.ItemId, capHelperOffset, notes);
        PatchCandidateQuantityInventoryClampBypass(text, options.ItemId, notes);

        notes.Add($"- text+0x{capHelperOffset:X}: shared Royal Candy cap helper returns the highest unlocked Royal Sword story cap; default cap is level {options.DefaultCap}.");
        if (options.MaxStoryCap is { } cap)
            notes.Add($"  - Diagnostic max story cap: only milestones at or below cap {cap} were included.");
        foreach (var milestone in milestones.OrderBy(milestone => milestone.Cap))
        {
            var source = milestone.Kind == RoyalSwordLevelCapMilestoneKind.WorkAtLeast
                ? $"work hash 0x{milestone.FlagHash:X16} >= {milestone.WorkMinimum}"
                : $"flag hash 0x{milestone.FlagHash:X16}";
            notes.Add($"  - Cap {milestone.Cap}: {milestone.Label} via {source}.");
        }
    }

    internal static RoyalCandyCapMilestoneDefinition[] GetDefaultCapMilestoneDefinitions() =>
    [
        new(16, HopEndorsementFlagHash, "Hop 007/008/009", "Hop 007/008/009"),
        new(20, SceneMainMasterWorkHash, "Hop 191/192/193", "Hop 191/192/193", RoyalCandyCapMilestoneKind.WorkAtLeast, 530),
        new(23, SceneMainMasterWorkHash, "Bede 195", "Bede 195", RoyalCandyCapMilestoneKind.WorkAtLeast, 550),
        new(25, 0xB02911749203329A, "Milo 032", "Milo 032"),
        new(28, SceneMainMasterWorkHash, "Hop 121/122/123", "Hop 121/122/123", RoyalCandyCapMilestoneKind.WorkAtLeast, 640),
        new(30, 0x8B4F4365890D1CF9, "Nessa 036", "Nessa 036"),
        new(32, SceneMainMasterWorkHash, "Bede 240", "Bede 240", RoyalCandyCapMilestoneKind.WorkAtLeast, 720),
        new(36, SceneMainMasterWorkHash, "Marnie 196", "Marnie 196", RoyalCandyCapMilestoneKind.WorkAtLeast, 760),
        new(38, 0xABFC3E0B626D6B24, "Kabu 037", "Kabu 037"),
        new(40, SceneMainMasterWorkHash, "Hop 124/125/126", "Hop 124/125/126", RoyalCandyCapMilestoneKind.WorkAtLeast, 950),
        new(42, 0xC07B67FC3148B754, "Bea 077", "Bea 077"),
        new(44, SceneMainMasterWorkHash, "Bede 133", "Bede 133", RoyalCandyCapMilestoneKind.WorkAtLeast, 1090),
        new(47, 0xDF7AC7105B946783, "Opal 108", "Opal 108"),
        new(50, SceneMainMasterWorkHash, "Hop 127/128/129", "Hop 127/128/129", RoyalCandyCapMilestoneKind.WorkAtLeast, 1200),
        new(52, 0x7042D310DF3DB17F, "Gordie 135", "Melony 136"),
        new(54, SceneMainMasterWorkHash, "Hop 202/203/204", "Hop 202/203/204", RoyalCandyCapMilestoneKind.WorkAtLeast, 1300),
        new(55, SceneMainMasterWorkHash, "Marnie 138", "Marnie 138", RoyalCandyCapMilestoneKind.WorkAtLeast, 1330),
        new(60, 0xA52A7561C28A76F1, "Piers 107", "Piers 107"),
        new(65, 0xE336BF34143E0946, "Raihan 144", "Raihan 144"),
        new(70, SceneMainMasterWorkHash, "Hop 130/131/132", "Hop 130/131/132", RoyalCandyCapMilestoneKind.WorkAtLeast, 1550),
        new(75, SceneMainMasterWorkHash, "Oleana 143", "Oleana 143", RoyalCandyCapMilestoneKind.WorkAtLeast, 1660),
        new(80, SceneMainMasterWorkHash, "Raihan 213", "Raihan 213", RoyalCandyCapMilestoneKind.WorkAtLeast, 1780),
        new(85, SceneMainMasterWorkHash, "Rose 175", "Rose 175", RoyalCandyCapMilestoneKind.WorkAtLeast, 1910),
        new(90, SceneMainMasterWorkHash, "Leon 149/189/190", "Leon 149/189/190", RoyalCandyCapMilestoneKind.WorkAtLeast, 3000),
    ];

    private static RoyalSwordLevelCapMilestone ToExeFsMilestone(RoyalCandyCapMilestone milestone, RoyalCandyGameFlavor gameFlavor)
    {
        var definition = milestone.Definition;
        var kind = definition.Kind == RoyalCandyCapMilestoneKind.WorkAtLeast
            ? RoyalSwordLevelCapMilestoneKind.WorkAtLeast
            : RoyalSwordLevelCapMilestoneKind.Flag;
        return new(milestone.Cap, definition.ProgressHash, $"{definition.GetDisplayName(gameFlavor)} clear", kind, definition.WorkMinimum);
    }

    private static int WriteStoryCapHelper(byte[] text, RoyalSwordLevelCapMilestone[] milestones, int defaultCap)
    {
        const int flagworkGlobalAddress = 0x02610798;
        const int flagworkObjectOffset = 0x1B8;
        const int flagGetOffset = 0x01410F00;
        const int workGetOffset = 0x014114C0;

        var ordered = milestones.OrderByDescending(milestone => milestone.Cap).ToArray();
        var checks = ordered.Select((milestone, index) => new
        {
            Milestone = milestone,
            Chunks = AllocateCapCheckChunks(text, index),
        }).ToArray();
        var defaultReturn = AllocateCodeCave(text, 0x8, "Royal Candy cap ladder default return");

        for (var i = 0; i < checks.Length; i++)
        {
            var current = checks[i];
            var nextOffset = i == checks.Length - 1 ? defaultReturn : checks[i + 1].Chunks.LoadGlobal;
            WriteLevelCapCheck(text, current.Chunks, current.Milestone, nextOffset, flagworkGlobalAddress, flagworkObjectOffset, flagGetOffset, workGetOffset);
        }

        WriteInstruction(text, defaultReturn, EncodeMovzImmediate32(0, defaultCap));
        WriteInstruction(text, defaultReturn + 4, EncodeRet());
        return checks[0].Chunks.LoadGlobal;
    }

    private static RoyalSwordLevelCapCheckChunks AllocateCapCheckChunks(byte[] text, int index) => new(
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} load global"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} load table"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} hash low"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} hash high"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} call flag getter"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} restore link register"),
        AllocateCodeCave(text, 0xC, $"Royal Candy cap ladder check {index} decision"),
        AllocateCodeCave(text, 0x8, $"Royal Candy cap ladder check {index} cap return"));

    private static int AllocateCodeCave(byte[] text, int requiredBytes, string label)
    {
        var offset = FindCodeCaveOrThrow(text, requiredBytes, label);
        ReserveCodeCave(text, offset, requiredBytes);
        return offset;
    }

    private static void WriteLevelCapCheck(byte[] text, RoyalSwordLevelCapCheckChunks chunks, RoyalSwordLevelCapMilestone milestone, int nextOffset, int flagworkGlobalAddress, int flagworkObjectOffset, int flagGetOffset, int workGetOffset)
    {
        WriteInstruction(text, chunks.LoadGlobal, EncodeAdrp(8, chunks.LoadGlobal, flagworkGlobalAddress));
        WriteInstruction(text, chunks.LoadGlobal + 4, EncodeLdrUnsigned64(8, 8, flagworkGlobalAddress & 0xFFF));
        WriteInstruction(text, chunks.LoadGlobal + 8, EncodeBranch(chunks.LoadGlobal + 8, chunks.LoadTable));

        WriteInstruction(text, chunks.LoadTable, EncodeLdrUnsigned64(8, 8, 0));
        WriteInstruction(text, chunks.LoadTable + 4, EncodeLdrUnsigned64(0, 8, flagworkObjectOffset));
        WriteInstruction(text, chunks.LoadTable + 8, EncodeBranch(chunks.LoadTable + 8, chunks.HashLow));

        WriteInstruction(text, chunks.HashLow, EncodeMovzImmediate64(1, (int)(milestone.FlagHash & 0xFFFF), 0));
        WriteInstruction(text, chunks.HashLow + 4, EncodeMovkImmediate64(1, (int)((milestone.FlagHash >> 16) & 0xFFFF), 16));
        WriteInstruction(text, chunks.HashLow + 8, EncodeBranch(chunks.HashLow + 8, chunks.HashHigh));

        WriteInstruction(text, chunks.HashHigh, EncodeMovkImmediate64(1, (int)((milestone.FlagHash >> 32) & 0xFFFF), 32));
        WriteInstruction(text, chunks.HashHigh + 4, EncodeMovkImmediate64(1, (int)((milestone.FlagHash >> 48) & 0xFFFF), 48));
        WriteInstruction(text, chunks.HashHigh + 8, EncodeBranch(chunks.HashHigh + 8, chunks.Call));

        var accessorOffset = milestone.Kind == RoyalSwordLevelCapMilestoneKind.WorkAtLeast ? workGetOffset : flagGetOffset;
        WriteInstruction(text, chunks.Call, 0xA9BF7BFD);
        WriteInstruction(text, chunks.Call + 4, EncodeBranchLink(chunks.Call + 4, accessorOffset));
        WriteInstruction(text, chunks.Call + 8, EncodeBranch(chunks.Call + 8, chunks.Restore));

        WriteInstruction(text, chunks.Restore, 0xA8C17BFD);
        WriteInstruction(text, chunks.Restore + 4, EncodeBranch(chunks.Restore + 4, chunks.Decision));
        WriteInstruction(text, chunks.Restore + 8, EncodeNop());

        if (milestone.Kind == RoyalSwordLevelCapMilestoneKind.WorkAtLeast)
        {
            WriteInstruction(text, chunks.Decision, EncodeCmpImmediate(0, milestone.WorkMinimum));
            WriteInstruction(text, chunks.Decision + 4, EncodeConditionalBranch(chunks.Decision + 4, chunks.ReturnCap, Arm64Condition.HS));
            WriteInstruction(text, chunks.Decision + 8, EncodeBranch(chunks.Decision + 8, nextOffset));
        }
        else
        {
            WriteInstruction(text, chunks.Decision, EncodeCompareAndBranchNonZero32(chunks.Decision, chunks.ReturnCap, 0));
            WriteInstruction(text, chunks.Decision + 4, EncodeBranch(chunks.Decision + 4, nextOffset));
            WriteInstruction(text, chunks.Decision + 8, EncodeNop());
        }

        WriteInstruction(text, chunks.ReturnCap, EncodeMovzImmediate32(0, milestone.Cap));
        WriteInstruction(text, chunks.ReturnCap + 4, EncodeRet());
    }

    private static void PatchCandidateUseGateDynamicCap(byte[] text, int candidateId, int capHelperOffset, List<string> notes)
    {
        const int rareCandyCompareOffset = 0x007BB204;
        const int rareCandyBranchOffset = 0x007BB208;
        const int nonRareCandyOffset = 0x007BB26C;
        const int epilogueOffset = 0x007BB2E0;
        const int getLevelOffset = 0x0077A5F0;
        const int itemRegister = 20;
        const uint expectedBranch = 0x54000321;
        const uint moveSelectedPokemonToX0 = 0xAA1303E0;

        ExpectInstruction(text, rareCandyCompareOffset, EncodeCmpImmediate(itemRegister, RareCandyItemId), "Rare Candy use-gate compare");
        ExpectInstruction(text, rareCandyBranchOffset, expectedBranch, "Rare Candy use-gate branch");

        var itemCheckCaveOffset = FindNearbyConditionalBranchZeroRun(text, 0xC, rareCandyBranchOffset);
        if (itemCheckCaveOffset < 0)
            throw NoCave("Royal Candy dynamic use-gate item check", text);

        WriteInstruction(text, rareCandyBranchOffset, EncodeConditionalBranch(rareCandyBranchOffset, itemCheckCaveOffset, Arm64Condition.NE));
        WriteInstruction(text, itemCheckCaveOffset, EncodeCmpImmediate(itemRegister, candidateId));
        WriteInstruction(text, itemCheckCaveOffset + 4, EncodeConditionalBranch(itemCheckCaveOffset + 4, nonRareCandyOffset, Arm64Condition.NE));

        var logicChunks = AllocateCodeCaves(text, 4, "Royal Candy dynamic use-gate logic");
        WriteInstruction(text, itemCheckCaveOffset + 8, EncodeBranch(itemCheckCaveOffset + 8, logicChunks[0]));
        WriteInstruction(text, logicChunks[0], moveSelectedPokemonToX0);
        WriteInstruction(text, logicChunks[0] + 4, EncodeBranchLink(logicChunks[0] + 4, getLevelOffset));
        WriteInstruction(text, logicChunks[0] + 8, EncodeBranch(logicChunks[0] + 8, logicChunks[1]));
        WriteInstruction(text, logicChunks[1], EncodeMovRegister32(21, 0));
        WriteInstruction(text, logicChunks[1] + 4, EncodeBranchLink(logicChunks[1] + 4, capHelperOffset));
        WriteInstruction(text, logicChunks[1] + 8, EncodeBranch(logicChunks[1] + 8, logicChunks[2]));
        WriteInstruction(text, logicChunks[2], EncodeCmpRegister32(21, 0));
        WriteInstruction(text, logicChunks[2] + 4, EncodeMovzImmediate32(8, 1));
        WriteInstruction(text, logicChunks[2] + 8, EncodeBranch(logicChunks[2] + 8, logicChunks[3]));
        WriteInstruction(text, logicChunks[3], EncodeConditionalSelect32(0, 8, 31, Arm64Condition.LT));
        WriteInstruction(text, logicChunks[3] + 4, EncodeBranch(logicChunks[3] + 4, epilogueOffset));

        notes.Add($"- text+0x{rareCandyBranchOffset:X}: Royal Candy use gate now checks runtime story cap helper text+0x{capHelperOffset:X}.");
    }

    private static void PatchCandidateQuantityMaxDynamicCap(byte[] text, int candidateId, int capHelperOffset, List<string> notes)
    {
        const int rareCandyCompareOffset = 0x007BB3C0;
        const int rareCandyBranchOffset = 0x007BB3C4;
        const int nonRareCandyOffset = 0x007BB3EC;
        const int epilogueOffset = 0x007BB458;
        const int getLevelOffset = 0x0077A5F0;
        const int itemRegister = 19;
        const uint expectedBranch = 0x54000141;
        const uint moveSelectedPokemonToX0 = 0xAA1403E0;

        ExpectInstruction(text, rareCandyCompareOffset, EncodeCmpImmediate(itemRegister, RareCandyItemId), "Rare Candy quantity-cap compare");
        ExpectInstruction(text, rareCandyBranchOffset, expectedBranch, "Rare Candy quantity-cap branch");

        var itemCheckCaveOffset = FindNearbyConditionalBranchZeroRun(text, 0xC, rareCandyBranchOffset);
        if (itemCheckCaveOffset < 0)
            throw NoCave("Royal Candy dynamic quantity item check", text);

        WriteInstruction(text, rareCandyBranchOffset, EncodeConditionalBranch(rareCandyBranchOffset, itemCheckCaveOffset, Arm64Condition.NE));
        WriteInstruction(text, itemCheckCaveOffset, EncodeCmpImmediate(itemRegister, candidateId));
        WriteInstruction(text, itemCheckCaveOffset + 4, EncodeConditionalBranch(itemCheckCaveOffset + 4, nonRareCandyOffset, Arm64Condition.NE));

        var logicChunks = AllocateCodeCaves(text, 4, "Royal Candy dynamic quantity logic");
        WriteInstruction(text, itemCheckCaveOffset + 8, EncodeBranch(itemCheckCaveOffset + 8, logicChunks[0]));
        WriteInstruction(text, logicChunks[0], moveSelectedPokemonToX0);
        WriteInstruction(text, logicChunks[0] + 4, EncodeBranchLink(logicChunks[0] + 4, getLevelOffset));
        WriteInstruction(text, logicChunks[0] + 8, EncodeBranch(logicChunks[0] + 8, logicChunks[1]));
        WriteInstruction(text, logicChunks[1], EncodeMovRegister32(21, 0));
        WriteInstruction(text, logicChunks[1] + 4, EncodeBranchLink(logicChunks[1] + 4, capHelperOffset));
        WriteInstruction(text, logicChunks[1] + 8, EncodeBranch(logicChunks[1] + 8, logicChunks[2]));
        WriteInstruction(text, logicChunks[2], EncodeSubRegister32(0, 0, 21));
        WriteInstruction(text, logicChunks[2] + 4, EncodeCmpImmediate(0, 0));
        WriteInstruction(text, logicChunks[2] + 8, EncodeBranch(logicChunks[2] + 8, logicChunks[3]));
        WriteInstruction(text, logicChunks[3], EncodeConditionalSelect32(0, 0, 31, Arm64Condition.GT));
        WriteInstruction(text, logicChunks[3] + 4, EncodeBranch(logicChunks[3] + 4, epilogueOffset));

        notes.Add($"- text+0x{rareCandyBranchOffset:X}: Royal Candy quantity max returns max(0, runtime story cap - selected Pokemon level).");
    }

    private static void PatchCandidateQuantityInventoryClampBypass(byte[] text, int candidateId, List<string> notes)
    {
        const int originalCompareOffset = 0x007BAF38;
        const int clampSelectOffset = 0x007BAF3C;
        const int resumeOffset = 0x007BAF40;
        const int getItemIdOffset = 0x007C8330;
        const uint expectedCompare = 0x6B36231F;
        const uint expectedClampSelect = 0x1A963316;
        const uint moveSelectedItemToX0 = 0xAA1703E0;

        ExpectInstruction(text, originalCompareOffset, expectedCompare, "quantity clamp compare");
        ExpectInstruction(text, clampSelectOffset, expectedClampSelect, "quantity clamp CSEL");

        var firstCaveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy inventory clamp first stub");
        WriteInstruction(text, firstCaveOffset, moveSelectedItemToX0);
        WriteInstruction(text, firstCaveOffset + 4, EncodeBranchLink(firstCaveOffset + 4, getItemIdOffset));

        var secondCaveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy inventory clamp item check");
        WriteInstruction(text, firstCaveOffset + 8, EncodeBranch(firstCaveOffset + 8, secondCaveOffset));
        WriteInstruction(text, secondCaveOffset, EncodeCmpImmediate(0, candidateId));
        WriteInstruction(text, secondCaveOffset + 4, EncodeConditionalBranch(secondCaveOffset + 4, resumeOffset, Arm64Condition.EQ));

        var thirdCaveOffset = FindCodeCaveOrThrow(text, 0xC, "Royal Candy inventory clamp vanilla replay");
        WriteInstruction(text, secondCaveOffset + 8, EncodeBranch(secondCaveOffset + 8, thirdCaveOffset));
        WriteInstruction(text, thirdCaveOffset, expectedCompare);
        WriteInstruction(text, thirdCaveOffset + 4, expectedClampSelect);
        WriteInstruction(text, thirdCaveOffset + 8, EncodeBranch(thirdCaveOffset + 8, resumeOffset));
        WriteInstruction(text, clampSelectOffset, EncodeBranch(clampSelectOffset, firstCaveOffset));

        notes.Add($"- text+0x{clampSelectOffset:X}: Royal Candy bypasses the vanilla min(inventory count, useful count) clamp.");
    }

    private static void PatchCandidateVirtualInventoryOwnership(byte[] text, int candidateId, List<string> notes)
    {
        const int itemOwnershipFunctionOffset = 0x01420EF0;
        const int resumeOffset = itemOwnershipFunctionOffset + 4;
        const uint expectedFirstInstruction = 0xF81D0FF5;

        ExpectInstruction(text, itemOwnershipFunctionOffset, expectedFirstInstruction, "item-ownership helper first instruction");

        var dispatchCaveOffset = AllocateCodeCave(text, 0xC, "Royal Candy virtual ownership dispatch");
        var returnCaveOffset = AllocateCodeCave(text, 0x8, "Royal Candy virtual ownership return");
        var vanillaCaveOffset = FindCodeCaveOrThrow(text, 0x8, "Royal Candy virtual ownership vanilla path");

        WriteInstruction(text, itemOwnershipFunctionOffset, EncodeBranch(itemOwnershipFunctionOffset, dispatchCaveOffset));
        WriteInstruction(text, dispatchCaveOffset, EncodeCmpImmediate(1, candidateId));
        WriteInstruction(text, dispatchCaveOffset + 4, EncodeConditionalBranch(dispatchCaveOffset + 4, returnCaveOffset, Arm64Condition.EQ));
        WriteInstruction(text, dispatchCaveOffset + 8, EncodeBranch(dispatchCaveOffset + 8, vanillaCaveOffset));
        WriteInstruction(text, returnCaveOffset, EncodeMovzImmediate32(0, 1));
        WriteInstruction(text, returnCaveOffset + 4, EncodeRet());
        WriteInstruction(text, vanillaCaveOffset, expectedFirstInstruction);
        WriteInstruction(text, vanillaCaveOffset + 4, EncodeBranch(vanillaCaveOffset + 4, resumeOffset));

        notes.Add($"- text+0x{itemOwnershipFunctionOffset:X}: item-ownership helper reports Royal Candy as owned.");
    }

    private static void PatchCandidateVirtualInventoryCount(byte[] text, int candidateId, int virtualCount, List<string> notes)
    {
        if (virtualCount is < 1 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(virtualCount), "Royal Candy virtual count must fit in a 16-bit MOV immediate.");

        const int itemCountFunctionOffset = 0x01421090;
        const int resumeOffset = itemCountFunctionOffset + 4;
        const uint expectedFirstInstruction = 0xA9BE4FF4;

        ExpectInstruction(text, itemCountFunctionOffset, expectedFirstInstruction, "item-count helper first instruction");

        var dispatchCaveOffset = AllocateCodeCave(text, 0xC, "Royal Candy virtual count dispatch");
        var returnCaveOffset = AllocateCodeCave(text, 0x8, "Royal Candy virtual count return");
        var vanillaCaveOffset = FindCodeCaveOrThrow(text, 0x8, "Royal Candy virtual count vanilla path");

        WriteInstruction(text, itemCountFunctionOffset, EncodeBranch(itemCountFunctionOffset, dispatchCaveOffset));
        WriteInstruction(text, dispatchCaveOffset, EncodeCmpImmediate(1, candidateId));
        WriteInstruction(text, dispatchCaveOffset + 4, EncodeConditionalBranch(dispatchCaveOffset + 4, returnCaveOffset, Arm64Condition.EQ));
        WriteInstruction(text, dispatchCaveOffset + 8, EncodeBranch(dispatchCaveOffset + 8, vanillaCaveOffset));
        WriteInstruction(text, returnCaveOffset, EncodeMovzImmediate32(0, virtualCount));
        WriteInstruction(text, returnCaveOffset + 4, EncodeRet());
        WriteInstruction(text, vanillaCaveOffset, expectedFirstInstruction);
        WriteInstruction(text, vanillaCaveOffset + 4, EncodeBranch(vanillaCaveOffset + 4, resumeOffset));

        notes.Add($"- text+0x{itemCountFunctionOffset:X}: item-count helper returns virtual count {virtualCount} for Royal Candy.");
    }

    private static int[] AllocateCodeCaves(byte[] text, int count, string label)
    {
        var offsets = new int[count];
        for (var i = 0; i < offsets.Length; i++)
            offsets[i] = AllocateCodeCave(text, 0xC, $"{label} chunk {i}");

        return offsets;
    }

    private static int FindCodeCaveOrThrow(byte[] text, int requiredBytes, string label)
    {
        var caveOffset = FindZeroRun(text, requiredBytes, RareCandyUiHookCodeCaveSearchStart);
        if (caveOffset < 0)
            caveOffset = FindZeroRun(text, requiredBytes, 0);
        if (caveOffset >= 0)
            return caveOffset;

        throw NoCave(label, text);
    }

    private static int FindNearbyConditionalBranchZeroRun(byte[] text, int requiredBytes, int anchorOffset)
    {
        const int ConditionalBranchReachBytes = (1 << 18) * 4;
        var minOffset = Math.Max(0, anchorOffset - ConditionalBranchReachBytes + requiredBytes);
        var maxOffset = Math.Min(text.Length - requiredBytes, anchorOffset + ConditionalBranchReachBytes - requiredBytes);
        if (minOffset > maxOffset)
            return -1;

        var afterAnchor = FindZeroRunWithin(text, requiredBytes, anchorOffset, maxOffset);
        if (afterAnchor >= 0)
            return afterAnchor;
        return FindZeroRunWithin(text, requiredBytes, minOffset, anchorOffset);
    }

    private static int FindZeroRunWithin(byte[] data, int requiredBytes, int startOffset, int endOffset)
    {
        var runStart = -1;
        var start = Math.Max(0, startOffset);
        var end = Math.Min(data.Length - 1, endOffset);
        for (var offset = start; offset <= end; offset++)
        {
            if (data[offset] == 0)
            {
                if (runStart < 0)
                    runStart = offset;
                var alignedStart = (runStart + 3) & ~3;
                if (offset - alignedStart + 1 >= requiredBytes)
                    return alignedStart;
                continue;
            }

            runStart = -1;
        }

        return -1;
    }

    private static int FindZeroRun(byte[] data, int requiredBytes, int startOffset)
    {
        var runStart = -1;
        for (var offset = Math.Max(0, startOffset); offset < data.Length; offset++)
        {
            if (data[offset] == 0)
            {
                if (runStart < 0)
                    runStart = offset;
                var alignedStart = (runStart + 3) & ~3;
                if (offset - alignedStart + 1 >= requiredBytes)
                    return alignedStart;
                continue;
            }

            runStart = -1;
        }

        return -1;
    }

    private static ZeroRun FindLargestZeroRun(byte[] data)
    {
        var best = new ZeroRun(-1, 0);
        var runStart = -1;
        for (var offset = 0; offset < data.Length; offset++)
        {
            if (data[offset] == 0)
            {
                if (runStart < 0)
                    runStart = offset;

                var length = offset - runStart + 1;
                if (length > best.Length)
                    best = new ZeroRun(runStart, length);
                continue;
            }

            runStart = -1;
        }

        return best;
    }

    private static InvalidOperationException NoCave(string label, byte[] text)
    {
        var largest = FindLargestZeroRun(text);
        return new InvalidOperationException($"Could not find a zero-filled code cave for {label}. Largest run: text+0x{largest.Offset:X} length 0x{largest.Length:X}.");
    }

    private static void ExpectInstruction(byte[] text, int offset, uint expected, string label)
    {
        var actual = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset));
        if (actual != expected)
            throw new InvalidOperationException($"Unexpected {label} at text+0x{offset:X}: {actual:X8}; expected {expected:X8}.");
    }

    private static void ReserveCodeCave(byte[] text, int offset, int length)
    {
        for (var current = offset; current < offset + length; current += 4)
            WriteInstruction(text, current, EncodeNop());
    }

    private static void WriteInstruction(byte[] text, int offset, uint instruction) =>
        BinaryPrimitives.WriteUInt32LittleEndian(text.AsSpan(offset, 4), instruction);

    private static uint EncodeCmpImmediate(int register, int immediate) =>
        (uint)(0x7100001F | ((immediate & 0xFFF) << 10) | ((register & 0x1F) << 5));

    private static uint EncodeConditionalBranch(int sourceOffset, int targetOffset, Arm64Condition condition)
    {
        var imm19 = GetBranchDelta(sourceOffset, targetOffset, 18, "Conditional branch");
        return (uint)(0x54000000 | ((imm19 & 0x7FFFF) << 5) | ((int)condition & 0xF));
    }

    private static uint EncodeCompareAndBranchNonZero32(int sourceOffset, int targetOffset, int register)
    {
        var imm19 = GetBranchDelta(sourceOffset, targetOffset, 18, "Compare-and-branch");
        return (uint)(0x35000000 | ((imm19 & 0x7FFFF) << 5) | (register & 0x1F));
    }

    private static uint EncodeBranch(int sourceOffset, int targetOffset)
    {
        var imm26 = GetBranchDelta(sourceOffset, targetOffset, 25, "Branch");
        return (uint)(0x14000000 | (imm26 & 0x03FFFFFF));
    }

    private static uint EncodeBranchLink(int sourceOffset, int targetOffset)
    {
        var imm26 = GetBranchDelta(sourceOffset, targetOffset, 25, "Branch-link");
        return (uint)(0x94000000 | (imm26 & 0x03FFFFFF));
    }

    private static int GetBranchDelta(int sourceOffset, int targetOffset, int signedBits, string label)
    {
        var delta = targetOffset - sourceOffset;
        if ((delta & 3) != 0)
            throw new ArgumentException($"{label} target must be 4-byte aligned.", nameof(targetOffset));

        var immediate = delta >> 2;
        var min = -(1 << signedBits);
        var max = 1 << signedBits;
        if (immediate < min || immediate >= max)
            throw new ArgumentOutOfRangeException(nameof(targetOffset), $"{label} target is outside ARM64 range.");
        return immediate;
    }

    private static uint EncodeNop() => 0xD503201F;
    private static uint EncodeRet() => 0xD65F03C0;

    private static uint EncodeMovzImmediate32(int register, int immediate)
    {
        if (immediate is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(immediate), "MOVZ immediate must fit in 16 bits.");
        return (uint)(0x52800000 | ((immediate & 0xFFFF) << 5) | (register & 0x1F));
    }

    private static uint EncodeMovzImmediate64(int register, int immediate, int shift)
    {
        if (immediate is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(immediate), "MOVZ immediate must fit in 16 bits.");
        if (shift is not (0 or 16 or 32 or 48))
            throw new ArgumentOutOfRangeException(nameof(shift), "MOVZ 64-bit shift must be 0, 16, 32, or 48.");
        return 0xD2800000u | (uint)((shift / 16) << 21) | (uint)((immediate & 0xFFFF) << 5) | (uint)(register & 0x1F);
    }

    private static uint EncodeMovkImmediate64(int register, int immediate, int shift)
    {
        if (immediate is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(immediate), "MOVK immediate must fit in 16 bits.");
        if (shift is not (0 or 16 or 32 or 48))
            throw new ArgumentOutOfRangeException(nameof(shift), "MOVK 64-bit shift must be 0, 16, 32, or 48.");
        return 0xF2800000u | (uint)((shift / 16) << 21) | (uint)((immediate & 0xFFFF) << 5) | (uint)(register & 0x1F);
    }

    private static uint EncodeMovRegister32(int destinationRegister, int sourceRegister) =>
        (uint)(0x2A0003E0 | ((sourceRegister & 0x1F) << 16) | (destinationRegister & 0x1F));

    private static uint EncodeCmpRegister32(int leftRegister, int rightRegister) =>
        (uint)(0x6B00001F | ((rightRegister & 0x1F) << 16) | ((leftRegister & 0x1F) << 5));

    private static uint EncodeSubRegister32(int destinationRegister, int leftRegister, int rightRegister) =>
        (uint)(0x4B000000 | ((rightRegister & 0x1F) << 16) | ((leftRegister & 0x1F) << 5) | (destinationRegister & 0x1F));

    private static uint EncodeLdrUnsigned64(int targetRegister, int baseRegister, int byteOffset)
    {
        if ((byteOffset & 7) != 0)
            throw new ArgumentException("64-bit LDR unsigned offset must be 8-byte aligned.", nameof(byteOffset));
        var scaled = byteOffset >> 3;
        if (scaled is < 0 or > 0xFFF)
            throw new ArgumentOutOfRangeException(nameof(byteOffset), "64-bit LDR unsigned offset must fit the ARM64 imm12 field.");
        return 0xF9400000u | (uint)(scaled << 10) | (uint)((baseRegister & 0x1F) << 5) | (uint)(targetRegister & 0x1F);
    }

    private static uint EncodeAdrp(int register, int sourceOffset, int targetAddress)
    {
        var sourcePage = sourceOffset & ~0xFFF;
        var targetPage = targetAddress & ~0xFFF;
        var pageDelta = (targetPage - sourcePage) >> 12;
        if (pageDelta < -(1 << 20) || pageDelta >= (1 << 20))
            throw new ArgumentOutOfRangeException(nameof(targetAddress), "ADRP target is outside ARM64 range.");

        var imm = pageDelta & 0x1FFFFF;
        var immlo = imm & 0x3;
        var immhi = (imm >> 2) & 0x7FFFF;
        return 0x90000000u | (uint)(immlo << 29) | (uint)(immhi << 5) | (uint)(register & 0x1F);
    }

    private static uint EncodeConditionalSelect32(int destinationRegister, int trueRegister, int falseRegister, Arm64Condition condition) =>
        (uint)(0x1A800000 | (((int)condition & 0xF) << 12) | ((falseRegister & 0x1F) << 16) | ((trueRegister & 0x1F) << 5) | (destinationRegister & 0x1F));

    private static IEnumerable<int> GetIdsSharingEntry(byte[] itemData, int itemId)
    {
        var targetEntry = GetItemEntryIndex(itemData, itemId);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(itemData);
        for (var i = 0; i < count; i++)
        {
            if (GetItemEntryIndex(itemData, i) == targetEntry)
                yield return i;
        }
    }

    private static byte[] AppendUniqueItemRow(byte[] itemData, int itemId, ReadOnlySpan<byte> row)
    {
        var maxEntryIndex = BinaryPrimitives.ReadUInt16LittleEndian(itemData.AsSpan(4));
        if (maxEntryIndex == ushort.MaxValue)
            throw new InvalidOperationException("Item raw-row table is already at the ushort limit.");

        var result = new byte[itemData.Length + row.Length];
        itemData.CopyTo(result.AsSpan());
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), (ushort)(maxEntryIndex + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x44 + (itemId * 2)), maxEntryIndex);
        row.CopyTo(result.AsSpan(itemData.Length));
        return result;
    }

    private static ushort GetItemEntryIndex(byte[] itemData, int itemId)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(itemData);
        if ((uint)itemId >= count)
            throw new IndexOutOfRangeException($"Item id {itemId} is outside the item index table.");

        return BinaryPrimitives.ReadUInt16LittleEndian(itemData.AsSpan(GetItemEntryIndexOffset(itemId)));
    }

    private static int GetItemEntryIndexOffset(int itemId) => 0x44 + (itemId * 2);

    private static int GetItemRawRowOffset(byte[] itemData, ushort rowIndex)
    {
        var maxEntryIndex = BinaryPrimitives.ReadUInt16LittleEndian(itemData.AsSpan(4, 2));
        if (rowIndex >= maxEntryIndex)
            throw new IndexOutOfRangeException($"Item raw row {rowIndex} is outside the item raw-row table.");

        var entriesStart = BinaryPrimitives.ReadInt32LittleEndian(itemData.AsSpan(0x40, 4));
        var offset = entriesStart + (rowIndex * ItemRawRowSize);
        if (offset < 0 || offset + ItemRawRowSize > itemData.Length)
            throw new IndexOutOfRangeException($"Item raw row {rowIndex} points outside the item data file.");

        return offset;
    }

    private static string FormatSharedIds(IEnumerable<int> sharedIds, int itemId)
    {
        var otherIds = sharedIds.Where(z => z != itemId).ToArray();
        return otherIds.Length == 0
            ? "none"
            : string.Join(", ", otherIds.Take(20)) + (otherIds.Length > 20 ? $", ... ({otherIds.Length} total)" : "");
    }

    private static void WriteMarkerFile(RoyalCandyBuildOptions options)
    {
        var text = string.Join(Environment.NewLine, [
            MarkerHeader,
            "Generated by pkNX Royal Sword.",
            "",
            "Manage this output through the Royal Candy editor.",
        ]);
        Directory.CreateDirectory(options.OutputPath);
        File.WriteAllText(Path.Combine(options.OutputPath, MarkerFileName), text);
    }

    private static string GetRomFsPath(string romFsPath, string relativePath) =>
        Path.Combine(romFsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string GetBaseRomFsPath(RoyalCandyBuildOptions options, string relativePath) =>
        GetRomFsPath(options.RomFsPath, relativePath);

    private static string GetLayeredFsRomFsPath(RoyalCandyBuildOptions options, string relativePath) =>
        Path.Combine(options.OutputPath, "romfs", relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string ResolveRomFsSourcePath(RoyalCandyBuildOptions options, string relativePath)
    {
        var layeredPath = GetLayeredFsRomFsPath(options, relativePath);
        return File.Exists(layeredPath) ? layeredPath : GetBaseRomFsPath(options, relativePath);
    }

    private static string GetBaseExeFsPath(RoyalCandyBuildOptions options, string relativePath) =>
        Path.Combine(options.ExeFsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string GetLayeredFsExeFsPath(RoyalCandyBuildOptions options, string relativePath) =>
        Path.Combine(options.OutputPath, "exefs", relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string ResolveExeFsSourcePath(RoyalCandyBuildOptions options, string relativePath)
    {
        var layeredPath = GetLayeredFsExeFsPath(options, relativePath);
        return File.Exists(layeredPath) ? layeredPath : GetBaseExeFsPath(options, relativePath);
    }

    private static string DescribeLayeredSource(RoyalCandyBuildOptions options, string section, string relativePath)
    {
        var layeredPath = Path.Combine(options.OutputPath, section, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(layeredPath) ? "the existing LayeredFS mod file" : "the base dump file";
    }

    private static Dictionary<int, ulong> ReadItemHashes(RoyalCandyBuildOptions options)
    {
        var sourcePath = ResolveRomFsSourcePath(options, ItemHashPath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Could not find Sword/Shield item hash table.", sourcePath);

        return ItemHash8.GetItemHashTable(File.ReadAllBytes(sourcePath));
    }

    private static IEnumerable<string> EnumerateMessageLanguages(RoyalCandyBuildOptions options)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        AddDirectoryNames(GetBaseRomFsPath(options, MessageRoot), names);
        AddDirectoryNames(GetLayeredFsRomFsPath(options, MessageRoot), names);
        return names;
    }

    private static IEnumerable<string> EnumerateMessageFileNames(RoyalCandyBuildOptions options, string language, string pattern)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFileNames(GetBaseRomFsPath(options, $"{MessageRoot}/{language}/common"), pattern, names);
        AddFileNames(GetLayeredFsRomFsPath(options, $"{MessageRoot}/{language}/common"), pattern, names);
        return names;
    }

    private static void AddDirectoryNames(string root, ISet<string> names)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var directory in Directory.EnumerateDirectories(root))
            names.Add(Path.GetFileName(directory));
    }

    private static void AddFileNames(string root, string pattern, ISet<string> names)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var file in Directory.EnumerateFiles(root, pattern))
            names.Add(Path.GetFileName(file));
    }

    public static RoyalSwordExeFsSignatureScan? AnalyzeLayeredExeFsMain(RoyalCandyBuildOptions options)
    {
        var layeredMainPath = GetLayeredFsExeFsPath(options, "main");
        if (!File.Exists(layeredMainPath))
            return null;

        var baseMainPath = GetBaseExeFsPath(options, "main");
        var baseMain = File.Exists(baseMainPath) ? File.ReadAllBytes(baseMainPath) : null;
        return RoyalSwordExeFsSignatureLibrary.AnalyzeMain(File.ReadAllBytes(layeredMainPath), baseMain, options.GameFlavor, options.ItemId);
    }

    public static RoyalCandyInstallScan? DetectInstalledRoyalCandy(RoyalCandyBuildOptions options) =>
        AnalyzeLayeredExeFsMain(options)?.InstalledRoyalCandy;

    private static IEnumerable<string> EnumerateRoyalCandyOwnedOutputRelativePaths(RoyalCandyBuildOptions options)
    {
        yield return "exefs/main";

        // Legacy generated notes from earlier Royal Candy builder versions.
        yield return "exefs/royal_candy_ui_hook_patch_notes.txt";
        yield return "royal_candy_item_row_notes.txt";
        yield return "royal_candy_source_cleanup_notes.txt";
        yield return "royal_candy_bag_event_script_notes.txt";

        if (IsOwnedRoyalCandyMarker(options.OutputPath))
            yield return MarkerFileName;

        if (IsOwnedRoyalCandyReadme(options.OutputPath))
            yield return "README.md";
    }

    private static bool IsOwnedRoyalCandyMarker(string outputRoot)
    {
        var markerPath = Path.Combine(outputRoot, MarkerFileName);
        return TextFileStartsWithLine(markerPath, MarkerHeader);
    }

    private static bool IsOwnedRoyalCandyReadme(string outputRoot)
    {
        var readmePath = Path.Combine(outputRoot, "README.md");
        return TextFileStartsWithLine(readmePath, "# Royal Candy Patch");
    }

    private static bool TextFileStartsWithLine(string path, string expectedLine)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var reader = File.OpenText(path);
            return string.Equals(reader.ReadLine(), expectedLine, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetTitleId(RoyalCandyGameFlavor gameFlavor) => gameFlavor switch
    {
        RoyalCandyGameFlavor.Sword => "0100ABF008968000",
        RoyalCandyGameFlavor.Shield => "01008DB008C2C000",
        _ => throw new ArgumentOutOfRangeException(nameof(gameFlavor), gameFlavor, null),
    };

    private static Dictionary<ulong, string> TryReadAhtbDictionary(GFPack pack, string fileName)
    {
        try
        {
            return pack.GetIndexFileName(fileName) < 0
                ? []
                : new AHTB(pack.GetDataFileName(fileName)).ToDictionary();
        }
        catch (ArgumentException)
        {
            return [];
        }
        catch (IndexOutOfRangeException)
        {
            return [];
        }
    }

    private static void WriteOutputBytes(string outputRoot, string relativePath, byte[] data)
    {
        var outputPath = Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, data);
    }

    private static void CommitLayeredRestore(string outputRoot, string relativePath, byte[] restoredData, byte[] baselineData, ISet<string> changedDirectories, List<string> notes, byte[]? normalizedBaselineData = null)
    {
        if (restoredData.SequenceEqual(baselineData) || normalizedBaselineData is not null && restoredData.SequenceEqual(normalizedBaselineData))
        {
            if (DeleteOutputFile(outputRoot, relativePath, changedDirectories))
                notes.Add($"- Removed {relativePath}; restored data now matches vanilla.");
            return;
        }

        WriteOutputBytes(outputRoot, relativePath, restoredData);
        var outputPath = GetContainedOutputPath(outputRoot, relativePath);
        if (Path.GetDirectoryName(outputPath) is { } directory)
            changedDirectories.Add(directory);
        notes.Add($"- Updated {relativePath}; Royal Candy-owned records were restored while unrelated layered data was preserved.");
    }

    private static int RemoveLayeredFilesMatchingBaseDump(RoyalCandyBuildOptions options, List<BuildResult> results, List<string> notes, ISet<string> changedDirectories)
    {
        if (!Directory.Exists(options.OutputPath))
            return 0;

        var removed = 0;
        foreach (var outputPath in Directory.EnumerateFiles(options.OutputPath, "*", SearchOption.AllDirectories).ToArray())
        {
            var relativePath = Path.GetRelativePath(options.OutputPath, outputPath).Replace(Path.DirectorySeparatorChar, '/');
            var basePath = GetBasePathForLayeredRelativePath(options, relativePath);
            if (basePath is null || !File.Exists(basePath))
                continue;

            if (!FilesAreEqual(outputPath, basePath))
                continue;

            File.Delete(outputPath);
            removed++;
            if (Path.GetDirectoryName(outputPath) is { } directory)
                changedDirectories.Add(directory);
            notes.Add($"- Removed {relativePath}; layered file matched the base dump exactly.");
        }

        results.Add(new(removed == 0 ? "Info" : "Pass", "Uninstall", options.OutputPath, removed == 0 ? "No vanilla-identical layered files remained after uninstall." : $"Removed {removed:N0} vanilla-identical layered file(s)."));
        return removed;
    }

    private static string? GetBasePathForLayeredRelativePath(RoyalCandyBuildOptions options, string relativePath)
    {
        const string RomFsPrefix = "romfs/";
        const string ExeFsPrefix = "exefs/";
        if (relativePath.StartsWith(RomFsPrefix, StringComparison.OrdinalIgnoreCase))
            return GetBaseRomFsPath(options, relativePath[RomFsPrefix.Length..]);
        if (relativePath.StartsWith(ExeFsPrefix, StringComparison.OrdinalIgnoreCase))
            return GetBaseExeFsPath(options, relativePath[ExeFsPrefix.Length..]);
        return null;
    }

    private static bool FilesAreEqual(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
            return false;

        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);
        Span<byte> leftBuffer = stackalloc byte[8192];
        Span<byte> rightBuffer = stackalloc byte[8192];
        while (true)
        {
            var leftRead = left.Read(leftBuffer);
            var rightRead = right.Read(rightBuffer);
            if (leftRead != rightRead)
                return false;
            if (leftRead == 0)
                return true;
            if (!leftBuffer[..leftRead].SequenceEqual(rightBuffer[..rightRead]))
                return false;
        }
    }

    private static bool DeleteOutputFile(string outputRoot, string relativePath, ISet<string> deletedDirectories)
    {
        var outputPath = GetContainedOutputPath(outputRoot, relativePath);
        if (!File.Exists(outputPath))
            return false;

        File.Delete(outputPath);
        if (Path.GetDirectoryName(outputPath) is { } directory)
            deletedDirectories.Add(directory);
        return true;
    }

    private static int PruneEmptyOutputDirectories(string outputRoot, IEnumerable<string> startDirectories)
    {
        var root = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var removed = 0;
        foreach (var start in startDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = Path.GetFullPath(start);
            while (!string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
                && IsPathInsideRoot(root, directory)
                && Directory.Exists(directory)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
                removed++;
                directory = Directory.GetParent(directory)?.FullName ?? root;
            }
        }

        return removed;
    }

    private static string GetContainedOutputPath(string outputRoot, string relativePath)
    {
        var root = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var outputPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathInsideRoot(root, outputPath))
            throw new InvalidOperationException($"Refusing to touch output path outside the selected LayeredFS root: {relativePath}");

        return outputPath;
    }

    private static bool IsPathInsideRoot(string root, string path) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private enum Arm64Condition
    {
        EQ = 0,
        NE = 1,
        HS = 2,
        HI = 8,
        LT = 11,
        GT = 12,
    }

    private enum RoyalSwordLevelCapMilestoneKind
    {
        Flag,
        WorkAtLeast,
    }

    private sealed record RareCandyUiCheck(int CompareOffset, int ItemRegister, int PassOffset, int FailOffset);
    private sealed record RoyalSwordLevelCapMilestone(int Cap, ulong FlagHash, string Label, RoyalSwordLevelCapMilestoneKind Kind = RoyalSwordLevelCapMilestoneKind.Flag, int WorkMinimum = 0);
    private sealed record RoyalSwordLevelCapCheckChunks(int LoadGlobal, int LoadTable, int HashLow, int HashHigh, int Call, int Restore, int Decision, int ReturnCap);
    private sealed record ZeroRun(int Offset, int Length);
}

internal sealed record RoyalCandyBuildOptions(
    string RomFsPath,
    string ExeFsPath,
    string OutputPath,
    int ItemId,
    int TemplateItemId,
    bool BuildRomFs,
    bool BuildExeFs,
    bool StoryCapLadder,
    bool InfiniteUse,
    int? VirtualCount,
    int? MaxStoryCap,
    bool GrantOnBagEvent,
    string ItemDescription,
    RoyalCandyGameFlavor GameFlavor,
    int DefaultCap,
    IReadOnlyList<RoyalCandyCapMilestone>? CustomCapMilestones,
    RoyalCandyBuildMode Mode);

internal sealed record RoyalCandyBuildSummary(IReadOnlyList<BuildResult> Results, IReadOnlyList<string> Notes);
internal sealed record RoyalCandyBuildPreflight(
    bool Passed,
    string Message,
    IReadOnlyList<BuildResult> Results,
    IReadOnlyList<string> Notes,
    RoyalCandyInstallScan? InstalledRoyalCandy = null,
    RoyalSwordExeFsSignatureScan? ExeFsSignatureScan = null);
internal sealed record RoyalCandyInstallScan(RoyalCandyBuildMode Mode, RoyalCandyGameFlavor GameFlavor, string Description, IReadOnlyList<string> Details);
internal sealed record BuildResult(string Status, string Area, string Output, string Message);

internal enum RoyalCandyBuildMode
{
    Unlimited,
    CustomLimits,
}

internal enum RoyalCandyGameFlavor
{
    Sword,
    Shield,
}

internal enum RoyalCandyCapMilestoneKind
{
    Flag,
    WorkAtLeast,
}

internal sealed record RoyalCandyCapMilestoneDefinition(
    int DefaultCap,
    ulong ProgressHash,
    string SwordName,
    string ShieldName,
    RoyalCandyCapMilestoneKind Kind = RoyalCandyCapMilestoneKind.Flag,
    int WorkMinimum = 0)
{
    public string GetDisplayName(RoyalCandyGameFlavor game) => game == RoyalCandyGameFlavor.Shield ? ShieldName : SwordName;
}

internal sealed record RoyalCandyCapMilestone(int Cap, RoyalCandyCapMilestoneDefinition Definition);

internal sealed record RoyalCandyProjectStatus(
    bool Ready,
    RoyalCandyGameFlavor? DetectedFlavor,
    string Message,
    IReadOnlyList<BuildResult> Results,
    IReadOnlyList<string> LogLines);
