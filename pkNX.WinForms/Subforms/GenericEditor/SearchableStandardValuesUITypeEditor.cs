using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace pkNX.WinForms;

public sealed class SearchableStandardValuesUITypeEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.DropDown;

    public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        if (context?.PropertyDescriptor == null)
            return value;

        var service = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
        if (service == null)
            return value;

        var converter = context.PropertyDescriptor.Converter;
        if (!Supports(context, converter))
            return value;

        var entries = GetEntries(context, converter).ToArray();
        if (entries.Length == 0)
            return value;

        using var picker = new SearchableStandardValuePicker(context, converter, entries, value, service);
        service.DropDownControl(picker);
        return picker.Committed ? picker.SelectedValue : value;
    }

    public static bool Supports(ITypeDescriptorContext? context, TypeConverter converter)
    {
        if (!converter.GetStandardValuesSupported(context))
            return false;

        try
        {
            return converter.GetStandardValues(context)?.Count > 1;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<StandardValueEntry> GetEntries(ITypeDescriptorContext context, TypeConverter converter)
    {
        var values = converter.GetStandardValues(context);
        if (values == null)
            yield break;

        foreach (var value in values)
        {
            var text = converter.ConvertToString(context, CultureInfo.CurrentCulture, value) ?? value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
                yield return new StandardValueEntry(value, text);
        }
    }

    private sealed record StandardValueEntry(object? Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed class SearchableStandardValuePicker : UserControl
    {
        private const int MaxVisibleRows = 12;

        private readonly ITypeDescriptorContext Context;
        private readonly TypeConverter Converter;
        private readonly StandardValueEntry[] Entries;
        private readonly IWindowsFormsEditorService Service;
        private readonly TextBox SearchText = new();
        private readonly SearchableValueListBox Results = new();
        private bool SuppressAutoComplete;
        private bool UpdatingFilter;

        public bool Committed { get; private set; }
        public object? SelectedValue { get; private set; }

        public SearchableStandardValuePicker(
            ITypeDescriptorContext context,
            TypeConverter converter,
            StandardValueEntry[] entries,
            object? currentValue,
            IWindowsFormsEditorService service)
        {
            Context = context;
            Converter = converter;
            Entries = entries;
            SelectedValue = currentValue;
            Service = service;

            BackColor = WinFormsTheme.InputBackground;
            ForeColor = WinFormsTheme.Text;
            MinimumSize = new Size(260, 28);
            Width = Math.Min(640, Math.Max(320, GetPreferredWidth(entries)));

            SearchText.BorderStyle = BorderStyle.FixedSingle;
            SearchText.BackColor = WinFormsTheme.InputBackground;
            SearchText.ForeColor = WinFormsTheme.Text;
            SearchText.Dock = DockStyle.Top;
            SearchText.Margin = Padding.Empty;
            SearchText.TextChanged += (_, _) =>
            {
                var append = !SuppressAutoComplete;
                SuppressAutoComplete = false;
                Filter(appendAutoComplete: append);
            };
            SearchText.KeyDown += SearchText_KeyDown;

            Results.BackColor = WinFormsTheme.InputBackground;
            Results.BorderStyle = BorderStyle.FixedSingle;
            Results.Cursor = Cursors.Default;
            Results.Dock = DockStyle.Top;
            Results.DrawMode = DrawMode.OwnerDrawFixed;
            Results.ForeColor = WinFormsTheme.Text;
            Results.IntegralHeight = false;
            Results.ItemHeight = 22;
            Results.Margin = Padding.Empty;
            Results.MouseClick += Results_MouseClick;
            Results.MouseMove += Results_MouseMove;
            Results.KeyDown += Results_KeyDown;
            Results.DrawItem += DrawResultItem;

            Controls.Add(Results);
            Controls.Add(SearchText);

            SearchText.Text = GetValueText(currentValue);
            SearchText.SelectAll();
            ShowMatches(Entries, GetCurrentValueIndex(currentValue));

            VisibleChanged += (_, _) =>
            {
                if (Visible)
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        SearchText.Focus();
                        SearchText.SelectAll();
                    }));
            };
        }

        private static int GetPreferredWidth(IReadOnlyList<StandardValueEntry> entries)
        {
            using var graphics = Graphics.FromHwnd(IntPtr.Zero);
            var sampleWidth = entries
                .Take(60)
                .Select(z => TextRenderer.MeasureText(graphics, z.Text, SystemFonts.MessageBoxFont).Width)
                .DefaultIfEmpty(260)
                .Max();
            return sampleWidth + SystemInformation.VerticalScrollBarWidth + 32;
        }

        private string GetValueText(object? value)
        {
            if (value == null)
                return string.Empty;

            return Converter.ConvertToString(Context, CultureInfo.CurrentCulture, value) ?? value.ToString() ?? string.Empty;
        }

        private int GetCurrentValueIndex(object? value)
        {
            if (value == null)
                return 0;

            for (var i = 0; i < Entries.Length; i++)
            {
                if (Equals(Entries[i].Value, value))
                    return i;
            }

            return 0;
        }

        private void Filter(bool appendAutoComplete)
        {
            if (UpdatingFilter)
                return;

            var userText = GetSearchText();
            var matches = GetMatches(userText).ToArray();
            var autoCompleteMatch = appendAutoComplete ? GetAutoCompleteMatch(userText, matches) : null;

            UpdatingFilter = true;
            if (autoCompleteMatch != null)
            {
                SearchText.Text = autoCompleteMatch.Text;
                var selectionStart = Math.Min(userText.Length, autoCompleteMatch.Text.Length);
                SelectSearchText(selectionStart, autoCompleteMatch.Text.Length - selectionStart);
            }
            else
            {
                SelectSearchText(Math.Min(userText.Length, SearchText.Text.Length), 0);
            }

            ShowMatches(matches, 0);
            UpdatingFilter = false;
        }

        private void ShowMatches(IReadOnlyList<StandardValueEntry> matches, int preferredSelectionIndex)
        {
            Results.BeginUpdate();
            Results.Items.Clear();
            foreach (var match in matches)
                Results.Items.Add(match);
            Results.EndUpdate();

            Results.SelectedIndex = Results.Items.Count == 0
                ? -1
                : Math.Clamp(preferredSelectionIndex, 0, Results.Items.Count - 1);
            ResizeForMatches(matches.Count);
        }

        private string GetSearchText()
        {
            var text = SearchText.Text;
            var selectionStart = GetSearchSelectionStart();
            return selectionStart <= 0 ? text : text[..selectionStart];
        }

        private IEnumerable<StandardValueEntry> GetMatches(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return Entries;

            return Entries
                .Where(entry => EntryMatches(entry, text))
                .OrderBy(entry => GetMatchRank(entry, text))
                .ThenBy(entry => entry.Text, StringComparer.OrdinalIgnoreCase);
        }

        private static bool EntryMatches(StandardValueEntry entry, string text)
        {
            return entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase) || EntryIDStartsWith(entry.Text, text);
        }

        private static int GetMatchRank(StandardValueEntry entry, string text)
        {
            if (entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                return 0;

            var open = entry.Text.LastIndexOf('(');
            if (open >= 0 && entry.Text[(open + 1)..].StartsWith(text, StringComparison.OrdinalIgnoreCase))
                return 1;

            return 2;
        }

        private static bool EntryIDStartsWith(string entryText, string text)
        {
            var open = entryText.LastIndexOf('(');
            var close = entryText.LastIndexOf(')');
            return open >= 0 &&
                close > open &&
                entryText.AsSpan(open + 1, close - open - 1).StartsWith(text, StringComparison.OrdinalIgnoreCase);
        }

        private static StandardValueEntry? GetAutoCompleteMatch(string text, IReadOnlyList<StandardValueEntry> matches)
        {
            text = text.Trim();
            return text.Length == 0
                ? null
                : matches.FirstOrDefault(entry => entry.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        }

        private void ResizeForMatches(int matchCount)
        {
            var visibleRows = Math.Clamp(matchCount, 1, MaxVisibleRows);
            Results.Height = visibleRows * Results.ItemHeight + 2;
            Height = SearchText.Height + Results.Height;
        }

        private void SearchText_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Back or Keys.Delete)
            {
                HandleDeleteKey(e);
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Enter:
                    CommitSelectedEntryOrText();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Escape:
                    Service.CloseDropDown();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        private void HandleDeleteKey(KeyEventArgs e)
        {
            SuppressAutoComplete = true;
            if (SearchText.SelectionLength == 0)
            {
                var caret = GetSearchSelectionStart();
                var canDelete = e.KeyCode == Keys.Back ? caret > 0 : caret < SearchText.Text.Length;
                if (!canDelete)
                    SuppressAutoComplete = false;
                return;
            }

            var text = SearchText.Text;
            var selectionStart = GetSearchSelectionStart();
            var newText = e.KeyCode == Keys.Back && selectionStart > 0
                ? text[..(selectionStart - 1)]
                : text[..selectionStart];

            UpdatingFilter = true;
            SearchText.Text = newText;
            SelectSearchText(newText.Length, 0);
            UpdatingFilter = false;
            SuppressAutoComplete = false;
            Filter(appendAutoComplete: false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void Results_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitSelectedEntry();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                Service.CloseDropDown();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void Results_MouseClick(object? sender, MouseEventArgs e)
        {
            var index = Results.IndexFromPoint(e.Location);
            if ((uint)index >= (uint)Results.Items.Count || Results.Items[index] is not StandardValueEntry entry)
                return;

            CommitEntry(entry);
        }

        private void Results_MouseMove(object? sender, MouseEventArgs e)
        {
            var index = Results.IndexFromPoint(e.Location);
            if ((uint)index < (uint)Results.Items.Count && Results.SelectedIndex != index)
                Results.SelectedIndex = index;
        }

        private void MoveSelection(int delta)
        {
            if (Results.Items.Count == 0)
                return;

            Results.SelectedIndex = Math.Clamp(Math.Max(0, Results.SelectedIndex) + delta, 0, Results.Items.Count - 1);
        }

        private void CommitSelectedEntryOrText()
        {
            if (Results.SelectedItem is StandardValueEntry)
            {
                CommitSelectedEntry();
                return;
            }

            var text = GetSearchText();
            try
            {
                SelectedValue = Converter.ConvertFrom(Context, CultureInfo.CurrentCulture, text);
                Committed = true;
            }
            catch
            {
                Committed = false;
            }

            Service.CloseDropDown();
        }

        private void CommitSelectedEntry()
        {
            if (Results.SelectedItem is not StandardValueEntry entry)
                return;

            CommitEntry(entry);
        }

        private void CommitEntry(StandardValueEntry entry)
        {
            SelectedValue = entry.Value;
            Committed = true;
            Service.CloseDropDown();
        }

        private static void DrawResultItem(object? sender, DrawItemEventArgs e)
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
                e.Font,
                new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private int GetSearchSelectionStart()
        {
            var textLength = SearchText.Text.Length;
            return Math.Clamp(SearchText.SelectionStart, 0, textLength);
        }

        private void SelectSearchText(int start, int length)
        {
            var textLength = SearchText.Text.Length;
            start = Math.Clamp(start, 0, textLength);
            length = Math.Clamp(length, 0, textLength - start);
            SearchText.Select(start, length);
        }
    }

    private sealed class SearchableValueListBox : ListBox
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var index = IndexFromPoint(e.Location);
            if ((uint)index < (uint)Items.Count && SelectedIndex != index)
                SelectedIndex = index;
            Cursor.Current = Cursors.Default;
        }
    }
}
