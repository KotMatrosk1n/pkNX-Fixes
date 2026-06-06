using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pkNX.Game;
using pkNX.Structures;
using BindingFlags = System.Reflection.BindingFlags;
using MethodInfo = System.Reflection.MethodInfo;
using PropertyInfo = System.Reflection.PropertyInfo;

namespace pkNX.WinForms;

public sealed partial class GenericEditor<T> : Form where T : class
{
    private sealed record EntryComboEntry(int Index, string Text)
    {
        public override string ToString() => Text;
    }

    private string[] Names;
    private EntryComboEntry[] EntryEntries = [];
    private DataCache<T> Cache;
    private readonly ShopTableView ShopTable = new();
    private readonly PlacementTableView PlacementTable = new();
    private readonly Size OriginalMinimumSize;
    private bool SuppressEntrySelectionChanged;
    private bool SuppressEntryAutoComplete;
    private bool UpdatingEntryFilter;
    private bool CloseConfirmed;
    private EntrySearchListBox? EntrySearchList;
    private readonly ToolTip GridToolTip = new()
    {
        AutoPopDelay = 12000,
        InitialDelay = 500,
        ReshowDelay = 100,
        ShowAlways = true,
        BackColor = WinFormsTheme.PanelBackground,
        ForeColor = WinFormsTheme.Text,
    };
    private Control? GridViewControl;
    private MethodInfo? GridEntryFromOffsetMethod;
    private PropertyInfo? GridEntryDescriptionProperty;
    private PropertyInfo? GridEntryLabelProperty;
    private string CurrentGridToolTip = string.Empty;
    public bool Modified { get; set; }

    public void ConfigurePlacementZoneNames(IReadOnlyDictionary<ulong, string> zoneNames)
    {
        PlacementTable.ZoneNames = zoneNames;
    }

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
        ContentPanel.Controls.Add(PlacementTable);
        PlacementTable.Visible = false;
        PlacementTable.BringToFront();

        TypeRegistrationHelper.RegisterIListConvertersRecursively(typeof(T));
        Text = title;
        WinFormsTheme.Apply(this);
        ConfigurePropertyGridToolTips();
        FormClosing += GenericEditor_FormClosing;
        Shown += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            CB_EntryName.Focus();
            CB_EntryName.SelectAll();
        }));
        ConfigureEntrySelector();
        if (CB_EntryName is EntrySelectorComboBox entrySelector)
            entrySelector.BeforeMouseWheel += CB_EntryName_BeforeMouseWheel;

        Cache = loadCache(this);
        Names = Cache.LoadAll().Select(nameSelector).ToArray();
        EntryEntries = Names.Select((z, i) => new EntryComboEntry(i, z)).ToArray();

        CB_EntryName.Items.AddRange(EntryEntries);
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
                EntryEntries = Names.Select((z, i) => new EntryComboEntry(i, z)).ToArray();
                ResetEntryEntries();
                UpdateShopEditorMinimumSize();

                System.Media.SystemSounds.Asterisk.Play();
            };
        }
    }

    private void ConfigurePropertyGridToolTips()
    {
        components?.Add(GridToolTip);
        Grid.HandleCreated += (_, _) => AttachPropertyGridToolTips();
        AttachPropertyGridToolTips();
    }

    private void AttachPropertyGridToolTips()
    {
        if (GridViewControl != null)
            return;

        var gridViewField = typeof(PropertyGrid).GetField("gridView", BindingFlags.Instance | BindingFlags.NonPublic);
        if (gridViewField?.GetValue(Grid) is not Control gridView)
            return;

        GridEntryFromOffsetMethod = gridView.GetType().GetMethod("GetGridEntryFromOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridEntryType = GridEntryFromOffsetMethod?.ReturnType;
        GridEntryDescriptionProperty = gridEntryType?.GetProperty("PropertyDescription", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        GridEntryLabelProperty = gridEntryType?.GetProperty("PropertyLabel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (GridEntryFromOffsetMethod == null || GridEntryDescriptionProperty == null)
            return;

        GridViewControl = gridView;
        GridViewControl.MouseMove += GridViewControl_MouseMove;
        GridViewControl.MouseLeave += (_, _) => HideGridToolTip();
    }

    private void GridViewControl_MouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Control control)
            return;

        var text = GetGridToolTipText(e.Y);
        if (string.IsNullOrWhiteSpace(text))
        {
            HideGridToolTip();
            return;
        }

        if (text == CurrentGridToolTip)
            return;

        CurrentGridToolTip = text;
        GridToolTip.SetToolTip(control, text);
    }

    private string GetGridToolTipText(int mouseY)
    {
        try
        {
            var entry = GridEntryFromOffsetMethod?.Invoke(GridViewControl, [mouseY]);
            if (entry == null)
                return string.Empty;

            var description = GridEntryDescriptionProperty?.GetValue(entry) as string;
            if (string.IsNullOrWhiteSpace(description))
                return string.Empty;

            var label = GridEntryLabelProperty?.GetValue(entry) as string;
            return string.IsNullOrWhiteSpace(label)
                ? description
                : $"{label}\n{description}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private void HideGridToolTip()
    {
        if (GridViewControl == null || CurrentGridToolTip.Length == 0)
            return;

        CurrentGridToolTip = string.Empty;
        GridToolTip.SetToolTip(GridViewControl, string.Empty);
        GridToolTip.Hide(GridViewControl);
    }

    private void CB_EntryName_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (SuppressEntrySelectionChanged)
            return;

        var index = GetSelectedEntryIndex();
        LoadIndex(index);
    }

    private void ConfigureEntrySelector()
    {
        CB_EntryName.DropDownStyle = ComboBoxStyle.DropDown;
        CB_EntryName.AutoCompleteMode = AutoCompleteMode.None;
        CB_EntryName.AutoCompleteSource = AutoCompleteSource.None;
        CB_EntryName.DrawMode = DrawMode.OwnerDrawFixed;
        CB_EntryName.ItemHeight = 22;
        CB_EntryName.DropDownHeight = 1;
        CB_EntryName.DrawItem += DrawEntryItem;
        CB_EntryName.TextUpdate += (_, _) =>
        {
            var append = !SuppressEntryAutoComplete;
            SuppressEntryAutoComplete = false;
            FilterEntryEntries(append);
        };
        CB_EntryName.DropDown += (_, _) =>
        {
            var closeCustomDropDown = EntrySearchList?.Visible == true;
            BeginInvoke((MethodInvoker)(() =>
            {
                CB_EntryName.DroppedDown = false;
                if (closeCustomDropDown)
                {
                    HideEntrySearchList();
                    CB_EntryName.Focus();
                    return;
                }

                ShowEntryDropDownList();
            }));
        };
        CB_EntryName.SelectionChangeCommitted += (_, _) =>
        {
            var index = GetSelectedEntryIndex();
            if (index >= 0)
                SelectEntryIndex(index);
        };
        CB_EntryName.DropDownClosed += (_, _) => CB_EntryName.DroppedDown = false;
        CB_EntryName.Leave += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            if (EntrySearchList?.Focused == true)
                return;

            RestoreSelectedEntryText();
            HideEntrySearchList();
        }));
        CB_EntryName.KeyDown += CB_EntryName_KeyDown;

        EntrySearchList = new EntrySearchListBox
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
        EntrySearchList.BeforeMouseWheel += EntrySearchList_BeforeMouseWheel;
        EntrySearchList.DrawItem += DrawEntryListItem;
        EntrySearchList.MouseClick += EntrySearchList_MouseClick;
        EntrySearchList.MouseMove += EntrySearchList_MouseMove;
        EntrySearchList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitEntryListSelection();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                HideEntrySearchList();
                CB_EntryName.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        EntrySearchList.Leave += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            if (CB_EntryName.Focused)
                return;

            HideEntrySearchList();
        }));
    }

    private void EntrySearchList_BeforeMouseWheel(object? sender, EntrySelectorMouseWheelEventArgs e)
    {
        if (EntrySearchList is { Visible: true, Items.Count: > 0 } list)
        {
            MoveEntrySearchSelection(list, e.MouseEvent.Delta);
            e.Handled = true;
        }
    }

    private void EntrySearchList_MouseClick(object? sender, MouseEventArgs e)
    {
        if (EntrySearchList == null)
            return;

        var index = EntrySearchList.IndexFromPoint(e.Location);
        if ((uint)index >= (uint)EntrySearchList.Items.Count || EntrySearchList.Items[index] is not EntryComboEntry entry)
            return;

        CommitEntryListEntry(entry);
    }

    private void EntrySearchList_MouseMove(object? sender, MouseEventArgs e)
    {
        if (EntrySearchList == null)
            return;

        var index = EntrySearchList.IndexFromPoint(e.Location);
        EntrySearchList.HoverIndex = (uint)index < (uint)EntrySearchList.Items.Count ? index : -1;
    }

    private void CB_EntryName_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            RestoreSelectedEntryText();
            HideEntrySearchList();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Down && EntrySearchList is { Visible: true, Items.Count: > 0 } list)
        {
            list.SelectedIndex = Math.Max(0, list.SelectedIndex);
            list.Focus();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode is Keys.Back or Keys.Delete)
        {
            HandleEntryDeleteKey(e);
            return;
        }

        if (e.KeyCode is not Keys.Enter)
            return;

        if (!CommitEntrySelectionFromText(allowPrefix: true))
            return;

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void CB_EntryName_BeforeMouseWheel(object? sender, EntrySelectorMouseWheelEventArgs e)
    {
        if (EntrySearchList is { Visible: true, Items.Count: > 0 } list)
        {
            MoveEntrySearchSelection(list, e.MouseEvent.Delta);
            e.Handled = true;
            return;
        }

        if (TryScrollMachineEntry(e.MouseEvent.Delta))
        {
            e.Handled = true;
            return;
        }

        CommitEntrySelectionFromText(allowPrefix: true);
    }

    private void HandleEntryDeleteKey(KeyEventArgs e)
    {
        SuppressEntryAutoComplete = true;
        if (CB_EntryName.SelectionLength == 0)
        {
            var caret = CB_EntryName.SelectionStart;
            var canDelete = e.KeyCode == Keys.Back ? caret > 0 : caret < CB_EntryName.Text.Length;
            if (!canDelete)
                SuppressEntryAutoComplete = false;
            return;
        }

        var text = CB_EntryName.Text;
        var selectionStart = Math.Min(CB_EntryName.SelectionStart, text.Length);
        var newText = e.KeyCode == Keys.Back && selectionStart > 0
            ? text[..(selectionStart - 1)]
            : text[..selectionStart];

        SetEntrySearchText(newText);
        SuppressEntryAutoComplete = false;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private bool CommitEntrySelectionFromText(bool allowPrefix)
    {
        var index = FindEntryIndex(CB_EntryName.Text, allowPrefix);
        if (index < 0)
            return false;

        if (index == GetSelectedEntryIndex())
        {
            SetEntryText(index);
            HideEntrySearchList();
            return true;
        }

        SelectEntryIndex(index);
        return true;
    }

    private int GetSelectedEntryIndex() => CB_EntryName.SelectedItem is EntryComboEntry entry ? entry.Index : CB_EntryName.SelectedIndex;

    private void FilterEntryEntries(bool appendAutoComplete)
    {
        if (UpdatingEntryFilter)
            return;

        var userText = GetEntrySearchText();
        var matches = GetEntryMatches(userText).ToArray();
        var autoCompleteMatch = appendAutoComplete ? GetEntryAutoCompleteMatch(userText, matches) : null;
        var displayText = autoCompleteMatch?.Text ?? userText;
        var selectionStart = autoCompleteMatch == null ? userText.Length : Math.Min(userText.Length, autoCompleteMatch.Text.Length);
        var selectionLength = autoCompleteMatch == null ? 0 : autoCompleteMatch.Text.Length - selectionStart;

        UpdatingEntryFilter = true;
        SuppressEntrySelectionChanged = true;
        CB_EntryName.BeginUpdate();
        CB_EntryName.Items.Clear();
        CB_EntryName.Items.AddRange(matches);
        CB_EntryName.EndUpdate();
        CB_EntryName.Text = displayText;
        CB_EntryName.SelectionStart = selectionStart;
        CB_EntryName.SelectionLength = selectionLength;
        SuppressEntrySelectionChanged = false;
        UpdatingEntryFilter = false;

        ShowEntrySearchList(userText, matches);
    }

    private void SetEntrySearchText(string text)
    {
        UpdatingEntryFilter = true;
        CB_EntryName.Text = text;
        CB_EntryName.SelectionStart = text.Length;
        CB_EntryName.SelectionLength = 0;
        UpdatingEntryFilter = false;
        FilterEntryEntries(false);
    }

    private void ShowEntrySearchList(string text)
    {
        ShowEntrySearchList(text, GetEntryMatches(text).ToArray());
    }

    private void ShowEntryDropDownList()
    {
        var text = GetEntryDropDownSearchText();
        var matches = GetEntryMatches(text).ToArray();
        var selectedIndex = text.Length == 0 ? GetSelectedEntryMatchIndex(matches) : 0;
        ShowEntrySearchList(text, matches, selectedIndex);
    }

    private string GetEntryDropDownSearchText()
    {
        var text = CB_EntryName.Text.Trim();
        var selectedIndex = GetSelectedEntryIndex();
        if ((uint)selectedIndex < (uint)EntryEntries.Length &&
            string.Equals(EntryEntries[selectedIndex].Text, text, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return GetEntrySearchText();
    }

    private int GetSelectedEntryMatchIndex(IReadOnlyList<EntryComboEntry> matches)
    {
        var selectedIndex = GetSelectedEntryIndex();
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Index == selectedIndex)
                return i;
        }

        return 0;
    }

    private void ShowEntrySearchList(string text, IReadOnlyList<EntryComboEntry> matches, int preferredSelectionIndex = 0)
    {
        if (EntrySearchList == null)
            return;

        if (EntrySearchList.Parent != this)
            Controls.Add(EntrySearchList);

        EntrySearchList.BeginUpdate();
        EntrySearchList.HoverIndex = -1;
        EntrySearchList.Items.Clear();
        foreach (var match in matches)
            EntrySearchList.Items.Add(match);
        EntrySearchList.EndUpdate();

        if (matches.Count == 0 || !CB_EntryName.Focused && EntrySearchList.Focused != true)
        {
            HideEntrySearchList();
            return;
        }

        const int maxVisibleRows = 12;
        var visibleRows = Math.Min(matches.Count, maxVisibleRows);
        var screenLocation = CB_EntryName.Parent?.PointToScreen(CB_EntryName.Location) ?? PointToScreen(CB_EntryName.Location);
        var location = PointToClient(new Point(screenLocation.X, screenLocation.Y + CB_EntryName.Height));
        var availableHeight = Math.Max(EntrySearchList.ItemHeight + 2, ClientSize.Height - location.Y - 4);
        var height = Math.Min(availableHeight, visibleRows * EntrySearchList.ItemHeight + 2);

        EntrySearchList.Bounds = new Rectangle(location.X, location.Y, CB_EntryName.Width, height);
        EntrySearchList.Visible = true;
        EntrySearchList.BringToFront();
        var selectedIndex = Math.Clamp(preferredSelectionIndex, 0, matches.Count - 1);
        EntrySearchList.SelectedIndex = selectedIndex;
        EntrySearchList.TopIndex = Math.Clamp(selectedIndex - visibleRows / 2, 0, Math.Max(0, matches.Count - visibleRows));
    }

    private void HideEntrySearchList()
    {
        if (EntrySearchList != null)
        {
            EntrySearchList.HoverIndex = -1;
            EntrySearchList.Visible = false;
        }
    }

    private void CommitEntryListSelection()
    {
        if (EntrySearchList?.SelectedItem is not EntryComboEntry entry)
            return;

        CommitEntryListEntry(entry);
    }

    private void CommitEntryListEntry(EntryComboEntry entry)
    {
        HideEntrySearchList();
        CB_EntryName.Focus();
        SelectEntryIndex(entry.Index);
    }

    private IEnumerable<EntryComboEntry> GetEntryMatches(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return EntryEntries;

        return EntryEntries
            .Where(entry => EntryMatches(entry, text))
            .OrderBy(entry => EntryMatchRank(entry, text))
            .ThenBy(entry => EntryMachineSort(entry.Text))
            .ThenBy(entry => entry.Index);
    }

    private static bool EntryMatches(EntryComboEntry entry, string text)
    {
        return entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase)
            || entry.Index.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase);
    }

    private static int EntryMatchRank(EntryComboEntry entry, string text)
    {
        if (TryGetMachineName(entry.Text, out var entryKind, out _) && IsMachineSearch(text, entryKind))
            return 0;
        if (entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (entry.Index.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 2;
        return 3;
    }

    private static int EntryMachineSort(string text)
    {
        return TryGetMachineName(text, out _, out var number) ? number : int.MaxValue;
    }

    private static EntryComboEntry? GetEntryAutoCompleteMatch(string text, IReadOnlyList<EntryComboEntry> matches)
    {
        text = text.Trim();
        if (text.Length == 0)
            return null;

        return matches.FirstOrDefault(entry => entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase));
    }

    private string GetEntrySearchText()
    {
        var text = CB_EntryName.Text;
        var selectionStart = Math.Min(CB_EntryName.SelectionStart, text.Length);
        return selectionStart <= 0 ? text : text[..selectionStart];
    }

    private void ResetEntryEntries()
    {
        SuppressEntrySelectionChanged = true;
        CB_EntryName.BeginUpdate();
        CB_EntryName.Items.Clear();
        CB_EntryName.Items.AddRange(EntryEntries);
        CB_EntryName.EndUpdate();
        SuppressEntrySelectionChanged = false;
    }

    private void SelectEntryIndex(int index)
    {
        if ((uint)index >= (uint)EntryEntries.Length)
            return;

        SuppressEntrySelectionChanged = true;
        CB_EntryName.BeginUpdate();
        CB_EntryName.Items.Clear();
        CB_EntryName.Items.AddRange(EntryEntries);
        CB_EntryName.EndUpdate();
        SuppressEntrySelectionChanged = false;
        CB_EntryName.SelectedItem = EntryEntries[index];
        SetEntryText(index);
        HideEntrySearchList();
    }

    private void SetEntryText(int index)
    {
        if ((uint)index >= (uint)EntryEntries.Length)
            return;

        CB_EntryName.Text = EntryEntries[index].Text;
        CB_EntryName.SelectionStart = 0;
        CB_EntryName.SelectionLength = 0;
    }

    private void RestoreSelectedEntryText()
    {
        var index = GetSelectedEntryIndex();
        if ((uint)index < (uint)EntryEntries.Length)
            SetEntryText(index);
    }

    private static void MoveEntrySearchSelection(ListBox list, int delta)
    {
        if (list.Items.Count == 0)
            return;

        var direction = delta < 0 ? 1 : -1;
        var selected = Math.Max(0, list.SelectedIndex);
        list.SelectedIndex = Math.Clamp(selected + direction, 0, list.Items.Count - 1);
    }

    private bool TryScrollMachineEntry(int delta)
    {
        if (!TryGetCurrentMachineEntry(out var kind, out var number))
            return false;

        var direction = delta < 0 ? 1 : -1;
        for (var next = number + direction; next is >= 0 and <= 99; next += direction)
        {
            var index = FindMachineEntryIndex(kind, next);
            if (index < 0)
                continue;

            SelectEntryIndex(index);
            return true;
        }

        return true;
    }

    private bool TryGetCurrentMachineEntry(out string kind, out int number)
    {
        if (TryGetMachineName(CB_EntryName.Text, out kind, out number))
            return true;

        var index = GetSelectedEntryIndex();
        if ((uint)index < (uint)Names.Length && TryGetMachineName(Names[index], out kind, out number))
            return true;

        kind = string.Empty;
        number = -1;
        return false;
    }

    private int FindMachineEntryIndex(string kind, int number)
    {
        var name = $"{kind}{number:00}";
        return Array.FindIndex(Names, z => string.Equals(z, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMachineSearch(string text, string kind)
    {
        text = text.Trim();
        return text.Length >= 2 && text.StartsWith(kind, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetMachineName(string text, out string kind, out int number)
    {
        text = text.Trim();
        if (text.Length < 4)
        {
            kind = string.Empty;
            number = -1;
            return false;
        }

        if (text.StartsWith("TM", StringComparison.OrdinalIgnoreCase))
            kind = "TM";
        else if (text.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
            kind = "TR";
        else
        {
            kind = string.Empty;
            number = -1;
            return false;
        }

        var index = 2;
        while (index < text.Length && text[index] == ' ')
            index++;

        var start = index;
        while (index < text.Length && char.IsDigit(text[index]))
            index++;

        if (index == start || !int.TryParse(text[start..index], out number) || number is < 0 or > 99)
        {
            kind = string.Empty;
            number = -1;
            return false;
        }

        return true;
    }

    private static void DrawEntryItem(object? sender, DrawItemEventArgs e)
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

    private static void DrawEntryListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
            return;

        var hovered = listBox is EntrySearchListBox searchList && searchList.HoverIndex == e.Index;
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var highlighted = selected || hovered;
        var backColor = highlighted ? WinFormsTheme.SelectionBackground : WinFormsTheme.InputBackground;
        var foreColor = highlighted ? WinFormsTheme.SelectionText : WinFormsTheme.Text;

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

    private int FindEntryIndex(string text, bool allowPrefix)
    {
        text = text.Trim();
        if (text.Length == 0)
            return -1;

        var exact = Array.FindIndex(EntryEntries, z => string.Equals(z.Text, text, StringComparison.OrdinalIgnoreCase));
        if (exact >= 0 || !allowPrefix)
            return exact;

        return GetEntryMatches(text).FirstOrDefault()?.Index ?? -1;
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
            PlacementTable.Visible = false;
            ShopTable.Visible = true;
            ShopTable.LoadShop(value);
            ShopTable.BringToFront();
            return;
        }

        if (PlacementTableView.Supports(value))
        {
            Grid.SelectedObject = null;
            Grid.Visible = false;
            ShopTable.Visible = false;
            PlacementTable.Visible = true;
            PlacementTable.LoadArchive(value);
            PlacementTable.BringToFront();
            return;
        }

        ShopTable.Visible = false;
        PlacementTable.Visible = false;
        Grid.Visible = true;
        var displayObject = ShopPropertyGridObjectFactory.Create(value);
        TypeRegistrationHelper.RegisterIListConvertersRecursively(displayObject.GetType());
        Grid.SelectedObject = displayObject;
        Grid.BringToFront();
    }

    private void UpdateShopEditorMinimumSize()
    {
        var isShopEditor = Cache.Length > 0 && ShopTableView.Supports(Cache[0]);
        var isPlacementEditor = Cache.Length > 0 && PlacementTableView.Supports(Cache[0]);
        MinimumSize = isShopEditor || isPlacementEditor
            ? new Size(Math.Max(OriginalMinimumSize.Width, isPlacementEditor ? 1120 : 900), OriginalMinimumSize.Height)
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

    private sealed class EntrySelectorComboBox : ComboBox
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        public event EventHandler<EntrySelectorMouseWheelEventArgs>? BeforeMouseWheel;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL && HandleMouseWheelMessage(m.WParam))
                return;

            base.WndProc(ref m);
        }

        private bool HandleMouseWheelMessage(IntPtr wParam)
        {
            var delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
            var location = PointToClient(MousePosition);
            var mouseEvent = new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, delta);
            var args = new EntrySelectorMouseWheelEventArgs(mouseEvent);
            BeforeMouseWheel?.Invoke(this, args);
            return args.Handled;
        }
    }

    private sealed class EntrySearchListBox : ListBox
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        private int hoverIndex = -1;

        public event EventHandler<EntrySelectorMouseWheelEventArgs>? BeforeMouseWheel;

        public int HoverIndex
        {
            get => hoverIndex;
            set
            {
                if (hoverIndex == value)
                    return;

                var oldIndex = hoverIndex;
                hoverIndex = value;
                InvalidateItem(oldIndex);
                InvalidateItem(hoverIndex);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            HoverIndex = -1;
            base.OnMouseLeave(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL && HandleMouseWheelMessage(m.WParam))
                return;

            base.WndProc(ref m);
        }

        private void InvalidateItem(int index)
        {
            if ((uint)index < (uint)Items.Count)
                Invalidate(GetItemRectangle(index));
        }

        private bool HandleMouseWheelMessage(IntPtr wParam)
        {
            var delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
            var location = PointToClient(MousePosition);
            var mouseEvent = new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, delta);
            var args = new EntrySelectorMouseWheelEventArgs(mouseEvent);
            BeforeMouseWheel?.Invoke(this, args);
            return args.Handled;
        }
    }

    private sealed class EntrySelectorMouseWheelEventArgs(MouseEventArgs mouseEvent) : EventArgs
    {
        public MouseEventArgs MouseEvent { get; } = mouseEvent;
        public bool Handled { get; set; }
    }
}
