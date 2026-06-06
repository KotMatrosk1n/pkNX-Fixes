using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class SearchableComboBoxBehavior
{
    private static readonly ConditionalWeakTable<ComboBox, SearchableComboBoxBehavior> RegisteredCombos = new();
    private static readonly ConditionalWeakTable<DataGridView, GridComboRegistration> RegisteredGrids = new();

    private readonly Control OwnerScope;
    private readonly ComboBox Combo;
    private readonly SearchListBox SearchList = new();
    private bool UpdatingText;

    public SearchableComboBoxBehavior(Control owner, ComboBox combo)
    {
        OwnerScope = owner;
        Combo = combo;

        Combo.DropDownStyle = ComboBoxStyle.DropDown;
        Combo.AutoCompleteMode = AutoCompleteMode.None;
        Combo.AutoCompleteSource = AutoCompleteSource.None;
        Combo.DrawMode = DrawMode.OwnerDrawFixed;
        Combo.ItemHeight = 22;
        Combo.DropDownHeight = 1;
        Combo.DrawItem += DrawComboItem;
        Combo.TextUpdate += (_, _) => FilterEntries();
        Combo.DropDown += (_, _) =>
        {
            var closeCustomDropDown = SearchList.Visible;
            BeginInvokeOnUi(() =>
            {
                Combo.DroppedDown = false;
                if (closeCustomDropDown)
                {
                    HideSearchList();
                    Combo.Focus();
                    return;
                }

                ShowDropDownList();
            });
        };
        Combo.DropDownClosed += (_, _) => Combo.DroppedDown = false;
        Combo.KeyDown += Combo_KeyDown;
        Combo.Leave += (_, _) => BeginInvokeOnUi(() =>
        {
            if (SearchList.Focused)
                return;

            RestoreSelectedText();
            HideSearchList();
        });

        SearchList.BackColor = WinFormsTheme.InputBackground;
        SearchList.BorderStyle = BorderStyle.FixedSingle;
        SearchList.Cursor = Cursors.Default;
        SearchList.DrawMode = DrawMode.OwnerDrawFixed;
        SearchList.ForeColor = WinFormsTheme.Text;
        SearchList.IntegralHeight = false;
        SearchList.ItemHeight = 22;
        SearchList.Visible = false;
        SearchList.DrawItem += DrawSearchListItem;
        SearchList.MouseClick += SearchList_MouseClick;
        SearchList.MouseMove += SearchList_MouseMove;
        SearchList.BeforeMouseWheel += SearchList_BeforeMouseWheel;
        SearchList.KeyDown += SearchList_KeyDown;
        SearchList.Leave += (_, _) => BeginInvokeOnUi(() =>
        {
            if (Combo.Focused)
                return;

            HideSearchList();
        });
    }

    public static void Register(Control owner, params ComboBox[] comboBoxes)
    {
        foreach (var comboBox in comboBoxes)
        {
            if (comboBox != null)
                _ = RegisteredCombos.GetValue(comboBox, combo => new SearchableComboBoxBehavior(owner, combo));
        }
    }

    public static void RegisterGrid(DataGridView grid)
        => _ = RegisteredGrids.GetValue(grid, static gridControl => new GridComboRegistration(gridControl));

    private void Combo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            RestoreSelectedText();
            HideSearchList();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            if (!SearchList.Visible)
                ShowDropDownList();

            if (SearchList.Items.Count > 0)
            {
                SearchList.SelectedIndex = Math.Max(0, SearchList.SelectedIndex);
                SearchList.Focus();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode is Keys.Back or Keys.Delete)
        {
            HandleDeleteKey(e);
            return;
        }

        if (e.KeyCode != Keys.Enter)
            return;

        if (!CommitSelectionFromText())
            RestoreSelectedText();

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void SearchList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CommitSearchListSelection();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            HideSearchList();
            Combo.Focus();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void SearchList_MouseClick(object? sender, MouseEventArgs e)
    {
        var index = SearchList.IndexFromPoint(e.Location);
        if ((uint)index >= (uint)SearchList.Items.Count || SearchList.Items[index] is not ComboEntry entry)
            return;

        CommitSearchListEntry(entry);
    }

    private void SearchList_MouseMove(object? sender, MouseEventArgs e)
    {
        var index = SearchList.IndexFromPoint(e.Location);
        if ((uint)index < (uint)SearchList.Items.Count && SearchList.SelectedIndex != index)
            SearchList.SelectedIndex = index;
    }

    private void HandleDeleteKey(KeyEventArgs e)
    {
        if (Combo.SelectionLength == 0)
            return;

        var text = Combo.Text;
        var selectionStart = GetSelectionStart();
        var newText = e.KeyCode == Keys.Back && selectionStart > 0
            ? text[..(selectionStart - 1)]
            : text[..selectionStart];

        SetSearchText(newText);
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void FilterEntries()
    {
        if (UpdatingText)
            return;

        var userText = GetSearchText();
        var matches = GetMatches(userText).ToArray();

        UpdatingText = true;
        Combo.Text = userText;
        SelectText(userText.Length, 0);
        UpdatingText = false;

        ShowSearchList(userText, matches);
    }

    private void SetSearchText(string text)
    {
        UpdatingText = true;
        Combo.Text = text;
        SelectText(text.Length, 0);
        UpdatingText = false;
        FilterEntries();
    }

    private void ShowDropDownList()
    {
        var text = GetDropDownSearchText();
        var matches = GetMatches(text).ToArray();
        var selectedIndex = text.Length == 0 ? GetSelectedMatchIndex(matches) : 0;
        ShowSearchList(text, matches, selectedIndex);
    }

    private string GetDropDownSearchText()
    {
        var text = Combo.Text.Trim();
        var selectedIndex = Combo.SelectedIndex;
        if ((uint)selectedIndex < (uint)Combo.Items.Count &&
            string.Equals(GetItemText(selectedIndex), text, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return GetSearchText();
    }

    private int GetSelectedMatchIndex(IReadOnlyList<ComboEntry> matches)
    {
        var selectedIndex = Combo.SelectedIndex;
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Index == selectedIndex)
                return i;
        }

        return 0;
    }

    private void ShowSearchList(string text, IReadOnlyList<ComboEntry> matches, int preferredSelectionIndex = 0)
    {
        var owner = GetPopupOwner();
        if (SearchList.Parent != owner)
            owner.Controls.Add(SearchList);

        SearchList.BeginUpdate();
        SearchList.Items.Clear();
        foreach (var match in matches)
            SearchList.Items.Add(match);
        SearchList.EndUpdate();

        if (matches.Count == 0 || !Combo.Focused && !SearchList.Focused)
        {
            HideSearchList();
            return;
        }

        SearchList.Bounds = GetPopupBounds(owner, Combo, Math.Max(Combo.Width, Combo.DropDownWidth), SearchList.ItemHeight, matches.Count, 12);
        SearchList.Visible = true;
        SearchList.BringToFront();
        var selectedIndex = Math.Clamp(preferredSelectionIndex, 0, matches.Count - 1);
        var visibleRows = Math.Max(1, SearchList.ClientSize.Height / Math.Max(1, SearchList.ItemHeight));
        SearchList.SelectedIndex = selectedIndex;
        SearchList.TopIndex = Math.Clamp(selectedIndex - visibleRows / 2, 0, Math.Max(0, matches.Count - visibleRows));
    }

    public static Rectangle GetPopupBounds(Control owner, Control anchor, int preferredWidth, int itemHeight, int itemCount, int maxVisibleRows)
    {
        const int margin = 4;
        var client = owner.ClientSize;
        var availableWidth = Math.Max(1, client.Width - margin * 2);
        var minimumWidth = Math.Min(Math.Max(1, anchor.Width), availableWidth);
        var width = Math.Clamp(Math.Max(anchor.Width, preferredWidth), minimumWidth, availableWidth);

        var anchorScreen = anchor.RectangleToScreen(anchor.ClientRectangle);
        var anchorLocation = owner.PointToClient(anchorScreen.Location);
        var anchorBounds = new Rectangle(anchorLocation, anchor.Size);
        var xMax = Math.Max(margin, client.Width - width - margin);
        var x = Math.Clamp(anchorBounds.Left, margin, xMax);

        var rows = Math.Max(1, Math.Min(itemCount, maxVisibleRows));
        var minimumHeight = Math.Max(1, itemHeight) + 2;
        var desiredHeight = Math.Max(minimumHeight, rows * Math.Max(1, itemHeight) + 2);
        var maxHeight = Math.Max(1, client.Height - margin * 2);
        var belowSpace = Math.Max(0, client.Height - anchorBounds.Bottom - margin);
        var aboveSpace = Math.Max(0, anchorBounds.Top - margin);
        var openBelow = belowSpace >= Math.Min(desiredHeight, minimumHeight) || belowSpace >= aboveSpace;
        var availableHeight = openBelow ? belowSpace : aboveSpace;
        var height = Math.Min(desiredHeight, Math.Min(maxHeight, Math.Max(minimumHeight, availableHeight)));

        var y = openBelow ? anchorBounds.Bottom : anchorBounds.Top - height;
        var yMax = Math.Max(margin, client.Height - height - margin);
        y = Math.Clamp(y, margin, yMax);

        return new Rectangle(x, y, width, height);
    }

    private Control GetPopupOwner()
        => Combo.FindForm() ?? OwnerScope.FindForm() ?? OwnerScope;

    private void BeginInvokeOnUi(MethodInvoker action)
    {
        var owner = GetPopupOwner();
        if (!owner.IsDisposed && owner.IsHandleCreated)
        {
            owner.BeginInvoke(action);
            return;
        }

        if (!Combo.IsDisposed && Combo.IsHandleCreated)
        {
            Combo.BeginInvoke(action);
            return;
        }

        action();
    }

    private void HideSearchList()
    {
        SearchList.Visible = false;
    }

    private void CommitSearchListSelection()
    {
        if (SearchList.SelectedItem is not ComboEntry entry)
            return;

        CommitSearchListEntry(entry);
    }

    private void CommitSearchListEntry(ComboEntry entry)
    {
        HideSearchList();
        Combo.Focus();
        SelectIndex(entry.Index);
    }

    private bool CommitSelectionFromText()
    {
        var index = FindIndex(Combo.Text, allowPrefix: true);
        if (index < 0)
            return false;

        SelectIndex(index);
        return true;
    }

    private void SelectIndex(int index)
    {
        if ((uint)index >= (uint)Combo.Items.Count)
            return;

        Combo.SelectedIndex = index;
        SetText(index);
        HideSearchList();
    }

    private void SetText(int index)
    {
        if ((uint)index >= (uint)Combo.Items.Count)
            return;

        UpdatingText = true;
        Combo.Text = GetItemText(index);
        SelectText(0, 0);
        UpdatingText = false;
    }

    private void RestoreSelectedText()
    {
        var index = Combo.SelectedIndex;
        if ((uint)index < (uint)Combo.Items.Count)
            SetText(index);
    }

    private IEnumerable<ComboEntry> GetMatches(string text)
    {
        text = text.Trim();
        var entries = Enumerable.Range(0, Combo.Items.Count)
            .Select(i => new ComboEntry(i, GetItemText(i)));

        if (text.Length == 0)
            return entries;

        return entries
            .Where(entry => Matches(entry, text))
            .OrderBy(entry => MatchRank(entry, text))
            .ThenBy(entry => entry.Index);
    }

    private static bool Matches(ComboEntry entry, string text)
    {
        return entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase)
            || entry.Index.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase)
            || entry.Index.ToString("000").StartsWith(text, StringComparison.OrdinalIgnoreCase);
    }

    private static int MatchRank(ComboEntry entry, string text)
    {
        if (entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (entry.Index.ToString("000").StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (entry.Index.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 2;
        return 3;
    }

    private int FindIndex(string text, bool allowPrefix)
    {
        text = text.Trim();
        if (text.Length == 0)
            return -1;

        for (var i = 0; i < Combo.Items.Count; i++)
        {
            if (string.Equals(GetItemText(i), text, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        if (!allowPrefix)
            return -1;

        return GetMatches(text).FirstOrDefault()?.Index ?? -1;
    }

    private string GetSearchText()
    {
        var text = Combo.Text;
        var selectionStart = GetSelectionStart();
        return selectionStart <= 0 ? text : text[..selectionStart];
    }

    private string GetItemText(int index) => Combo.GetItemText(Combo.Items[index]) ?? string.Empty;

    private int GetSelectionStart()
    {
        var textLength = Combo.Text.Length;
        return Math.Clamp(Combo.SelectionStart, 0, textLength);
    }

    private void SelectText(int start, int length)
    {
        var textLength = Combo.Text.Length;
        start = Math.Clamp(start, 0, textLength);
        length = Math.Clamp(length, 0, textLength - start);
        Combo.Select(start, length);
    }

    private void SearchList_BeforeMouseWheel(object? sender, SearchMouseWheelEventArgs e)
    {
        if (!SearchList.Visible || SearchList.Items.Count == 0)
            return;

        ScrollListByWheel(SearchList, e.MouseEvent.Delta);
        SelectRowUnderMouse(SearchList);
        e.Handled = true;
    }

    private static void ScrollListByWheel(ListBox list, int delta)
    {
        if (list.Items.Count == 0)
            return;

        var visibleRows = Math.Max(1, list.ClientSize.Height / Math.Max(1, list.ItemHeight));
        var maxTopIndex = Math.Max(0, list.Items.Count - visibleRows);
        var scrollLines = SystemInformation.MouseWheelScrollLines <= 0 ? 1 : SystemInformation.MouseWheelScrollLines;
        var direction = delta < 0 ? 1 : -1;
        list.TopIndex = Math.Clamp(list.TopIndex + direction * scrollLines, 0, maxTopIndex);
    }

    private static void SelectRowUnderMouse(ListBox list)
    {
        var location = list.PointToClient(Cursor.Position);
        var index = list.IndexFromPoint(location);
        if ((uint)index < (uint)list.Items.Count && list.SelectedIndex != index)
            list.SelectedIndex = index;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0 || e.Index >= combo.Items.Count)
            return;

        DrawTextItem(e, combo.GetItemText(combo.Items[e.Index]) ?? string.Empty, 0);
    }

    private static void DrawSearchListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
            return;

        DrawTextItem(e, listBox.Items[e.Index]?.ToString() ?? string.Empty, 4);
    }

    private static void DrawTextItem(DrawItemEventArgs e, string text, int leftPadding)
    {
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backColor = selected ? WinFormsTheme.SelectionBackground : WinFormsTheme.InputBackground;
        var foreColor = selected ? WinFormsTheme.SelectionText : WinFormsTheme.Text;

        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            e.Font,
            new Rectangle(e.Bounds.X + leftPadding, e.Bounds.Y, e.Bounds.Width - leftPadding, e.Bounds.Height),
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }

    private sealed record ComboEntry(int Index, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed class SearchListBox : ListBox
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        public event EventHandler<SearchMouseWheelEventArgs>? BeforeMouseWheel;

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
            var args = new SearchMouseWheelEventArgs(mouseEvent);
            BeforeMouseWheel?.Invoke(this, args);
            return args.Handled;
        }
    }

    private sealed class SearchMouseWheelEventArgs(MouseEventArgs mouseEvent) : EventArgs
    {
        public MouseEventArgs MouseEvent { get; } = mouseEvent;
        public bool Handled { get; set; }
    }

    private sealed class GridComboRegistration
    {
        private readonly DataGridView Grid;

        public GridComboRegistration(DataGridView grid)
        {
            Grid = grid;
            Grid.EditingControlShowing += Grid_EditingControlShowing;
        }

        private void Grid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox comboBox)
                Register((Control?)Grid.FindForm() ?? Grid, comboBox);
        }
    }
}
