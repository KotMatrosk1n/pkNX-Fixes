namespace pkNX.WinForms
{
    partial class GenericEditor<T>
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
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            B_Save = new System.Windows.Forms.Button();
            CB_EntryName = new EntrySelectorComboBox();
            B_Dump = new System.Windows.Forms.Button();
            B_Rand = new System.Windows.Forms.Button();
            B_AddEntry = new System.Windows.Forms.Button();
            HeaderLayout = new System.Windows.Forms.TableLayoutPanel();
            ContentPanel = new System.Windows.Forms.Panel();
            RootLayout = new System.Windows.Forms.TableLayoutPanel();
            Grid = new System.Windows.Forms.PropertyGrid();
            ContentPanel.SuspendLayout();
            RootLayout.SuspendLayout();
            HeaderLayout.SuspendLayout();
            SuspendLayout();
            // 
            // B_Save
            // 
            B_Save.Dock = System.Windows.Forms.DockStyle.Fill;
            B_Save.Location = new System.Drawing.Point(606, 5);
            B_Save.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            B_Save.Name = "B_Save";
            B_Save.Size = new System.Drawing.Size(117, 44);
            B_Save.TabIndex = 1;
            B_Save.Text = "Save";
            B_Save.UseVisualStyleBackColor = true;
            B_Save.Click += B_Save_Click;
            // 
            // CB_EntryName
            // 
            CB_EntryName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            CB_EntryName.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.None;
            CB_EntryName.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.None;
            CB_EntryName.Dock = System.Windows.Forms.DockStyle.Fill;
            CB_EntryName.FormattingEnabled = true;
            CB_EntryName.Location = new System.Drawing.Point(8, 9);
            CB_EntryName.Margin = new System.Windows.Forms.Padding(8, 9, 10, 8);
            CB_EntryName.Name = "CB_EntryName";
            CB_EntryName.Size = new System.Drawing.Size(584, 33);
            CB_EntryName.TabIndex = 2;
            CB_EntryName.SelectedIndexChanged += CB_EntryName_SelectedIndexChanged;
            // 
            // B_Dump
            // 
            B_Dump.Dock = System.Windows.Forms.DockStyle.Fill;
            B_Dump.Location = new System.Drawing.Point(731, 5);
            B_Dump.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            B_Dump.Name = "B_Dump";
            B_Dump.Size = new System.Drawing.Size(117, 44);
            B_Dump.TabIndex = 3;
            B_Dump.Text = "Dump";
            B_Dump.UseVisualStyleBackColor = true;
            B_Dump.Click += B_Dump_Click;
            // 
            // B_Rand
            // 
            B_Rand.Dock = System.Windows.Forms.DockStyle.Fill;
            B_Rand.Location = new System.Drawing.Point(856, 5);
            B_Rand.Margin = new System.Windows.Forms.Padding(4, 5, 8, 5);
            B_Rand.Name = "B_Rand";
            B_Rand.Size = new System.Drawing.Size(117, 44);
            B_Rand.TabIndex = 4;
            B_Rand.Text = "Randomize";
            B_Rand.UseVisualStyleBackColor = true;
            B_Rand.Visible = false;
            // 
            // B_AddEntry
            // 
            B_AddEntry.Dock = System.Windows.Forms.DockStyle.Fill;
            B_AddEntry.Location = new System.Drawing.Point(981, 5);
            B_AddEntry.Margin = new System.Windows.Forms.Padding(4, 5, 8, 5);
            B_AddEntry.Name = "B_AddEntry";
            B_AddEntry.Size = new System.Drawing.Size(117, 44);
            B_AddEntry.TabIndex = 5;
            B_AddEntry.Text = "Add Entry";
            B_AddEntry.UseVisualStyleBackColor = true;
            B_AddEntry.Visible = false;
            //
            // HeaderLayout
            //
            HeaderLayout.ColumnCount = 5;
            HeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            HeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            HeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            HeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            HeaderLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            HeaderLayout.Controls.Add(CB_EntryName, 0, 0);
            HeaderLayout.Controls.Add(B_Save, 1, 0);
            HeaderLayout.Controls.Add(B_Dump, 2, 0);
            HeaderLayout.Controls.Add(B_Rand, 3, 0);
            HeaderLayout.Controls.Add(B_AddEntry, 4, 0);
            HeaderLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            HeaderLayout.Location = new System.Drawing.Point(0, 0);
            HeaderLayout.Margin = new System.Windows.Forms.Padding(0);
            HeaderLayout.Name = "HeaderLayout";
            HeaderLayout.RowCount = 1;
            HeaderLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            HeaderLayout.Size = new System.Drawing.Size(983, 54);
            HeaderLayout.TabIndex = 6;
            //
            // ContentPanel
            //
            ContentPanel.Controls.Add(Grid);
            ContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            ContentPanel.Location = new System.Drawing.Point(0, 54);
            ContentPanel.Margin = new System.Windows.Forms.Padding(0);
            ContentPanel.Name = "ContentPanel";
            ContentPanel.Size = new System.Drawing.Size(983, 415);
            ContentPanel.TabIndex = 8;
            //
            // RootLayout
            //
            RootLayout.ColumnCount = 1;
            RootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            RootLayout.Controls.Add(HeaderLayout, 0, 0);
            RootLayout.Controls.Add(ContentPanel, 0, 1);
            RootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            RootLayout.Location = new System.Drawing.Point(0, 0);
            RootLayout.Name = "RootLayout";
            RootLayout.RowCount = 2;
            RootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            RootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            RootLayout.Size = new System.Drawing.Size(983, 469);
            RootLayout.TabIndex = 9;
            // 
            // Grid
            // 
            Grid.BackColor = System.Drawing.SystemColors.Control;
            Grid.Dock = System.Windows.Forms.DockStyle.Fill;
            Grid.Location = new System.Drawing.Point(0, 0);
            Grid.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            Grid.Name = "Grid";
            Grid.Size = new System.Drawing.Size(983, 415);
            Grid.TabIndex = 7;
            // 
            // GenericEditor
            // 
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            ClientSize = new System.Drawing.Size(983, 469);
            Controls.Add(RootLayout);
            Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            MinimumSize = new System.Drawing.Size(735, 525);
            Name = "GenericEditor";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "GenericEditor";
            ContentPanel.ResumeLayout(false);
            RootLayout.ResumeLayout(false);
            HeaderLayout.ResumeLayout(false);
            HeaderLayout.PerformLayout();
            ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button B_Save;
        private System.Windows.Forms.ComboBox CB_EntryName;
        private System.Windows.Forms.Button B_Dump;
        private System.Windows.Forms.Button B_Rand;
        private System.Windows.Forms.Button B_AddEntry;
        private System.Windows.Forms.TableLayoutPanel HeaderLayout;
        private System.Windows.Forms.Panel ContentPanel;
        private System.Windows.Forms.TableLayoutPanel RootLayout;
        private System.Windows.Forms.PropertyGrid Grid;
    }
}
