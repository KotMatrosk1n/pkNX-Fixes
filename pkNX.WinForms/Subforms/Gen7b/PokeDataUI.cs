using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pkNX.Game;
using pkNX.Randomization;
using pkNX.Structures;
using PKHeX.Drawing.PokeSprite;
using Util = pkNX.Randomization.Util;

namespace pkNX.WinForms;

public partial class PokeDataUI : Form
{
    private sealed record SpeciesComboEntry(int Index, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly bool Loaded;
    private readonly GameData Data;
    private bool CloseConfirmed;
    private bool SpeciesSelectionQueued;
    private bool UpdatingSpeciesFilter;
    private bool SuppressSpeciesAutoComplete;
    private ListBox? SpeciesSearchList;
    private int CurrentSpeciesIndex = -1;
    private int PendingSpeciesIndex = -1;
    private const int PokemonHeaderHeight = 32;
    private const int ThemedTabHeight = 24;
    private const int NestedTabHeaderHeight = 40;
    private const int NestedTabButtonHeight = 27;

    public PokeDataUI(PokeEditor editor, GameManager rom, GameData data)
    {
        ROM = rom;
        Editor = editor;
        Data = data;
        InitializeComponent();

        helditem_boxes = [CB_HeldItem1, CB_HeldItem2, CB_HeldItem3];
        ability_boxes = [CB_Ability1, CB_Ability2, CB_Ability3];
        typing_boxes = [CB_Type1, CB_Type2];
        eggGroup_boxes = [CB_EggGroup1, CB_EggGroup2];

        items = ROM.GetStrings(TextName.ItemNames);
        movelist = ROM.GetStrings(TextName.MoveNames);
        species = ROM.GetStrings(TextName.SpeciesNames);
        abilities = ROM.GetStrings(TextName.AbilityNames);
        types = ROM.GetStrings(TextName.TypeNames);
        movelist = EditorUtil.SanitizeMoveList(movelist);

        species[0] = "---";
        abilities[0] = items[0] = movelist[0] = "";

        var pt = Data.PersonalData;
        cPersonal = pt[0];
        cLearnset = Editor.Learn[0];
        cEvos = Editor.Evolve[0];
        cMega = Editor.Mega != null ? Editor.Mega[0] : [];

        var altForms = pt.GetFormList(species);
        entryNames = pt.GetPersonalEntryList(altForms, species, out baseForms, out formVal);

        InitPersonal();
        InitLearn();

        InitEvo(Editor.Evolve[0].PossibleEvolutions.Length);

        Megas = Editor.Mega != null ? InitMega(2) : [];

        ConfigureSpeciesSelector();
        CB_Species.SelectedIndex = 1;
        Loaded = true;

        PG_Personal.SelectedObject = EditUtil.Settings.Personal;
        PG_Evolution.SelectedObject = EditUtil.Settings.Species;
        PG_Learn.SelectedObject = EditUtil.Settings.Learn;
        PG_Move.SelectedObject = EditUtil.Settings.Move;

        ApplyPokemonEditorTheme();
        FormClosing += PokeDataUI_FormClosing;
        Shown += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            CB_Species.Focus();
            CB_Species.SelectAll();
        }));
    }

    public readonly GameManager ROM;
    public readonly PokeEditor Editor;
    public bool Modified { get; set; }

    private readonly ComboBox[] helditem_boxes;
    private readonly ComboBox[] ability_boxes;
    private readonly ComboBox[] typing_boxes;
    private readonly ComboBox[] eggGroup_boxes;

    private readonly string[] items;
    private readonly string[] movelist;
    private readonly string[] species;
    private readonly string[] abilities;
    private readonly string[] types;

    private readonly string[] entryNames;
    private readonly int[] baseForms, formVal;
    private SpeciesComboEntry[] SpeciesEntries = [];

    public IPersonalInfo cPersonal;
    public Learnset cLearnset;
    public EvolutionSet cEvos;
    public MegaEvolutionSet[] cMega;
    private readonly MegaEvoEntry[] Megas;

    public void InitPersonal()
    {
        var TMs = Editor.TMHM;
        if (TMs.Count == 0) // No ExeFS to grab TMs from.
        {
            for (int i = 0; i < 100; i++)
                CLB_TM.Items.Add($"TM{i + 1:00}");
        }
        else // Use TM moves.
        {
            if (GameVersion.SWSH.Contains(ROM.Game))
            {
                var TRs = Editor.TR;
                for (int i = 0; i < TMs.Count; i++)
                    CLB_TM.Items.Add($"TM{i:00} {movelist[TMs[i]]}");
                for (int i = 0; i < TRs.Count; i++)
                    CLB_TM.Items.Add($"TR{i:00} {movelist[TRs[i]]}");

                foreach (var move in Legal.TypeTutor8)
                    CLB_TypeTutor.Items.Add(movelist[move]);
                foreach (var move in Legal.Tutors_SWSH_1)
                    CLB_SpecialTutor.Items.Add(movelist[move]);
            }
            else
            {
                for (int i = 0; i < TMs.Count; i++)
                    CLB_TM.Items.Add($"TM{i + 1:00} {movelist[TMs[i]]}");
            }
        }

        SpeciesEntries = entryNames.Select((z, i) => new SpeciesComboEntry(i, $"{z} - {i:000}")).ToArray();
        CB_Species.Items.AddRange(SpeciesEntries);

        foreach (ComboBox cb in helditem_boxes)
            cb.Items.AddRange(items);

        CB_ZItem.Items.AddRange(items);
        CB_ZBaseMove.Items.AddRange(movelist);
        CB_ZMove.Items.AddRange(movelist);

        foreach (ComboBox cb in ability_boxes)
            cb.Items.AddRange(abilities);

        foreach (ComboBox cb in typing_boxes)
            cb.Items.AddRange(types);

        foreach (ComboBox cb in eggGroup_boxes)
            cb.Items.AddRange(Enum.GetNames<EggGroup>());

        CB_Color.Items.AddRange(Enum.GetNames<PokeColor>());
        CB_EXPGroup.Items.AddRange(Enum.GetNames<EXPGroup>());
    }

    public void InitLearn()
    {
        string[] sortedmoves = (string[])movelist.Clone();

        Array.Sort(sortedmoves);
        DataGridViewColumn dgvLevel = new DataGridViewTextBoxColumn();
        {
            dgvLevel.HeaderText = "Level";
            dgvLevel.DisplayIndex = 0;
            dgvLevel.Width = 45;
            dgvLevel.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        DataGridViewComboBoxColumn dgvMove = new();
        {
            dgvMove.HeaderText = "Move";
            dgvMove.DisplayIndex = 1;
            for (int i = 0; i < movelist.Length; i++)
                dgvMove.Items.Add(sortedmoves[i]); // add only the Names

            dgvMove.Width = 135;
            dgvMove.FlatStyle = FlatStyle.Flat;
        }
        dgv.Columns.Add(dgvLevel);
        dgv.Columns.Add(dgvMove);
    }

    private EvolutionRow[] EvoRows = [];

    public void InitEvo(int rows)
    {
        EvoRows = new EvolutionRow[rows];
        EvolutionRow.species = species;
        EvolutionRow.items = items;
        EvolutionRow.movelist = movelist;
        EvolutionRow.types = types;

        for (int i = 0; i < rows; i++)
        {
            var row = new EvolutionRow();
            flowLayoutPanel1.Controls.Add(row);
            flowLayoutPanel1.SetFlowBreak(row, true);
            EvoRows[i] = row;
        }
    }

    public MegaEvoEntry[] InitMega(int count)
    {
        var result = new MegaEvoEntry[count];
        MegaEvoEntry.items = items;

        for (int i = 0; i < count; i++)
        {
            var row = new MegaEvoEntry();
            flowLayoutPanel1.Controls.Add(row);
            result[i] = row;
        }

        return result;
    }

    public void UpdateIndex(object sender, EventArgs e)
    {
        if (UpdatingSpeciesFilter)
            return;

        var index = GetSelectedSpeciesIndex();
        if (index < 0)
            return;

        if (!Loaded)
        {
            ApplySpeciesIndex(index, false);
            return;
        }

        QueueSpeciesIndexUpdate(index);
    }

    private void ConfigureSpeciesSelector()
    {
        CB_Species.DropDownStyle = ComboBoxStyle.DropDown;
        CB_Species.AutoCompleteMode = AutoCompleteMode.None;
        CB_Species.AutoCompleteSource = AutoCompleteSource.None;
        CB_Species.DrawMode = DrawMode.OwnerDrawFixed;
        CB_Species.ItemHeight = 22;
        CB_Species.DropDownHeight = 1;
        CB_Species.DrawItem += DrawSpeciesItem;
        CB_Species.TextUpdate += (_, _) =>
        {
            var append = !SuppressSpeciesAutoComplete;
            SuppressSpeciesAutoComplete = false;
            FilterSpeciesEntries(append);
        };
        CB_Species.DropDown += (_, _) =>
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                CB_Species.DroppedDown = false;
                ShowSpeciesSearchList(GetSpeciesSearchText());
            }));
        };
        CB_Species.SelectionChangeCommitted += (_, _) =>
        {
            if (GetSelectedSpeciesIndex() is { } index && index >= 0)
                QueueSpeciesIndexUpdate(index);
        };
        CB_Species.DropDownClosed += (_, _) => CB_Species.DroppedDown = false;
        CB_Species.Leave += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            if (SpeciesSearchList?.Focused == true)
                return;

            CommitSpeciesTextSelection();
            HideSpeciesSearchList();
        }));
        CB_Species.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                RestoreCurrentSpeciesText();
                HideSpeciesSearchList();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Down && SpeciesSearchList is { Visible: true, Items.Count: > 0 } list)
            {
                list.SelectedIndex = Math.Max(0, list.SelectedIndex);
                list.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode is Keys.Back or Keys.Delete)
            {
                HandleSpeciesDeleteKey(e);
                return;
            }

            if (e.KeyCode != Keys.Enter)
                return;

            CommitSpeciesTextSelection();
            e.Handled = true;
            e.SuppressKeyPress = true;
        };

        SpeciesSearchList = new ListBox
        {
            BackColor = WinFormsTheme.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Default,
            DrawMode = DrawMode.OwnerDrawFixed,
            ForeColor = WinFormsTheme.Text,
            IntegralHeight = false,
            ItemHeight = 22,
            Visible = false,
        };
        SpeciesSearchList.DrawItem += DrawSpeciesListItem;
        SpeciesSearchList.Click += (_, _) => CommitSpeciesListSelection();
        SpeciesSearchList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitSpeciesListSelection();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                HideSpeciesSearchList();
                CB_Species.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        SpeciesSearchList.Leave += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            if (CB_Species.Focused)
                return;

            HideSpeciesSearchList();
        }));
    }

    private void HandleSpeciesDeleteKey(KeyEventArgs e)
    {
        SuppressSpeciesAutoComplete = true;
        if (CB_Species.SelectionLength == 0)
        {
            var caret = CB_Species.SelectionStart;
            var canDelete = e.KeyCode == Keys.Back ? caret > 0 : caret < CB_Species.Text.Length;
            if (!canDelete)
                SuppressSpeciesAutoComplete = false;
            return;
        }

        var text = CB_Species.Text;
        var selectionStart = Math.Min(CB_Species.SelectionStart, text.Length);
        var newText = e.KeyCode == Keys.Back && selectionStart > 0
            ? text[..(selectionStart - 1)]
            : text[..selectionStart];

        SetSpeciesSearchText(newText);
        SuppressSpeciesAutoComplete = false;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void QueueSpeciesIndexUpdate(int index)
    {
        PendingSpeciesIndex = index;
        if (SpeciesSelectionQueued || IsDisposed || !IsHandleCreated)
            return;

        SpeciesSelectionQueued = true;
        BeginInvoke((MethodInvoker)(() =>
        {
            SpeciesSelectionQueued = false;
            var queuedIndex = PendingSpeciesIndex;
            PendingSpeciesIndex = -1;
            ApplySpeciesIndex(queuedIndex, true);
        }));
    }

    private void ApplySpeciesIndex(int index, bool saveCurrent)
    {
        if ((uint)index >= (uint)entryNames.Length)
            return;

        if (index == CurrentSpeciesIndex)
        {
            SetSpeciesText(index);
            return;
        }

        if (saveCurrent)
            SaveCurrent();

        LoadIndex(index);
        CurrentSpeciesIndex = index;
        SetSpeciesText(index);
    }

    private void CommitSpeciesTextSelection()
    {
        var index = GetSpeciesIndexFromText(CB_Species.Text);
        if (index < 0)
        {
            RestoreCurrentSpeciesText();
            HideSpeciesSearchList();
            return;
        }

        HideSpeciesSearchList();
        if (Loaded)
            QueueSpeciesIndexUpdate(index);
        else
            ApplySpeciesIndex(index, false);
    }

    private int GetSpeciesIndexFromText(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return -1;

        foreach (var entry in SpeciesEntries)
        {
            if (string.Equals(entry.Text, text, StringComparison.OrdinalIgnoreCase))
                return entry.Index;
        }

        var separator = text.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator >= 0 && int.TryParse(text[(separator + 3)..], out var suffixIndex) && (uint)suffixIndex < (uint)SpeciesEntries.Length)
            return suffixIndex;

        var match = -1;
        foreach (var entry in SpeciesEntries)
        {
            if (!SpeciesMatches(entry, text))
                continue;
            if (match >= 0)
                return -1;
            match = entry.Index;
        }

        return match;
    }

    private int GetSelectedSpeciesIndex() => CB_Species.SelectedItem is SpeciesComboEntry entry ? entry.Index : -1;

    private void FilterSpeciesEntries(bool appendAutoComplete)
    {
        if (UpdatingSpeciesFilter)
            return;

        var userText = GetSpeciesSearchText();
        var matches = GetSpeciesMatches(userText).ToArray();
        var autoCompleteMatch = appendAutoComplete ? GetSpeciesAutoCompleteMatch(userText, matches) : null;
        var displayText = autoCompleteMatch?.Text ?? userText;
        var selectionStart = autoCompleteMatch == null ? userText.Length : Math.Min(userText.Length, autoCompleteMatch.Text.Length);
        var selectionLength = autoCompleteMatch == null ? 0 : autoCompleteMatch.Text.Length - selectionStart;

        UpdatingSpeciesFilter = true;
        CB_Species.BeginUpdate();
        CB_Species.Items.Clear();
        CB_Species.Items.AddRange(matches);
        CB_Species.EndUpdate();
        CB_Species.Text = displayText;
        CB_Species.SelectionStart = selectionStart;
        CB_Species.SelectionLength = selectionLength;

        UpdatingSpeciesFilter = false;
        ShowSpeciesSearchList(userText, matches);
    }

    private void SetSpeciesSearchText(string text)
    {
        UpdatingSpeciesFilter = true;
        CB_Species.Text = text;
        CB_Species.SelectionStart = text.Length;
        CB_Species.SelectionLength = 0;
        UpdatingSpeciesFilter = false;
        FilterSpeciesEntries(false);
    }

    private void ShowSpeciesSearchList(string text)
    {
        ShowSpeciesSearchList(text, GetSpeciesMatches(text).ToArray());
    }

    private void ShowSpeciesSearchList(string text, IReadOnlyCollection<SpeciesComboEntry> matches)
    {
        if (SpeciesSearchList == null)
            return;

        if (SpeciesSearchList.Parent != this)
            Controls.Add(SpeciesSearchList);

        SpeciesSearchList.BeginUpdate();
        SpeciesSearchList.Items.Clear();
        foreach (var match in matches)
            SpeciesSearchList.Items.Add(match);
        SpeciesSearchList.EndUpdate();

        if (matches.Count == 0 || !CB_Species.Focused && SpeciesSearchList.Focused != true)
        {
            HideSpeciesSearchList();
            return;
        }

        const int maxVisibleRows = 10;
        var visibleRows = Math.Min(matches.Count, maxVisibleRows);
        var screenLocation = CB_Species.Parent?.PointToScreen(CB_Species.Location) ?? PointToScreen(CB_Species.Location);
        var location = PointToClient(new Point(screenLocation.X, screenLocation.Y + CB_Species.Height));
        var availableHeight = Math.Max(SpeciesSearchList.ItemHeight + 2, ClientSize.Height - location.Y - 4);
        var height = Math.Min(availableHeight, visibleRows * SpeciesSearchList.ItemHeight + 2);

        SpeciesSearchList.Bounds = new Rectangle(location.X, location.Y, CB_Species.Width, height);
        SpeciesSearchList.Visible = true;
        SpeciesSearchList.BringToFront();
        SpeciesSearchList.SelectedIndex = matches.Count > 0 ? 0 : -1;
    }

    private void HideSpeciesSearchList()
    {
        if (SpeciesSearchList != null)
            SpeciesSearchList.Visible = false;
    }

    private void CommitSpeciesListSelection()
    {
        if (SpeciesSearchList?.SelectedItem is not SpeciesComboEntry entry)
            return;

        HideSpeciesSearchList();
        CB_Species.Focus();
        if (Loaded)
            QueueSpeciesIndexUpdate(entry.Index);
        else
            ApplySpeciesIndex(entry.Index, false);
    }

    private IEnumerable<SpeciesComboEntry> GetSpeciesMatches(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return SpeciesEntries;

        return SpeciesEntries
            .Where(entry => SpeciesMatches(entry, text))
            .OrderBy(entry => SpeciesMatchRank(entry, text))
            .ThenBy(entry => entry.Index);
    }

    private static bool SpeciesMatches(SpeciesComboEntry entry, string text)
    {
        return entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase)
            || entry.Index.ToString("000").StartsWith(text, StringComparison.OrdinalIgnoreCase);
    }

    private static int SpeciesMatchRank(SpeciesComboEntry entry, string text)
    {
        if (entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (entry.Index.ToString("000").StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static SpeciesComboEntry? GetSpeciesAutoCompleteMatch(string text, IReadOnlyList<SpeciesComboEntry> matches)
    {
        text = text.Trim();
        if (text.Length == 0)
            return null;

        return matches.FirstOrDefault(entry => entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase));
    }

    private string GetSpeciesSearchText()
    {
        var text = CB_Species.Text;
        var selectionStart = Math.Min(CB_Species.SelectionStart, text.Length);
        return selectionStart <= 0 ? text : text[..selectionStart];
    }

    private void ResetSpeciesEntries()
    {
        var wasUpdating = UpdatingSpeciesFilter;
        UpdatingSpeciesFilter = true;
        CB_Species.BeginUpdate();
        CB_Species.Items.Clear();
        CB_Species.Items.AddRange(SpeciesEntries);
        CB_Species.EndUpdate();
        UpdatingSpeciesFilter = wasUpdating;
    }

    private void SetSpeciesText(int index)
    {
        if ((uint)index >= (uint)SpeciesEntries.Length)
            return;

        UpdatingSpeciesFilter = true;
        ResetSpeciesEntries();
        CB_Species.SelectedItem = SpeciesEntries[index];
        CB_Species.Text = SpeciesEntries[index].Text;
        CB_Species.SelectionStart = 0;
        CB_Species.SelectionLength = 0;
        UpdatingSpeciesFilter = false;
        HideSpeciesSearchList();
    }

    private void RestoreCurrentSpeciesText()
    {
        if ((uint)CurrentSpeciesIndex < (uint)SpeciesEntries.Length)
            SetSpeciesText(CurrentSpeciesIndex);
    }

    private static void DrawSpeciesItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox comboBox || e.Index < 0 || e.Index >= comboBox.Items.Count)
            return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backColor = selected ? WinFormsTheme.SelectionBackground : WinFormsTheme.InputBackground;
        var foreColor = selected ? WinFormsTheme.SelectionText : WinFormsTheme.Text;

        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            comboBox.Items[e.Index]?.ToString() ?? string.Empty,
            comboBox.Font,
            e.Bounds,
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }

    private static void DrawSpeciesListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
            return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backColor = selected ? WinFormsTheme.SelectionBackground : WinFormsTheme.InputBackground;
        var foreColor = selected ? WinFormsTheme.SelectionText : WinFormsTheme.Text;

        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            listBox.Items[e.Index]?.ToString() ?? string.Empty,
            listBox.Font,
            new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }

    private void LoadIndex(int index)
    {
        int spec = baseForms[index];
        if (spec == 0)
            spec = index;
        var form = formVal[index];
        LoadPersonal(Editor.Personal[index]);
        LoadLearnset(Editor.Learn[index]);
        LoadEvolutions(Editor.Evolve[index]);
        if (Editor.Mega != null)
            LoadMegas(Editor.Mega[index], spec);
        Bitmap rawImg = (Bitmap)SpriteUtil.GetSprite((ushort)spec, (byte)form, 0, 0, 0, false, PKHeX.Core.Shiny.Never);
        Bitmap bigImg = new(rawImg.Width * 2, rawImg.Height * 2);
        for (int x = 0; x < rawImg.Width; x++)
        {
            for (int y = 0; y < rawImg.Height; y++)
            {
                Color c = rawImg.GetPixel(x, y);
                bigImg.SetPixel(2 * x, 2 * y, c);
                bigImg.SetPixel((2 * x) + 1, 2 * y, c);
                bigImg.SetPixel(2 * x, (2 * y) + 1, c);
                bigImg.SetPixel((2 * x) + 1, (2 * y) + 1, c);
            }
        }
        PB_MonSprite.Image = bigImg;
    }

    private void SaveCurrent()
    {
        ValidateChildren();
        dgv.EndEdit();
        SavePersonal();
        SaveLearnset();
        SaveEvolutions();
        SaveMegas();
    }

    public void LoadPersonal(IPersonalInfo pkm)
    {
        cPersonal = pkm;
        TB_BaseHP.Text = pkm.HP.ToString("000");
        TB_BaseATK.Text = pkm.ATK.ToString("000");
        TB_BaseDEF.Text = pkm.DEF.ToString("000");
        TB_BaseSPE.Text = pkm.SPE.ToString("000");
        TB_BaseSPA.Text = pkm.SPA.ToString("000");
        TB_BaseSPD.Text = pkm.SPD.ToString("000");
        TB_HPEVs.Text = pkm.EV_HP.ToString("0");
        TB_ATKEVs.Text = pkm.EV_ATK.ToString("0");
        TB_DEFEVs.Text = pkm.EV_DEF.ToString("0");
        TB_SPEEVs.Text = pkm.EV_SPE.ToString("0");
        TB_SPAEVs.Text = pkm.EV_SPA.ToString("0");
        TB_SPDEVs.Text = pkm.EV_SPD.ToString("0");

        CB_Type1.SelectedIndex = (int)pkm.Type1;
        CB_Type2.SelectedIndex = (int)pkm.Type2;

        TB_CatchRate.Text = pkm.CatchRate.ToString("000");
        TB_Stage.Text = pkm.EvoStage.ToString("0");

        CB_HeldItem1.SelectedIndex = pkm.Item1;
        CB_HeldItem2.SelectedIndex = pkm.Item2;
        CB_HeldItem3.SelectedIndex = pkm.Item3;

        TB_Gender.Text = pkm.Gender.ToString("000");

        if (pkm is IPersonalEgg_v1 eggInfo)
            TB_HatchCycles.Text = eggInfo.HatchCycles.ToString("000");

        TB_Friendship.Text = pkm.BaseFriendship.ToString("000");

        CB_EXPGroup.SelectedIndex = pkm.EXPGrowth;

        CB_EggGroup1.SelectedIndex = pkm.EggGroup1;
        CB_EggGroup2.SelectedIndex = pkm.EggGroup2;

        CB_Ability1.SelectedIndex = pkm.Ability1;
        CB_Ability2.SelectedIndex = pkm.Ability2;
        CB_Ability3.SelectedIndex = pkm.AbilityH;

        TB_FormCount.Text = pkm.FormCount.ToString("000");
        TB_FormSprite.Text = pkm.FormSprite.ToString("000");

        TB_RawColor.Text = pkm.Color.ToString("000");
        CB_Color.SelectedIndex = pkm.Color & 0xF;

        TB_BaseExp.Text = pkm.BaseEXP.ToString("000");
        TB_BST.Text = pkm.GetBaseStatTotal().ToString("000");

        TB_Height.Text = ((decimal)pkm.Height / 100).ToString("00.00");
        TB_Weight.Text = ((decimal)pkm.Weight / 10).ToString("000.0");

        if (pkm is IPersonalInfoSM sm)
        {
            TB_CallRate.Text = sm.EscapeRate.ToString("000");
            CB_ZItem.SelectedIndex = sm.SpecialZ_Item;
            CB_ZBaseMove.SelectedIndex = sm.SpecialZ_BaseMove;
            CB_ZMove.SelectedIndex = sm.SpecialZ_ZMove;
            CHK_Variant.Checked = sm.IsRegionalForm;
            CHK_IsPresentInGame.Visible = CHK_CanNotDynamax.Visible =
                L_RegionalDex.Visible = L_ArmorDex.Visible = L_CrownDex.Visible = TB_RegionalDex.Visible = TB_ArmorDex.Visible = TB_CrownDex.Visible = false;
        }
        if (pkm is IPersonalInfoGG gg)
        {
            MT_GoID.Text = gg.GoSpecies.ToString("000");
            CHK_Variant.Checked = gg.IsRegionalForm;
            GB_ZMove.Visible = CHK_IsPresentInGame.Visible = CHK_CanNotDynamax.Visible = L_TypeTutors.Visible = CLB_TypeTutor.Visible = CLB_SpecialTutor.Visible =
                L_RegionalDex.Visible = L_ArmorDex.Visible = L_CrownDex.Visible = TB_RegionalDex.Visible = TB_ArmorDex.Visible = TB_CrownDex.Visible = false;
        }
        if (pkm is IPersonalInfoSWSH swsh)
        {
            L_TM.Text = "TMs/TRs:";
            MT_GoID.Text = swsh.DexIndexNational.ToString("000");
            TB_RegionalDex.Text = swsh.DexIndexRegional.ToString("000");
            TB_ArmorDex.Text = swsh.ArmorDexIndex.ToString("000");
            TB_CrownDex.Text = swsh.CrownDexIndex.ToString("000");
            CHK_IsPresentInGame.Checked = swsh.IsPresentInGame;
            CHK_Variant.Checked = swsh.IsRegionalForm;
            CHK_CanNotDynamax.Checked = swsh.CanNotDynamax;
            L_CallRate.Visible = TB_CallRate.Visible = GB_ZMove.Visible = false;
        }

        if (pkm is IMovesInfo_v1 mi)
        {
            int halfList = CLB_TM.Items.Count / 2;
            int fullList = CLB_TM.Items.Count;

            for (int i = 0; i < halfList; i++)
                CLB_TM.SetItemChecked(i, mi.TMHM[i]); // Bitflags for TM

            if (pkm is IMovesInfo_SWSH mitr) // if SWSH, the second half is just TRs
            {
                for (int i = 0; i < halfList; i++)
                    CLB_TM.SetItemChecked(i + halfList, mitr.TR[i]); // Bitflags for TR
            }
            else
            {
                for (int i = halfList; i < fullList; i++) // not SWSH, finish the remaining TMs
                    CLB_TM.SetItemChecked(i, mi.TMHM[i]);
            }

            for (int i = 0; i < CLB_TypeTutor.Items.Count; i++)
                CLB_TypeTutor.SetItemChecked(i, mi.TypeTutors[i]);
        }

        if (pkm is IMovesInfo_B2W2 mi2)
        {
            for (int i = 0; i < CLB_SpecialTutor.Items.Count; i++)
                CLB_SpecialTutor.SetItemChecked(i, mi2.SpecialTutors[i]);
        }
    }

    public void SavePersonal()
    {
        var pkm = cPersonal;
        pkm.HP = Util.ToInt32(TB_BaseHP.Text);
        pkm.ATK = Util.ToInt32(TB_BaseATK.Text);
        pkm.DEF = Util.ToInt32(TB_BaseDEF.Text);
        pkm.SPE = Util.ToInt32(TB_BaseSPE.Text);
        pkm.SPA = Util.ToInt32(TB_BaseSPA.Text);
        pkm.SPD = Util.ToInt32(TB_BaseSPD.Text);

        pkm.EV_HP = Util.ToInt32(TB_HPEVs.Text);
        pkm.EV_ATK = Util.ToInt32(TB_ATKEVs.Text);
        pkm.EV_DEF = Util.ToInt32(TB_DEFEVs.Text);
        pkm.EV_SPE = Util.ToInt32(TB_SPEEVs.Text);
        pkm.EV_SPA = Util.ToInt32(TB_SPAEVs.Text);
        pkm.EV_SPD = Util.ToInt32(TB_SPDEVs.Text);

        pkm.CatchRate = Util.ToInt32(TB_CatchRate.Text);
        pkm.EvoStage = Util.ToInt32(TB_Stage.Text);

        pkm.Type1 = (Types)CB_Type1.SelectedIndex;
        pkm.Type2 = (Types)CB_Type2.SelectedIndex;
        pkm.Item1 = CB_HeldItem1.SelectedIndex;
        pkm.Item2 = CB_HeldItem2.SelectedIndex;
        pkm.Item3 = CB_HeldItem3.SelectedIndex;

        pkm.Gender = Util.ToInt32(TB_Gender.Text);

        if (pkm is IPersonalEgg_v1 eggInfo)
            eggInfo.HatchCycles = Convert.ToByte(TB_HatchCycles.Text);

        pkm.BaseFriendship = Util.ToInt32(TB_Friendship.Text);
        pkm.EXPGrowth = (byte)CB_EXPGroup.SelectedIndex;
        pkm.EggGroup1 = CB_EggGroup1.SelectedIndex;
        pkm.EggGroup2 = CB_EggGroup2.SelectedIndex;
        pkm.Ability1 = CB_Ability1.SelectedIndex;
        pkm.Ability2 = CB_Ability2.SelectedIndex;
        pkm.AbilityH = CB_Ability3.SelectedIndex;

        pkm.FormSprite = Convert.ToUInt16(TB_FormSprite.Text);
        pkm.FormCount = Convert.ToByte(TB_FormCount.Text);
        pkm.Color = (byte)(CB_Color.SelectedIndex) | (Util.ToInt32(TB_RawColor.Text) & 0xF0);
        pkm.BaseEXP = Convert.ToUInt16(TB_BaseExp.Text);

        if (decimal.TryParse(TB_Height.Text, out decimal h))
            pkm.Height = (int)(h * 100);

        if (decimal.TryParse(TB_Weight.Text, out decimal w))
            pkm.Weight = (int)(w * 10);

        if (pkm is IPersonalInfoSM sm)
        {
            pkm.EscapeRate = Util.ToInt32(TB_CallRate.Text);
            sm.SpecialZ_Item = CB_ZItem.SelectedIndex;
            sm.SpecialZ_BaseMove = CB_ZBaseMove.SelectedIndex;
            sm.SpecialZ_ZMove = CB_ZMove.SelectedIndex;
            sm.IsRegionalForm = CHK_Variant.Checked;
        }
        if (pkm is IPersonalInfoGG gg)
        {
            gg.GoSpecies = Convert.ToUInt16(MT_GoID.Text);
        }
        if (pkm is IPersonalInfoSWSH swsh)
        {
            swsh.DexIndexRegional = Convert.ToUInt16(TB_RegionalDex.Text);
            swsh.ArmorDexIndex = Convert.ToUInt16(TB_ArmorDex.Text);
            swsh.CrownDexIndex = Convert.ToUInt16(TB_CrownDex.Text);
            swsh.IsPresentInGame = CHK_IsPresentInGame.Checked;
            swsh.IsRegionalForm = CHK_Variant.Checked;
            swsh.CanNotDynamax = CHK_CanNotDynamax.Checked;
        }

        if (pkm is IMovesInfo_v1 mi)
        {
            int halfList = CLB_TM.Items.Count / 2;
            int fullList = CLB_TM.Items.Count;

            for (int i = 0; i < halfList; i++)
                mi.TMHM[i] = CLB_TM.GetItemChecked(i);

            if (pkm is IMovesInfo_SWSH mitr)
            {
                for (int i = 0; i < halfList; i++)
                    mitr.TR[i] = CLB_TM.GetItemChecked(i + halfList);
            }
            else
            {
                for (int i = halfList; i < fullList; i++)
                    mi.TMHM[i] = CLB_TM.GetItemChecked(i);
            }

            for (int i = 0; i < CLB_TypeTutor.Items.Count; i++)
                mi.TypeTutors[i] = CLB_TypeTutor.GetItemChecked(i);
        }

        if (pkm is IMovesInfo_B2W2 mi2)
        {
            for (int i = 0; i < CLB_SpecialTutor.Items.Count; i++)
                mi2.SpecialTutors[i] = CLB_SpecialTutor.GetItemChecked(i);
        }
    }

    public void LoadLearnset(Learnset pkm)
    {
        cLearnset = pkm;
        dgv.Rows.Clear();
        if (pkm.Count == 0)
        {
            dgv.CancelEdit();
            return;
        }
        dgv.Rows.Add(pkm.Count);

        // Fill Entries
        for (int i = 0; i < pkm.Count; i++)
        {
            dgv.Rows[i].Cells[0].Value = pkm.Levels[i];
            dgv.Rows[i].Cells[1].Value = movelist[pkm.Moves[i]];
        }

        dgv.CancelEdit();
    }

    public void SaveLearnset()
    {
        dgv.EndEdit();
        var pkm = cLearnset;
        List<int> moves = [];
        List<int> levels = [];
        for (int i = 0; i < dgv.Rows.Count - 1; i++)
        {
            var cells = dgv.Rows[i].Cells;
            int move = Array.IndexOf(movelist, cells[1].Value);
            if (move < 1)
                continue;

            moves.Add((short)move);
            string level = cells[0].Value?.ToString() ?? "0";
            _ = int.TryParse(level, out var lv);
            levels.Add(Math.Min(100, lv));
        }
        pkm.Update([.. moves], [.. levels]);
    }

    public void LoadEvolutions(EvolutionSet s)
    {
        cEvos = s;
        Debug.Assert(EvoRows.Length == s.PossibleEvolutions.Length);
        for (int i = 0; i < EvoRows.Length; i++)
        {
            var row = EvoRows[i];
            row.LoadEvolution(s.PossibleEvolutions[i]);
        }
    }

    public void SaveEvolutions()
    {
        var s = cEvos;
        Debug.Assert(EvoRows.Length == s.PossibleEvolutions.Length);
        foreach (var row in EvoRows)
            row.SaveEvolution();
    }

    public void LoadMegas(MegaEvolutionSet[] m, int spec)
    {
        if (Editor.Mega == null)
            return;
        cMega = m;
        Debug.Assert(Megas.Length == m.Length);
        for (int i = 0; i < Megas.Length; i++)
        {
            var entry = Megas[i];
            entry.LoadEvolution(m[i], spec);
        }
    }

    public void SaveMegas()
    {
        if (Editor.Mega == null)
            return;
        var m = cMega;
        Debug.Assert(Megas.Length == m.Length);
        foreach (var row in Megas)
            row.SaveEvolution();
    }

    private void B_PDumpTable_Click(object sender, EventArgs e)
    {
        if (!ConfirmDump())
            return;

        var arr = Editor.Personal.Table;
        var result = TableUtil.GetNamedTypeTable(arr, entryNames, "Species");
        Clipboard.SetText(result);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_RandPersonal_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Randomize Personal Data", "Randomize personal data for Pokemon entries?"))
            return;

        SaveCurrent();
        var settings = (PersonalRandSettings)PG_Personal.SelectedObject!;
        var rand = new PersonalRandomizer(Editor.Personal, ROM.Info, Editor.Evolve.LoadAll()) { Settings = settings };
        rand.Execute();
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_AmpExperience_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Amplify Base EXP", "Apply the base EXP multiplier to Pokemon entries?"))
            return;

        SaveCurrent();
        decimal rate = NUD_AmpEXP.Value;
        foreach (var p in Editor.Personal.Table)
            p.BaseEXP = (int)Math.Max(0, Math.Min(byte.MaxValue, p.BaseEXP * rate));
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_RandEvo_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Randomize Evolutions", "Randomize evolution data for Pokemon entries?"))
            return;

        SaveCurrent();
        var settings = (SpeciesSettings)PG_Evolution.SelectedObject!;
        if (ROM.Info.GG)
            settings.Gen2 = settings.Gen3 = settings.Gen4 = settings.Gen5 = settings.Gen6 = settings.Gen7 = settings.Gen8 = false;
        var rand = new EvolutionRandomizer(ROM.Info, Editor.Evolve.LoadAll(), Editor.Personal);
        int[] ban = [];

        if (ROM.Info.SWSH)
        {
            var pt = Data.PersonalData;
            ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();
        }

        rand.RandSpec.Initialize(settings, ban);
        rand.Execute();
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_TradeEvo_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Remove Trade Evolutions", "Replace trade evolution requirements for Pokemon entries?"))
            return;

        SaveCurrent();
        var settings = (SpeciesSettings)PG_Evolution.SelectedObject!;
        if (ROM.Info.GG)
            settings.Gen2 = settings.Gen3 = settings.Gen4 = settings.Gen5 = settings.Gen6 = settings.Gen7 = settings.Gen8 = false;
        var rand = new EvolutionRandomizer(ROM.Info, Editor.Evolve.LoadAll(), Editor.Personal);
        rand.RandSpec.Initialize(settings);
        rand.ExecuteTrade();
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_EvolveEveryLevel_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Evolve Every Level", "Apply evolve-every-level evolution data for Pokemon entries?"))
            return;

        SaveCurrent();
        var settings = (SpeciesSettings)PG_Evolution.SelectedObject!;
        if (ROM.Info.GG)
            settings.Gen2 = settings.Gen3 = settings.Gen4 = settings.Gen5 = settings.Gen6 = settings.Gen7 = settings.Gen8 = false;
        var rand = new EvolutionRandomizer(ROM.Info, Editor.Evolve.LoadAll(), Editor.Personal);
        int[] ban = [];

        if (ROM.Info.SWSH)
        {
            var pt = Data.PersonalData;
            ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
                .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
                .Where(z => !z.Present).Select(z => z.Species).ToArray();
        }

        rand.RandSpec.Initialize(settings, ban);
        rand.ExecuteEvolveEveryLevel();
        rand.Execute(); // randomize right after
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_RandLearn_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Randomize Learnsets", "Randomize learnset data for Pokemon entries?"))
            return;

        SaveCurrent();
        var settings = (LearnSettings)PG_Learn.SelectedObject!;
        var moveset = (MovesetRandSettings)PG_Move.SelectedObject!;
        var rand = new LearnsetRandomizer(ROM.Info, Editor.Learn.LoadAll(), Editor.Personal);
        var moves = Data.MoveData.LoadAll();
        int[] banned = Legal.GetBannedMoves(ROM.Info.Game, moves.Length);
        rand.Initialize(moves, settings, moveset, banned);
        rand.Execute();
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_LearnExpand_Click(object sender, EventArgs e)
    {
        var settings = (LearnSettings)PG_Learn.SelectedObject!;
        if (!settings.Expand)
        {
            WinFormsUtil.Error("Expand moves not selected. Please double check settings.",
                "Not expanding learnsets.");
            return;
        }

        if (!ConfirmBulkAction("Expand Learnsets", "Expand learnsets for Pokemon entries?"))
            return;

        SaveCurrent();
        var moveset = (MovesetRandSettings)PG_Move.SelectedObject!;
        var rand = new LearnsetRandomizer(ROM.Info, Editor.Learn.LoadAll(), Editor.Personal);
        rand.Initialize(Data.MoveData.LoadAll(), settings, moveset);
        rand.ExecuteExpandOnly();
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_LearnMetronome_Click(object sender, EventArgs e)
    {
        if (!ConfirmBulkAction("Apply Metronome Learnsets", "Replace learnsets with Metronome for Pokemon entries?"))
            return;

        SaveCurrent();
        var settings = (LearnSettings)PG_Learn.SelectedObject!;
        var moveset = (MovesetRandSettings)PG_Move.SelectedObject!;
        var rand = new LearnsetRandomizer(ROM.Info, Editor.Learn.LoadAll(), Editor.Personal);
        rand.Initialize(Data.MoveData.LoadAll(), settings, moveset);
        rand.ExecuteMetronome();
        LoadIndex(CB_Species.SelectedIndex);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (!ConfirmSave())
            return;

        SaveCurrent();
        Modified = true;
        CloseConfirmed = true;
        Close();
    }

    private void ApplyPokemonEditorTheme()
    {
        WinFormsTheme.Apply(this);
        AddPokemonEditorHeader();
        AddThemedTabStrip(TC_Rand);
        AddThemedTabStrip(tabControl2);

        CHK_IsPresentInGame.Enabled = true;
        CHK_IsPresentInGame.AutoCheck = false;
        CHK_IsPresentInGame.ForeColor = WinFormsTheme.DisabledText;
    }

    private void AddPokemonEditorHeader()
    {
        var nativeTabHeight = Math.Max(1, tabControl1.DisplayRectangle.Top);
        tabControl1.Dock = DockStyle.None;
        tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tabControl1.Location = new Point(0, PokemonHeaderHeight - nativeTabHeight);
        tabControl1.Size = new Size(ClientSize.Width, ClientSize.Height - PokemonHeaderHeight + nativeTabHeight);

        var header = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = WinFormsTheme.WindowBackground,
            Bounds = new Rectangle(0, 0, ClientSize.Width, PokemonHeaderHeight),
        };
        header.Paint += (_, e) =>
        {
            using var border = new Pen(WinFormsTheme.Border);
            e.Graphics.DrawLine(border, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        Controls.Remove(CB_Species);
        Controls.Remove(B_Save);
        header.Controls.Add(CB_Species);
        header.Controls.Add(B_Save);

        var tabButtons = AddThemedTabButtons(header, tabControl1, 3, 3, ThemedTabHeight + 2);
        LayoutPokemonHeader(header, tabButtons);
        header.Resize += (_, _) => LayoutPokemonHeader(header, tabButtons);

        Controls.Add(header);
        header.BringToFront();
    }

    private static void AddThemedTabStrip(TabControl tabControl)
    {
        var parent = tabControl.Parent;
        if (parent == null)
            return;

        const int headerHeight = NestedTabHeaderHeight;
        var nativeTabHeight = Math.Max(1, tabControl.DisplayRectangle.Top);
        var originalBounds = tabControl.Bounds;
        var contentOffset = Math.Max(0, headerHeight - nativeTabHeight);

        if (tabControl.Dock != DockStyle.None)
        {
            tabControl.Dock = DockStyle.None;
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        tabControl.Bounds = new Rectangle(
            originalBounds.Left,
            originalBounds.Top + contentOffset,
            originalBounds.Width,
            Math.Max(1, originalBounds.Height - contentOffset));

        var header = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = WinFormsTheme.WindowBackground,
            Bounds = new Rectangle(originalBounds.Left, originalBounds.Top, originalBounds.Width, headerHeight),
        };
        header.Paint += (_, e) =>
        {
            using var border = new Pen(WinFormsTheme.Border);
            e.Graphics.DrawLine(border, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        _ = AddThemedTabButtons(header, tabControl, 7, 6, NestedTabButtonHeight, 76, 28);

        parent.Controls.Add(header);
        header.BringToFront();
    }

    private static Button[] AddThemedTabButtons(Control parent, TabControl tabControl, int left, int top, int height, int minimumWidth = 58, int horizontalPadding = 18)
    {
        var buttons = new Button[tabControl.TabPages.Count];
        var x = left;
        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            var tabIndex = i;
            var tabPage = tabControl.TabPages[i];
            var textWidth = TextRenderer.MeasureText(tabPage.Text, tabControl.Font).Width;
            var button = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = tabControl.Font,
                Height = height,
                Location = new Point(x, top),
                Margin = Padding.Empty,
                Text = tabPage.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Width = Math.Max(minimumWidth, textWidth + horizontalPadding),
            };
            button.FlatAppearance.BorderSize = 1;
            button.Click += (_, _) => tabControl.SelectedIndex = tabIndex;
            parent.Controls.Add(button);
            buttons[i] = button;
            x += button.Width + 2;
        }

        tabControl.SelectedIndexChanged += (_, _) => UpdateThemedTabButtons(buttons, tabControl.SelectedIndex);
        UpdateThemedTabButtons(buttons, tabControl.SelectedIndex);
        return buttons;
    }

    private void LayoutPokemonHeader(Control header, IReadOnlyList<Button> tabButtons)
    {
        const int padding = 6;
        const int gap = 8;

        B_Save.Size = new Size(92, ThemedTabHeight + 2);
        B_Save.Location = new Point(header.Width - B_Save.Width - padding, 3);

        var speciesLeft = tabButtons.Count == 0 ? padding : tabButtons[^1].Right + gap;
        var speciesRight = B_Save.Left - gap;
        CB_Species.Location = new Point(speciesLeft, 5);
        CB_Species.Width = Math.Max(120, speciesRight - speciesLeft);

        if (CB_Species.Right > speciesRight)
            CB_Species.Width = Math.Max(80, speciesRight - speciesLeft);
    }

    private static void UpdateThemedTabButtons(IReadOnlyList<Button> buttons, int selectedIndex)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            var selected = i == selectedIndex;
            var button = buttons[i];
            button.BackColor = selected ? WinFormsTheme.PanelBackground : WinFormsTheme.WindowBackground;
            button.ForeColor = selected ? WinFormsTheme.Text : WinFormsTheme.MutedText;
            button.FlatAppearance.BorderColor = selected ? WinFormsTheme.SelectionBackground : WinFormsTheme.Border;
            button.FlatAppearance.MouseOverBackColor = selected ? WinFormsTheme.PanelBackground : Color.FromArgb(53, 55, 60);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(65, 68, 74);
        }
    }

    private void PokeDataUI_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (CloseConfirmed || e.CloseReason != CloseReason.UserClosing)
            return;

        if (ConfirmCloseWithoutSaving())
            return;

        e.Cancel = true;
    }

    private bool ConfirmSave()
        => ThemedConfirmationDialog.Show(
            this,
            "Save Pokemon Changes",
            "Save the current Pokemon editor changes?\n\nThis applies personal data, learnsets, evolutions, and enhancement changes to the loaded project. Closing without saving will discard this editor session.",
            "Save");

    private bool ConfirmDump()
        => ThemedConfirmationDialog.Show(
            this,
            "Dump Personal Table",
            "Dump the personal table to the clipboard?\n\nThis replaces your current clipboard contents. It does not save or apply changes to the project.",
            "Dump");

    private bool ConfirmBulkAction(string title, string action)
        => ThemedConfirmationDialog.Show(
            this,
            title,
            $"{action}\n\nThis can change many values at once. Review the results before saving, or close without saving to discard them.",
            "Continue");

    private bool ConfirmCloseWithoutSaving()
        => ThemedConfirmationDialog.Show(
            this,
            "Close Pokemon Editor",
            "Close the Pokemon editor without saving?\n\nAny unsaved personal, learnset, evolution, or enhancement changes will be discarded and the loaded project data will not be updated.",
            "Close");
}
