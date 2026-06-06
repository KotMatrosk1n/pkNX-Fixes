using pkNX.Containers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class RoyalSwordPatchManagerForm : Form
{
    private const int RareCandyItemId = 50;
    private const int RoyalCandyItemId = 1128;
    private const int RareCandyUiHookCodeCaveSearchStart = 0x007BC338;

    private readonly string InitialExeFsPath;
    private readonly TextBox MainPathBox = new();
    private readonly Button BrowseButton = new();
    private readonly Button ReloadButton = new();
    private readonly TextBox SummaryText = new();
    private readonly ComboBox ScopeFilter = new();
    private readonly TextBox SearchBox = new();
    private readonly DataGridView ValidationGrid = new();
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

    private readonly List<PatchValidationEntry> Entries = [];
    private readonly List<PatchValidationEntry> VisibleEntries = [];
    private PatchValidationEntry? SelectedEntry;
    private string? CurrentMainPath;

    public RoyalSwordPatchManagerForm(string exefsPath)
    {
        InitialExeFsPath = exefsPath;

        Text = "Royal Sword Patch Manager";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 660);
        Size = new Size(1320, 760);

        InitializeLayout();
        ApplyTheme();

        var mainPath = Path.Combine(InitialExeFsPath, "main");
        if (File.Exists(mainPath))
            LoadMain(mainPath);
        else
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
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

        MainPathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        MainPathBox.ReadOnly = true;
        MainPathBox.PlaceholderText = "Open exefs/main...";
        MainPathBox.Margin = new Padding(0, 3, 8, 3);

        ConfigureActionButton(BrowseButton, "Browse", "Open an ExeFS main NSO file.", BrowseForMain);
        ConfigureActionButton(ReloadButton, "Reload", "Reload the selected main file from disk.", ReloadMain);
        ReloadButton.Enabled = false;

        CountLabel.AutoEllipsis = true;
        CountLabel.Dock = DockStyle.Fill;
        CountLabel.TextAlign = ContentAlignment.MiddleRight;

        fileRow.Controls.Add(CreateLabel("Main"), 0, 0);
        fileRow.Controls.Add(MainPathBox, 1, 0);
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
        ScopeFilter.Items.AddRange(["All", "Pass", "Warning", "Fail", "Info"]);
        ScopeFilter.SelectedIndex = 0;
        ScopeFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        ScopeFilter.Margin = new Padding(0, 3, 14, 3);
        ScopeFilter.SelectedIndexChanged += (_, _) => QueueRefreshGrid();

        SearchBox.PlaceholderText = "Search check, area, offset, expected, actual, or notes...";
        SearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SearchBox.Margin = new Padding(0, 3, 14, 3);
        SearchBox.TextChanged += (_, _) => QueueRefreshGrid();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        ConfigureActionButton(CopyVisibleButton, "Copy Visible", "Copy all visible validation rows as TSV.", CopyVisibleRows);
        ConfigureActionButton(CopySelectedButton, "Copy Row", "Copy the selected validation row as TSV.", CopySelectedRow);
        actions.Controls.Add(CopyVisibleButton);
        actions.Controls.Add(CopySelectedButton);

        top.Controls.Add(CreateLabel("Scope"), 0, 0);
        top.Controls.Add(ScopeFilter, 1, 0);
        top.Controls.Add(CreateLabel("Search"), 2, 0);
        top.Controls.Add(SearchBox, 3, 0);
        top.Controls.Add(actions, 4, 0);

        ConfigureGrid(ValidationGrid);
        ValidationGrid.VirtualMode = true;
        ValidationGrid.CellValueNeeded += ValidationGrid_CellValueNeeded;
        ValidationGrid.SelectionChanged += (_, _) => SelectCurrentEntry();
        ValidationGrid.Columns.Add(CreateTextColumn("Status", 86));
        ValidationGrid.Columns.Add(CreateTextColumn("Area", 110));
        ValidationGrid.Columns.Add(CreateTextColumn("Offset", 94));
        ValidationGrid.Columns.Add(CreateTextColumn("Check", 270));
        ValidationGrid.Columns.Add(CreateTextColumn("Expected", 116));
        ValidationGrid.Columns.Add(CreateTextColumn("Actual", 116));
        ValidationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Notes",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 320,
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
        root.Controls.Add(ValidationGrid, 0, 2);
        root.Controls.Add(DetailsText, 0, 3);
        Controls.Add(root);
    }

    private void BrowseForMain()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open ExeFS main",
            Filter = "Nintendo Switch main NSO|main|All files|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadMain(dialog.FileName);
    }

    private void ReloadMain()
    {
        if (!string.IsNullOrWhiteSpace(CurrentMainPath))
            LoadMain(CurrentMainPath);
    }

    private void LoadMain(string path)
    {
        Entries.Clear();
        CurrentMainPath = path;
        MainPathBox.Text = path;
        ReloadButton.Enabled = true;

        try
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < NSOHeader.SIZE)
            {
                SummaryText.Text = $"File is too small to be an NSO: {data.Length:N0} bytes.";
                Entries.Add(new("Fail", "main", string.Empty, "NSO header", ">= 0x100 bytes", $"{data.Length:X}", "File is shorter than the NSO header."));
                RefreshGrid();
                return;
            }

            var nso = new NSO(data);
            if (!nso.Header.Valid)
            {
                SummaryText.Text = $"NSO magic is invalid: 0x{nso.Header.Magic:X8}.";
                Entries.Add(new("Fail", "main", "0x000000", "NSO magic", "NSO0", $"0x{nso.Header.Magic:X8}", "The selected file is not a valid NSO main."));
                RefreshGrid();
                return;
            }

            BuildValidationEntries(path, data, nso);
            RefreshGrid();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            SummaryText.Text = $"Could not inspect main: {ex.Message}";
            Entries.Add(new("Fail", "main", string.Empty, "Load main", "readable NSO", ex.GetType().Name, ex.Message));
            RefreshGrid();
        }
    }

    private void BuildValidationEntries(string path, byte[] raw, NSO nso)
    {
        var header = nso.Header;
        var text = nso.DecompressedText;
        var ro = nso.DecompressedRO;
        var data = nso.DecompressedData;
        var largestZeroRun = FindLargestZeroRun(text);
        var firstHookCave = FindZeroRun(text, 0xC, RareCandyUiHookCodeCaveSearchStart);

        SummaryText.Text = string.Join(Environment.NewLine, [
            $"Build ID: {ToHex(header.DigestBuildID)}",
            $"File: {Path.GetFileName(path)} | Size: {raw.Length:N0} bytes | Flags: {header.Flags}",
            $"Text: file+0x{header.HeaderText.FileOffset:X}, VA 0x{header.HeaderText.MemoryOffset:X}, decompressed 0x{text.Length:X}, SHA-256 {ToHex(NSO.Hash(text))}",
            $"RO: file+0x{header.HeaderRO.FileOffset:X}, VA 0x{header.HeaderRO.MemoryOffset:X}, decompressed 0x{ro.Length:X}, SHA-256 {ToHex(NSO.Hash(ro))}",
            $"Data: file+0x{header.HeaderData.FileOffset:X}, VA 0x{header.HeaderData.MemoryOffset:X}, decompressed 0x{data.Length:X}, SHA-256 {ToHex(NSO.Hash(data))}",
            $"First 12-byte cave after text+0x{RareCandyUiHookCodeCaveSearchStart:X}: {(firstHookCave < 0 ? "missing" : $"text+0x{firstHookCave:X}")}; largest zero run: text+0x{largestZeroRun.Offset:X} length 0x{largestZeroRun.Length:X}",
        ]);

        AddInfo("main", string.Empty, "NSO magic", "NSO0", "NSO0", "Valid NSO header.");
        AddHashCheck(".text", "Segment hash", header.HashText, NSO.Hash(text));
        AddHashCheck(".ro", "Segment hash", header.HashRO, NSO.Hash(ro));
        AddHashCheck(".data", "Segment hash", header.HashData, NSO.Hash(data));
        AddZeroRunCheck(text, "Patch code cave", 0xC, RareCandyUiHookCodeCaveSearchStart);
        AddZeroRunSummary(largestZeroRun);

        AddInstructionChecks(text, "Rare Candy UI route", [
            new("UI check A", 0x00747988, EncodeCmpImmediate(28, RareCandyItemId), "CMP w28, #50"),
            new("UI check B", 0x00747D44, EncodeCmpImmediate(9, RareCandyItemId), "CMP w9, #50"),
            new("UI check C", 0x0074BA24, EncodeCmpImmediate(26, RareCandyItemId), "CMP w26, #50"),
            new("UI check D", 0x0074BDA8, EncodeCmpImmediate(9, RareCandyItemId), "CMP w9, #50"),
            new("UI check E", 0x0074DFE4, EncodeCmpImmediate(9, RareCandyItemId), "CMP w9, #50"),
            new("UI check F", 0x0074DFF8, EncodeCmpImmediate(28, RareCandyItemId), "CMP w28, #50"),
            new("UI check G", 0x0075CEFC, EncodeCmpImmediate(9, RareCandyItemId), "CMP w9, #50"),
            new("UI check H", 0x007BB204, EncodeCmpImmediate(20, RareCandyItemId), "CMP w20, #50"),
            new("UI check I", 0x007BB3C0, EncodeCmpImmediate(19, RareCandyItemId), "CMP w19, #50"),
            new("UI check J", 0x007BC1F8, EncodeCmpImmediate(8, RareCandyItemId), "CMP w8, #50"),
        ]);

        AddInstructionChecks(text, "Rare Candy equal branch", [
            new("Equal branch A", 0x00747DE0, EncodeCmpImmediate(9, RareCandyItemId), "CMP w9, #50"),
            new("Equal branch B", 0x0074BE44, EncodeCmpImmediate(9, RareCandyItemId), "CMP w9, #50"),
            new("Equal branch C", 0x0075CCE8, EncodeCmpImmediate(27, RareCandyItemId), "CMP w27, #50"),
            new("Equal branch D", 0x0075D08C, EncodeCmpImmediate(10, RareCandyItemId), "CMP w10, #50"),
            new("Equal branch E", 0x007BBFD4, EncodeCmpImmediate(23, RareCandyItemId), "CMP w23, #50"),
        ]);

        AddInstructionChecks(text, "Royal Candy support", [
            new("Exp Candy upper bound A", 0x007BC1BC, EncodeCmpImmediate(9, 4), "CMP w9, #4"),
            new("Exp Candy upper bound B", 0x007BC1C4, EncodeCmpImmediate(9, 4), "CMP w9, #4"),
            new("Consume quantity move", 0x007B1F20, 0x2A0003E2, "MOV w2, w0"),
            new("Allowed consumable upper bound", 0x007DDA8C, EncodeCmpImmediate(8, 0x32), "CMP w8, #0x32"),
        ]);

        var candidateImmediateHits = CountAlignedInstruction(text, EncodeCmpImmediate(8, RoyalCandyItemId))
            + CountAlignedInstruction(text, EncodeCmpImmediate(9, RoyalCandyItemId))
            + CountAlignedInstruction(text, EncodeCmpImmediate(19, RoyalCandyItemId))
            + CountAlignedInstruction(text, EncodeCmpImmediate(20, RoyalCandyItemId))
            + CountAlignedInstruction(text, EncodeCmpImmediate(23, RoyalCandyItemId))
            + CountAlignedInstruction(text, EncodeCmpImmediate(27, RoyalCandyItemId))
            + CountAlignedInstruction(text, EncodeCmpImmediate(28, RoyalCandyItemId));
        Entries.Add(new(
            candidateImmediateHits == 0 ? "Info" : "Warning",
            ".text",
            string.Empty,
            "Royal Candy immediate scan",
            "0 patched CMP immediates in vanilla main",
            candidateImmediateHits.ToString(),
            candidateImmediateHits == 0
                ? "No obvious item-id 1128 CMP immediates were found in the known route registers."
                : "Potential already-patched or experimental main; review before applying new patches."));
    }

    private void AddInstructionChecks(byte[] text, string area, IEnumerable<InstructionCheck> checks)
    {
        foreach (var check in checks)
            AddInstructionCheck(text, area, check);
    }

    private void AddInstructionCheck(byte[] text, string area, InstructionCheck check)
    {
        if (check.Offset < 0 || check.Offset + 4 > text.Length)
        {
            Entries.Add(new("Fail", area, FormatOffset(check.Offset), check.Name, FormatInstruction(check.Expected), "outside .text", "Expected instruction offset is outside the decompressed .text segment."));
            return;
        }

        var actual = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(check.Offset, 4));
        Entries.Add(new(
            actual == check.Expected ? "Pass" : "Fail",
            area,
            FormatOffset(check.Offset),
            check.Name,
            $"{check.Description} / {FormatInstruction(check.Expected)}",
            FormatInstruction(actual),
            actual == check.Expected
                ? "Signature matches the known vanilla anchor."
                : "Signature mismatch. This main may be a different build or already patched."));
    }

    private void AddHashCheck(string area, string name, byte[] expected, byte[] actual)
    {
        var matches = expected.SequenceEqual(actual);
        Entries.Add(new(
            matches ? "Pass" : "Warning",
            area,
            string.Empty,
            name,
            ToHex(expected),
            ToHex(actual),
            matches ? "Segment hash matches the NSO header." : "Segment hash differs from the NSO header."));
    }

    private void AddZeroRunCheck(byte[] text, string name, int requiredBytes, int startOffset)
    {
        var offset = FindZeroRun(text, requiredBytes, startOffset);
        Entries.Add(new(
            offset >= 0 ? "Pass" : "Fail",
            ".text",
            offset >= 0 ? FormatOffset(offset) : string.Empty,
            name,
            $"{requiredBytes} zero bytes after text+0x{startOffset:X}",
            offset >= 0 ? $"text+0x{offset:X}" : "missing",
            offset >= 0 ? "A code cave is available for small stubs." : "No aligned zero run was found for this stub size."));
    }

    private void AddZeroRunSummary(ZeroRun run)
    {
        Entries.Add(new(
            run.Length >= 0xC ? "Info" : "Warning",
            ".text",
            run.Offset >= 0 ? FormatOffset(run.Offset) : string.Empty,
            "Largest zero run",
            "at least 0xC bytes",
            $"0x{run.Length:X} bytes",
            run.Offset >= 0 ? $"Largest continuous zero-filled region starts at text+0x{run.Offset:X}." : "No zero-filled region found."));
    }

    private void AddInfo(string area, string offset, string name, string expected, string actual, string notes) =>
        Entries.Add(new("Info", area, offset, name, expected, actual, notes));

    private void QueueRefreshGrid()
    {
        FilterTimer.Stop();
        FilterTimer.Start();
    }

    private void RefreshGrid()
    {
        var scope = ScopeFilter.SelectedItem as string ?? "All";
        var query = SearchBox.Text.Trim();
        var selected = SelectedEntry;
        var filtered = Entries.Where(z => MatchesFilter(z, scope, query)).ToArray();

        ValidationGrid.SuspendLayout();
        ValidationGrid.RowCount = 0;
        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        ValidationGrid.RowCount = VisibleEntries.Count;
        ValidationGrid.ResumeLayout();

        CountLabel.Text = $"{VisibleEntries.Count:N0} / {Entries.Count:N0}";

        if (VisibleEntries.Count == 0)
        {
            SelectEntry(null);
            return;
        }

        var index = selected == null ? 0 : VisibleEntries.FindIndex(z => z.Equals(selected));
        if (index < 0)
            index = 0;

        ValidationGrid.CurrentCell = ValidationGrid.Rows[index].Cells[0];
        ValidationGrid.Rows[index].Selected = true;
        SelectEntry(VisibleEntries[index]);
    }

    private void ValidationGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= VisibleEntries.Count)
            return;

        var entry = VisibleEntries[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => entry.Status,
            1 => entry.Area,
            2 => entry.Offset,
            3 => entry.Name,
            4 => entry.Expected,
            5 => entry.Actual,
            6 => entry.Notes,
            _ => string.Empty,
        };
    }

    private static bool MatchesFilter(PatchValidationEntry entry, string scope, string query)
    {
        if (scope != "All" && !entry.Status.Equals(scope, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Length == 0)
            return true;

        return entry.Status.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Area.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Offset.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Expected.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Actual.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Notes.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectCurrentEntry()
    {
        if (ValidationGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= VisibleEntries.Count)
        {
            SelectEntry(null);
            return;
        }

        SelectEntry(VisibleEntries[row]);
    }

    private void SelectEntry(PatchValidationEntry? entry)
    {
        SelectedEntry = entry;
        if (entry == null)
        {
            DetailsText.Text = CurrentMainPath == null ? "No ExeFS main loaded." : "No validation row selected.";
            CopySelectedButton.Enabled = false;
            CopyVisibleButton.Enabled = VisibleEntries.Count != 0;
            return;
        }

        DetailsText.Text = string.Join(Environment.NewLine, [
            $"Status: {entry.Status}",
            $"Area: {entry.Area}",
            $"Offset: {entry.Offset}",
            $"Check: {entry.Name}",
            $"Expected: {entry.Expected}",
            $"Actual: {entry.Actual}",
            $"Notes: {entry.Notes}",
            string.Empty,
            "TSV:",
            ToTsv(entry),
        ]);
        CopySelectedButton.Enabled = true;
        CopyVisibleButton.Enabled = VisibleEntries.Count != 0;
    }

    private void CopySelectedRow()
    {
        if (SelectedEntry is not { } entry)
            return;

        Clipboard.SetText(TsvHeader() + Environment.NewLine + ToTsv(entry));
    }

    private void CopyVisibleRows()
    {
        var rows = VisibleEntries.Select(ToTsv);
        Clipboard.SetText(TsvHeader() + Environment.NewLine + string.Join(Environment.NewLine, rows));
    }

    private static int CountAlignedInstruction(byte[] text, uint instruction)
    {
        var count = 0;
        for (var offset = 0; offset <= text.Length - 4; offset += 4)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset, 4)) == instruction)
                count++;
        }

        return count;
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

    private static uint EncodeCmpImmediate(int register, int immediate) =>
        (uint)(0x7100001F | ((immediate & 0xFFF) << 10) | ((register & 0x1F) << 5));

    private static string FormatInstruction(uint instruction) => $"0x{instruction:X8}";
    private static string FormatOffset(int offset) => $"text+0x{offset:X}";
    private static string ToHex(byte[] data) => Convert.ToHexString(data);
    private static string TsvHeader() => "Status\tArea\tOffset\tCheck\tExpected\tActual\tNotes";

    private static string ToTsv(PatchValidationEntry entry) =>
        $"{entry.Status}\t{entry.Area}\t{entry.Offset}\t{CleanTsv(entry.Name)}\t{CleanTsv(entry.Expected)}\t{CleanTsv(entry.Actual)}\t{CleanTsv(entry.Notes)}";

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
        WinFormsTheme.Apply(this);
    }

    private sealed record InstructionCheck(string Name, int Offset, uint Expected, string Description);
    private sealed record PatchValidationEntry(string Status, string Area, string Offset, string Name, string Expected, string Actual, string Notes);
    private sealed record ZeroRun(int Offset, int Length);
}
