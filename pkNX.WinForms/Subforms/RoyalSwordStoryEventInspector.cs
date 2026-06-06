using pkNX.Containers;
using pkNX.Structures.FlatBuffers;
using pkNX.Structures.FlatBuffers.SWSH;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class RoyalSwordStoryEventInspector : Form
{
    private readonly TextContainer ScriptText;
    private readonly string RomFsPath;
    private readonly List<StoryEventEntry> Entries = [];
    private readonly List<StoryEventEntry> VisibleEntries = [];
    private readonly ComboBox ScopeFilter = new();
    private readonly TextBox SearchBox = new();
    private readonly DataGridView EventGrid = new();
    private readonly DataGridView LineGrid = new();
    private readonly TextBox DetailsText = new();
    private readonly Button CopyEventButton = new();
    private readonly Button CopyLineButton = new();
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

    private StoryEventEntry? SelectedEvent;
    private StoryEventLine? SelectedLine;

    public RoyalSwordStoryEventInspector(TextContainer scriptText, string romFsPath)
    {
        ScriptText = scriptText;
        RomFsPath = romFsPath;

        Text = "Royal Sword Story Events";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 680);
        Size = new Size(1320, 760);

        InitializeLayout();
        ApplyTheme();
        BuildEntries();
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
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

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
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        ScopeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        ScopeFilter.Items.AddRange(["Main Events", "All Script Text"]);
        ScopeFilter.SelectedIndex = 0;
        ScopeFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        ScopeFilter.Margin = new Padding(0, 3, 14, 3);
        ScopeFilter.SelectedIndexChanged += (_, _) => QueueRefreshGrid();

        SearchBox.PlaceholderText = "Search event, label, text, AMX, or script path...";
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

        top.Controls.Add(CreateLabel("Scope"), 0, 0);
        top.Controls.Add(ScopeFilter, 1, 0);
        top.Controls.Add(CreateLabel("Search"), 2, 0);
        top.Controls.Add(SearchBox, 3, 0);
        top.Controls.Add(SummaryLabel, 4, 0);

        ConfigureGrid(EventGrid);
        EventGrid.VirtualMode = true;
        EventGrid.CellValueNeeded += EventGrid_CellValueNeeded;
        EventGrid.SelectionChanged += (_, _) => SelectCurrentEvent();
        EventGrid.Columns.Add(CreateTextColumn("Event", 142));
        EventGrid.Columns.Add(CreateTextColumn("Lines", 64));
        EventGrid.Columns.Add(CreateTextColumn("Labels", 70));
        EventGrid.Columns.Add(CreateTextColumn("AMX", 160));
        EventGrid.Columns.Add(CreateTextColumn("Size", 86));
        EventGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Preview",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 360,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        var bottom = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            RowCount = 1,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        ConfigureGrid(LineGrid);
        LineGrid.VirtualMode = true;
        LineGrid.CellValueNeeded += LineGrid_CellValueNeeded;
        LineGrid.SelectionChanged += (_, _) => SelectCurrentLine();
        LineGrid.Columns.Add(CreateTextColumn("Line", 60));
        LineGrid.Columns.Add(CreateTextColumn("Label", 220));
        LineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Text",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 300,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        var detailPane = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 0, 0),
            RowCount = 2,
        };
        detailPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPane.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

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
        ConfigureActionButton(CopyVisibleButton, "Copy Visible", "Copy all visible story events as TSV.", CopyVisibleEvents);
        ConfigureActionButton(CopyLineButton, "Copy Line", "Copy the selected story text line as TSV.", CopySelectedLine);
        ConfigureActionButton(CopyEventButton, "Copy Event", "Copy the selected story event summary as TSV.", CopySelectedEvent);
        actions.Controls.Add(CopyVisibleButton);
        actions.Controls.Add(CopyLineButton);
        actions.Controls.Add(CopyEventButton);

        detailPane.Controls.Add(DetailsText, 0, 0);
        detailPane.Controls.Add(actions, 0, 1);

        bottom.Controls.Add(LineGrid, 0, 0);
        bottom.Controls.Add(detailPane, 1, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(EventGrid, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);
    }

    private void BuildEntries()
    {
        var scriptMap = LoadScriptMap();

        for (int fileIndex = 0; fileIndex < ScriptText.Length; fileIndex++)
        {
            var fileName = ScriptText.GetFileName(fileIndex);
            var filePath = ScriptText.GetFilePath(fileIndex);
            var labels = ReadTextLabels(filePath);
            var lineCount = Math.Max(labels.Length, ScriptText[fileIndex].Length);
            var lines = new List<StoryEventLine>(lineCount);

            for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                var label = lineIndex < labels.Length ? labels[lineIndex] : string.Empty;
                var raw = lineIndex < ScriptText[fileIndex].Length ? ScriptText[fileIndex][lineIndex] : string.Empty;
                lines.Add(new(lineIndex, label, raw, TextSyntaxHelper.GetReadableTextPreview(raw)));
            }

            var scriptRefs = scriptMap.TryGetValue(fileName, out var refs) ? refs : [];
            Entries.Add(new(
                fileName,
                TryGetMainEventNumber(fileName),
                labels.Length,
                lines,
                scriptRefs,
                FindAmxFiles(scriptRefs, fileName),
                GetFirstPreview(lines),
                BuildSourcePath(filePath)));
        }
    }

    private IReadOnlyDictionary<string, ScriptReference[]> LoadScriptMap()
    {
        var path = Path.Combine(RomFsPath, "bin", "script", "param", "script_id", "script_id_record.bin");
        if (!File.Exists(path))
            return new Dictionary<string, ScriptReference[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var meta = FlatBufferConverter.DeserializeFrom<ScriptMeta>(path);
            return meta.Table
                .Where(z => !string.IsNullOrWhiteSpace(z.PathText))
                .GroupBy(z => GetBaseName(z.PathText), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    z => z.Key,
                    z => z.Select(x => new ScriptReference(CleanScriptPath(x.PathAMX), CleanScriptPath(x.PathText))).Distinct().ToArray(),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ScriptReference[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private AmxReference[] FindAmxFiles(IReadOnlyList<ScriptReference> scriptRefs, string fileName)
    {
        var names = scriptRefs
            .Select(z => z.Amx)
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .DefaultIfEmpty(fileName)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return names.Select(FindAmxFile).ToArray();
    }

    private AmxReference FindAmxFile(string amxName)
    {
        var name = Path.GetFileNameWithoutExtension(amxName);
        var relative = Path.Combine("bin", "script", "amx", $"{name}.amx");
        var path = Path.Combine(RomFsPath, relative);
        return File.Exists(path)
            ? new(name, relative, new FileInfo(path).Length)
            : new(name, relative, null);
    }

    private void QueueRefreshGrid()
    {
        FilterTimer.Stop();
        FilterTimer.Start();
    }

    private void RefreshGrid()
    {
        var mainOnly = (ScopeFilter.SelectedItem as string ?? "Main Events") == "Main Events";
        var query = SearchBox.Text.Trim();
        var selected = SelectedEvent;
        var filtered = Entries.Where(z => MatchesFilter(z, mainOnly, query)).ToArray();

        EventGrid.SuspendLayout();
        EventGrid.RowCount = 0;
        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        EventGrid.RowCount = VisibleEntries.Count;
        EventGrid.ResumeLayout();

        SummaryLabel.Text = $"{VisibleEntries.Count:N0} / {Entries.Count:N0}";

        if (VisibleEntries.Count == 0)
        {
            SelectEvent(null);
            return;
        }

        var index = selected == null ? 0 : VisibleEntries.FindIndex(z => z.FileName.Equals(selected.FileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = 0;

        EventGrid.CurrentCell = EventGrid.Rows[index].Cells[0];
        EventGrid.Rows[index].Selected = true;
        SelectEvent(VisibleEntries[index]);
    }

    private void EventGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= VisibleEntries.Count)
            return;

        var entry = VisibleEntries[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => entry.FileName,
            1 => entry.Lines.Count.ToString(),
            2 => entry.LabelCount.ToString(),
            3 => SummarizeAmx(entry.AmxFiles),
            4 => SummarizeAmxSizes(entry.AmxFiles),
            5 => entry.Preview,
            _ => string.Empty,
        };
    }

    private void LineGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (SelectedEvent is not { } entry || e.RowIndex < 0 || e.RowIndex >= entry.Lines.Count)
            return;

        var line = entry.Lines[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => line.Index.ToString(),
            1 => line.Label,
            2 => line.Preview,
            _ => string.Empty,
        };
    }

    private static bool MatchesFilter(StoryEventEntry entry, bool mainOnly, string query)
    {
        if (mainOnly && entry.MainEventNumber is null)
            return false;
        if (query.Length == 0)
            return true;

        return entry.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Preview.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.ScriptRefs.Any(z => z.Amx.Contains(query, StringComparison.OrdinalIgnoreCase) || z.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            || entry.AmxFiles.Any(z => z.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || z.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            || entry.Lines.Any(z => z.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || z.Preview.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectCurrentEvent()
    {
        if (EventGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= VisibleEntries.Count)
        {
            SelectEvent(null);
            return;
        }

        SelectEvent(VisibleEntries[row]);
    }

    private void SelectEvent(StoryEventEntry? entry)
    {
        SelectedEvent = entry;
        SelectedLine = null;

        LineGrid.SuspendLayout();
        LineGrid.RowCount = 0;
        LineGrid.RowCount = entry?.Lines.Count ?? 0;
        LineGrid.ResumeLayout();

        if (entry == null)
        {
            DetailsText.Text = "No story event selected.";
            SetActionButtonsEnabled(false, false);
            return;
        }

        if (entry.Lines.Count != 0)
        {
            LineGrid.CurrentCell = LineGrid.Rows[0].Cells[0];
            LineGrid.Rows[0].Selected = true;
            SelectedLine = entry.Lines[0];
        }

        DetailsText.Text = BuildDetails(entry, SelectedLine);
        SetActionButtonsEnabled(true, SelectedLine is not null);
    }

    private void SelectCurrentLine()
    {
        if (SelectedEvent is not { } entry || LineGrid.CurrentCell?.RowIndex is not int row || row < 0 || row >= entry.Lines.Count)
        {
            SelectedLine = null;
            DetailsText.Text = SelectedEvent == null ? "No story event selected." : BuildDetails(SelectedEvent, null);
            SetActionButtonsEnabled(SelectedEvent is not null, false);
            return;
        }

        SelectedLine = entry.Lines[row];
        DetailsText.Text = BuildDetails(entry, SelectedLine);
        SetActionButtonsEnabled(true, true);
    }

    private static string BuildDetails(StoryEventEntry entry, StoryEventLine? line)
    {
        var values = new List<string>
        {
            $"Event: {entry.FileName}",
            $"Main Event: {(entry.MainEventNumber.HasValue ? entry.MainEventNumber.Value.ToString("0000") : "No")}",
            $"Lines: {entry.Lines.Count}",
            $"Labels: {entry.LabelCount}",
            $"Text Source: {entry.Source}",
            $"AMX: {SummarizeAmx(entry.AmxFiles)}",
            $"AMX Size: {SummarizeAmxSizes(entry.AmxFiles)}",
            $"Script Metadata: {SummarizeScriptRefs(entry.ScriptRefs)}",
        };

        if (line is not null)
        {
            values.Add(string.Empty);
            values.Add($"Selected Line: {line.Index}");
            values.Add($"Label: {line.Label}");
            values.Add($"Raw: {line.Raw}");
            values.Add($"Preview: {line.Preview}");
        }

        values.Add(string.Empty);
        values.Add("Event TSV:");
        values.Add(ToEventTsv(entry));
        if (line is not null)
        {
            values.Add(string.Empty);
            values.Add("Line TSV:");
            values.Add(ToLineTsv(entry, line));
        }

        return string.Join(Environment.NewLine, values);
    }

    private void CopySelectedEvent()
    {
        if (SelectedEvent is not { } entry)
            return;

        Clipboard.SetText(EventTsvHeader() + Environment.NewLine + ToEventTsv(entry));
    }

    private void CopySelectedLine()
    {
        if (SelectedEvent is not { } entry || SelectedLine is not { } line)
            return;

        Clipboard.SetText(LineTsvHeader() + Environment.NewLine + ToLineTsv(entry, line));
    }

    private void CopyVisibleEvents()
    {
        var rows = VisibleEntries.Select(ToEventTsv);
        Clipboard.SetText(EventTsvHeader() + Environment.NewLine + string.Join(Environment.NewLine, rows));
    }

    private void SetActionButtonsEnabled(bool hasEvent, bool hasLine)
    {
        CopyEventButton.Enabled = hasEvent;
        CopyLineButton.Enabled = hasLine;
        CopyVisibleButton.Enabled = VisibleEntries.Count != 0;
    }

    private static string[] ReadTextLabels(string? dataPath)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
            return [];

        var tablePath = Path.ChangeExtension(dataPath, ".tbl");
        if (!File.Exists(tablePath))
            return [];

        try
        {
            return new AHTB(File.ReadAllBytes(tablePath)).Entries.Select(z => z.Name).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static int? TryGetMainEventNumber(string fileName)
    {
        const string prefix = "main_event_";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var suffix = fileName[prefix.Length..];
        return int.TryParse(suffix, out var value) ? value : null;
    }

    private static string BuildSourcePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        var directory = Path.GetDirectoryName(filePath);
        var file = Path.GetFileName(filePath);
        var parent = string.IsNullOrWhiteSpace(directory) ? string.Empty : Path.GetFileName(directory);
        return string.IsNullOrWhiteSpace(parent) ? file : Path.Combine(parent, file);
    }

    private static string GetFirstPreview(IEnumerable<StoryEventLine> lines) =>
        lines.Select(z => z.Preview).FirstOrDefault(z => !string.IsNullOrWhiteSpace(z)) ?? string.Empty;

    private static string SummarizeScriptRefs(IReadOnlyList<ScriptReference> refs) =>
        refs.Count == 0 ? "None" : string.Join(", ", refs.Select(z => string.IsNullOrWhiteSpace(z.Amx) ? z.Text : z.Amx).Distinct(StringComparer.OrdinalIgnoreCase).Take(5));

    private static string SummarizeAmx(IReadOnlyList<AmxReference> refs) =>
        refs.Count == 0 ? "None" : string.Join(", ", refs.Select(z => z.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(5));

    private static string SummarizeAmxSizes(IReadOnlyList<AmxReference> refs)
    {
        var sizes = refs.Where(z => z.Size.HasValue).Select(z => FormatBytes(z.Size!.Value)).ToArray();
        if (sizes.Length == 0)
            return "Missing";

        return string.Join(", ", sizes.Take(5));
    }

    private static string FormatBytes(long size) => size < 1024 ? $"{size} B" : $"{size / 1024.0:F1} KB";
    private static string EventTsvHeader() => "Event\tMainEvent\tLines\tLabels\tAMX\tAMXSize\tPreview\tSource";
    private static string LineTsvHeader() => "Event\tLine\tLabel\tPreview\tRaw";

    private static string ToEventTsv(StoryEventEntry entry) =>
        $"{entry.FileName}\t{entry.MainEventNumber?.ToString("0000") ?? string.Empty}\t{entry.Lines.Count}\t{entry.LabelCount}\t{SummarizeAmx(entry.AmxFiles)}\t{SummarizeAmxSizes(entry.AmxFiles)}\t{CleanTsv(entry.Preview)}\t{entry.Source}";

    private static string ToLineTsv(StoryEventEntry entry, StoryEventLine line) =>
        $"{entry.FileName}\t{line.Index}\t{CleanTsv(line.Label)}\t{CleanTsv(line.Preview)}\t{CleanTsv(line.Raw)}";

    private static string CleanTsv(string value) => value.Replace('\t', ' ').Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');

    private static string CleanScriptPath(string path)
    {
        var value = path.Replace('\\', '/');
        var slash = value.LastIndexOf('/');
        if (slash >= 0 && slash + 1 < value.Length)
            value = value[(slash + 1)..];
        return Path.GetFileNameWithoutExtension(value);
    }

    private static string GetBaseName(string path)
    {
        var value = path.Replace('\\', '/');
        var slash = value.LastIndexOf('/');
        if (slash >= 0 && slash + 1 < value.Length)
            value = value[(slash + 1)..];
        return Path.GetFileNameWithoutExtension(value);
    }

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

    private sealed record StoryEventEntry(
        string FileName,
        int? MainEventNumber,
        int LabelCount,
        IReadOnlyList<StoryEventLine> Lines,
        IReadOnlyList<ScriptReference> ScriptRefs,
        IReadOnlyList<AmxReference> AmxFiles,
        string Preview,
        string Source);

    private sealed record StoryEventLine(int Index, string Label, string Raw, string Preview);
    private sealed record ScriptReference(string Amx, string Text);
    private sealed record AmxReference(string Name, string RelativePath, long? Size);
}
