using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pkNX.Containers;
using pkNX.Structures.FlatBuffers;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public sealed class DialogueMapEditor : Form
{
    private const int TokenButtonWidth = 112;
    private const int TokenButtonHeight = 28;

    private readonly TextContainer CommonText;
    private readonly TextContainer ScriptText;
    private readonly List<DialogueMapEntry> Entries = [];

    private readonly ComboBox SourceFilter = new();
    private readonly TextBox SearchBox = new();
    private readonly DataGridView EntryGrid = new();
    private readonly TextBox FriendlyText = new();
    private readonly TextBox RawText = new();
    private readonly TextBox DetailsText = new();
    private readonly Button SaveButton = new();
    private readonly Button ApplyFriendlyButton = new();
    private readonly Button ApplyRawButton = new();
    private readonly Button UndoButton = new();
    private readonly Button RedoButton = new();
    private readonly List<DialogueMapEntry> VisibleEntries = [];
    private readonly Stack<DialogueEdit> UndoHistory = new();
    private readonly Stack<DialogueEdit> RedoHistory = new();
    private readonly Dictionary<DialogueMapEntry, string> OriginalTextByEntry = [];
    private readonly System.Windows.Forms.Timer FilterTimer = new() { Interval = 200 };
    private readonly ToolTip ButtonToolTips = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 450,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    private DialogueMapEntry? SelectedEntry;
    private bool CloseConfirmed;
    private bool IsDirty => OriginalTextByEntry.Count != 0;

    public DialogueMapEditor(TextContainer commonText, TextContainer scriptText, string? romFsPath)
    {
        CommonText = commonText;
        ScriptText = scriptText;

        Text = "Dialogue Map";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1320, 780);

        InitializeLayout();
        ApplyTheme();
        BuildEntries(romFsPath);
        RefreshGrid();
    }

    public bool Modified { get; private set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!CloseConfirmed && e.CloseReason == CloseReason.UserClosing && !ConfirmClose())
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UndoHistory.Clear();
            RedoHistory.Clear();
            OriginalTextByEntry.Clear();
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
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var top = new TableLayoutPanel
        {
            ColumnCount = 6,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 6),
            RowCount = 1,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        SourceFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        SourceFilter.Items.AddRange(["All", "Common", "Script"]);
        SourceFilter.SelectedIndex = 0;
        SourceFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SourceFilter.Margin = new Padding(0, 3, 14, 3);
        SourceFilter.SelectedIndexChanged += (_, _) => QueueRefreshGrid();

        SearchBox.PlaceholderText = "Search text, label, file, owner, context...";
        SearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SearchBox.Margin = new Padding(0, 3, 14, 3);
        SearchBox.TextChanged += (_, _) => QueueRefreshGrid();

        SaveButton.Text = "Save";
        SaveButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        SaveButton.Height = 32;
        SaveButton.Margin = new Padding(0, 1, 0, 1);
        SaveButton.Click += (_, _) => SaveAndClose();

        FilterTimer.Tick += (_, _) =>
        {
            FilterTimer.Stop();
            RefreshGrid();
        };

        top.Controls.Add(CreateLabel("Source"), 0, 0);
        top.Controls.Add(SourceFilter, 1, 0);
        top.Controls.Add(CreateLabel("Search"), 2, 0);
        top.Controls.Add(SearchBox, 3, 0);
        top.Controls.Add(SaveButton, 5, 0);

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
        EntryGrid.Columns.Add(CreateTextColumn("Source", 76));
        EntryGrid.Columns.Add(CreateTextColumn("File", 170));
        EntryGrid.Columns.Add(CreateTextColumn("Line", 58));
        EntryGrid.Columns.Add(CreateTextColumn("Likely Owner", 150));
        EntryGrid.Columns.Add(CreateTextColumn("Label", 260));
        EntryGrid.Columns.Add(CreateTextColumn("Context", 300));
        EntryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Readable Text",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 260,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });

        var editor = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 1,
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));

        var left = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 8, 0),
            RowCount = 2,
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        DetailsText.Dock = DockStyle.Fill;
        DetailsText.Multiline = true;
        DetailsText.ReadOnly = true;
        DetailsText.ScrollBars = ScrollBars.Vertical;

        RawText.Dock = DockStyle.Fill;
        RawText.Multiline = true;
        RawText.ScrollBars = ScrollBars.Vertical;
        RawText.WordWrap = false;

        left.Controls.Add(DetailsText, 0, 0);
        left.Controls.Add(RawText, 0, 1);

        var right = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 8, 0, 0),
            RowCount = 3,
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var tokenButtons = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 6),
            RowCount = 2,
        };
        for (var i = 0; i < tokenButtons.ColumnCount; i++)
            tokenButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, TokenButtonWidth + 8));
        tokenButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, TokenButtonHeight + 8));
        tokenButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, TokenButtonHeight + 8));

        tokenButtons.Controls.Add(CreateTokenButton("Line Break", Environment.NewLine, "Insert a normal line break in the friendly text."), 0, 0);
        tokenButtons.Controls.Add(CreateTokenButton("Wait + Clear", "[WAIT_CLEAR]", "Insert a wait command that clears the text box afterward."), 1, 0);
        tokenButtons.Controls.Add(CreateTokenButton("Wait + Scroll", "[WAIT_SCROLL]", "Insert a wait command that scrolls to the next text box."), 2, 0);
        tokenButtons.Controls.Add(CreateVarButton("Pokemon Var", TextVariableGroup.Pokemon, "Open a list of Pokemon-related text variables."), 3, 0);
        tokenButtons.Controls.Add(CreateVarButton("Item Var", TextVariableGroup.Item, "Open a list of item-related text variables."), 4, 0);
        tokenButtons.Controls.Add(CreateVarButton("Move Var", TextVariableGroup.Move, "Open a list of move-related text variables."), 0, 1);
        tokenButtons.Controls.Add(CreateVarButton("Number Var", TextVariableGroup.Number, "Open a list of numeric text variables."), 1, 1);

        FriendlyText.Dock = DockStyle.Fill;
        FriendlyText.Multiline = true;
        FriendlyText.ScrollBars = ScrollBars.Vertical;
        FriendlyText.AcceptsReturn = true;
        FriendlyText.AcceptsTab = true;

        var actionRow = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 1,
        };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var undoButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        UndoButton.Text = "Undo";
        UndoButton.Width = 90;
        UndoButton.Height = 32;
        UndoButton.Margin = new Padding(0, 0, 8, 4);
        UndoButton.Click += (_, _) => UndoLastEdit();
        ButtonToolTips.SetToolTip(UndoButton, "Undo the last applied Dialogue Map edit from this editor session.");
        RedoButton.Text = "Redo";
        RedoButton.Width = 90;
        RedoButton.Height = 32;
        RedoButton.Margin = new Padding(0, 0, 8, 4);
        RedoButton.Click += (_, _) => RedoLastEdit();
        ButtonToolTips.SetToolTip(RedoButton, "Redo the next undone Dialogue Map edit from this editor session.");
        undoButtons.Controls.Add(UndoButton);
        undoButtons.Controls.Add(RedoButton);

        var applyButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        ApplyRawButton.Text = "Apply Raw";
        ApplyRawButton.Width = 116;
        ApplyRawButton.Height = 32;
        ApplyRawButton.Margin = new Padding(6, 0, 0, 4);
        ApplyRawButton.Click += (_, _) => ApplyRaw();
        ButtonToolTips.SetToolTip(ApplyRawButton, "Apply the raw syntax text to this line in the Dialogue Map session.");
        ApplyFriendlyButton.Text = "Apply Friendly";
        ApplyFriendlyButton.Width = 130;
        ApplyFriendlyButton.Height = 32;
        ApplyFriendlyButton.Margin = new Padding(6, 0, 0, 4);
        ApplyFriendlyButton.Click += (_, _) => ApplyFriendly();
        ButtonToolTips.SetToolTip(ApplyFriendlyButton, "Convert the readable text to game syntax and apply it to this line.");
        applyButtons.Controls.Add(ApplyRawButton);
        applyButtons.Controls.Add(ApplyFriendlyButton);
        actionRow.Controls.Add(undoButtons, 0, 0);
        actionRow.Controls.Add(applyButtons, 1, 0);

        right.Controls.Add(tokenButtons, 0, 0);
        right.Controls.Add(FriendlyText, 0, 1);
        right.Controls.Add(actionRow, 0, 2);

        editor.Controls.Add(left, 0, 0);
        editor.Controls.Add(right, 1, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(EntryGrid, 0, 1);
        root.Controls.Add(editor, 0, 2);
        Controls.Add(root);
        UpdateHistoryButtonState();
    }

    private void BuildEntries(string? romFsPath)
    {
        var scriptUsage = LoadScriptUsageMap(romFsPath);
        AddSourceEntries("Common", CommonText, new Dictionary<string, string>());
        AddSourceEntries("Script", ScriptText, scriptUsage);
    }

    private void AddSourceEntries(string source, TextContainer container, IReadOnlyDictionary<string, string> scriptUsage)
    {
        for (int fileIndex = 0; fileIndex < container.Length; fileIndex++)
        {
            var fileName = container.GetFileName(fileIndex);
            var labels = ReadTextLabels(container.GetFilePath(fileIndex));
            var scriptContext = scriptUsage.TryGetValue(fileName, out var usage) ? usage : string.Empty;
            var lineCount = labels.Length == 0 ? container[fileIndex].Length : labels.Length;
            for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                var label = lineIndex < labels.Length ? labels[lineIndex] : string.Empty;
                var owner = InferOwner(fileName, label);
                var context = BuildContext(source, fileName, label, scriptContext);
                Entries.Add(new DialogueMapEntry(source, container, fileIndex, fileName, lineIndex, label, owner, context));
            }
        }
    }

    private void QueueRefreshGrid()
    {
        FilterTimer.Stop();
        FilterTimer.Start();
    }

    private void RefreshGrid()
    {
        var source = SourceFilter.SelectedItem as string ?? "All";
        var query = SearchBox.Text.Trim();
        var selected = SelectedEntry;
        var filtered = Entries.Where(z => MatchesFilter(z, source, query)).ToArray();

        EntryGrid.SuspendLayout();
        EntryGrid.RowCount = 0;
        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        EntryGrid.RowCount = VisibleEntries.Count;
        EntryGrid.ResumeLayout();

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
            0 => entry.Source,
            1 => entry.FileName,
            2 => entry.LineIndex.ToString(),
            3 => entry.Owner,
            4 => entry.Label,
            5 => entry.Context,
            6 => TextSyntaxHelper.GetReadableTextPreview(entry.RawText),
            _ => string.Empty,
        };
    }

    private static bool MatchesFilter(DialogueMapEntry entry, string source, string query)
    {
        if (source != "All" && !entry.Source.Equals(source, StringComparison.OrdinalIgnoreCase))
            return false;
        if (query.Length == 0)
            return true;

        if (Contains(entry.FileName, query) ||
            Contains(entry.LineIndex.ToString(), query) ||
            Contains(entry.Label, query) ||
            Contains(entry.Owner, query) ||
            Contains(entry.Context, query))
        {
            return true;
        }

        // Text search has to decode text files; wait for a meaningful query so opening and quick filtering stay fast.
        return query.Length >= 2 &&
               (Contains(entry.RawText, query) ||
                Contains(TextSyntaxHelper.GetReadableTextPreview(entry.RawText), query));
    }

    private static bool Contains(string value, string query) => value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void SelectCurrentEntry()
    {
        var rowIndex = EntryGrid.CurrentCell?.RowIndex ?? -1;
        var entry = rowIndex >= 0 && rowIndex < VisibleEntries.Count ? VisibleEntries[rowIndex] : null;
        SelectEntry(entry);
    }

    private void SelectEntry(DialogueMapEntry? entry)
    {
        SelectedEntry = entry;
        var hasEntry = entry != null;
        RawText.Enabled = hasEntry;
        FriendlyText.Enabled = hasEntry;
        ApplyRawButton.Enabled = hasEntry;
        ApplyFriendlyButton.Enabled = hasEntry;
        UpdateHistoryButtonState();

        if (entry == null)
        {
            DetailsText.Clear();
            RawText.Clear();
            FriendlyText.Clear();
            return;
        }

        DetailsText.Text =
            $"Source: {entry.Source}{Environment.NewLine}" +
            $"File: {entry.FileName}{Environment.NewLine}" +
            $"Line: {entry.LineIndex}{Environment.NewLine}" +
            $"Label: {entry.Label}{Environment.NewLine}" +
            $"Likely owner: {entry.Owner}{Environment.NewLine}" +
            $"Context: {entry.Context}";
        RawText.Text = entry.RawText;
        FriendlyText.Text = TextSyntaxHelper.RawToFriendly(entry.RawText);
    }

    private void ApplyFriendly()
    {
        if (SelectedEntry == null)
            return;
        if (!ConfirmApplyFriendly())
            return;

        RawText.Text = TextSyntaxHelper.FriendlyToRaw(FriendlyText.Text);
        ApplyRawCore();
    }

    private void ApplyRaw()
    {
        if (SelectedEntry == null)
            return;
        if (!ConfirmApplyRaw())
            return;

        ApplyRawCore();
    }

    private void ApplyRawCore()
    {
        if (SelectedEntry == null)
            return;

        var oldText = SelectedEntry.RawText;
        var newText = RawText.Text;
        if (oldText == newText)
        {
            FriendlyText.Text = TextSyntaxHelper.RawToFriendly(RawText.Text);
            return;
        }

        SetEntryRawText(SelectedEntry, newText);
        UndoHistory.Push(new DialogueEdit(SelectedEntry, oldText, newText));
        RedoHistory.Clear();
        FriendlyText.Text = TextSyntaxHelper.RawToFriendly(newText);
        UpdateHistoryButtonState();
        UpdateCurrentGridRow(SelectedEntry);
    }

    private void UndoLastEdit()
    {
        if (!UndoHistory.TryPop(out var edit))
            return;

        SetEntryRawText(edit.Entry, edit.OldText);
        RedoHistory.Push(edit);
        FocusEntry(edit.Entry);
        UpdateHistoryButtonState();
    }

    private void RedoLastEdit()
    {
        if (!RedoHistory.TryPop(out var edit))
            return;

        SetEntryRawText(edit.Entry, edit.NewText);
        UndoHistory.Push(edit);
        FocusEntry(edit.Entry);
        UpdateHistoryButtonState();
    }

    private void SetEntryRawText(DialogueMapEntry entry, string newText)
    {
        var oldText = entry.RawText;
        if (oldText == newText)
            return;

        if (!OriginalTextByEntry.ContainsKey(entry))
            OriginalTextByEntry[entry] = oldText;

        entry.RawText = newText;
        if (OriginalTextByEntry.TryGetValue(entry, out var originalText) && newText == originalText)
            OriginalTextByEntry.Remove(entry);

        InvalidateEntryRow(entry);
    }

    private void FocusEntry(DialogueMapEntry entry)
    {
        if (!VisibleEntries.Contains(entry))
        {
            var source = SourceFilter.SelectedItem as string ?? "All";
            if (source != "All" && !source.Equals(entry.Source, StringComparison.OrdinalIgnoreCase))
                SourceFilter.SelectedItem = entry.Source;
            if (SearchBox.TextLength != 0)
                SearchBox.Clear();

            FilterTimer.Stop();
            RefreshGrid();
        }

        var index = VisibleEntries.FindIndex(z => z.Equals(entry));
        if (index >= 0)
        {
            EntryGrid.CurrentCell = EntryGrid.Rows[index].Cells[0];
            EntryGrid.Rows[index].Selected = true;
        }

        SelectEntry(entry);
    }

    private void UpdateHistoryButtonState()
    {
        UndoButton.Enabled = UndoHistory.Count != 0;
        RedoButton.Enabled = RedoHistory.Count != 0;
    }

    private void UpdateCurrentGridRow(DialogueMapEntry entry)
    {
        if (EntryGrid.CurrentRow == null)
            return;

        EntryGrid.InvalidateRow(EntryGrid.CurrentRow.Index);
    }

    private void InvalidateEntryRow(DialogueMapEntry entry)
    {
        var index = VisibleEntries.FindIndex(z => z.Equals(entry));
        if (index >= 0)
            EntryGrid.InvalidateRow(index);
    }

    private void SaveAndClose()
    {
        if (!ConfirmSave())
            return;

        CommonText.Save();
        ScriptText.Save();
        Modified = true;
        OriginalTextByEntry.Clear();
        CloseConfirmed = true;
        Close();
    }

    private void InsertFriendlyToken(string token)
    {
        if (!FriendlyText.Enabled)
            return;

        FriendlyText.SelectedText = token;
        FriendlyText.Focus();
    }

    private void ShowVariablePicker(TextVariableGroup group)
    {
        if (!FriendlyText.Enabled)
            return;

        using var dialog = new TextVariablePickerDialog(group);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            InsertFriendlyToken(dialog.SelectedToken);
    }

    private void ApplyTheme()
    {
        WinFormsTheme.Apply(this);
        EntryGrid.RowTemplate.Height = 26;
        EntryGrid.ColumnHeadersHeight = 28;
        EntryGrid.ShowCellToolTips = true;
        EntryGrid.BackgroundColor = WinFormsTheme.WindowBackground;
        EntryGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        EntryGrid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= VisibleEntries.Count)
                return;

            var entry = VisibleEntries[e.RowIndex];
            e.ToolTipText = TextSyntaxHelper.GetReadableTextToolTip(entry.RawText);
        };
    }

    private static Label CreateLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        UseMnemonic = false,
    };

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, int width) => new()
    {
        HeaderText = header,
        Width = width,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    private Button CreateTokenButton(string text, string token, string tooltip)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Height = TokenButtonHeight,
            Margin = new Padding(0, 0, 8, 8),
            Text = text,
            Width = TokenButtonWidth,
        };
        button.Click += (_, _) => InsertFriendlyToken(token);
        ButtonToolTips.SetToolTip(button, tooltip);
        return button;
    }

    private Button CreateVarButton(string text, TextVariableGroup group, string tooltip)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Height = TokenButtonHeight,
            Margin = new Padding(0, 0, 8, 8),
            Text = text,
            Width = TokenButtonWidth,
        };
        button.Click += (_, _) => ShowVariablePicker(group);
        ButtonToolTips.SetToolTip(button, tooltip);
        return button;
    }

    private bool ConfirmSave()
        => ThemedConfirmationDialog.Show(
            this,
            "Save Dialogue Map Changes",
            "Save dialogue changes?\n\nThis writes edited Common and Script text files to the loaded project. Close without saving to discard this Dialogue Map session.",
            "Save");

    private bool ConfirmApplyFriendly()
        => ThemedConfirmationDialog.Show(
            this,
            "Apply Friendly Text",
            "Apply the friendly text to this dialogue line?\n\nThis converts the readable text into game text syntax and updates the loaded Dialogue Map session. Use Save afterward to write it to the project.",
            "Apply");

    private bool ConfirmApplyRaw()
        => ThemedConfirmationDialog.Show(
            this,
            "Apply Raw Text",
            "Apply the raw text to this dialogue line?\n\nRaw syntax is written exactly as entered. Invalid control codes or variables can break the in-game text that uses this line.",
            "Apply");

    private bool ConfirmClose()
    {
        var message = IsDirty
            ? "Close Dialogue Map without saving?\n\nAny applied dialogue edits made in this editor session will be discarded."
            : "Close Dialogue Map?\n\nNo dialogue edits have been applied in this editor session.";

        return ThemedConfirmationDialog.Show(this, "Close Dialogue Map", message, "Close");
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

    private static IReadOnlyDictionary<string, string> LoadScriptUsageMap(string? romFsPath)
    {
        if (string.IsNullOrWhiteSpace(romFsPath))
            return new Dictionary<string, string>();

        var path = Path.Combine(romFsPath, "bin", "script", "param", "script_id", "script_id_record.bin");
        if (!File.Exists(path))
            return new Dictionary<string, string>();

        try
        {
            var meta = FlatBufferConverter.DeserializeFrom<ScriptMeta>(path);
            return meta.Table
                .Where(z => !string.IsNullOrWhiteSpace(z.PathText))
                .GroupBy(z => GetBaseName(z.PathText), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    z => z.Key,
                    z => SummarizeScriptPaths(z.Select(x => x.PathAMX)),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static string SummarizeScriptPaths(IEnumerable<string> paths)
    {
        var clean = paths
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Select(CleanScriptPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(z => z, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        return clean.Length == 0 ? string.Empty : string.Join(", ", clean);
    }

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

    private static string BuildContext(string source, string fileName, string label, string scriptContext)
    {
        if (source == "Common")
            return InferCommonContext(fileName, label);

        return string.IsNullOrWhiteSpace(scriptContext)
            ? "Script text; no script metadata found"
            : $"AMX: {scriptContext}";
    }

    private static string InferCommonContext(string fileName, string label)
    {
        var key = $"{fileName}_{label}".ToLowerInvariant();
        if (key.Contains("shop"))
            return "Common shop/UI text";
        if (key.Contains("bag"))
            return "Bag/menu UI text";
        if (key.Contains("battle"))
            return "Battle UI text";
        if (key.Contains("pokemon") || key.Contains("poke"))
            return "Pokemon UI text";
        if (key.Contains("pw") || key.Contains("job"))
            return "Poke Jobs UI text";
        if (key.Contains("place") || key.Contains("town") || key.Contains("map"))
            return "Location/map text";
        return "Common/shared text";
    }

    private static string InferOwner(string fileName, string label)
    {
        var key = $"{fileName}_{label}".ToLowerInvariant();
        foreach (var (needle, owner) in OwnerHints)
        {
            if (key.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return owner;
        }

        return string.IsNullOrWhiteSpace(label) ? "Unknown" : "Label only";
    }

    private static readonly (string Needle, string Owner)[] OwnerHints =
    [
        ("rival", "Rival / Hop"),
        ("hop", "Hop"),
        ("bede", "Bede"),
        ("mary", "Marnie"),
        ("marnie", "Marnie"),
        ("sonia", "Sonia"),
        ("dande", "Leon"),
        ("leon", "Leon"),
        ("mama", "Mom"),
        ("mother", "Mom"),
        ("rose", "Rose"),
        ("olive", "Oleana"),
        ("yarrow", "Milo"),
        ("rurina", "Nessa"),
        ("kabigon", "Snorlax event"),
        ("ballguy", "Ball Guy"),
        ("shop", "Shop / clerk"),
        ("nurse", "Pokemon Center nurse"),
        ("gym", "Gym script"),
        ("trainertip", "Trainer tip/sign"),
        ("sign", "Sign / field object"),
    ];

    private sealed class TextVariablePickerDialog : Form
    {
        private readonly IReadOnlyList<TextVariableDefinition> Variables;
        private readonly DataGridView Grid = new();
        private readonly TextBox ArgsText = new();
        private readonly TextBox TokenPreview = new();
        private readonly Button InsertButton = new();
        private readonly Button CancelPickerButton = new();

        private TextVariableDefinition? SelectedVariable;

        public TextVariablePickerDialog(TextVariableGroup group)
        {
            Variables = TextSyntaxHelper.GetVariables(group);
            SelectedToken = Variables.Count == 0 ? string.Empty : Variables[0].Token;

            Text = $"{GetGroupTitle(group)} Variables";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 420);
            Size = new Size(820, 460);

            InitializeLayout();
            PopulateGrid();
            ApplyTheme();
        }

        public string SelectedToken { get; private set; }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            Grid.AllowUserToAddRows = false;
            Grid.AllowUserToDeleteRows = false;
            Grid.AllowUserToResizeRows = false;
            Grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            Grid.Dock = DockStyle.Fill;
            Grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            Grid.MultiSelect = false;
            Grid.ReadOnly = true;
            Grid.RowHeadersVisible = false;
            Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grid.SelectionChanged += (_, _) => SelectCurrentVariable();
            Grid.CellDoubleClick += (_, _) => AcceptSelection();
            Grid.Columns.Add(CreatePickerColumn("Variable", 150));
            Grid.Columns.Add(CreatePickerColumn("Code", 78));
            Grid.Columns.Add(CreatePickerColumn("Args", 78));
            Grid.Columns.Add(CreatePickerColumn("Token", 150));
            Grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Description",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 260,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            var bottom = new TableLayoutPanel
            {
                ColumnCount = 6,
                Dock = DockStyle.Fill,
                RowCount = 1,
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));

            ArgsText.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            ArgsText.Margin = new Padding(0, 6, 10, 6);
            ArgsText.TextChanged += (_, _) => UpdateTokenPreview();

            TokenPreview.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            TokenPreview.Margin = new Padding(0, 6, 10, 6);
            TokenPreview.ReadOnly = true;

            InsertButton.Text = "Insert";
            InsertButton.Dock = DockStyle.Fill;
            InsertButton.Margin = new Padding(0, 6, 8, 6);
            InsertButton.Click += (_, _) => AcceptSelection();

            CancelPickerButton.Text = "Cancel";
            CancelPickerButton.Dock = DockStyle.Fill;
            CancelPickerButton.Margin = new Padding(0, 6, 0, 6);
            CancelPickerButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

            bottom.Controls.Add(CreateLabel("Arguments"), 0, 0);
            bottom.Controls.Add(ArgsText, 1, 0);
            bottom.Controls.Add(CreateLabel("Token"), 2, 0);
            bottom.Controls.Add(TokenPreview, 3, 0);
            bottom.Controls.Add(InsertButton, 4, 0);
            bottom.Controls.Add(CancelPickerButton, 5, 0);

            root.Controls.Add(Grid, 0, 0);
            root.Controls.Add(bottom, 0, 1);
            Controls.Add(root);

            AcceptButton = InsertButton;
            CancelButton = CancelPickerButton;
        }

        private void PopulateGrid()
        {
            foreach (var variable in Variables)
            {
                var rowIndex = Grid.Rows.Add(variable.Name, variable.Code, variable.DefaultArgs, variable.Token, variable.Description);
                Grid.Rows[rowIndex].Tag = variable;
            }

            if (Grid.Rows.Count == 0)
                return;

            Grid.CurrentCell = Grid.Rows[0].Cells[0];
            Grid.Rows[0].Selected = true;
            SelectCurrentVariable();
        }

        private void SelectCurrentVariable()
        {
            if (Grid.CurrentRow?.Tag is not TextVariableDefinition variable)
                return;

            SelectedVariable = variable;
            ArgsText.Text = variable.DefaultArgs;
            UpdateTokenPreview();
        }

        private void UpdateTokenPreview()
        {
            if (SelectedVariable == null)
                return;

            SelectedToken = TextSyntaxHelper.BuildVariableToken(SelectedVariable.Code, ArgsText.Text);
            TokenPreview.Text = SelectedToken;
        }

        private void AcceptSelection()
        {
            if (SelectedVariable == null)
                return;

            UpdateTokenPreview();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ApplyTheme()
        {
            WinFormsTheme.Apply(this);
            Grid.RowTemplate.Height = 26;
            Grid.ColumnHeadersHeight = 28;
            Grid.BackgroundColor = WinFormsTheme.WindowBackground;
        }

        private static DataGridViewTextBoxColumn CreatePickerColumn(string header, int width) => new()
        {
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };

        private static string GetGroupTitle(TextVariableGroup group) => group switch
        {
            TextVariableGroup.Pokemon => "Pokemon",
            TextVariableGroup.Item => "Item",
            TextVariableGroup.Move => "Move",
            TextVariableGroup.Number => "Number",
            _ => "Text",
        };
    }

    private sealed record DialogueMapEntry(
        string Source,
        TextContainer Container,
        int FileIndex,
        string FileName,
        int LineIndex,
        string Label,
        string Owner,
        string Context)
    {
        public string RawText
        {
            get
            {
                var lines = Container[FileIndex];
                return LineIndex < lines.Length ? lines[LineIndex] : string.Empty;
            }
            set
            {
                var lines = Container[FileIndex];
                if (LineIndex >= lines.Length)
                    Array.Resize(ref lines, LineIndex + 1);
                lines[LineIndex] = value;
                Container[FileIndex] = lines;
            }
        }
    }

    private sealed record DialogueEdit(DialogueMapEntry Entry, string OldText, string NewText);
}
