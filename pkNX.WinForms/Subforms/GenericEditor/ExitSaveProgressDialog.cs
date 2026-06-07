using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace pkNX.WinForms;

public sealed class ExitSaveProgressDialog : Form
{
    private const int MinimumStepDurationMs = 350;

    private readonly IReadOnlyList<string> PendingEdits;
    private readonly Action<Action<string>?, Action<string>?> CloseEditor;
    private readonly Label StatusLabel = new();
    private readonly Label DetailLabel = new();
    private readonly ListBox StepList = new();
    private readonly ProgressBar Progress = new();
    private readonly Button DoneButton = new();

    private int CurrentStepIndex = -1;
    private bool IsRunning = true;
    private bool Succeeded;
    private long StepStartTimestamp;
    private int SavedEditCount;

    private bool HasModifiedFiles => PendingEdits.Count > 0;

    private ExitSaveProgressDialog(IReadOnlyList<string> pendingEdits, Action<Action<string>?, Action<string>?> closeEditor)
    {
        PendingEdits = pendingEdits;
        CloseEditor = closeEditor;

        Text = "Finishing Edits";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(680, 320);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        StatusLabel.Dock = DockStyle.Fill;
        StatusLabel.Font = new Font(Font, FontStyle.Bold);
        StatusLabel.Text = "Making saved edits...";
        StatusLabel.TextAlign = ContentAlignment.MiddleLeft;

        DetailLabel.Dock = DockStyle.Fill;
        DetailLabel.ForeColor = WinFormsTheme.MutedText;
        DetailLabel.Text = "pkNX is finishing the editor changes before exit.";
        DetailLabel.TextAlign = ContentAlignment.MiddleLeft;

        StepList.Dock = DockStyle.Fill;
        StepList.HorizontalScrollbar = true;
        StepList.IntegralHeight = false;

        Progress.Dock = DockStyle.Fill;
        Progress.Maximum = HasModifiedFiles ? PendingEdits.Count + 1 : 1;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };

        DoneButton.DialogResult = DialogResult.OK;
        DoneButton.Enabled = false;
        DoneButton.Height = 30;
        DoneButton.Margin = new Padding(8, 0, 0, 0);
        DoneButton.Text = "OK";
        DoneButton.UseVisualStyleBackColor = false;
        DoneButton.Width = 112;
        buttonPanel.Controls.Add(DoneButton);

        root.Controls.Add(StatusLabel, 0, 0);
        root.Controls.Add(DetailLabel, 0, 1);
        root.Controls.Add(StepList, 0, 2);
        root.Controls.Add(Progress, 0, 3);
        root.Controls.Add(buttonPanel, 0, 4);
        Controls.Add(root);

        AcceptButton = DoneButton;
        CancelButton = DoneButton;
        ControlBox = false;
        Shown += (_, _) => BeginInvoke((MethodInvoker)RunExitSequence);
        WinFormsTheme.Apply(this);
        DetailLabel.ForeColor = WinFormsTheme.MutedText;
    }

    public static bool Show(IWin32Window? owner, IReadOnlyList<string> pendingEdits, Action<Action<string>?, Action<string>?> closeEditor)
    {
        using var dialog = new ExitSaveProgressDialog(pendingEdits, closeEditor);
        _ = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return dialog.Succeeded;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (IsRunning)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private void RunExitSequence()
    {
        try
        {
            if (HasModifiedFiles)
                RunModifiedSequence();
            else
                RunNoChangeSequence();

            Succeeded = true;
            DoneButton.Enabled = true;
            IsRunning = false;
            ControlBox = true;
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            Succeeded = false;
            IsRunning = false;
            ControlBox = true;
            DoneButton.Enabled = true;
            DoneButton.Text = "Cancel Exit";
            DoneButton.DialogResult = DialogResult.Cancel;
            StatusLabel.Text = "Exit was paused.";
            DetailLabel.Text = ex.Message;
            AddStep("[!] Could not finish saved edits.");
            System.Media.SystemSounds.Hand.Play();
        }
    }

    private void RunModifiedSequence()
    {
        DetailLabel.Text = PendingEdits.Count == 1
            ? "pkNX is writing 1 edited data group."
            : $"pkNX is writing {PendingEdits.Count} edited data groups.";
        PumpUi();

        CloseEditor(BeginSavingEdit, CompleteSavingEdit);
        CompleteVisualStep("Finalizing pkNX state");
        StatusLabel.Text = "Saved edits are complete.";
        DetailLabel.Text = "It is now safe to exit.";
    }

    private void RunNoChangeSequence()
    {
        BeginStep("Checking open editor data");
        CompleteStep("Open editor data checked");
        CloseEditor(BeginSavingEdit, CompleteSavingEdit);
        if (SavedEditCount == 0)
        {
            CompleteVisualStep("No changes were made");
            StatusLabel.Text = "No changes were made.";
            DetailLabel.Text = "It is safe to exit.";
            return;
        }

        CompleteVisualStep("Finalizing pkNX state");
        StatusLabel.Text = "Saved edits are complete.";
        DetailLabel.Text = "It is now safe to exit.";
    }

    private void CompleteVisualStep(string text)
    {
        BeginStep(text);
        CompleteStep(text);
    }

    private void BeginStep(string text)
    {
        if (Progress.Value >= Progress.Maximum)
            Progress.Maximum = Progress.Value + 1;

        CurrentStepIndex = AddStep("[>] " + text);
        StepStartTimestamp = Stopwatch.GetTimestamp();
        PumpUi();
    }

    private void BeginSavingEdit(string label)
    {
        BeginStep("Applying " + label);
    }

    private void CompleteSavingEdit(string label)
    {
        SavedEditCount++;
        CompleteStep("Applied " + label);
    }

    private void CompleteStep(string text)
    {
        WaitForReadableStep();
        if (CurrentStepIndex >= 0 && CurrentStepIndex < StepList.Items.Count)
            StepList.Items[CurrentStepIndex] = "[OK] " + text;

        Progress.Value = Math.Min(Progress.Value + 1, Progress.Maximum);
        PumpUi();
    }

    private int AddStep(string text)
    {
        var index = StepList.Items.Add(text);
        StepList.SelectedIndex = index;
        return index;
    }

    private void WaitForReadableStep()
    {
        var elapsed = (Stopwatch.GetTimestamp() - StepStartTimestamp) * 1000d / Stopwatch.Frequency;
        var remaining = MinimumStepDurationMs - (int)elapsed;
        if (remaining > 0)
            Thread.Sleep(remaining);
    }

    private void PumpUi()
    {
        StepList.Refresh();
        Progress.Refresh();
        StatusLabel.Refresh();
        DetailLabel.Refresh();
        Application.DoEvents();
    }
}
