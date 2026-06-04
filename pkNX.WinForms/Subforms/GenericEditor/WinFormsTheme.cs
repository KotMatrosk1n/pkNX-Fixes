using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace pkNX.WinForms;

public static class WinFormsTheme
{
    public static readonly Color WindowBackground = Color.FromArgb(45, 46, 50);
    public static readonly Color PanelBackground = Color.FromArgb(58, 60, 65);
    public static readonly Color InputBackground = Color.FromArgb(37, 38, 42);
    public static readonly Color AlternateRowBackground = Color.FromArgb(51, 53, 58);
    public static readonly Color Border = Color.FromArgb(91, 94, 101);
    public static readonly Color Text = Color.FromArgb(245, 246, 248);
    public static readonly Color MutedText = Color.FromArgb(198, 203, 211);
    public static readonly Color DisabledText = Color.FromArgb(142, 148, 158);
    public static readonly Color SelectionBackground = Color.FromArgb(20, 111, 184);
    public static readonly Color SelectionText = Color.White;

    public static void Apply(Form form)
    {
        form.BackColor = WindowBackground;
        form.ForeColor = Text;
        Apply((Control)form);
    }

    public static void Apply(Control control)
    {
        switch (control)
        {
            case Button button:
                Apply(button);
                break;
            case ComboBox comboBox:
                Apply(comboBox);
                break;
            case TabControl tabControl:
                Apply(tabControl);
                break;
            case TabPage tabPage:
                tabPage.UseVisualStyleBackColor = false;
                tabPage.BackColor = WindowBackground;
                tabPage.ForeColor = Text;
                break;
            case DataGridView dataGridView:
                Apply(dataGridView);
                break;
            case PropertyGrid propertyGrid:
                Apply(propertyGrid);
                break;
            case ListBox listBox:
                listBox.BackColor = InputBackground;
                listBox.ForeColor = Text;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = InputBackground;
                numericUpDown.ForeColor = Text;
                break;
            case CheckBox checkBox:
                Apply(checkBox);
                break;
            case RadioButton radioButton:
                radioButton.UseVisualStyleBackColor = false;
                radioButton.FlatStyle = FlatStyle.Standard;
                radioButton.BackColor = WindowBackground;
                radioButton.ForeColor = radioButton.Enabled ? Text : DisabledText;
                break;
            case GroupBox groupBox:
                groupBox.BackColor = WindowBackground;
                groupBox.ForeColor = Text;
                break;
            case TextBoxBase textBox:
                textBox.BackColor = InputBackground;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case Panel or FlowLayoutPanel or UserControl:
                control.BackColor = WindowBackground;
                control.ForeColor = Text;
                break;
            default:
                control.BackColor = WindowBackground;
                control.ForeColor = Text;
                break;
        }

        foreach (Control child in control.Controls)
            Apply(child);
    }

    public static void Apply(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = PanelBackground;
        button.ForeColor = Text;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(68, 71, 77);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(75, 79, 86);
    }

    public static void Apply(ComboBox comboBox)
    {
        comboBox.BackColor = InputBackground;
        comboBox.ForeColor = Text;
        comboBox.FlatStyle = FlatStyle.Flat;
    }

    public static void Apply(CheckBox checkBox)
    {
        checkBox.UseVisualStyleBackColor = false;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.BackColor = WindowBackground;
        checkBox.ForeColor = checkBox.Enabled ? Text : DisabledText;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.FlatAppearance.BorderColor = Border;
        checkBox.FlatAppearance.CheckedBackColor = WindowBackground;
        checkBox.FlatAppearance.MouseOverBackColor = WindowBackground;
        checkBox.FlatAppearance.MouseDownBackColor = WindowBackground;
        checkBox.Paint -= DrawCheckBox;
        checkBox.Paint += DrawCheckBox;
    }

    private static void DrawCheckBox(object? sender, PaintEventArgs e)
    {
        if (sender is not CheckBox checkBox)
            return;

        e.Graphics.Clear(checkBox.BackColor);
        var boxSize = 13;
        var box = new Rectangle(0, Math.Max(0, (checkBox.Height - boxSize) / 2), boxSize, boxSize);
        var checkedBox = checkBox.CheckState == CheckState.Checked;
        var enabled = checkBox.Enabled;

        using (var fill = new SolidBrush(checkedBox && enabled ? SelectionBackground : InputBackground))
            e.Graphics.FillRectangle(fill, box);

        using (var border = new Pen(enabled ? Color.FromArgb(214, 219, 228) : Border))
            e.Graphics.DrawRectangle(border, box);

        if (checkedBox)
        {
            using var check = new Pen(enabled ? Color.White : DisabledText, 2f);
            check.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            check.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            e.Graphics.DrawLines(check,
            [
                new Point(box.Left + 3, box.Top + 7),
                new Point(box.Left + 6, box.Top + 10),
                new Point(box.Left + 11, box.Top + 3),
            ]);
        }

        var textRect = new Rectangle(box.Right + 6, 0, Math.Max(0, checkBox.Width - box.Right - 6), checkBox.Height);
        TextRenderer.DrawText(
            e.Graphics,
            checkBox.Text,
            checkBox.Font,
            textRect,
            enabled ? Text : DisabledText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    public static void Apply(TabControl tabControl)
    {
        tabControl.BackColor = WindowBackground;
        tabControl.ForeColor = Text;
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.DrawItem -= DrawTabItem;
        tabControl.DrawItem += DrawTabItem;
    }

    private static void DrawTabItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabControl || (uint)e.Index >= (uint)tabControl.TabPages.Count)
            return;

        var tabPage = tabControl.TabPages[e.Index];
        var selected = e.Index == tabControl.SelectedIndex;
        var backColor = selected ? PanelBackground : WindowBackground;
        var foreColor = selected ? Text : MutedText;

        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            tabPage.Text,
            tabControl.Font,
            e.Bounds,
            foreColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    public static void Apply(PropertyGrid grid)
    {
        grid.BackColor = WindowBackground;
        grid.CategoryForeColor = Text;
        grid.CategorySplitterColor = Border;
        grid.CommandsBackColor = WindowBackground;
        grid.CommandsBorderColor = Border;
        grid.CommandsForeColor = Text;
        grid.HelpBackColor = WindowBackground;
        grid.HelpBorderColor = Border;
        grid.HelpForeColor = MutedText;
        grid.LineColor = Border;
        grid.SelectedItemWithFocusBackColor = SelectionBackground;
        grid.SelectedItemWithFocusForeColor = SelectionText;
        grid.ViewBackColor = InputBackground;
        grid.ViewBorderColor = Border;
        grid.ViewForeColor = Text;
    }

    public static void Apply(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = WindowBackground;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Border;

        grid.DefaultCellStyle.BackColor = InputBackground;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = SelectionBackground;
        grid.DefaultCellStyle.SelectionForeColor = SelectionText;

        grid.AlternatingRowsDefaultCellStyle.BackColor = AlternateRowBackground;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Text;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = SelectionBackground;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = SelectionText;

        grid.ColumnHeadersDefaultCellStyle.BackColor = PanelBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = PanelBackground;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;

        grid.RowHeadersDefaultCellStyle.BackColor = PanelBackground;
        grid.RowHeadersDefaultCellStyle.ForeColor = Text;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = SelectionBackground;
        grid.RowHeadersDefaultCellStyle.SelectionForeColor = SelectionText;

        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.DefaultCellStyle.BackColor = InputBackground;
            column.DefaultCellStyle.ForeColor = Text;
            column.DefaultCellStyle.SelectionBackColor = SelectionBackground;
            column.DefaultCellStyle.SelectionForeColor = SelectionText;

            if (column is DataGridViewButtonColumn buttonColumn)
            {
                buttonColumn.FlatStyle = FlatStyle.Flat;
                buttonColumn.DefaultCellStyle.BackColor = PanelBackground;
                buttonColumn.DefaultCellStyle.SelectionBackColor = SelectionBackground;
                buttonColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }
    }

    public static void Apply(ContextMenuStrip menu)
    {
        menu.BackColor = InputBackground;
        menu.ForeColor = Text;
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());

        foreach (ToolStripItem item in menu.Items)
        {
            item.BackColor = InputBackground;
            item.ForeColor = Text;
        }
    }

    public static void AddThemedTabStrip(TabControl tabControl, int headerHeight = 30, int left = 7, int top = 5, int buttonHeight = 24)
    {
        var parent = tabControl.Parent;
        if (parent == null)
            return;

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
            BackColor = WindowBackground,
            Bounds = new Rectangle(originalBounds.Left, originalBounds.Top, originalBounds.Width, headerHeight),
        };
        header.Paint += (_, e) =>
        {
            using var border = new Pen(Border);
            e.Graphics.DrawLine(border, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        _ = AddThemedTabButtons(header, tabControl, left, top, buttonHeight);

        parent.Controls.Add(header);
        header.BringToFront();
    }

    public static Button[] AddThemedTabButtons(Control parent, TabControl tabControl, int left, int top, int height, int minimumWidth = 58, int horizontalPadding = 18)
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

    public static void UpdateThemedTabButtons(IReadOnlyList<Button> buttons, int selectedIndex)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            var selected = i == selectedIndex;
            var button = buttons[i];
            button.BackColor = selected ? PanelBackground : WindowBackground;
            button.ForeColor = selected ? Text : MutedText;
            button.FlatAppearance.BorderColor = selected ? SelectionBackground : Border;
            button.FlatAppearance.MouseOverBackColor = selected ? PanelBackground : Color.FromArgb(53, 55, 60);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(65, 68, 74);
        }
    }

    private sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => InputBackground;
        public override Color ImageMarginGradientBegin => InputBackground;
        public override Color ImageMarginGradientMiddle => InputBackground;
        public override Color ImageMarginGradientEnd => InputBackground;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => SelectionBackground;
        public override Color MenuItemSelected => SelectionBackground;
        public override Color MenuItemSelectedGradientBegin => SelectionBackground;
        public override Color MenuItemSelectedGradientEnd => SelectionBackground;
        public override Color MenuItemPressedGradientBegin => PanelBackground;
        public override Color MenuItemPressedGradientMiddle => PanelBackground;
        public override Color MenuItemPressedGradientEnd => PanelBackground;
        public override Color CheckBackground => PanelBackground;
        public override Color CheckSelectedBackground => SelectionBackground;
        public override Color CheckPressedBackground => SelectionBackground;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
