using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers.LGPE;
using SWSH = pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public sealed class ShopTableView : UserControl
{
    private const int HeaderHeight = 30;
    private const int RowHeight = 40;
    private const int InventoryColumnWidth = 176;
    private const int CountColumnWidth = 72;
    private const int SummaryMinimumWidth = 360;
    private const int EditColumnWidth = 104;
    private const int TableMinimumWidth = InventoryColumnWidth + CountColumnWidth + SummaryMinimumWidth + EditColumnWidth;

    private readonly Panel ScrollPanel = new();
    private readonly TableLayoutPanel Table = new();
    private readonly List<ShopInventoryTableRow> Rows = [];

    public ShopTableView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8, 10, 8, 8);

        ScrollPanel.Dock = DockStyle.Fill;
        ScrollPanel.AutoScroll = true;
        ScrollPanel.Controls.Add(Table);
        ScrollPanel.Resize += (_, _) => ResizeTable();

        Table.AutoSize = false;
        Table.ColumnCount = 4;
        Table.Location = Point.Empty;
        Table.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
        Table.Margin = Padding.Empty;
        Table.Padding = Padding.Empty;
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, InventoryColumnWidth));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, CountColumnWidth));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, EditColumnWidth));

        Controls.Add(ScrollPanel);
        WinFormsTheme.Apply(this);
    }

    public static bool Supports(object value) => value switch
    {
        SingleShop or MultiShop or SWSH.SingleShop or SWSH.MultiShop => true,
        _ => false,
    };

    public void LoadShop(object shop)
    {
        Rows.Clear();
        Rows.AddRange(CreateRows(shop));
        RebuildTable();
    }

    private void RebuildTable()
    {
        Table.SuspendLayout();
        Table.Controls.Clear();
        Table.RowStyles.Clear();
        Table.RowCount = Math.Max(Rows.Count, 1) + 1;
        AddHeaderRow();

        if (Rows.Count == 0)
            AddEmptyRow();
        else
        {
            for (int i = 0; i < Rows.Count; i++)
                AddInventoryRow(Rows[i], i + 1);
        }

        ResizeTable();
        Table.ResumeLayout();
    }

    private void AddHeaderRow()
    {
        Table.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
        Table.Controls.Add(CreateHeaderCell("Inventory"), 0, 0);
        Table.Controls.Add(CreateHeaderCell("Items", ContentAlignment.MiddleCenter), 1, 0);
        Table.Controls.Add(CreateHeaderCell("Contents"), 2, 0);
        Table.Controls.Add(CreateHeaderCell(string.Empty), 3, 0);
    }

    private void AddInventoryRow(ShopInventoryTableRow row, int rowIndex)
    {
        Table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

        Table.Controls.Add(CreateCell(row.Inventory), 0, rowIndex);
        Table.Controls.Add(CreateCell(row.Count.ToString(), ContentAlignment.MiddleCenter), 1, rowIndex);
        Table.Controls.Add(CreateCell(row.Summary), 2, rowIndex);
        Table.Controls.Add(CreateEditCell(rowIndex - 1), 3, rowIndex);
    }

    private void AddEmptyRow()
    {
        Table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
        var cell = CreateCell("No inventories found");
        Table.Controls.Add(cell, 0, 1);
        Table.SetColumnSpan(cell, 4);
    }

    private void ResizeTable()
    {
        var dataRows = Math.Max(Rows.Count, 1);
        var height = HeaderHeight + (dataRows * RowHeight);
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
        button.Click += (_, _) => EditRow(rowIndex);
        WinFormsTheme.Apply(button);
        return button;
    }

    private void EditRow(int rowIndex)
    {
        if ((uint)rowIndex >= (uint)Rows.Count)
            return;

        var row = Rows[rowIndex];
        using var form = new ShopItemListEditorForm(row.Items, ItemConverter.ItemNames);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        row.ReplaceItems(form.Items.ToList());
        RebuildTable();
    }

    private static IEnumerable<ShopInventoryTableRow> CreateRows(object shop)
    {
        return shop switch
        {
            SingleShop single => [CreateRow("Inventory", single.Inventories)],
            MultiShop multi => multi.Inventories.Select((inventory, index) => CreateRow(GetInventoryLabel(index, multi.Inventories.Count), inventory)),
            SWSH.SingleShop single => [CreateRow("Inventory", single.Inventories)],
            SWSH.MultiShop multi => multi.Inventories.Select((inventory, index) => CreateRow(GetInventoryLabel(index, multi.Inventories.Count), inventory)),
            _ => [],
        };
    }

    private static ShopInventoryTableRow CreateRow(string label, Inventory inventory)
        => new(label, () => inventory.Items, value => inventory.Items = value);

    private static ShopInventoryTableRow CreateRow(string label, SWSH.Inventory inventory)
        => new(label, () => inventory.Items, value => inventory.Items = value);

    private static string GetInventoryLabel(int index, int count)
    {
        if (count == 9)
            return index == 1 ? "1 Badge" : $"{index} Badges";

        return $"Inventory {index}";
    }

    private sealed class ShopInventoryTableRow(
        string inventory,
        Func<IList<int>> getItems,
        Action<IList<int>> setItems)
    {
        public string Inventory { get; } = inventory;
        public IList<int> Items => getItems();
        public int Count => Items.Count;
        public string Summary => ShopItemNameFormatter.GetSummary(Items, 12);

        public void ReplaceItems(IList<int> items) => setItems(items);
    }
}
