using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class RoyalSwordSaveInspector : Form
{
    private const int DefaultBaseCap = 10;
    private const ulong SceneMainMasterWorkHash = 0x00188D41BB7B57FB;
    private const ulong HopEndorsementFlagHash = 0x005A329212277F11;

    private static readonly RoyalSaveMilestone[] Milestones =
    [
        new(90, SceneMainMasterWorkHash, "Leon 149/189/190 clear, post-Leon main_event_3000 progress", RoyalSaveMilestoneKind.WorkAtLeast, 3000),
        new(85, SceneMainMasterWorkHash, "Rose 175 clear, story progress reaches post-Rose main_event_1910", RoyalSaveMilestoneKind.WorkAtLeast, 1910),
        new(80, SceneMainMasterWorkHash, "Raihan 213 finals clear, pre-Leon story progress", RoyalSaveMilestoneKind.WorkAtLeast, 1780),
        new(75, SceneMainMasterWorkHash, "Oleana 143 clear, Rose Tower resolved", RoyalSaveMilestoneKind.WorkAtLeast, 1660),
        new(70, SceneMainMasterWorkHash, "Hop 130/131/132 Semifinals clear", RoyalSaveMilestoneKind.WorkAtLeast, 1550),
        new(65, 0xE336BF34143E0946, "Raihan gym clear (FE_GC_DORAGON_CLEAR)"),
        new(60, 0xA52A7561C28A76F1, "Piers gym clear (FE_GC_AKU_CLEAR)"),
        new(55, SceneMainMasterWorkHash, "Marnie 138 Route 9/Spikemuth clear", RoyalSaveMilestoneKind.WorkAtLeast, 1330),
        new(54, SceneMainMasterWorkHash, "Hop 202/203/204 Hero's Bath clear", RoyalSaveMilestoneKind.WorkAtLeast, 1300),
        new(52, 0x7042D310DF3DB17F, "Gordie 135 / Melony 136 gym clear (FE_GC_IWAKO_CLEAR shared Gym 6 flag)"),
        new(50, SceneMainMasterWorkHash, "Hop 127/128/129 Route 7 clear", RoyalSaveMilestoneKind.WorkAtLeast, 1200),
        new(47, 0xDF7AC7105B946783, "Opal gym clear (FE_GC_FAIRY_CLEAR)"),
        new(44, SceneMainMasterWorkHash, "Bede 133 Stow-on-Side mural clear", RoyalSaveMilestoneKind.WorkAtLeast, 1090),
        new(42, 0xC07B67FC3148B754, "Bea gym clear, Sword (FE_GC_KAKUGO_CLEAR)"),
        new(40, SceneMainMasterWorkHash, "Hop 124/125/126 Stow-on-Side clear", RoyalSaveMilestoneKind.WorkAtLeast, 950),
        new(38, 0xABFC3E0B626D6B24, "Kabu gym clear (FE_GC_HONO_CLEAR)"),
        new(36, SceneMainMasterWorkHash, "Marnie 196 Budew Drop Inn clear", RoyalSaveMilestoneKind.WorkAtLeast, 760),
        new(32, SceneMainMasterWorkHash, "Bede 240 Galar Mine No. 2 clear", RoyalSaveMilestoneKind.WorkAtLeast, 720),
        new(30, 0x8B4F4365890D1CF9, "Nessa gym clear (FE_GC_MIZU_CLEAR)"),
        new(28, SceneMainMasterWorkHash, "Hop 121/122/123 Hulbury clear", RoyalSaveMilestoneKind.WorkAtLeast, 640),
        new(25, 0xB02911749203329A, "Milo gym clear (FE_GC_KUSA_CLEAR)"),
        new(23, SceneMainMasterWorkHash, "Bede 195 Galar Mine clear", RoyalSaveMilestoneKind.WorkAtLeast, 550),
        new(20, SceneMainMasterWorkHash, "Hop 191/192/193 Motostoke post-battle progress", RoyalSaveMilestoneKind.WorkAtLeast, 530),
        new(16, HopEndorsementFlagHash, "Hop 007/008/009 endorsement battle clear (FE_EV0280_WIN)"),
    ];

    private readonly TextBox SavePathBox = new();
    private readonly Button BrowseButton = new();
    private readonly Button ReloadButton = new();
    private readonly ComboBox ScopeFilter = new();
    private readonly TextBox SearchBox = new();
    private readonly TextBox SummaryText = new();
    private readonly DataGridView MilestoneGrid = new();
    private readonly TextBox DetailsText = new();
    private readonly Button CopySelectedButton = new();
    private readonly Button CopyVisibleButton = new();
    private readonly Label CountLabel = new();
    private readonly Timer FilterTimer = new() { Interval = 200 };
    private readonly ToolTip ButtonToolTips = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 450,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    private readonly List<RoyalSaveMilestoneResult> Results = [];
    private readonly List<RoyalSaveMilestoneResult> VisibleResults = [];
    private string? CurrentSavePath;
    private SAV8SWSH? CurrentSave;
    private RoyalSaveMilestoneResult? SelectedResult;

    public RoyalSwordSaveInspector()
    {
        Text = "Royal Sword Save Inspector";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1040, 640);
        Size = new Size(1240, 740);

        InitializeLayout();
        ApplyTheme();
        EvaluateMilestones(null);
        RefreshGrid();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FilterTimer.Dispose();
            ButtonToolTips.Dispose();
        }

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var fileRow = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 6, 0, 6),
            RowCount = 1,
        };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        SavePathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SavePathBox.ReadOnly = true;
        SavePathBox.PlaceholderText = "Open a Sword/Shield main save...";
        SavePathBox.Margin = new Padding(0, 3, 8, 3);

        ConfigureActionButton(BrowseButton, "Browse", "Open a Sword/Shield main save file.", BrowseForSave);
        ConfigureActionButton(ReloadButton, "Reload", "Reload the selected save from disk.", ReloadSave);
        ReloadButton.Enabled = false;

        CountLabel.AutoEllipsis = true;
        CountLabel.Dock = DockStyle.Fill;
        CountLabel.TextAlign = ContentAlignment.MiddleRight;

        fileRow.Controls.Add(CreateLabel("Save"), 0, 0);
        fileRow.Controls.Add(SavePathBox, 1, 0);
        fileRow.Controls.Add(BrowseButton, 2, 0);
        fileRow.Controls.Add(ReloadButton, 3, 0);
        fileRow.Controls.Add(CountLabel, 4, 0);

        SummaryText.Dock = DockStyle.Fill;
        SummaryText.Multiline = true;
        SummaryText.ReadOnly = true;
        SummaryText.ScrollBars = ScrollBars.Vertical;
        SummaryText.WordWrap = false;

        var top = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 6),
            RowCount = 1,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244));

        ScopeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        ScopeFilter.Items.AddRange(["All", "Unlocked", "Locked", "Missing"]);
        ScopeFilter.SelectedIndex = 0;
        ScopeFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        ScopeFilter.Margin = new Padding(0, 3, 14, 3);
        ScopeFilter.SelectedIndexChanged += (_, _) => QueueRefreshGrid();

        SearchBox.PlaceholderText = "Search cap, hash, state, or label...";
        SearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SearchBox.Margin = new Padding(0, 3, 14, 3);
        SearchBox.TextChanged += (_, _) => QueueRefreshGrid();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 0, 0, 0),
            WrapContents = false,
        };
        ConfigureActionButton(CopyVisibleButton, "Copy Visible", "Copy all visible milestone rows as TSV.", CopyVisibleRows);
        ConfigureActionButton(CopySelectedButton, "Copy Row", "Copy the selected milestone row as TSV.", CopySelectedRow);
        actions.Controls.Add(CopyVisibleButton);
        actions.Controls.Add(CopySelectedButton);

        top.Controls.Add(CreateLabel("Scope"), 0, 0);
        top.Controls.Add(ScopeFilter, 1, 0);
        top.Controls.Add(CreateLabel("Search"), 2, 0);
        top.Controls.Add(SearchBox, 3, 0);
        top.Controls.Add(actions, 4, 0);

        ConfigureGrid(MilestoneGrid);
        MilestoneGrid.VirtualMode = true;
        MilestoneGrid.CellValueNeeded += MilestoneGrid_CellValueNeeded;
        MilestoneGrid.SelectionChanged += (_, _) => SelectCurrentMilestone();
        MilestoneGrid.Columns.Add(CreateTextColumn("Cap", 58));
        MilestoneGrid.Columns.Add(CreateTextColumn("Unlocked", 86));
        MilestoneGrid.Columns.Add(CreateTextColumn("Kind", 112));
        MilestoneGrid.Columns.Add(CreateTextColumn("Key32", 104));
        MilestoneGrid.Columns.Add(CreateTextColumn("Key64", 156));
        MilestoneGrid.Columns.Add(CreateTextColumn("State", 180));
        MilestoneGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Label",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 360,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        DetailsText.Dock = DockStyle.Fill;
        DetailsText.Multiline = true;
        DetailsText.ReadOnly = true;
        DetailsText.ScrollBars = ScrollBars.Vertical;
        DetailsText.WordWrap = false;

        FilterTimer.Tick += (_, _) =>
        {
            FilterTimer.Stop();
            RefreshGrid();
        };

        root.Controls.Add(fileRow, 0, 0);
        root.Controls.Add(SummaryText, 0, 1);
        root.Controls.Add(MilestoneGrid, 0, 2);
        root.Controls.Add(DetailsText, 0, 3);
        Controls.Add(root);
    }

    private void BrowseForSave()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open Sword/Shield Save",
            Filter = "Sword/Shield main save|main|All files|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadSave(dialog.FileName);
    }

    private void ReloadSave()
    {
        if (!string.IsNullOrWhiteSpace(CurrentSavePath))
            LoadSave(CurrentSavePath);
    }

    private void LoadSave(string path)
    {
        try
        {
            var save = new SAV8SWSH(File.ReadAllBytes(path));
            CurrentSave = save;
            CurrentSavePath = path;
            SavePathBox.Text = path;
            ReloadButton.Enabled = true;
            EvaluateMilestones(save);
            RefreshGrid();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            CurrentSave = null;
            CurrentSavePath = null;
            SavePathBox.Text = string.Empty;
            ReloadButton.Enabled = false;
            EvaluateMilestones(null);
            SummaryText.Text = $"Could not load save: {ex.Message}";
            RefreshGrid();
        }
    }

    private void EvaluateMilestones(SAV8SWSH? save)
    {
        Results.Clear();
        foreach (var milestone in Milestones.OrderBy(z => z.Cap))
            Results.Add(save == null
                ? new(milestone, new(false, "not loaded", true))
                : new(milestone, EvaluateMilestone(save, milestone)));

        SummaryText.Text = save == null
            ? "No save loaded."
            : BuildSaveSummary(save, Results);
    }

    private static string BuildSaveSummary(SAV8SWSH save, IReadOnlyList<RoyalSaveMilestoneResult> results)
    {
        var currentCap = results
            .Where(z => z.State.Unlocked)
            .Select(z => z.Milestone.Cap)
            .DefaultIfEmpty(DefaultBaseCap)
            .Max();

        return string.Join(Environment.NewLine, [
            $"Royal Current Cap: {currentCap}",
            $"Version: {save.Version} | OT: {save.OT} | ID: {save.DisplayTID:000000} | Badges Raw: {save.Badges}",
            $"Checksums Valid: {save.ChecksumsValid} | Blocks: {save.AllBlocks.Count} | Party: {save.PartyCount} | Played: {save.PlayTimeString} | Money: {save.Money:N0}",
        ]);
    }

    private static RoyalSaveMilestoneState EvaluateMilestone(SAV8SWSH save, RoyalSaveMilestone milestone)
    {
        var key = (uint)milestone.Hash;
        if (!save.Accessor.TryGetBlock(key, out var block))
            return new(false, "missing", true);

        return milestone.Kind switch
        {
            RoyalSaveMilestoneKind.Flag => EvaluateFlagBlock(block),
            RoyalSaveMilestoneKind.WorkAtLeast => EvaluateWorkBlock(block, milestone.WorkMinimum),
            _ => new(false, $"unsupported kind {milestone.Kind}", false),
        };
    }

    private static RoyalSaveMilestoneState EvaluateFlagBlock(SCBlock block) =>
        block.Type switch
        {
            SCTypeCode.Bool2 => new(true, "true (Bool2)", false),
            SCTypeCode.Bool1 => new(false, "false (Bool1)", false),
            SCTypeCode.Bool3 when block.Data.Length > 0 && block.Data[0] != 0 => new(true, "true (Bool3 data)", false),
            SCTypeCode.Bool3 => new(false, "false (Bool3 data)", false),
            _ => new(false, $"not boolean: {block.Type}", false),
        };

    private static RoyalSaveMilestoneState EvaluateWorkBlock(SCBlock block, int minimum)
    {
        if (!TryReadInteger(block, out var value))
            return new(false, $"not numeric: {block.Type}", false);

        return new(value >= minimum, $"{value} >= {minimum}", false);
    }

    private static bool TryReadInteger(SCBlock block, out long value)
    {
        value = 0;
        try
        {
            var raw = block.GetValue();
            value = raw switch
            {
                byte z => z,
                sbyte z => z,
                ushort z => z,
                short z => z,
                uint z => z,
                int z => z,
                ulong z when z <= long.MaxValue => (long)z,
                long z => z,
                _ => 0,
            };
            return raw is byte or sbyte or ushort or short or uint or int or ulong or long;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private void QueueRefreshGrid()
    {
        FilterTimer.Stop();
        FilterTimer.Start();
    }

    private void RefreshGrid()
    {
        var scope = ScopeFilter.SelectedItem as string ?? "All";
        var query = SearchBox.Text.Trim();
        var selected = SelectedResult;
        var filtered = Results.Where(z => MatchesFilter(z, scope, query)).ToArray();

        MilestoneGrid.SuspendLayout();
        MilestoneGrid.RowCount = 0;
        VisibleResults.Clear();
        VisibleResults.AddRange(filtered);
        MilestoneGrid.RowCount = VisibleResults.Count;
        MilestoneGrid.ResumeLayout();

        CountLabel.Text = $"{VisibleResults.Count:N0} / {Results.Count:N0}";

        if (VisibleResults.Count == 0)
        {
            SelectMilestone(null);
            return;
        }

        var index = selected == null ? 0 : VisibleResults.FindIndex(z => z.Milestone.Cap == selected.Milestone.Cap);
        if (index < 0)
            index = 0;

        MilestoneGrid.CurrentCell = MilestoneGrid.Rows[index].Cells[0];
        MilestoneGrid.Rows[index].Selected = true;
        SelectMilestone(VisibleResults[index]);
    }

    private void MilestoneGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= VisibleResults.Count)
            return;

        var result = VisibleResults[e.RowIndex];
        var milestone = result.Milestone;
        e.Value = e.ColumnIndex switch
        {
            0 => milestone.Cap.ToString(),
            1 => result.State.Unlocked ? "Yes" : "No",
            2 => FormatKind(milestone),
            3 => FormatLow32(milestone.Hash),
            4 => FormatHash(milestone.Hash),
            5 => result.State.Description,
            6 => milestone.Label,
            _ => string.Empty,
        };
    }

    private static bool MatchesFilter(RoyalSaveMilestoneResult result, string scope, string query)
    {
        if (scope == "Unlocked" && !result.State.Unlocked)
            return false;
        if (scope == "Locked" && result.State.Unlocked)
            return false;
        if (scope == "Missing" && !result.State.Missing)
            return false;

        if (query.Length == 0)
            return true;

        var milestone = result.Milestone;
        var hash = FormatHash(milestone.Hash);
        var low32 = FormatLow32(milestone.Hash);
        return milestone.Cap.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
            || milestone.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
            || FormatKind(milestone).Contains(query, StringComparison.OrdinalIgnoreCase)
            || result.State.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || hash.Contains(query, StringComparison.OrdinalIgnoreCase)
            || hash[2..].Contains(query, StringComparison.OrdinalIgnoreCase)
            || low32.Contains(query, StringComparison.OrdinalIgnoreCase)
            || low32[2..].Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectCurrentMilestone()
    {
        if (MilestoneGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= VisibleResults.Count)
        {
            SelectMilestone(null);
            return;
        }

        SelectMilestone(VisibleResults[row]);
    }

    private void SelectMilestone(RoyalSaveMilestoneResult? result)
    {
        SelectedResult = result;
        if (result == null)
        {
            DetailsText.Text = CurrentSave == null ? "No save loaded." : "No milestone selected.";
            CopySelectedButton.Enabled = false;
            CopyVisibleButton.Enabled = VisibleResults.Count != 0;
            return;
        }

        DetailsText.Text = BuildDetails(result);
        CopySelectedButton.Enabled = true;
        CopyVisibleButton.Enabled = VisibleResults.Count != 0;
    }

    private static string BuildDetails(RoyalSaveMilestoneResult result)
    {
        var milestone = result.Milestone;
        return string.Join(Environment.NewLine, [
            $"Cap: {milestone.Cap}",
            $"Unlocked: {(result.State.Unlocked ? "Yes" : "No")}",
            $"Kind: {FormatKind(milestone)}",
            $"Key64: {FormatHash(milestone.Hash)}",
            $"Key32: {FormatLow32(milestone.Hash)}",
            $"State: {result.State.Description}",
            $"Label: {milestone.Label}",
            string.Empty,
            "TSV:",
            ToTsv(result),
        ]);
    }

    private void CopySelectedRow()
    {
        if (SelectedResult is not { } result)
            return;

        Clipboard.SetText(TsvHeader() + Environment.NewLine + ToTsv(result));
    }

    private void CopyVisibleRows()
    {
        var rows = VisibleResults.Select(ToTsv);
        Clipboard.SetText(TsvHeader() + Environment.NewLine + string.Join(Environment.NewLine, rows));
    }

    private static string FormatKind(RoyalSaveMilestone milestone) =>
        milestone.Kind == RoyalSaveMilestoneKind.WorkAtLeast
            ? $"Work >= {milestone.WorkMinimum}"
            : "Flag";

    private static string FormatHash(ulong hash) => $"0x{hash:X16}";
    private static string FormatLow32(ulong hash) => $"0x{(uint)hash:X8}";
    private static string TsvHeader() => "Cap\tUnlocked\tKind\tKey32\tKey64\tState\tLabel";

    private static string ToTsv(RoyalSaveMilestoneResult result)
    {
        var milestone = result.Milestone;
        return $"{milestone.Cap}\t{result.State.Unlocked}\t{FormatKind(milestone)}\t{FormatLow32(milestone.Hash)}\t{FormatHash(milestone.Hash)}\t{CleanTsv(result.State.Description)}\t{CleanTsv(milestone.Label)}";
    }

    private static string CleanTsv(string value) => value.Replace('\t', ' ').Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.Dock = DockStyle.Fill;
        grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    }

    private static Label CreateLabel(string text) => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, int width) => new()
    {
        HeaderText = header,
        Width = width,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    private void ConfigureActionButton(Button button, string text, string tooltip, Action action)
    {
        button.Text = text;
        button.Width = 88;
        button.Height = 30;
        button.Margin = new Padding(6, 0, 0, 4);
        button.Click += (_, _) => action();
        ButtonToolTips.SetToolTip(button, tooltip);
    }

    private void ApplyTheme()
    {
        BackColor = SystemColors.Control;
        SummaryText.BackColor = SystemColors.Window;
        SummaryText.ForeColor = SystemColors.WindowText;
        MilestoneGrid.BackgroundColor = SystemColors.Window;
        DetailsText.BackColor = SystemColors.Window;
        DetailsText.ForeColor = SystemColors.WindowText;
    }

    private enum RoyalSaveMilestoneKind
    {
        Flag,
        WorkAtLeast,
    }

    private sealed record RoyalSaveMilestone(
        int Cap,
        ulong Hash,
        string Label,
        RoyalSaveMilestoneKind Kind = RoyalSaveMilestoneKind.Flag,
        int WorkMinimum = 0);

    private sealed record RoyalSaveMilestoneState(bool Unlocked, string Description, bool Missing);
    private sealed record RoyalSaveMilestoneResult(RoyalSaveMilestone Milestone, RoyalSaveMilestoneState State);
}
