using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Drawing.PokeSprite;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public partial class EncounterList8 : UserControl
{
    private IList<EncounterSlot>? Slots;
    public static string[] SpeciesNames { private get; set; } = [];
    private const string FormColumn = nameof(FormColumn);

    public EncounterList8() => InitializeComponent();

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutEncounterGrid();
    }

    public void Initialize()
    {
        var dgvPicture = new DataGridViewImageColumn
        {
            HeaderText = "Sprite",
            DisplayIndex = 0,
            Width = Math.Max(72, SpriteUtil.Spriter.Width + 20),
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
            ReadOnly = true,
        };
        var padding = SpriteUtil.Spriter.Height > 40
            ? new Padding(0, (SpriteUtil.Spriter.Height / 2) - 8, 0, 0)
            : new Padding(0);
        var speciesPadding = new Padding(8, padding.Top, 0, 0);
        var dgvSpecies = new DataGridViewComboBoxColumn
        {
            HeaderText = "Species",
            DisplayIndex = 1,
            Width = 190,
            MinimumWidth = 160,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft, Padding = speciesPadding },
            FlatStyle = FlatStyle.Flat,
        };
        var dgvForm = new DataGridViewTextBoxColumn
        {
            Name = FormColumn,
            HeaderText = "Form",
            DisplayIndex = 2,
            Width = 64,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
        };
        var dgvPercent = new DataGridViewTextBoxColumn
        {
            HeaderText = "Chance",
            DisplayIndex = 3,
            Width = 72,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
        };

        dgvSpecies.Items.AddRange(SpeciesNames);

        dgv.Columns.Add(dgvPicture);
        dgv.Columns.Add(dgvSpecies);
        dgv.Columns.Add(dgvForm);
        dgv.Columns.Add(dgvPercent);
        SearchableComboBoxBehavior.RegisterGrid(dgv);

        dgv.CellValueChanged += (s, e) =>
        {
            if (e.ColumnIndex == 0)
                return;
            UpdateRowImage(e.RowIndex);
        };
        LayoutEncounterGrid();
        dgv.Columns[0].DefaultCellStyle.SelectionBackColor = dgv.DefaultCellStyle.BackColor;
    }

    private void LayoutEncounterGrid()
    {
        if (dgv is null)
            return;

        var availableWidth = Math.Max(1, Width - (dgv.Left * 2));
        var availableHeight = Math.Max(1, Height - dgv.Top - 16);
        var preferredWidth = dgv.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width) + 3;
        var preferredHeight = dgv.ColumnHeadersHeight + dgv.Rows.Cast<DataGridViewRow>().Sum(r => r.Height) + 3;

        dgv.Size = new System.Drawing.Size(
            Math.Min(availableWidth, Math.Max(1, preferredWidth)),
            Math.Min(availableHeight, Math.Max(1, preferredHeight)));
    }

    public System.Drawing.Size GetPreferredEditorSize()
    {
        LayoutEncounterGrid();
        return new System.Drawing.Size(dgv.Right + dgv.Left, dgv.Bottom + 16);
    }

    private void UpdateRowImage(int row)
    {
        var cells = dgv.Rows[row].Cells;
        int sp = Array.IndexOf(SpeciesNames, cells[1].Value);
        string formstr = cells[2].Value?.ToString() ?? "0";
        if (!int.TryParse(formstr, out var form) || (uint) form > 100)
            cells[2].Value = 0;
        if (!int.TryParse(dgv.Rows[row].Cells[2].Value?.ToString(), out var rate) || (uint)rate > 100)
            cells[3].Value = 0;

        cells[0].Value = SpriteUtil.GetSprite((ushort)sp, (byte)form, 0, 0, 0, false, PKHeX.Core.Shiny.Never);
    }

    public void LoadSlots(IList<EncounterSlot> slots)
    {
        Slots = slots;

        dgv.Rows.Clear();
        dgv.Rows.Add(slots.Count);
        // Fill Entries
        for (int i = 0; i < slots.Count; i++)
        {
            var row = dgv.Rows[i];
            var cells = row.Cells;
            cells[1].Value = SpeciesNames[slots[i].Species];
            cells[2].Value = slots[i].Form;
            cells[3].Value = slots[i].Probability;

            row.Height = SpriteUtil.Spriter.Height + 2;
        }

        dgv.CancelEdit();
        LayoutEncounterGrid();
    }

    public void SaveCurrent()
    {
        if (Slots == null)
            return;
        for (int i = 0; i < Slots.Count; i++)
        {
            SaveRow(i, Slots[i]);
        }
    }

    private void SaveRow(int row, EncounterSlot s)
    {
        var cells = dgv.Rows[row].Cells;
        int sp = Array.IndexOf(SpeciesNames, cells[1].Value ?? SpeciesNames[0]);
        string formstr = cells[2].Value?.ToString() ?? "0";
        _ = byte.TryParse(formstr, out var form);
        string probstr = cells[3].Value?.ToString() ?? "0";
        _ = byte.TryParse(probstr, out var prob);

        if (sp == 0)
        {
            s.Species = s.Form = s.Probability = 0;
            return;
        }

        s.Species = sp;
        s.Form = form;
        s.Probability = prob;
    }

    private void CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (dgv.IsCurrentCellDirty)
            dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
    }
}
