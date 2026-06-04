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
                checkBox.UseVisualStyleBackColor = false;
                checkBox.BackColor = WindowBackground;
                checkBox.ForeColor = checkBox.Enabled ? Text : DisabledText;
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
}
