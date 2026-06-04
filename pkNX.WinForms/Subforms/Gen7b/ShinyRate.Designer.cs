namespace pkNX.WinForms
{
    partial class ShinyRate
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.B_Save = new System.Windows.Forms.Button();
            this.B_Cancel = new System.Windows.Forms.Button();
            this.RB_Default = new System.Windows.Forms.RadioButton();
            this.RB_Fixed = new System.Windows.Forms.RadioButton();
            this.RB_Always = new System.Windows.Forms.RadioButton();
            this.GB_Mode = new System.Windows.Forms.GroupBox();
            this.L_ModeDescription = new System.Windows.Forms.Label();
            this.GB_Rerolls = new System.Windows.Forms.GroupBox();
            this.L_CurrentOdds = new System.Windows.Forms.Label();
            this.L_FixedRollCount = new System.Windows.Forms.Label();
            this.NUD_Rerolls = new System.Windows.Forms.NumericUpDown();
            this.L_Overall = new System.Windows.Forms.Label();
            this.L_OverallCaption = new System.Windows.Forms.Label();
            this.GB_RerollHelper = new System.Windows.Forms.GroupBox();
            this.B_ApplyTarget = new System.Windows.Forms.Button();
            this.L_TargetPercent = new System.Windows.Forms.Label();
            this.NUD_Rate = new System.Windows.Forms.NumericUpDown();
            this.L_RerollCount = new System.Windows.Forms.Label();
            this.GB_Presets = new System.Windows.Forms.GroupBox();
            this.B_Preset100 = new System.Windows.Forms.Button();
            this.B_Preset512 = new System.Windows.Forms.Button();
            this.B_Preset1365 = new System.Windows.Forms.Button();
            this.B_Preset4096 = new System.Windows.Forms.Button();
            this.L_Note = new System.Windows.Forms.Label();
            this.Tips = new System.Windows.Forms.ToolTip(this.components);
            this.GB_Mode.SuspendLayout();
            this.GB_Rerolls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NUD_Rerolls)).BeginInit();
            this.GB_RerollHelper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NUD_Rate)).BeginInit();
            this.GB_Presets.SuspendLayout();
            this.SuspendLayout();
            // 
            // B_Save
            // 
            this.B_Save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.B_Save.Location = new System.Drawing.Point(392, 309);
            this.B_Save.Name = "B_Save";
            this.B_Save.Size = new System.Drawing.Size(92, 30);
            this.B_Save.TabIndex = 5;
            this.B_Save.Text = "Save";
            this.B_Save.UseVisualStyleBackColor = true;
            this.B_Save.Click += new System.EventHandler(this.B_Save_Click);
            // 
            // B_Cancel
            // 
            this.B_Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.B_Cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.B_Cancel.Location = new System.Drawing.Point(294, 309);
            this.B_Cancel.Name = "B_Cancel";
            this.B_Cancel.Size = new System.Drawing.Size(92, 30);
            this.B_Cancel.TabIndex = 6;
            this.B_Cancel.Text = "Cancel";
            this.B_Cancel.UseVisualStyleBackColor = true;
            this.B_Cancel.Click += new System.EventHandler(this.B_Cancel_Click);
            // 
            // RB_Default
            // 
            this.RB_Default.AutoSize = true;
            this.RB_Default.Location = new System.Drawing.Point(16, 24);
            this.RB_Default.Name = "RB_Default";
            this.RB_Default.Size = new System.Drawing.Size(112, 19);
            this.RB_Default.TabIndex = 0;
            this.RB_Default.TabStop = true;
            this.RB_Default.Text = "Default Logic";
            this.RB_Default.UseVisualStyleBackColor = true;
            this.RB_Default.CheckedChanged += new System.EventHandler(this.ChangeSelection);
            // 
            // RB_Fixed
            // 
            this.RB_Fixed.AutoSize = true;
            this.RB_Fixed.Location = new System.Drawing.Point(16, 50);
            this.RB_Fixed.Name = "RB_Fixed";
            this.RB_Fixed.Size = new System.Drawing.Size(109, 19);
            this.RB_Fixed.TabIndex = 1;
            this.RB_Fixed.TabStop = true;
            this.RB_Fixed.Text = "Fixed PID Rolls";
            this.RB_Fixed.UseVisualStyleBackColor = true;
            this.RB_Fixed.CheckedChanged += new System.EventHandler(this.ChangeSelection);
            // 
            // RB_Always
            // 
            this.RB_Always.AutoSize = true;
            this.RB_Always.Location = new System.Drawing.Point(16, 76);
            this.RB_Always.Name = "RB_Always";
            this.RB_Always.Size = new System.Drawing.Size(94, 19);
            this.RB_Always.TabIndex = 2;
            this.RB_Always.TabStop = true;
            this.RB_Always.Text = "Always Shiny";
            this.RB_Always.UseVisualStyleBackColor = true;
            this.RB_Always.CheckedChanged += new System.EventHandler(this.ChangeSelection);
            // 
            // GB_Mode
            // 
            this.GB_Mode.Controls.Add(this.L_ModeDescription);
            this.GB_Mode.Controls.Add(this.RB_Default);
            this.GB_Mode.Controls.Add(this.RB_Fixed);
            this.GB_Mode.Controls.Add(this.RB_Always);
            this.GB_Mode.Location = new System.Drawing.Point(14, 84);
            this.GB_Mode.Name = "GB_Mode";
            this.GB_Mode.Size = new System.Drawing.Size(220, 114);
            this.GB_Mode.TabIndex = 1;
            this.GB_Mode.TabStop = false;
            this.GB_Mode.Text = "Mode";
            // 
            // L_ModeDescription
            // 
            this.L_ModeDescription.Location = new System.Drawing.Point(126, 24);
            this.L_ModeDescription.Name = "L_ModeDescription";
            this.L_ModeDescription.Size = new System.Drawing.Size(83, 68);
            this.L_ModeDescription.TabIndex = 3;
            this.L_ModeDescription.Text = "Mode description";
            // 
            // GB_Rerolls
            // 
            this.GB_Rerolls.Controls.Add(this.L_CurrentOdds);
            this.GB_Rerolls.Controls.Add(this.L_FixedRollCount);
            this.GB_Rerolls.Controls.Add(this.NUD_Rerolls);
            this.GB_Rerolls.Controls.Add(this.L_Overall);
            this.GB_Rerolls.Controls.Add(this.L_OverallCaption);
            this.GB_Rerolls.Location = new System.Drawing.Point(248, 84);
            this.GB_Rerolls.Name = "GB_Rerolls";
            this.GB_Rerolls.Size = new System.Drawing.Size(236, 114);
            this.GB_Rerolls.TabIndex = 2;
            this.GB_Rerolls.TabStop = false;
            this.GB_Rerolls.Text = "Fixed PID Rolls";
            // 
            // L_CurrentOdds
            // 
            this.L_CurrentOdds.AutoSize = true;
            this.L_CurrentOdds.Location = new System.Drawing.Point(16, 78);
            this.L_CurrentOdds.Name = "L_CurrentOdds";
            this.L_CurrentOdds.Size = new System.Drawing.Size(84, 15);
            this.L_CurrentOdds.TabIndex = 4;
            this.L_CurrentOdds.Text = "Approx. odds";
            // 
            // L_FixedRollCount
            // 
            this.L_FixedRollCount.AutoSize = true;
            this.L_FixedRollCount.Location = new System.Drawing.Point(16, 26);
            this.L_FixedRollCount.Name = "L_FixedRollCount";
            this.L_FixedRollCount.Size = new System.Drawing.Size(66, 15);
            this.L_FixedRollCount.TabIndex = 0;
            this.L_FixedRollCount.Text = "Roll count:";
            // 
            // NUD_Rerolls
            // 
            this.NUD_Rerolls.Location = new System.Drawing.Point(132, 23);
            this.NUD_Rerolls.Maximum = new decimal(new int[] {
            4091,
            0,
            0,
            0});
            this.NUD_Rerolls.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.NUD_Rerolls.Name = "NUD_Rerolls";
            this.NUD_Rerolls.Size = new System.Drawing.Size(78, 23);
            this.NUD_Rerolls.TabIndex = 0;
            this.NUD_Rerolls.Value = new decimal(new int[] {
            125,
            0,
            0,
            0});
            this.NUD_Rerolls.ValueChanged += new System.EventHandler(this.ChangeRerollCount);
            // 
            // L_Overall
            // 
            this.L_Overall.AutoSize = true;
            this.L_Overall.Location = new System.Drawing.Point(132, 54);
            this.L_Overall.Name = "L_Overall";
            this.L_Overall.Size = new System.Drawing.Size(29, 15);
            this.L_Overall.TabIndex = 3;
            this.L_Overall.Text = "PCT";
            // 
            // L_OverallCaption
            // 
            this.L_OverallCaption.AutoSize = true;
            this.L_OverallCaption.Location = new System.Drawing.Point(16, 54);
            this.L_OverallCaption.Name = "L_OverallCaption";
            this.L_OverallCaption.Size = new System.Drawing.Size(87, 15);
            this.L_OverallCaption.TabIndex = 2;
            this.L_OverallCaption.Text = "Overall chance:";
            // 
            // GB_RerollHelper
            // 
            this.GB_RerollHelper.Controls.Add(this.B_ApplyTarget);
            this.GB_RerollHelper.Controls.Add(this.L_TargetPercent);
            this.GB_RerollHelper.Controls.Add(this.NUD_Rate);
            this.GB_RerollHelper.Controls.Add(this.L_RerollCount);
            this.GB_RerollHelper.Location = new System.Drawing.Point(14, 210);
            this.GB_RerollHelper.Name = "GB_RerollHelper";
            this.GB_RerollHelper.Size = new System.Drawing.Size(308, 82);
            this.GB_RerollHelper.TabIndex = 3;
            this.GB_RerollHelper.TabStop = false;
            this.GB_RerollHelper.Text = "Target Helper";
            // 
            // B_ApplyTarget
            // 
            this.B_ApplyTarget.Location = new System.Drawing.Point(210, 46);
            this.B_ApplyTarget.Name = "B_ApplyTarget";
            this.B_ApplyTarget.Size = new System.Drawing.Size(82, 25);
            this.B_ApplyTarget.TabIndex = 1;
            this.B_ApplyTarget.Text = "Use Count";
            this.B_ApplyTarget.UseVisualStyleBackColor = true;
            this.B_ApplyTarget.Click += new System.EventHandler(this.B_ApplyTarget_Click);
            // 
            // L_TargetPercent
            // 
            this.L_TargetPercent.AutoSize = true;
            this.L_TargetPercent.Location = new System.Drawing.Point(16, 27);
            this.L_TargetPercent.Name = "L_TargetPercent";
            this.L_TargetPercent.Size = new System.Drawing.Size(128, 15);
            this.L_TargetPercent.TabIndex = 0;
            this.L_TargetPercent.Text = "Target overall chance:";
            // 
            // NUD_Rate
            // 
            this.NUD_Rate.DecimalPlaces = 2;
            this.NUD_Rate.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.NUD_Rate.Location = new System.Drawing.Point(150, 24);
            this.NUD_Rate.Maximum = new decimal(new int[] {
            6320,
            0,
            0,
            131072});
            this.NUD_Rate.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.NUD_Rate.Name = "NUD_Rate";
            this.NUD_Rate.Size = new System.Drawing.Size(60, 23);
            this.NUD_Rate.TabIndex = 0;
            this.NUD_Rate.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.NUD_Rate.ValueChanged += new System.EventHandler(this.ChangePercent);
            // 
            // L_RerollCount
            // 
            this.L_RerollCount.AutoSize = true;
            this.L_RerollCount.Location = new System.Drawing.Point(16, 51);
            this.L_RerollCount.Name = "L_RerollCount";
            this.L_RerollCount.Size = new System.Drawing.Size(51, 15);
            this.L_RerollCount.TabIndex = 2;
            this.L_RerollCount.Text = "Count: 0";
            // 
            // GB_Presets
            // 
            this.GB_Presets.Controls.Add(this.B_Preset100);
            this.GB_Presets.Controls.Add(this.B_Preset512);
            this.GB_Presets.Controls.Add(this.B_Preset1365);
            this.GB_Presets.Controls.Add(this.B_Preset4096);
            this.GB_Presets.Location = new System.Drawing.Point(336, 210);
            this.GB_Presets.Name = "GB_Presets";
            this.GB_Presets.Size = new System.Drawing.Size(148, 82);
            this.GB_Presets.TabIndex = 4;
            this.GB_Presets.TabStop = false;
            this.GB_Presets.Text = "Quick Odds";
            // 
            // B_Preset100
            // 
            this.B_Preset100.Location = new System.Drawing.Point(78, 49);
            this.B_Preset100.Name = "B_Preset100";
            this.B_Preset100.Size = new System.Drawing.Size(58, 24);
            this.B_Preset100.TabIndex = 3;
            this.B_Preset100.Tag = 100;
            this.B_Preset100.Text = "1:100";
            this.B_Preset100.UseVisualStyleBackColor = true;
            this.B_Preset100.Click += new System.EventHandler(this.Preset_Click);
            // 
            // B_Preset512
            // 
            this.B_Preset512.Location = new System.Drawing.Point(14, 49);
            this.B_Preset512.Name = "B_Preset512";
            this.B_Preset512.Size = new System.Drawing.Size(58, 24);
            this.B_Preset512.TabIndex = 2;
            this.B_Preset512.Tag = 512;
            this.B_Preset512.Text = "1:512";
            this.B_Preset512.UseVisualStyleBackColor = true;
            this.B_Preset512.Click += new System.EventHandler(this.Preset_Click);
            // 
            // B_Preset1365
            // 
            this.B_Preset1365.Location = new System.Drawing.Point(78, 22);
            this.B_Preset1365.Name = "B_Preset1365";
            this.B_Preset1365.Size = new System.Drawing.Size(58, 24);
            this.B_Preset1365.TabIndex = 1;
            this.B_Preset1365.Tag = 1365;
            this.B_Preset1365.Text = "1:1365";
            this.B_Preset1365.UseVisualStyleBackColor = true;
            this.B_Preset1365.Click += new System.EventHandler(this.Preset_Click);
            // 
            // B_Preset4096
            // 
            this.B_Preset4096.Location = new System.Drawing.Point(14, 22);
            this.B_Preset4096.Name = "B_Preset4096";
            this.B_Preset4096.Size = new System.Drawing.Size(58, 24);
            this.B_Preset4096.TabIndex = 0;
            this.B_Preset4096.Tag = 4096;
            this.B_Preset4096.Text = "1:4096";
            this.B_Preset4096.UseVisualStyleBackColor = true;
            this.B_Preset4096.Click += new System.EventHandler(this.Preset_Click);
            // 
            // L_Note
            // 
            this.L_Note.Location = new System.Drawing.Point(14, 14);
            this.L_Note.Name = "L_Note";
            this.L_Note.Size = new System.Drawing.Size(470, 58);
            this.L_Note.TabIndex = 0;
            this.L_Note.Text = "Changing this value patches how many PID attempts the game makes. It does not rew" +
    "rite the IsShiny check itself; it only changes how many chances that check gets." +
    "\r\n\r\nFixed rolls are limited to 1-4091. Use Always Shiny for a guaranteed SWSH pa" +
    "tch.";
            // 
            // ShinyRate
            // 
            this.AcceptButton = this.B_Save;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.B_Cancel;
            this.ClientSize = new System.Drawing.Size(498, 351);
            this.Controls.Add(this.GB_Presets);
            this.Controls.Add(this.B_Cancel);
            this.Controls.Add(this.GB_Mode);
            this.Controls.Add(this.GB_Rerolls);
            this.Controls.Add(this.GB_RerollHelper);
            this.Controls.Add(this.L_Note);
            this.Controls.Add(this.B_Save);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShinyRate";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Shiny Rate";
            this.GB_Mode.ResumeLayout(false);
            this.GB_Mode.PerformLayout();
            this.GB_Rerolls.ResumeLayout(false);
            this.GB_Rerolls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NUD_Rerolls)).EndInit();
            this.GB_RerollHelper.ResumeLayout(false);
            this.GB_RerollHelper.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.NUD_Rate)).EndInit();
            this.GB_Presets.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button B_Save;
        private System.Windows.Forms.Button B_Cancel;
        private System.Windows.Forms.RadioButton RB_Default;
        private System.Windows.Forms.RadioButton RB_Fixed;
        private System.Windows.Forms.RadioButton RB_Always;
        private System.Windows.Forms.GroupBox GB_Mode;
        private System.Windows.Forms.Label L_ModeDescription;
        private System.Windows.Forms.GroupBox GB_Rerolls;
        private System.Windows.Forms.Label L_CurrentOdds;
        private System.Windows.Forms.Label L_FixedRollCount;
        private System.Windows.Forms.NumericUpDown NUD_Rerolls;
        private System.Windows.Forms.Label L_Overall;
        private System.Windows.Forms.Label L_OverallCaption;
        private System.Windows.Forms.GroupBox GB_RerollHelper;
        private System.Windows.Forms.Button B_ApplyTarget;
        private System.Windows.Forms.Label L_TargetPercent;
        private System.Windows.Forms.NumericUpDown NUD_Rate;
        private System.Windows.Forms.Label L_RerollCount;
        private System.Windows.Forms.GroupBox GB_Presets;
        private System.Windows.Forms.Button B_Preset100;
        private System.Windows.Forms.Button B_Preset512;
        private System.Windows.Forms.Button B_Preset1365;
        private System.Windows.Forms.Button B_Preset4096;
        private System.Windows.Forms.Label L_Note;
        private System.Windows.Forms.ToolTip Tips;
    }
}
