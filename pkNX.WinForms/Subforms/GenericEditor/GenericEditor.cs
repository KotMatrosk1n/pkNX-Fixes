using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pkNX.Game;
using pkNX.Structures;

namespace pkNX.WinForms;

public sealed partial class GenericEditor<T> : Form where T : class
{
    private string[] Names;
    private DataCache<T> Cache;
    private readonly ShopTableView ShopTable = new();
    private readonly Size OriginalMinimumSize;
    public bool Modified { get; set; }

    public GenericEditor(DataCache<T> Cache, string[] names, string title, Action? randomizeCallback = null, Action? addEntryCallback = null, bool canSave = true)
        : this(_ => Cache, (_, i) => names[i], title, _ => randomizeCallback?.Invoke(), addEntryCallback, canSave)
    { }

    public GenericEditor(Func<GenericEditor<T>, DataCache<T>> loadCache, Func<T, int, string> nameSelector, string title, Action<IEnumerable<T>>? randomizeCallback = null, Action? addEntryCallback = null, bool canSave = true)
    {
        InitializeComponent();
        OriginalMinimumSize = MinimumSize;

        ContentPanel.Controls.Add(ShopTable);
        ShopTable.Visible = false;
        ShopTable.BringToFront();

        TypeRegistrationHelper.RegisterIListConvertersRecursively(typeof(T));
        Text = title;
        WinFormsTheme.Apply(this);

        Cache = loadCache(this);
        Names = Cache.LoadAll().Select(nameSelector).ToArray();

        CB_EntryName.Items.AddRange(Names);
        CB_EntryName.SelectedIndex = 0;
        UpdateShopEditorMinimumSize();

        if (!canSave)
            B_Save.Enabled = false;

        if (randomizeCallback != null)
        {
            B_Rand.Visible = true;
            B_Rand.Click += (_, __) =>
            {
                randomizeCallback(Cache.LoadAll());
                LoadIndex(0);
                System.Media.SystemSounds.Asterisk.Play();
            };
        }

        if (addEntryCallback != null)
        {
            B_AddEntry.Visible = true;
            B_AddEntry.Click += (_, __) =>
            {
                addEntryCallback();
                Modified = true;

                // Reload editor
                Cache = loadCache(this);
                Names = Cache.LoadAll().Select(nameSelector).ToArray();
                CB_EntryName.Items.Clear();
                CB_EntryName.Items.AddRange(Names);
                UpdateShopEditorMinimumSize();

                System.Media.SystemSounds.Asterisk.Play();
            };
        }
    }

    private void CB_EntryName_SelectedIndexChanged(object sender, EventArgs e)
    {
        var index = CB_EntryName.SelectedIndex;
        LoadIndex(index);
    }

    private void LoadIndex(int index)
    {
        if ((uint)index >= (uint)Cache.Length)
            return;

        var value = Cache[index];
        if (ShopTableView.Supports(value))
        {
            Grid.SelectedObject = null;
            Grid.Visible = false;
            ShopTable.Visible = true;
            ShopTable.LoadShop(value);
            ShopTable.BringToFront();
            return;
        }

        ShopTable.Visible = false;
        Grid.Visible = true;
        var displayObject = ShopPropertyGridObjectFactory.Create(value);
        TypeRegistrationHelper.RegisterIListConvertersRecursively(displayObject.GetType());
        Grid.SelectedObject = displayObject;
        Grid.BringToFront();
    }

    private void UpdateShopEditorMinimumSize()
    {
        var isShopEditor = Cache.Length > 0 && ShopTableView.Supports(Cache[0]);
        MinimumSize = isShopEditor
            ? new Size(Math.Max(OriginalMinimumSize.Width, 900), OriginalMinimumSize.Height)
            : OriginalMinimumSize;
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (Grid.Visible)
            LoadIndex(0);

        Modified = true;
        Close();
    }

    private void B_Dump_Click(object sender, EventArgs e)
    {
        var arr = Cache.LoadAll();
        var result = TableUtil.GetNamedTypeTable(arr, Names, Text.Split(' ')[0]);
        Clipboard.SetText(result);
        System.Media.SystemSounds.Asterisk.Play();
    }
}
