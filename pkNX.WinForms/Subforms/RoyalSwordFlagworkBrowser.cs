using pkNX.Containers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class RoyalSwordFlagworkBrowser : Form
{
    private readonly string RomFsPath;
    private readonly List<FlagworkEntry> Entries = [];
    private readonly List<FlagworkEntry> VisibleEntries = [];
    private readonly List<string> LoadErrors = [];
    private readonly ComboBox TableFilter = new();
    private readonly TextBox SearchBox = new();
    private readonly DataGridView EntryGrid = new();
    private readonly TextBox DetailsText = new();
    private readonly Button CopyNameButton = new();
    private readonly Button CopyHashButton = new();
    private readonly Button CopyRowButton = new();
    private readonly Button CopyVisibleButton = new();
    private readonly Label SummaryLabel = new();
    private readonly Timer FilterTimer = new() { Interval = 200 };
    private readonly ToolTip ButtonToolTips = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 450,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    private FlagworkEntry? SelectedEntry;

    public RoyalSwordFlagworkBrowser(string romFsPath)
    {
        RomFsPath = romFsPath;

        Text = "Royal Sword Flagwork";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1180, 720);

        InitializeLayout();
        ApplyTheme();
        LoadEntries();
        RefreshTableFilter();
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
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        var top = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 6),
            RowCount = 1,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        TableFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        TableFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        TableFilter.Margin = new Padding(0, 3, 14, 3);
        TableFilter.SelectedIndexChanged += (_, _) => QueueRefreshGrid();

        SearchBox.PlaceholderText = "Search name, table, hash, or low32 key...";
        SearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SearchBox.Margin = new Padding(0, 3, 14, 3);
        SearchBox.TextChanged += (_, _) => QueueRefreshGrid();

        SummaryLabel.AutoEllipsis = true;
        SummaryLabel.Dock = DockStyle.Fill;
        SummaryLabel.TextAlign = ContentAlignment.MiddleRight;

        FilterTimer.Tick += (_, _) =>
        {
            FilterTimer.Stop();
            RefreshGrid();
        };

        top.Controls.Add(CreateLabel("Table"), 0, 0);
        top.Controls.Add(TableFilter, 1, 0);
        top.Controls.Add(CreateLabel("Search"), 2, 0);
        top.Controls.Add(SearchBox, 3, 0);
        top.Controls.Add(SummaryLabel, 4, 0);

        EntryGrid.AllowUserToAddRows = false;
        EntryGrid.AllowUserToDeleteRows = false;
        EntryGrid.AllowUserToResizeRows = false;
        EntryGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        EntryGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        EntryGrid.Dock = DockStyle.Fill;
        EntryGrid.EditMode = DataGridViewEditMode.EditProgrammatically;
        EntryGrid.MultiSelect = false;
        EntryGrid.ReadOnly = true;
        EntryGrid.RowHeadersVisible = false;
        EntryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        EntryGrid.VirtualMode = true;
        EntryGrid.CellValueNeeded += EntryGrid_CellValueNeeded;
        EntryGrid.SelectionChanged += (_, _) => SelectCurrentEntry();
        EntryGrid.Columns.Add(CreateTextColumn("Table", 170));
        EntryGrid.Columns.Add(CreateTextColumn("Index", 64));
        EntryGrid.Columns.Add(CreateTextColumn("Kind", 72));
        EntryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 280,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        EntryGrid.Columns.Add(CreateTextColumn("Hash64", 156));
        EntryGrid.Columns.Add(CreateTextColumn("Low32", 104));

        var bottom = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            RowCount = 2,
        };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        DetailsText.Dock = DockStyle.Fill;
        DetailsText.Multiline = true;
        DetailsText.ReadOnly = true;
        DetailsText.ScrollBars = ScrollBars.Vertical;
        DetailsText.WordWrap = false;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };

        ConfigureActionButton(CopyVisibleButton, "Copy Visible", "Copy all visible flagwork rows as TSV.", CopyVisibleRows);
        ConfigureActionButton(CopyRowButton, "Copy Row", "Copy the selected flagwork row as TSV.", CopySelectedRow);
        ConfigureActionButton(CopyHashButton, "Copy Hash", "Copy the selected 64-bit hash.", CopySelectedHash);
        ConfigureActionButton(CopyNameButton, "Copy Name", "Copy the selected flagwork name.", CopySelectedName);
        actions.Controls.Add(CopyVisibleButton);
        actions.Controls.Add(CopyRowButton);
        actions.Controls.Add(CopyHashButton);
        actions.Controls.Add(CopyNameButton);

        bottom.Controls.Add(DetailsText, 0, 0);
        bottom.Controls.Add(actions, 0, 1);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(EntryGrid, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);
    }

    private void LoadEntries()
    {
        var flagworkDirectory = Path.Combine(RomFsPath, "bin", "flagwork");
        if (!Directory.Exists(flagworkDirectory))
        {
            LoadErrors.Add($"Missing folder: {Path.Combine("bin", "flagwork")}");
            return;
        }

        foreach (var file in Directory.EnumerateFiles(flagworkDirectory, "*.tbl", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
        {
            var tableName = Path.GetFileNameWithoutExtension(file);
            var relativePath = Path.Combine("bin", "flagwork", Path.GetFileName(file));

            try
            {
                var data = File.ReadAllBytes(file);
                if (data.Length < 8 || !AHTB.IsAHTB(data))
                {
                    LoadErrors.Add($"{relativePath}: not an AHTB table.");
                    continue;
                }

                var table = new AHTB(data);
                for (int i = 0; i < table.Entries.Count; i++)
                {
                    var entry = table.Entries[i];
                    Entries.Add(new(tableName, relativePath, i, InferKind(tableName, entry.Name), entry.Name, entry.Hash));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                LoadErrors.Add($"{relativePath}: {ex.Message}");
            }
        }
    }

    private void RefreshTableFilter()
    {
        TableFilter.Items.Clear();
        TableFilter.Items.Add("All");
        foreach (var table in Entries.Select(z => z.Table).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(z => z))
            TableFilter.Items.Add(table);
        TableFilter.SelectedIndex = 0;
    }

    private void QueueRefreshGrid()
    {
        FilterTimer.Stop();
        FilterTimer.Start();
    }

    private void RefreshGrid()
    {
        var table = TableFilter.SelectedItem as string ?? "All";
        var query = SearchBox.Text.Trim();
        var selected = SelectedEntry;
        var filtered = Entries.Where(z => MatchesFilter(z, table, query)).ToArray();

        EntryGrid.SuspendLayout();
        EntryGrid.RowCount = 0;
        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        EntryGrid.RowCount = VisibleEntries.Count;
        EntryGrid.ResumeLayout();

        SummaryLabel.Text = $"{VisibleEntries.Count:N0} / {Entries.Count:N0}";

        if (VisibleEntries.Count == 0)
        {
            SelectEntry(null);
            return;
        }

        var index = selected == null ? 0 : VisibleEntries.FindIndex(z => z.Equals(selected));
        if (index < 0)
            index = 0;

        EntryGrid.CurrentCell = EntryGrid.Rows[index].Cells[0];
        EntryGrid.Rows[index].Selected = true;
        SelectEntry(VisibleEntries[index]);
    }

    private void EntryGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= VisibleEntries.Count)
            return;

        var entry = VisibleEntries[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => entry.Table,
            1 => entry.Index.ToString(),
            2 => entry.Kind,
            3 => entry.Name,
            4 => FormatHash(entry.Hash),
            5 => FormatLow32(entry.Hash),
            _ => string.Empty,
        };
    }

    private static bool MatchesFilter(FlagworkEntry entry, string table, string query)
    {
        if (table != "All" && !entry.Table.Equals(table, StringComparison.OrdinalIgnoreCase))
            return false;
        if (query.Length == 0)
            return true;

        var hash = FormatHash(entry.Hash);
        var low32 = FormatLow32(entry.Hash);
        return entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Table.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Kind.Contains(query, StringComparison.OrdinalIgnoreCase)
            || hash.Contains(query, StringComparison.OrdinalIgnoreCase)
            || hash[2..].Contains(query, StringComparison.OrdinalIgnoreCase)
            || low32.Contains(query, StringComparison.OrdinalIgnoreCase)
            || low32[2..].Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectCurrentEntry()
    {
        if (EntryGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= VisibleEntries.Count)
        {
            SelectEntry(null);
            return;
        }

        SelectEntry(VisibleEntries[row]);
    }

    private void SelectEntry(FlagworkEntry? entry)
    {
        SelectedEntry = entry;
        if (entry == null)
        {
            DetailsText.Text = LoadErrors.Count == 0
                ? "No flagwork entry selected."
                : string.Join(Environment.NewLine, LoadErrors);
            SetActionButtonsEnabled(false);
            return;
        }

        DetailsText.Text = string.Join(Environment.NewLine, [
            $"Name: {entry.Name}",
            $"Kind: {entry.Kind}",
            $"Hash64: {FormatHash(entry.Hash)}",
            $"Low32: {FormatLow32(entry.Hash)}",
            $"Table: {entry.Table}",
            $"Index: {entry.Index}",
            $"Source: {entry.Source}",
            string.Empty,
            "TSV:",
            ToTsv(entry),
        ]);
        SetActionButtonsEnabled(true);
    }

    private void CopySelectedName()
    {
        if (SelectedEntry is not { } entry)
            return;

        Clipboard.SetText(entry.Name);
    }

    private void CopySelectedHash()
    {
        if (SelectedEntry is not { } entry)
            return;

        Clipboard.SetText(FormatHash(entry.Hash));
    }

    private void CopySelectedRow()
    {
        if (SelectedEntry is not { } entry)
            return;

        Clipboard.SetText(ToTsvHeader() + Environment.NewLine + ToTsv(entry));
    }

    private void CopyVisibleRows()
    {
        var rows = VisibleEntries.Select(ToTsv);
        Clipboard.SetText(ToTsvHeader() + Environment.NewLine + string.Join(Environment.NewLine, rows));
    }

    private void SetActionButtonsEnabled(bool selected)
    {
        CopyNameButton.Enabled = selected;
        CopyHashButton.Enabled = selected;
        CopyRowButton.Enabled = selected;
        CopyVisibleButton.Enabled = VisibleEntries.Count != 0;
    }

    private static string InferKind(string table, string name)
    {
        if (name.StartsWith("WK_", StringComparison.OrdinalIgnoreCase) || table.Contains("work", StringComparison.OrdinalIgnoreCase))
            return "Work";

        return "Flag";
    }

    private static string FormatHash(ulong hash) => $"0x{hash:X16}";
    private static string FormatLow32(ulong hash) => $"0x{(uint)hash:X8}";
    private static string ToTsvHeader() => "Table\tIndex\tKind\tName\tHash64\tLow32\tSource";
    private static string ToTsv(FlagworkEntry entry) => $"{entry.Table}\t{entry.Index}\t{entry.Kind}\t{entry.Name}\t{FormatHash(entry.Hash)}\t{FormatLow32(entry.Hash)}\t{entry.Source}";

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
        button.Width = 116;
        button.Height = 32;
        button.Margin = new Padding(6, 0, 0, 4);
        button.Click += (_, _) => action();
        ButtonToolTips.SetToolTip(button, tooltip);
    }

    private void ApplyTheme()
    {
        WinFormsTheme.Apply(this);
    }

    private sealed record FlagworkEntry(string Table, string Source, int Index, string Kind, string Name, ulong Hash);
}
