using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pkNX.Randomization;

namespace pkNX.WinForms;

public partial class TextEditor : Form
{
    private const int LineColumnIndex = 0;
    private const int TextColumnIndex = 1;
    private const int ReadableColumnIndex = 2;

    public enum TextEditorMode
    {
        Common,
        Script,
    }

    private readonly TextContainer TextData;
    private bool CloseConfirmed;

    public TextEditor(TextContainer c, TextEditorMode mode)
    {
        InitializeComponent();
        TextData = c;
        Mode = mode;
        for (int i = 0; i < TextData.Length; i++)
            CB_Entry.Items.Add(c.GetFileName(i));
        SearchableComboBoxBehavior.Register(this, CB_Entry);
        CB_Entry.SelectedIndex = 0;
        dgv.EditMode = DataGridViewEditMode.EditOnEnter;
        dgv.EditingControlShowing += (_, e) => ApplyTextEditingControlTheme(e.Control);
        dgv.CellEndEdit += (_, e) => UpdateReadablePreview(e.RowIndex);
        dgv.CellValueChanged += (_, e) =>
        {
            if (e.ColumnIndex == TextColumnIndex)
                UpdateReadablePreview(e.RowIndex);
        };
        dgv.CellToolTipTextNeeded += Dgv_CellToolTipTextNeeded;
        ApplyTextEditorTheme();
    }

    private readonly TextEditorMode Mode;
    private int entry = -1;

    // IO
    private void B_Export_Click(object sender, EventArgs e)
    {
        if (TextData.Length <= 0) return;
        using var dump = new SaveFileDialog { Filter = "Text File|*.txt" };
        if (dump.ShowDialog() != DialogResult.OK)
            return;

        var result = ShowTextChoiceDialog(
            this,
            "Export Text",
            "Remove newline formatting codes? (\\n,\\r,\\c)",
            "Removing newline formatting makes the export easier to read, but that file cannot be imported back into pkNX.",
            "Remove Codes",
            "Keep Codes");
        if (result < 0)
            return;

        bool newline = result == 0;
        string path = dump.FileName;
        ExportTextFile(path, newline, TextData);
    }

    private void B_Import_Click(object sender, EventArgs e)
    {
        if (TextData.Length <= 0) return;
        using var dump = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (dump.ShowDialog() != DialogResult.OK)
            return;

        string path = dump.FileName;
        if (!ConfirmImport())
            return;

        if (!ImportTextFiles(path))
            return;

        // Reload the form with the new data.
        ChangeEntry(this, e);
        WinFormsUtil.Alert("Imported Text from Input Path:", path);
    }

    public static void ExportTextFile(string fileName, bool newline, TextContainer lineData)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xFE], 0, 2); // Write Unicode BOM
        using (TextWriter tw = new StreamWriter(ms, new UnicodeEncoding()))
        {
            for (int i = 0; i < lineData.Length; i++)
            {
                // Get Strings for the File
                string[] data = lineData[i];
                string fn = lineData.GetFileName(i);
                WriteTextFile(tw, fn, data, newline);
            }
        }
        File.WriteAllBytes(fileName, ms.ToArray());
    }

    private static void WriteTextFile(TextWriter tw, string fn, string[] data, bool newline = false)
    {
        // Append the File Header
        tw.WriteLine("~~~~~~~~~~~~~~~");
        tw.WriteLine("Text File : " + fn);
        tw.WriteLine("~~~~~~~~~~~~~~~");
        // Write the String to the File
        foreach (string line in data)
        {
            tw.WriteLine(newline
                ? line.Replace("\\n\\n", " ")
                    .Replace("\\n", " ")
                    .Replace("\\c", "")
                    .Replace("\\r", "")
                    .Replace("\\\\", "\\")
                    .Replace("\\[", "[")
                : line);
        }
    }

    private bool ImportTextFiles(string fileName)
    {
        string[] fileText = File.ReadAllLines(fileName, Encoding.Unicode);
        string[][] textLines = new string[TextData.Length][];
        int ctr = 0;
        bool newlineFormatting = false;
        // Loop through all files
        for (int i = 0; i < fileText.Length; i++)
        {
            string line = fileText[i];
            if (line != "~~~~~~~~~~~~~~~")
                continue;
            string[] brokenLine = fileText[i++ + 1].Split(" : ");
            if (brokenLine.Length != 2)
            { WinFormsUtil.Error($"Invalid Line @ {i}, expected Text File : {ctr}"); return false; }

            var file = brokenLine[1];
            if (int.TryParse(file, out var fnum))
            {
                if (fnum != ctr)
                {
                    WinFormsUtil.Error($"Invalid Line @ {i}, expected Text File : {ctr}");
                    return false;
                }
            }
            // else pray that the filename index lines up

            i += 2; // Skip over the other header line
            List<string> Lines = [];
            while (i < fileText.Length && fileText[i] != "~~~~~~~~~~~~~~~")
            {
                Lines.Add(fileText[i]);
                newlineFormatting |= fileText[i].Contains("\\n"); // Check if any line wasn't stripped of ingame formatting codes for human readability.
                i++;
            }
            i--;
            textLines[ctr++] = [.. Lines];
        }

        // Error Check
        if (ctr != TextData.Length)
        {
            WinFormsUtil.Error("The amount of Text Files in the input file does not match the required for the text file.",
                $"Received: {ctr}, Expected: {TextData.Length}"); return false;
        }
        if (!newlineFormatting)
        {
            WinFormsUtil.Error("The input Text Files do not have the in-game newline formatting codes (\\n,\\r,\\c).",
                "When exporting text, do not remove newline formatting."); return false;
        }

        // All Text Lines received. Store all back.
        for (int i = 0; i < TextData.Length; i++)
        {
            try { TextData[i] = textLines[i]; }
            catch (Exception e) { WinFormsUtil.Error($"The input Text File (# {i}) failed to convert:", e.ToString()); return false; }
        }

        return true;
    }

    private void ChangeEntry(object sender, EventArgs e)
    {
        // Save All the old text
        if (entry > -1 && sender != this)
        {
            try
            {
                TextData[entry] = GetCurrentDGLines();
            }
            catch (Exception ex) { WinFormsUtil.Error(ex.ToString()); }
        }

        // Reset
        entry = CB_Entry.SelectedIndex;
        SetStringsDataGridView(TextData[entry]);
    }

    // Main Handling
    private void SetStringsDataGridView(string[] textArray)
    {
        // Clear the datagrid row content to remove all text lines.
        dgv.Rows.Clear();
        // Clear the header columns, these are repopulated every time.
        dgv.Columns.Clear();
        if (textArray.Length == 0)
            return;
        // Reset settings and columns.
        dgv.AllowUserToResizeColumns = true;
        DataGridViewColumn dgvLine = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line",
            DisplayIndex = 0,
            Width = 42,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        dgvLine.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        DataGridViewTextBoxColumn dgvText = new()
        {
            HeaderText = "Text",
            DisplayIndex = 1,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 65,
            MinimumWidth = 360,
        };

        DataGridViewTextBoxColumn dgvReadable = new()
        {
            HeaderText = "Readable",
            DisplayIndex = 2,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 45,
            MinimumWidth = 260,
            ReadOnly = true,
        };

        dgv.Columns.Add(dgvLine);
        dgv.Columns.Add(dgvText);
        dgv.Columns.Add(dgvReadable);
        dgv.Rows.Add(textArray.Length);
        WinFormsTheme.Apply(dgv);

        // Add the text lines into their cells.
        for (int i = 0; i < textArray.Length; i++)
        {
            dgv.Rows[i].Cells[LineColumnIndex].Value = i;
            dgv.Rows[i].Cells[TextColumnIndex].Value = textArray[i];
            UpdateReadablePreview(i);
        }
    }

    private string[] GetCurrentDGLines()
    {
        // Get Line Count
        string[] lines = new string[dgv.RowCount];
        for (int i = 0; i < dgv.RowCount; i++)
            lines[i] = dgv.Rows[i].Cells[TextColumnIndex].Value as string ?? string.Empty;
        return lines;
    }
    // Meta Usage
    private void B_AddLine_Click(object sender, EventArgs e)
    {
        int currentRow = 0;
        try { currentRow = dgv.CurrentRow!.Index; }
        catch { dgv.Rows.Add(); }
        if (dgv.Rows.Count != 1 && (currentRow < dgv.Rows.Count - 1 || currentRow == 0))
        {
            if (ModifierKeys != Keys.Control && currentRow != 0)
            {
                if (!ConfirmInsertLine())
                    return;
            }
            // Insert new Row after current row.
            dgv.Rows.Insert(currentRow + 1);
        }

        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            dgv.Rows[i].Cells[LineColumnIndex].Value = i.ToString();
            UpdateReadablePreview(i);
        }
    }

    private void B_RemoveLine_Click(object sender, EventArgs e)
    {
        int currentRow = dgv.CurrentRow!.Index;
        if (currentRow < dgv.Rows.Count - 1)
        {
            if (ModifierKeys != Keys.Control && !ConfirmRemoveLine())
                return;
        }
        dgv.Rows.RemoveAt(currentRow);

        // Resequence the Index Value column
        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            dgv.Rows[i].Cells[LineColumnIndex].Value = i.ToString();
            UpdateReadablePreview(i);
        }
    }

    private void SaveCurrentFile()
    {
        // Save any pending edits
        dgv.EndEdit();
        // Save All the old text
        if (entry > -1)
            TextData[entry] = GetCurrentDGLines();
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        // gametext can be horribly broken if randomized
        if (Mode == TextEditorMode.Common && !ConfirmCommonRandomize())
            return;

        // get if the user wants to randomize current text file or all files
        var dr = ShowTextChoiceDialog(
            this,
            "Randomize Text",
            "Choose which text files to randomize.",
            "This can change many lines at once. Review the results before saving, or close without saving to discard them.",
            "All Files",
            "Current File");

        if (dr < 0)
            return;

        // get if pure shuffle or smart shuffle (no shuffle if variable present)
        var drs = ShowTextChoiceDialog(
            this,
            "Shuffle Mode",
            "Choose how randomization should treat text with variables.",
            "Smart shuffle skips lines that contain variable markers. Pure shuffle can move every line, including strings with control or variable markers.",
            "Smart Shuffle",
            "Pure Shuffle");

        if (drs < 0)
            return;

        bool all = dr == 0;
        bool smart = drs == 0;

        // save current
        if (entry > -1)
            TextData[entry] = GetCurrentDGLines();

        // single-entire looping
        int start = all ? 0 : entry;
        int end = all ? TextData.Length - 1 : entry;

        // Gather strings
        List<string> strings = [];
        for (int i = start; i <= end; i++)
        {
            string[] data = TextData[i];
            strings.AddRange(smart
                ? data.Where(line => !line.Contains('['))
                : data);
        }

        // Shuffle up
        string[] pool = [.. strings];
        Util.Shuffle(pool);

        // Apply Text
        int ctr = 0;
        for (int i = start; i <= end; i++)
        {
            string[] data = TextData[i];

            for (int j = 0; j < data.Length; j++) // apply lines
            {
                if (!smart || !data[j].Contains("["))
                    data[j] = pool[ctr++];
            }

            TextData[i] = data;
        }

        // Load current text file
        SetStringsDataGridView(TextData[entry]);

        WinFormsUtil.Alert("Strings randomized!");
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (!ConfirmSave())
            return;

        Modified = true;
        SaveCurrentFile();
        TextData.Save();
        CloseConfirmed = true;
        Close();
    }

    public bool Modified { get; set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!CloseConfirmed && e.CloseReason == CloseReason.UserClosing && !ConfirmCloseWithoutSaving())
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private void ApplyTextEditorTheme()
    {
        WinFormsTheme.Apply(this);
        dgv.RowTemplate.Height = 26;
        dgv.ColumnHeadersHeight = 28;
        dgv.EditMode = DataGridViewEditMode.EditOnEnter;
        dgv.BackgroundColor = WinFormsTheme.WindowBackground;
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        dgv.ShowCellToolTips = true;
        CB_Entry.DropDownWidth = Math.Max(CB_Entry.Width, 260);
        MinimumSize = new Size(Math.Max(MinimumSize.Width, 980), Math.Max(MinimumSize.Height, 420));
        if (Width < MinimumSize.Width)
            Width = MinimumSize.Width;
        if (Height < 520)
            Height = 520;
    }

    private static void ApplyTextEditingControlTheme(Control control)
    {
        control.BackColor = WinFormsTheme.InputBackground;
        control.ForeColor = WinFormsTheme.Text;
    }

    private void UpdateReadablePreview(int rowIndex)
    {
        if ((uint)rowIndex >= dgv.Rows.Count || dgv.Columns.Count <= ReadableColumnIndex)
            return;

        var text = dgv.Rows[rowIndex].Cells[TextColumnIndex].Value as string ?? string.Empty;
        var readable = TextSyntaxHelper.GetReadableTextPreview(text);
        var tooltip = TextSyntaxHelper.GetReadableTextToolTip(text);
        dgv.Rows[rowIndex].Cells[ReadableColumnIndex].Value = readable;
        dgv.Rows[rowIndex].Cells[ReadableColumnIndex].ToolTipText = tooltip;
        dgv.Rows[rowIndex].Cells[TextColumnIndex].ToolTipText = tooltip;
    }

    private void Dgv_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex is not TextColumnIndex and not ReadableColumnIndex)
            return;

        var text = dgv.Rows[e.RowIndex].Cells[TextColumnIndex].Value as string ?? string.Empty;
        e.ToolTipText = TextSyntaxHelper.GetReadableTextToolTip(text);
    }

    private bool ConfirmSave()
        => ThemedConfirmationDialog.Show(
            this,
            "Save Text Changes",
            "Save the current text changes?\n\nThis writes the edited text files to the loaded project. Closing without saving will discard this text editor session.",
            "Save");

    private bool ConfirmImport()
        => ThemedConfirmationDialog.Show(
            this,
            "Import Text",
            "Import all text from the selected file?\n\nThis replaces the loaded text tables in this editor session. Review the result before saving, or close without saving to discard it.",
            "Import");

    private bool ConfirmCommonRandomize()
        => ThemedConfirmationDialog.Show(
            this,
            "Randomize Game Text",
            "Randomizing Common text can break menus, labels, item names, control-code strings, and other game-wide UI text.\n\nContinue only if you are experimenting and can review the result before saving.",
            "Continue");

    private bool ConfirmInsertLine()
        => ThemedConfirmationDialog.Show(
            this,
            "Insert Text Line",
            "Insert a line here?\n\nAdding a line before the end shifts every following line index. Script and UI references may point at different text afterward.",
            "Insert");

    private bool ConfirmRemoveLine()
        => ThemedConfirmationDialog.Show(
            this,
            "Remove Text Line",
            "Remove this line?\n\nDeleting a line before the end shifts every following line index. Script and UI references may point at different text afterward.",
            "Remove");

    private bool ConfirmCloseWithoutSaving()
        => ThemedConfirmationDialog.Show(
            this,
            "Close Text Editor",
            "Close the text editor without saving?\n\nAny text edits, imports, added lines, removed lines, or randomization made in this editor session will be discarded.",
            "Close");

    private static int ShowTextChoiceDialog(IWin32Window owner, string title, string heading, string message, params string[] choices)
    {
        var result = -1;
        using var dialog = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 210),
        };

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var headingLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(dialog.Font, FontStyle.Bold),
            Text = heading,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };
        var messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false,
        };

        var cancel = CreateChoiceButton("Cancel", DialogResult.Cancel);
        buttons.Controls.Add(cancel);
        for (int i = choices.Length - 1; i >= 0; i--)
        {
            var choiceIndex = i;
            var button = CreateChoiceButton(choices[i], DialogResult.OK);
            button.Click += (_, _) => result = choiceIndex;
            buttons.Controls.Add(button);
        }

        root.Controls.Add(headingLabel, 0, 0);
        root.Controls.Add(messageLabel, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(root);
        dialog.CancelButton = cancel;
        WinFormsTheme.Apply(dialog);

        return dialog.ShowDialog(owner) == DialogResult.OK ? result : -1;
    }

    private static Button CreateChoiceButton(string text, DialogResult result)
    {
        var width = Math.Max(112, TextRenderer.MeasureText(text, SystemFonts.MessageBoxFont).Width + 32);
        return new Button
        {
            DialogResult = result,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0),
            Text = text,
            UseVisualStyleBackColor = false,
            Width = width,
        };
    }
}
