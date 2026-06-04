using System.Drawing;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class ThemedConfirmationDialog : Form
{
    private readonly Button ConfirmButton = new();
    private readonly Button CancelEditorButton = new();

    private ThemedConfirmationDialog(string title, string message, string confirmText)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 190);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 2,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = WinFormsTheme.Text,
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };

        ConfigureButton(ConfirmButton, confirmText, DialogResult.OK);
        ConfigureButton(CancelEditorButton, "Cancel", DialogResult.Cancel);
        buttons.Controls.Add(CancelEditorButton);
        buttons.Controls.Add(ConfirmButton);

        root.Controls.Add(messageLabel, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);

        AcceptButton = ConfirmButton;
        CancelButton = CancelEditorButton;
        WinFormsTheme.Apply(this);
    }

    public static bool Show(IWin32Window owner, string title, string message, string confirmText)
    {
        using var dialog = new ThemedConfirmationDialog(title, message, confirmText);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    private static void ConfigureButton(Button button, string text, DialogResult result)
    {
        button.DialogResult = result;
        button.Height = 30;
        button.Margin = new Padding(8, 0, 0, 0);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
        button.Width = 112;
    }
}
