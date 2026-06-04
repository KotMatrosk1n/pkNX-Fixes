using System.Drawing;
using System.Windows.Forms;

namespace pkNX.WinForms;

public static class WinFormsTheme
{
    public static readonly Color WindowBackground = Color.FromArgb(51, 51, 51);
    public static readonly Color PanelBackground = Color.FromArgb(64, 64, 64);
    public static readonly Color InputBackground = Color.FromArgb(43, 43, 43);
    public static readonly Color AlternateRowBackground = Color.FromArgb(55, 55, 55);
    public static readonly Color Border = Color.FromArgb(118, 118, 118);
    public static readonly Color Text = Color.White;
    public static readonly Color MutedText = Color.FromArgb(220, 220, 220);
    public static readonly Color SelectionBackground = Color.FromArgb(10, 100, 173);
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
            case DataGridView dataGridView:
                Apply(dataGridView);
                break;
            case PropertyGrid propertyGrid:
                Apply(propertyGrid);
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
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 72, 72);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 80, 80);
    }

    public static void Apply(ComboBox comboBox)
    {
        comboBox.BackColor = InputBackground;
        comboBox.ForeColor = Text;
        comboBox.FlatStyle = FlatStyle.Popup;
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
