using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers;
using SWSH = pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public sealed class PlacementTableView : UserControl
{
    private const int SummaryHeight = 34;
    private const int HeaderHeight = 30;
    private const int RowHeight = 40;
    private const int IndexColumnWidth = 58;
    private const int ZoneColumnWidth = 190;
    private const int SpawnColumnMinimumWidth = 260;
    private const int ItemColumnMinimumWidth = 220;
    private const int ActorColumnMinimumWidth = 260;
    private const int EditColumnWidth = 104;
    private const int TableMinimumWidth = IndexColumnWidth + ZoneColumnWidth + SpawnColumnMinimumWidth + ItemColumnMinimumWidth + ActorColumnMinimumWidth + EditColumnWidth;

    private readonly Label AreaSummary = new();
    private readonly Panel ScrollPanel = new();
    private readonly TableLayoutPanel Table = new();
    private SWSH.PlacementZoneArchive? Archive;
    public IReadOnlyDictionary<ulong, string> ZoneNames { get; set; } = new Dictionary<ulong, string>();

    public PlacementTableView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8, 10, 8, 8);

        AreaSummary.Dock = DockStyle.Top;
        AreaSummary.Height = SummaryHeight;
        AreaSummary.AutoEllipsis = true;
        AreaSummary.BackColor = WinFormsTheme.PanelBackground;
        AreaSummary.BorderStyle = BorderStyle.FixedSingle;
        AreaSummary.ForeColor = WinFormsTheme.Text;
        AreaSummary.Margin = Padding.Empty;
        AreaSummary.Padding = new Padding(8, 0, 8, 0);
        AreaSummary.TextAlign = ContentAlignment.MiddleLeft;
        AreaSummary.UseMnemonic = false;

        ScrollPanel.Dock = DockStyle.Fill;
        ScrollPanel.AutoScroll = true;
        ScrollPanel.Controls.Add(Table);
        ScrollPanel.Resize += (_, _) => ResizeTable();

        Table.AutoSize = false;
        Table.ColumnCount = 6;
        Table.Location = Point.Empty;
        Table.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
        Table.Margin = Padding.Empty;
        Table.Padding = Padding.Empty;
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, IndexColumnWidth));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ZoneColumnWidth));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, EditColumnWidth));

        Controls.Add(ScrollPanel);
        Controls.Add(AreaSummary);
        WinFormsTheme.Apply(this);
    }

    public static bool Supports(object value) => value is SWSH.PlacementZoneArchive;

    public void LoadArchive(object value)
    {
        if (value is not SWSH.PlacementZoneArchive archive)
            return;

        Archive = archive;
        AreaSummary.Text = GetArchiveSummary(archive);
        RebuildTable();
    }

    private void RebuildTable()
    {
        Table.SuspendLayout();
        Table.Controls.Clear();
        Table.RowStyles.Clear();

        var zones = Archive?.Table;
        var rowCount = zones?.Count ?? 0;
        Table.RowCount = Math.Max(rowCount, 1) + 1;
        AddHeaderRow();

        if (rowCount == 0)
            AddEmptyRow();
        else
        {
            for (int i = 0; i < rowCount; i++)
                AddZoneRow(zones![i], i + 1);
        }

        ResizeTable();
        Table.ResumeLayout();
    }

    private void AddHeaderRow()
    {
        Table.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
        Table.Controls.Add(CreateHeaderCell("#", ContentAlignment.MiddleCenter), 0, 0);
        Table.Controls.Add(CreateHeaderCell("Zone"), 1, 0);
        Table.Controls.Add(CreateHeaderCell("Spawns / Objects"), 2, 0);
        Table.Controls.Add(CreateHeaderCell("Items"), 3, 0);
        Table.Controls.Add(CreateHeaderCell("NPC / Travel"), 4, 0);
        Table.Controls.Add(CreateHeaderCell(string.Empty), 5, 0);
    }

    private void AddZoneRow(SWSH.PlacementZone zone, int rowIndex)
    {
        Table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

        Table.Controls.Add(CreateCell((rowIndex - 1).ToString(), ContentAlignment.MiddleCenter), 0, rowIndex);
        Table.Controls.Add(CreateCell(GetZoneName(zone)), 1, rowIndex);
        Table.Controls.Add(CreateCell(GetSpawnSummary(zone)), 2, rowIndex);
        Table.Controls.Add(CreateCell(GetItemSummary(zone)), 3, rowIndex);
        Table.Controls.Add(CreateCell(GetActorSummary(zone)), 4, rowIndex);
        Table.Controls.Add(CreateEditCell(rowIndex - 1), 5, rowIndex);
    }

    private void AddEmptyRow()
    {
        Table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
        var cell = CreateCell("No placement zones found");
        Table.Controls.Add(cell, 0, 1);
        Table.SetColumnSpan(cell, 6);
    }

    private void ResizeTable()
    {
        var zones = Math.Max(Archive?.Table.Count ?? 0, 1);
        var height = HeaderHeight + (zones * RowHeight);
        var width = ScrollPanel.ClientSize.Width;
        if (height > ScrollPanel.ClientSize.Height)
            width -= SystemInformation.VerticalScrollBarWidth;

        Table.Width = Math.Max(TableMinimumWidth, width);
        Table.Height = Math.Max(HeaderHeight + RowHeight, height);
        ScrollPanel.AutoScrollMinSize = Table.Size;
    }

    private static Label CreateHeaderCell(string text, ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        return new Label
        {
            AutoEllipsis = true,
            BackColor = WinFormsTheme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font(Control.DefaultFont, FontStyle.Bold),
            ForeColor = WinFormsTheme.Text,
            Margin = new Padding(0, 0, 0, 1),
            Padding = new Padding(6, 0, 6, 0),
            Text = text,
            TextAlign = alignment,
            UseMnemonic = false,
        };
    }

    private static Label CreateCell(string text, ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        return new Label
        {
            AutoEllipsis = true,
            BackColor = WinFormsTheme.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            ForeColor = WinFormsTheme.Text,
            Margin = new Padding(0, 0, 0, 1),
            Padding = new Padding(6, 0, 6, 0),
            Text = text,
            TextAlign = alignment,
            UseMnemonic = false,
        };
    }

    private Control CreateEditCell(int rowIndex)
    {
        var panel = new Panel
        {
            BackColor = WinFormsTheme.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 1),
            Padding = new Padding(5, 4, 5, 4),
        };
        panel.Controls.Add(CreateEditButton(rowIndex));
        return panel;
    }

    private Button CreateEditButton(int rowIndex)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Text = "Edit...",
            UseVisualStyleBackColor = false,
        };
        button.Click += (_, _) => EditZone(rowIndex);
        WinFormsTheme.Apply(button);
        return button;
    }

    private void EditZone(int rowIndex)
    {
        if (Archive == null || (uint)rowIndex >= (uint)Archive.Table.Count)
            return;

        var original = Archive.Table[rowIndex];
        var clone = FlatBufferConverter.DeserializeFrom<SWSH.PlacementZone>(FlatBufferConverter.SerializeFrom(original));
        using var form = new PlacementZoneEditorForm(clone, $"Placement Zone {rowIndex}: {GetZoneName(original)}");
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        Archive.Table[rowIndex] = clone;
        RebuildTable();
    }

    private static string GetArchiveSummary(SWSH.PlacementZoneArchive archive)
    {
        var description = string.IsNullOrWhiteSpace(archive.Description) ? "No description" : archive.Description;
        return $"{description} | Zones: {archive.Table.Count} | Hash: {archive.Hash:X16}";
    }

    private string GetZoneName(SWSH.PlacementZone zone)
    {
        var zoneId = zone.Meta.ZoneID;
        var objectHash = zone.Meta.Field00.HashObjectName;
        if (ZoneNames.TryGetValue(zoneId, out var zoneName))
            return zoneName;

        return $"{zoneId:X16} / {objectHash:X16}";
    }

    private static string GetSpawnSummary(SWSH.PlacementZone zone)
    {
        return string.Join(", ", new[]
        {
            Count("Critters", zone.Critters.Count),
            Count("Symbols", zone.Symbols.Count),
            Count("Static", zone.StaticObjects.Count),
            Count("Unit", zone.UnitObjects.Count),
        }.Where(z => z.Length != 0));
    }

    private static string GetItemSummary(SWSH.PlacementZone zone)
    {
        return string.Join(", ", new[]
        {
            Count("Field", zone.FieldItems.Count),
            Count("Hidden", zone.HiddenItems.Count),
            Count("Berry", zone.BerryTrees.Count),
            Count("Nests", zone.Nests.Count),
        }.Where(z => z.Length != 0));
    }

    private static string GetActorSummary(SWSH.PlacementZone zone)
    {
        return string.Join(", ", new[]
        {
            Count("Trainers", zone.Trainers.Count),
            Count("NPC1", zone.NPCType1.Count),
            Count("NPC2", zone.NPCType2.Count),
            Count("Warps", zone.Warps.Count),
            Count("Fly", zone.FlyTo.Count),
        }.Where(z => z.Length != 0));
    }

    private static string Count(string label, int count) => count == 0 ? string.Empty : $"{label}: {count}";

    private sealed class PlacementZoneEditorForm : Form
    {
        private readonly PropertyGrid Grid = new();
        private readonly Button OkButton = new();
        private readonly Button CancelEditButton = new();
        private readonly TableLayoutPanel RootLayout = new();
        private readonly FlowLayoutPanel Footer = new();

        public PlacementZoneEditorForm(SWSH.PlacementZone zone, string title)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 560);
            ClientSize = new Size(900, 650);

            RootLayout.ColumnCount = 1;
            RootLayout.RowCount = 2;
            RootLayout.Dock = DockStyle.Fill;
            RootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            RootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            Grid.Dock = DockStyle.Fill;
            TypeRegistrationHelper.RegisterIListConvertersRecursively(typeof(SWSH.PlacementZone));
            TypeRegistrationHelper.RegisterIListConvertersRecursively(zone.GetType());
            Grid.SelectedObject = zone;

            Footer.Dock = DockStyle.Fill;
            Footer.FlowDirection = FlowDirection.RightToLeft;
            Footer.Padding = new Padding(8);
            Footer.WrapContents = false;

            OkButton.Text = "OK";
            OkButton.Width = 110;
            OkButton.Height = 34;
            OkButton.DialogResult = DialogResult.OK;

            CancelEditButton.Text = "Cancel";
            CancelEditButton.Width = 110;
            CancelEditButton.Height = 34;
            CancelEditButton.DialogResult = DialogResult.Cancel;

            Footer.Controls.Add(CancelEditButton);
            Footer.Controls.Add(OkButton);
            RootLayout.Controls.Add(Grid, 0, 0);
            RootLayout.Controls.Add(Footer, 0, 1);
            Controls.Add(RootLayout);

            AcceptButton = OkButton;
            CancelButton = CancelEditButton;
            WinFormsTheme.Apply(this);
        }
    }
}
