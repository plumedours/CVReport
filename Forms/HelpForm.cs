using CVReport.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CVReport.Forms
{
    public class HelpForm : Form
    {
        public HelpForm()
        {
            this.Text = TranslationService.T("help.title");
            this.Icon = Properties.Resources.favicon;
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                Padding = new Padding(20),
            };

            // Titre principal
            var lblTitle = new Label
            {
                Text = TranslationService.T("help.title"),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 20)
            };

            layout.Controls.Add(lblTitle);

            // Section Intro
            layout.Controls.Add(MakeSection("help.intro", bold: false));

            // Section Rapports
            layout.Controls.Add(MakeSectionTitle("help.reports.available"));
            layout.Controls.Add(MakeSection("help.reports.byCabinet"));
            layout.Controls.Add(MakeSection("help.reports.materials"));
            layout.Controls.Add(MakeSection("help.reports.soumission"));

            // Section Utilisation
            layout.Controls.Add(MakeSectionTitle("help.usageTitle"));
            layout.Controls.Add(MakeSection("help.usage"));

            // Section Pré-requis
            layout.Controls.Add(MakeSectionTitle("help.prereqTitle"));
            layout.Controls.Add(MakeSection("help.prereq"));

            scrollPanel.Controls.Add(layout);
            this.Controls.Add(scrollPanel);
        }

        private Control MakeSection(string key, bool bold = false)
        {
            return new Label
            {
                Text = TranslationService.T(key),
                AutoSize = true,
                MaximumSize = new Size(740, 0), // pour wrap
                Font = new Font("Segoe UI", 10, bold ? FontStyle.Bold : FontStyle.Regular),
                Margin = new Padding(0, 0, 0, 15)
            };
        }

        private Control MakeSectionTitle(string key)
        {
            return new Label
            {
                Text = TranslationService.T(key),
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Margin = new Padding(0, 20, 0, 10)
            };
        }
    }
}