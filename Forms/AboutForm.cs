using CVReport.Services;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CVReport.Forms
{
    public class AboutForm : Form
    {
        private readonly Label lblAppName = new();
        private readonly Label lblVersion = new();
        private readonly LinkLabel lblAuthor = new();

        public AboutForm()
        {
            this.Text = TranslationService.T("about.title");
            this.Icon = Properties.Resources.favicon;
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20),
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Nom appli
            lblAppName.Text = Application.ProductName;
            lblAppName.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblAppName.TextAlign = ContentAlignment.MiddleCenter;
            lblAppName.Dock = DockStyle.Fill;

            // Version
            var version = "2.0.0";
            lblVersion.Text = string.Format(TranslationService.T("about.version"), version);
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            lblVersion.Dock = DockStyle.Fill;

            // Auteur avec lien
            lblAuthor.Text = TranslationService.T("about.author");
            lblAuthor.TextAlign = ContentAlignment.MiddleCenter;
            lblAuthor.Dock = DockStyle.Fill;
            lblAuthor.LinkColor = Color.Blue;
            lblAuthor.ActiveLinkColor = Color.DarkBlue;
            lblAuthor.VisitedLinkColor = Color.Purple;
            lblAuthor.LinkClicked += (_, __) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/plumedours", // 🔗 ton GitHub ici
                    UseShellExecute = true
                });
            };

            layout.Controls.Add(lblAppName, 0, 0);
            layout.Controls.Add(lblVersion, 0, 1);
            layout.Controls.Add(lblAuthor, 0, 2);

            this.Controls.Add(layout);
        }
    }
}