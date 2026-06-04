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
    private bool CloseConfirmed;
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
        FormClosing += GenericEditor_FormClosing;

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
                if (!ConfirmRandomize())
                    return;

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
        if (!ConfirmSave())
            return;

        if (Grid.Visible)
            LoadIndex(0);

        Modified = true;
        CloseConfirmed = true;
        Close();
    }

    private void B_Dump_Click(object sender, EventArgs e)
    {
        if (!ConfirmDump())
            return;

        var arr = Cache.LoadAll();
        var result = TableUtil.GetNamedTypeTable(arr, Names, Text.Split(' ')[0]);
        Clipboard.SetText(result);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void GenericEditor_FormClosing(object? sender, FormClosingEventArgs e)
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
            "Save Changes",
            "Save the current editor changes?\n\nThis applies the edited data to the loaded project. Closing without saving will discard this editor session.",
            "Save");

    private bool ConfirmDump()
        => ThemedConfirmationDialog.Show(
            this,
            "Dump Editor Data",
            "Dump the current editor data to the clipboard?\n\nThis replaces your current clipboard contents. It does not save or apply changes to the project.",
            "Dump");

    private bool ConfirmRandomize()
        => ThemedConfirmationDialog.Show(
            this,
            "Randomize Entries",
            "Randomize this editor's entries?\n\nThis can change many values at once. Review the results before saving, or close without saving to discard them.",
            "Randomize");

    private bool ConfirmCloseWithoutSaving()
        => ThemedConfirmationDialog.Show(
            this,
            "Close Editor",
            "Close this editor without saving?\n\nAny changes made in this editor session will be discarded and the loaded project data will not be updated.",
            "Close");
}
