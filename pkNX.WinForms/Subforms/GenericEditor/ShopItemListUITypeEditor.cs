using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using pkNX.Structures;

namespace pkNX.WinForms;

public sealed class ShopItemListUITypeEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.Modal;

    public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        if (value is not IList<int> items)
            return value;

        using var form = new ShopItemListEditorForm(items, ItemConverter.ItemNames);
        var service = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
        var result = service?.ShowDialog(form) ?? form.ShowDialog();
        if (result != DialogResult.OK)
            return value;

        var edited = form.Items.ToList();
        if (ReplaceItems(items, edited))
            return items;

        return edited;
    }

    private static bool ReplaceItems(IList<int> items, IReadOnlyList<int> edited)
    {
        try
        {
            items.Clear();
            foreach (var item in edited)
                items.Add(item);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

public sealed class ShopItemListEditorForm : Form
{
    private readonly BindingList<ShopItemRow> Rows;
    private readonly DataGridView Grid = new();
    private readonly Button AddButton = new();
    private readonly Button RemoveButton = new();
    private readonly Button UpButton = new();
    private readonly Button DownButton = new();
    private readonly Button OkButton = new();
    private readonly Button CancelEditorButton = new();
    private readonly int DefaultItemID;

    public ShopItemListEditorForm(IEnumerable<int> items, string[] itemNames)
    {
        Text = "Shop Item Editor";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 520);

        var choices = CreateChoices(itemNames, items).ToArray();
        DefaultItemID = choices.FirstOrDefault()?.ID ?? 0;
        Rows = new BindingList<ShopItemRow>(items.Select((item, index) => new ShopItemRow(index, item)).ToList());

        ConfigureGrid(choices);
        ConfigureButtons();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8),
        };
        buttonPanel.Controls.Add(CancelEditorButton);
        buttonPanel.Controls.Add(OkButton);
        buttonPanel.Controls.Add(DownButton);
        buttonPanel.Controls.Add(UpButton);
        buttonPanel.Controls.Add(RemoveButton);
        buttonPanel.Controls.Add(AddButton);

        Controls.Add(Grid);
        Controls.Add(buttonPanel);

        AcceptButton = OkButton;
        CancelButton = CancelEditorButton;
        ResetIndexes();
    }

    public IEnumerable<int> Items => Rows.Select(z => z.ItemID);

    private void ConfigureGrid(ItemChoice[] choices)
    {
        Grid.AllowUserToAddRows = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.AutoGenerateColumns = false;
        Grid.Dock = DockStyle.Fill;
        Grid.MultiSelect = false;
        Grid.RowHeadersVisible = false;
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid.DataSource = Rows;
        Grid.DataError += (_, __) => { };

        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ShopItemRow.Index),
            HeaderText = "#",
            ReadOnly = true,
            Width = 48,
        });

        Grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(ShopItemRow.ItemID),
            HeaderText = "Item",
            DataSource = choices,
            DisplayMember = nameof(ItemChoice.Display),
            ValueMember = nameof(ItemChoice.ID),
            AutoComplete = true,
            FlatStyle = FlatStyle.Flat,
            Width = 520,
        });

        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ShopItemRow.ItemID),
            HeaderText = "ID",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
    }

    private void ConfigureButtons()
    {
        ConfigureButton(AddButton, "Add", (_, __) => AddRow());
        ConfigureButton(RemoveButton, "Remove", (_, __) => RemoveSelectedRow());
        ConfigureButton(UpButton, "Up", (_, __) => MoveSelectedRow(-1));
        ConfigureButton(DownButton, "Down", (_, __) => MoveSelectedRow(1));
        ConfigureButton(OkButton, "OK", (_, __) => CompleteEdit(DialogResult.OK));
        ConfigureButton(CancelEditorButton, "Cancel", (_, __) => CompleteEdit(DialogResult.Cancel));
    }

    private static void ConfigureButton(Button button, string text, EventHandler handler)
    {
        button.Text = text;
        button.Width = 96;
        button.Height = 30;
        button.UseVisualStyleBackColor = true;
        button.Click += handler;
    }

    private static IEnumerable<ItemChoice> CreateChoices(string[] itemNames, IEnumerable<int> currentItems)
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < itemNames.Length; i++)
        {
            if (!seen.Add(i))
                continue;

            var name = itemNames[i];
            yield return new ItemChoice(i, string.IsNullOrWhiteSpace(name) ? $"Item {i}" : $"{name} ({i})");
        }

        foreach (var item in currentItems)
        {
            if (seen.Add(item))
                yield return new ItemChoice(item, item.ToString());
        }

        if (seen.Count == 0)
            yield return new ItemChoice(0, "Item 0");
    }

    private void AddRow()
    {
        var index = Rows.Count;
        Rows.Add(new ShopItemRow(index, DefaultItemID));
        SelectRow(index);
    }

    private void RemoveSelectedRow()
    {
        var index = SelectedRowIndex;
        if (index < 0)
            return;

        Rows.RemoveAt(index);
        ResetIndexes();
        SelectRow(Math.Min(index, Rows.Count - 1));
    }

    private void MoveSelectedRow(int offset)
    {
        var index = SelectedRowIndex;
        var next = index + offset;
        if (index < 0 || next < 0 || next >= Rows.Count)
            return;

        var row = Rows[index];
        Rows.RemoveAt(index);
        Rows.Insert(next, row);
        ResetIndexes();
        SelectRow(next);
    }

    private int SelectedRowIndex => Grid.CurrentCell?.RowIndex ?? -1;

    private void SelectRow(int index)
    {
        if (index < 0 || index >= Grid.Rows.Count)
            return;

        Grid.CurrentCell = Grid.Rows[index].Cells[1];
        Grid.Rows[index].Selected = true;
    }

    private void ResetIndexes()
    {
        for (int i = 0; i < Rows.Count; i++)
            Rows[i].Index = i;

        Grid.Refresh();
    }

    private void CompleteEdit(DialogResult result)
    {
        Grid.EndEdit();
        DialogResult = result;
        Close();
    }

    private sealed class ShopItemRow(int index, int itemID)
    {
        public int Index { get; set; } = index;
        public int ItemID { get; set; } = itemID;
    }

    private sealed class ItemChoice(int id, string display)
    {
        public int ID { get; } = id;
        public string Display { get; } = display;
    }
}
