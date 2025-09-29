namespace CVReport
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            soumReportBtn = new Button();
            cabReportBtn = new Button();
            matReportBtn = new Button();
            appTitleLabel = new Label();
            tooltip = new ToolTip(components);
            panel1 = new Panel();
            label2 = new Label();
            menuStrip1 = new MenuStrip();
            fichierToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            quitToolStripMenuItem = new ToolStripMenuItem();
            rapportsToolStripMenuItem = new ToolStripMenuItem();
            byCabToolStripMenuItem = new ToolStripMenuItem();
            byMatToolStripMenuItem = new ToolStripMenuItem();
            submissionToolStripMenuItem = new ToolStripMenuItem();
            langueToolStripMenuItem = new ToolStripMenuItem();
            panel1.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // soumReportBtn
            // 
            soumReportBtn.Font = new Font("Comic Sans MS", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            soumReportBtn.Location = new Point(384, 100);
            soumReportBtn.Name = "soumReportBtn";
            soumReportBtn.Size = new Size(180, 40);
            soumReportBtn.TabIndex = 9;
            soumReportBtn.Tag = "btn.load.soumission";
            soumReportBtn.Text = "Soumission (bêta)";
            tooltip.SetToolTip(soumReportBtn, "Listes pour soumission");
            soumReportBtn.UseVisualStyleBackColor = true;
            soumReportBtn.Click += soumReportBtn_Click;
            // 
            // cabReportBtn
            // 
            cabReportBtn.Font = new Font("Comic Sans MS", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            cabReportBtn.Location = new Point(12, 100);
            cabReportBtn.Name = "cabReportBtn";
            cabReportBtn.Size = new Size(180, 40);
            cabReportBtn.TabIndex = 8;
            cabReportBtn.Tag = "btn.load.cabinets";
            cabReportBtn.Text = "Sommaire par cabinet";
            tooltip.SetToolTip(cabReportBtn, "Sommaire par cabinet");
            cabReportBtn.UseVisualStyleBackColor = true;
            cabReportBtn.Click += cabReportBtn_Click;
            // 
            // matReportBtn
            // 
            matReportBtn.Font = new Font("Comic Sans MS", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            matReportBtn.Location = new Point(198, 100);
            matReportBtn.Name = "matReportBtn";
            matReportBtn.Size = new Size(180, 40);
            matReportBtn.TabIndex = 7;
            matReportBtn.Tag = "btn.load.materials";
            matReportBtn.Text = "Sommaire des matériaux";
            tooltip.SetToolTip(matReportBtn, "Sommaire des matériaux");
            matReportBtn.UseVisualStyleBackColor = true;
            matReportBtn.Click += matReportBtn_Click;
            // 
            // appTitleLabel
            // 
            appTitleLabel.AutoSize = true;
            appTitleLabel.Dock = DockStyle.Top;
            appTitleLabel.Font = new Font("Comic Sans MS", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            appTitleLabel.ForeColor = Color.FromArgb(30, 39, 46);
            appTitleLabel.Location = new Point(10, 10);
            appTitleLabel.Name = "appTitleLabel";
            appTitleLabel.Padding = new Padding(5);
            appTitleLabel.Size = new Size(110, 37);
            appTitleLabel.TabIndex = 0;
            appTitleLabel.Tag = "main.title";
            appTitleLabel.Text = "main.title";
            // 
            // panel1
            // 
            panel1.BackColor = Color.Gainsboro;
            panel1.Controls.Add(appTitleLabel);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 24);
            panel1.Margin = new Padding(3, 3, 3, 20);
            panel1.Name = "panel1";
            panel1.Padding = new Padding(10);
            panel1.Size = new Size(579, 55);
            panel1.TabIndex = 10;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 9.75F, FontStyle.Italic, GraphicsUnit.Point, 0);
            label2.ForeColor = Color.Gray;
            label2.Location = new Point(148, 168);
            label2.Name = "label2";
            label2.Size = new Size(258, 17);
            label2.TabIndex = 12;
            label2.Tag = "main.subtitle.info";
            label2.Text = "D'autres rapports seront bientôt disponibles...";
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fichierToolStripMenuItem, rapportsToolStripMenuItem, langueToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(579, 24);
            menuStrip1.TabIndex = 13;
            menuStrip1.Text = "menuStrip1";
            // 
            // fichierToolStripMenuItem
            // 
            fichierToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem, helpToolStripMenuItem, toolStripSeparator1, quitToolStripMenuItem });
            fichierToolStripMenuItem.Name = "fichierToolStripMenuItem";
            fichierToolStripMenuItem.Size = new Size(69, 20);
            fichierToolStripMenuItem.Tag = "menu.file";
            fichierToolStripMenuItem.Text = "menu.file";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(158, 22);
            aboutToolStripMenuItem.Tag = "menu.file.about";
            aboutToolStripMenuItem.Text = "menu.file.about";
            aboutToolStripMenuItem.Click += aboutMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(158, 22);
            helpToolStripMenuItem.Tag = "menu.file.help";
            helpToolStripMenuItem.Text = "menu.file.help";
            helpToolStripMenuItem.Click += helpMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(155, 6);
            // 
            // quitToolStripMenuItem
            // 
            quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            quitToolStripMenuItem.Size = new Size(158, 22);
            quitToolStripMenuItem.Tag = "menu.file.exit";
            quitToolStripMenuItem.Text = "menu.file.exit";
            quitToolStripMenuItem.Click += quitMenuItem_Click;
            // 
            // rapportsToolStripMenuItem
            // 
            rapportsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { byCabToolStripMenuItem, byMatToolStripMenuItem, submissionToolStripMenuItem });
            rapportsToolStripMenuItem.Name = "rapportsToolStripMenuItem";
            rapportsToolStripMenuItem.Size = new Size(90, 20);
            rapportsToolStripMenuItem.Text = "menu.reports";
            // 
            // byCabToolStripMenuItem
            // 
            byCabToolStripMenuItem.Name = "byCabToolStripMenuItem";
            byCabToolStripMenuItem.Size = new Size(208, 22);
            byCabToolStripMenuItem.Tag = "menu.reports.cabinets";
            byCabToolStripMenuItem.Text = "menu.reports.cabinets";
            byCabToolStripMenuItem.Click += cabReportBtn_Click;
            // 
            // byMatToolStripMenuItem
            // 
            byMatToolStripMenuItem.Name = "byMatToolStripMenuItem";
            byMatToolStripMenuItem.Size = new Size(208, 22);
            byMatToolStripMenuItem.Tag = "menu.reports.materials";
            byMatToolStripMenuItem.Text = "menu.reports.materials";
            byMatToolStripMenuItem.Click += matReportBtn_Click;
            // 
            // submissionToolStripMenuItem
            // 
            submissionToolStripMenuItem.Name = "submissionToolStripMenuItem";
            submissionToolStripMenuItem.Size = new Size(208, 22);
            submissionToolStripMenuItem.Tag = "menu.reports.soumission";
            submissionToolStripMenuItem.Text = "menu.reports.soumission";
            submissionToolStripMenuItem.Click += soumReportBtn_Click;
            // 
            // langueToolStripMenuItem
            // 
            langueToolStripMenuItem.Name = "langueToolStripMenuItem";
            langueToolStripMenuItem.Size = new Size(102, 20);
            langueToolStripMenuItem.Text = "menu.language";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(579, 276);
            Controls.Add(label2);
            Controls.Add(panel1);
            Controls.Add(soumReportBtn);
            Controls.Add(matReportBtn);
            Controls.Add(cabReportBtn);
            Controls.Add(menuStrip1);
            MaximizeBox = false;
            MaximumSize = new Size(595, 315);
            MinimumSize = new Size(595, 315);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Form1";
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label appTitleLabel;
        private ToolTip tooltip;
        private Button cabReportBtn;
        private Button matReportBtn;
        private Button soumReportBtn;
        private Panel panel1;
        private Label label2;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fichierToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem quitToolStripMenuItem;
        private ToolStripMenuItem rapportsToolStripMenuItem;
        private ToolStripMenuItem byCabToolStripMenuItem;
        private ToolStripMenuItem byMatToolStripMenuItem;
        private ToolStripMenuItem submissionToolStripMenuItem;
        private ToolStripMenuItem langueToolStripMenuItem;
    }
}
