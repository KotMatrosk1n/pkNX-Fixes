using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pkNX.Game;
using pkNX.Randomization;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public sealed partial class SSWE : Form
{
    private readonly EncounterArchive Symbols;
    private readonly EncounterArchive Hidden;
    private readonly GameManagerSWSH ROM;
    private ulong entry;
    private bool sizingToContent;
    private bool CloseConfirmed;

    private readonly EncounterList8[] SL;

    private sealed class ThemedLeftTabControl : TabControl
    {
        private const int WmPaint = 0x000F;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WmPaint)
                PaintUnusedTabGutter();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void PaintUnusedTabGutter()
        {
            if (Alignment != TabAlignment.Left || TabPages.Count == 0 || IsDisposed)
                return;

            var lastTab = GetTabRect(TabPages.Count - 1);
            var gutterTop = Math.Max(0, lastTab.Bottom);
            if (gutterTop >= Height)
                return;

            var gutterWidth = Math.Max(DisplayRectangle.Left, lastTab.Right);
            using var graphics = CreateGraphics();
            using var brush = new SolidBrush(WinFormsTheme.AlternateRowBackground);
            graphics.FillRectangle(brush, new Rectangle(0, gutterTop, gutterWidth, Height - gutterTop));
        }
    }

    public SSWE(GameManagerSWSH rom, EncounterArchive sym, EncounterArchive hid)
    {
        InitializeComponent();
        SearchableComboBoxBehavior.Register(this, CB_Location);
        Symbols = sym;
        Hidden = hid;
        ROM = rom;

        var spec = rom.GetStrings(TextName.SpeciesNames);
        var species = (string[])spec.Clone();
        species[0] = "";
        EncounterList8.SpeciesNames = species;

        SL = [SL_0, SL_1, SL_2, SL_3, SL_4, SL_5, SL_6, SL_7, SL_8, SL_9, SL_10];
        foreach (var z in SL)
            z.Initialize();

        PG_Species.SelectedObject = EditUtil.Settings.Species;
        WinFormsTheme.Apply(this);
        splitContainer1.Panel2.BackColor = WinFormsTheme.AlternateRowBackground;
        TC_Types.BackColor = WinFormsTheme.AlternateRowBackground;
        TC_Types.DrawItem -= TC_Tables_DrawItem;
        TC_Types.DrawItem += TC_Tables_DrawItem;
        TC_Types.SelectedIndexChanged += (_, _) => FitWindowToActiveTab();
        EnsureHeaderHeight();

        LoadLocations();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitWindowToActiveTab();
    }

    private class LocationHash(ulong hash, string loc)
    {
        public ulong Hash { get; } = hash;
        public string LocationName { get; } = loc;
    }

    public bool Modified { get; private set; }

    public void LoadLocations()
    {
        static string GetLocationName(ulong id) => SWSHInfo.Zones.TryGetValue(id, out var zoneName) ? zoneName : id.ToString("X16");
        var sl = Symbols.EncounterTables
            .Select(area => new LocationHash(area.ZoneID, GetLocationName(area.ZoneID) + " [S]"));
        var hl = Hidden.EncounterTables
            .Select(area => new LocationHash(area.ZoneID, GetLocationName(area.ZoneID) + " [H]"));
        var locs = sl.Concat(hl)
            .OrderBy(z => z.LocationName)
            .ToArray();

        CB_Location.ValueMember = nameof(LocationHash.Hash);
        CB_Location.DisplayMember = nameof(LocationHash.LocationName);
        CB_Location.DataSource = new BindingSource(locs, string.Empty);

        CB_Location.SelectedIndex = 0;
    }

    private void CB_Location_SelectedIndexChanged(object sender, EventArgs e)
    {
        SaveEntry(entry);
        var item = (LocationHash)CB_Location.SelectedItem!;
        entry = item.Hash;
        Debug.WriteLine($"Loading area data for [0x{entry:X16}] {item.LocationName}");
        L_Hash.Text = entry.ToString("X16");
        LoadEntry(entry);
    }

    private void LoadEntry(ulong zone)
    {
        Load(SL, Symbols, "Symbols");
        Load(SL, Hidden, "Hidden");
        FitWindowToActiveTab();

        void Load(EncounterList8[] arr, EncounterArchive arc, string name)
        {
            var table = arc.EncounterTables.FirstOrDefault(z => z.ZoneID == zone);
            if (table == null)
                return;

            L_Type.Text = name;

            var subs = table.SubTables;
            for (int i = 0; i < subs.Count; i++)
            {
                var t = subs[i];
                arr[i].NUD_Max.Value = t.LevelMax;
                arr[i].NUD_Min.Value = t.LevelMin;
                arr[i].LoadSlots(subs[i].Slots);
                arr[i].Visible = true;
            }

            // some tables don't have tree/fish
            for (int i = subs.Count; i < arr.Length; i++)
                arr[i].Visible = false;
        }
    }

    private void FitWindowToActiveTab()
    {
        if (sizingToContent || WindowState != FormWindowState.Normal)
            return;

        var tabContent = GetPreferredActiveTabContentSize();
        var headerHeight = EnsureHeaderHeight();
        var tabChromeWidth = TC_Types.DisplayRectangle.Left + Math.Max(0, TC_Types.Width - TC_Types.DisplayRectangle.Right);
        var tabChromeHeight = TC_Types.DisplayRectangle.Top + Math.Max(0, TC_Types.Height - TC_Types.DisplayRectangle.Bottom);
        var desiredWidth = Math.Max(GetPreferredHeaderWidth(), tabChromeWidth + tabContent.Width);
        var desiredHeight = headerHeight + splitContainer1.SplitterWidth + tabChromeHeight + tabContent.Height;
        var desiredClientSize = new Size(desiredWidth, desiredHeight);
        var desiredWindowSize = SizeFromClientSize(desiredClientSize);

        sizingToContent = true;
        try
        {
            if (MinimumSize != desiredWindowSize)
                MinimumSize = desiredWindowSize;
            if (ClientSize != desiredClientSize)
                ClientSize = desiredClientSize;
        }
        finally
        {
            sizingToContent = false;
        }
    }

    private int EnsureHeaderHeight()
    {
        var headerHeight = new[] { CB_Location.Bottom, L_Hash.Bottom, B_Save.Bottom }.Max() + 10;
        if (splitContainer1.SplitterDistance != headerHeight)
            splitContainer1.SplitterDistance = headerHeight;
        return headerHeight;
    }

    private Size GetPreferredActiveTabContentSize()
    {
        if (TC_Types.SelectedTab?.Controls.OfType<EncounterList8>().FirstOrDefault() is { } encounterList)
            return encounterList.GetPreferredEditorSize();

        var right = new[] { B_RandAll.Right, CHK_FillEmpty.Right, CHK_Level.Right, NUD_LevelBoost.Right, PG_Species.Right }.Max() + 16;
        var bottom = PG_Species.Bottom + 16;
        return new Size(right, bottom);
    }

    private int GetPreferredHeaderWidth()
    {
        const int locationWidth = 300;
        const int gap = 24;
        const int rightPadding = 12;
        var hashWidth = TextRenderer.MeasureText(L_Hash.Text, L_Hash.Font).Width;
        var width = CB_Location.Left + locationWidth + gap + hashWidth + gap + B_Save.Width + rightPadding;
        return width;
    }

    private void SaveEntry(ulong zone)
    {
        if (zone == 0)
            return;

        Save(SL, Symbols);
        Save(SL, Hidden);
        void Save(EncounterList8[] arr, EncounterArchive arc)
        {
            var table = arc.EncounterTables.FirstOrDefault(z => z.ZoneID == zone);
            if (table == null)
                return;

            var subs = table.SubTables;
            for (int i = 0; i < subs.Count; i++)
            {
                var t = subs[i];
                t.LevelMax = (byte)arr[i].NUD_Max.Value;
                t.LevelMin = (byte)arr[i].NUD_Min.Value;
                arr[i].SaveCurrent();
            }
        }
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (!ConfirmSave())
            return;

        SaveEntry(entry);
        Modified = true;
        CloseConfirmed = true;
        Close();
    }

    private void B_RandAll_Click(object sender, EventArgs e)
    {
        if (!ConfirmRandomize())
            return;

        SaveEntry(entry);
        var settings = (SpeciesSettings)PG_Species.SelectedObject!;
        var rand = new SpeciesRandomizer(ROM.Info, ROM.Data.PersonalData);

        var pt = ROM.Data.PersonalData;
        var ban = pt.Table.Take(ROM.Info.MaxSpeciesID + 1)
            .Select((z, i) => new { Species = i, Present = ((IPersonalInfoSWSH)z).IsPresentInGame })
            .Where(z => !z.Present).Select(z => z.Species).ToArray();

        rand.Initialize(settings, ban);
        RandomizeWild(rand, CHK_FillEmpty.Checked, CHK_Level.Checked);
        LoadEntry(entry);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void RandomizeWild(SpeciesRandomizer rand, bool fill, bool boost)
    {
        var pt = ROM.Data.PersonalData;
        var fr = new FormRandomizer(pt);
        var settings = (SpeciesSettings)PG_Species.SelectedObject!;
        foreach (var area in Symbols.EncounterTables.Concat(Hidden.EncounterTables))
        {
            foreach (var sub in area.SubTables)
            {
                if (boost)
                {
                    sub.LevelMin = (byte)Legal.GetModifiedLevel(sub.LevelMin, (double)NUD_LevelBoost.Value);
                    sub.LevelMax = (byte)Legal.GetModifiedLevel(sub.LevelMax, (double)NUD_LevelBoost.Value);
                }
                ApplyRand(sub.Slots);
            }
        }

        void ApplyRand(IList<EncounterSlot> slots)
        {
            if (slots[0].Species == 0)
                return;

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Species == 0)
                {
                    if (!fill)
                        continue;
                    s.Species = slots.FirstOrDefault(z => z.Species != 0)?.Species ?? rand.GetRandomSpecies();
                    s.Form = 0; // ensure it's not junk
                }

                s.Species = rand.GetRandomSpecies(s.Species);
                s.Form = (byte)fr.GetRandomForm(s.Species, false, settings.AllowRandomFusions, ROM.Info.Generation, ROM.Data.PersonalData.Table);
                if (fill)
                    s.Probability = RandomScaledRates[slots.Count][i];
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!CloseConfirmed && e.CloseReason == CloseReason.UserClosing && !ConfirmCloseWithoutSaving())
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private bool ConfirmSave()
        => ThemedConfirmationDialog.Show(
            this,
            "Save Wild Encounters",
            "Save the current wild encounter changes?\n\nThis applies the edited encounter data to the loaded project. Closing without saving will discard this editor session.",
            "Save");

    private bool ConfirmRandomize()
        => ThemedConfirmationDialog.Show(
            this,
            "Randomize Wild Encounters",
            "Randomize wild encounter species with the current settings?\n\nThis can change many encounter slots at once. Review the results before saving, or close without saving to discard them.",
            "Randomize");

    private bool ConfirmCloseWithoutSaving()
        => ThemedConfirmationDialog.Show(
            this,
            "Close Wild Editor",
            "Close this editor without saving?\n\nAny changes made in this editor session will be discarded and the loaded project data will not be updated.",
            "Close");

    public static readonly Dictionary<int, byte[]> RandomScaledRates = new()
    {
        [01] = [100],
        [04] = [60, 30, 7, 3],
        [05] = [40, 30, 18, 10, 2],
        [10] = [20, 15, 15, 10, 10, 10, 10, 5, 4, 1],
    };

    private void TC_Tables_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tc)
            return;

        var tabPage = tc.TabPages[e.Index];
        var selected = e.Index == tc.SelectedIndex;
        var tabBounds = tc.GetTabRect(e.Index);
        var backgroundColor = selected ? WinFormsTheme.SelectionBackground : WinFormsTheme.AlternateRowBackground;
        var textColor = selected ? WinFormsTheme.SelectionText : WinFormsTheme.Text;

        using (var background = new SolidBrush(backgroundColor))
            e.Graphics.FillRectangle(background, e.Bounds);

        TextRenderer.DrawText(
            e.Graphics,
            tabPage.Text,
            tc.Font,
            tabBounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
