using CVReport.Data.Sql;
using CVReport.Services;
using CVReport.Forms;
using Microsoft.Data.SqlClient;
using RapportCVVentes.Data;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CVReport.Forms.Reports
{
    /// <summary>
    /// Fenêtre: Soumission
    /// Colonne gauche: boutons de listes
    /// Colonne droite: une grille (secondList)
    /// </summary>
    public sealed class SoumissionForm : ReportBaseForm
    {
        private readonly Func<Task>? _ensureCxTablesAsync;

        private readonly Panel leftPanel = new();
        private readonly FlowLayoutPanel btnFlow = new();
        private readonly Panel spacerPanel = new();
        private readonly Panel rightPanel = new();
        private readonly DataGridView secondList = new();

        private readonly Button btnPanels = new();
        private readonly Button btnDoors = new();
        private readonly Button btnDrawers = new();
        private readonly Button btnPulls = new();

        public SoumissionForm(SqlDb db, Func<Task>? ensureCxTablesAsync) : base(db)
        {
            _ensureCxTablesAsync = ensureCxTablesAsync;

            this.Tag = "report.soumission.title";
            Size = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            ConfigureGrid(secondList);

            Shown += async (_, __) =>
            {
                UseWaitCursor = true;
                try
                {
                    if (_ensureCxTablesAsync != null) await _ensureCxTablesAsync();
                    secondList.DataSource = null;
                    secondList.Columns.Clear();
                }
                finally { UseWaitCursor = false; }

                // Traduction globale (titre fenêtre, boutons via Tag, menus, tooltips éventuels)
                Translator.Apply(this);

                await InitializeHeaderAsync(showExpandAll: false);
            };
        }

        protected override string ReportKindLabel => TranslationService.T("report.soumission.kind");
        protected override DataGridView? GetMainGrid() => secondList; // c’est la grille imprimée
        protected override DataGridView? GetDetailGrid() => null;

        private void BuildLayout()
        {
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 250;
            leftPanel.Padding = new Padding(8);

            spacerPanel.Dock = DockStyle.Left;
            spacerPanel.Width = 8;

            rightPanel.Dock = DockStyle.Fill;
            secondList.Dock = DockStyle.Fill;

            // Flow & boutons
            btnFlow.Dock = DockStyle.Top;
            btnFlow.FlowDirection = FlowDirection.TopDown;
            btnFlow.WrapContents = false;
            btnFlow.AutoSize = true;
            btnFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            InitButton(btnPanels, "report.soumission.btn.panels", BtnPanels_Click);
            InitButton(btnDoors, "report.soumission.btn.doors", BtnDoors_Click);
            InitButton(btnDrawers, "report.soumission.btn.drawers", BtnDrawers_Click);
            InitButton(btnPulls, "report.soumission.btn.pulls", BtnPulls_Click);

            btnFlow.Controls.Add(btnPanels);
            btnFlow.Controls.Add(btnDoors);
            btnFlow.Controls.Add(btnDrawers);
            btnFlow.Controls.Add(btnPulls);

            leftPanel.Controls.Add(btnFlow);

            rightPanel.Controls.Add(secondList);

            var host = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add(rightPanel);
            host.Controls.Add(spacerPanel);
            host.Controls.Add(leftPanel);
            SetContent(host);
        }

        // ====== Boutons ======
        private async void BtnPanels_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var raw = await _db.QueryAsync(Queries.Soumission.PanelsSheetsDetailed);

                // Colonnes stables -> traductions fiables via GridTranslator
                var table = new DataTable();
                table.Columns.Add("MaterialName", typeof(string));
                table.Columns.Add("PartDescription", typeof(string));
                table.Columns.Add("TotalSqFt", typeof(double));
                table.Columns.Add("RealSheets", typeof(double));
                table.Columns.Add("RoundedSheets", typeof(int));

                foreach (DataRow r in raw.Rows)
                {
                    string name = ReadString(r, "Name");
                    string desc = ReadString(r, "Description");
                    double sqftWaste = ReadDouble(r, "SqFtWaste");
                    double sheetsReal = ReadDouble(r, "NoOfSheetsWaste");
                    if (sheetsReal <= 0 && sqftWaste <= 0) continue;
                    int sheetsRounded = (int)Math.Ceiling(sheetsReal);

                    table.Rows.Add(
                        name,
                        desc,
                        Math.Round(sqftWaste, 2),
                        Math.Round(sheetsReal, 2),
                        sheetsRounded
                    );
                }

                Bind(table);

                if (secondList.Columns.Contains("TotalSqFt")) secondList.Columns["TotalSqFt"].DefaultCellStyle.Format = "N2";
                if (secondList.Columns.Contains("RealSheets")) secondList.Columns["RealSheets"].DefaultCellStyle.Format = "N2";
                if (secondList.Columns.Contains("RoundedSheets")) secondList.Columns["RoundedSheets"].DefaultCellStyle.Format = "N0";

                // Traduire via le Translator central
                Translator.GridTranslator.TranslateColumns(secondList);

                // Titre dynamique i18n
                this.Text = string.Format(TranslationService.T("report.soumission.panels.title"), table.Rows.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erreur - Panneaux", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; }
        }

        private async void BtnDoors_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var dt = await _db.QueryAsync(Queries.Soumission.DoorsList);

                // Normalisation des entêtes pour i18n
                if (dt.Columns.Contains("Nom")) dt.Columns["Nom"].ColumnName = "MaterialName";
                if (dt.Columns.Contains("Description")) dt.Columns["Description"].ColumnName = "PartDescription";
                if (dt.Columns.Contains("Qté")) dt.Columns["Qté"].ColumnName = "Quantity";
                if (dt.Columns.Contains("Pi² total")) dt.Columns["Pi² total"].ColumnName = "TotalSqFt";

                Bind(dt);

                // Traduction des colonnes via Translator
                Translator.GridTranslator.TranslateColumns(secondList);

                this.Text = TranslationService.T("report.soumission.doors.title");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erreur - Portes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; }
        }

        private async void BtnDrawers_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var dt = await _db.QueryAsync(Queries.Soumission.DrawersList);

                if (dt.Columns.Contains("Nom")) dt.Columns["Nom"].ColumnName = "MaterialName";
                if (dt.Columns.Contains("Description")) dt.Columns["Description"].ColumnName = "PartDescription";
                if (dt.Columns.Contains("Qté")) dt.Columns["Qté"].ColumnName = "Quantity";

                Bind(dt);

                Translator.GridTranslator.TranslateColumns(secondList);

                this.Text = TranslationService.T("report.soumission.drawers.title");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erreur - Coulisses", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; }
        }

        private async void BtnPulls_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var dt = await _db.QueryAsync(Queries.Soumission.PullsList);

                if (dt.Columns.Contains("Nom")) dt.Columns["Nom"].ColumnName = "MaterialName";
                if (dt.Columns.Contains("Description")) dt.Columns["Description"].ColumnName = "PartDescription";
                if (dt.Columns.Contains("Qté")) dt.Columns["Qté"].ColumnName = "Quantity";

                Bind(dt);

                Translator.GridTranslator.TranslateColumns(secondList);

                this.Text = TranslationService.T("report.soumission.pulls.title");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erreur - Poignées", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; }
        }

        // ====== Bind commun & helpers ======
        private void Bind(DataTable dt)
        {
            secondList.SuspendLayout();
            secondList.DataSource = null;
            secondList.Columns.Clear();
            secondList.RowHeadersVisible = false;

            secondList.DataSource = dt;

            ApplyGridTheme(secondList);
            FitColumns(secondList, 16, 900);

            secondList.Dock = DockStyle.Fill;
            secondList.Visible = true;
            secondList.BringToFront();

            secondList.ResumeLayout(true);
        }

        private static void InitButton(Button b, string tagKey, EventHandler onClick)
        {
            b.Tag = tagKey; // Clé i18n (traduite via Translator.Apply)
            b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            b.AutoSize = false;
            b.Width = 180;
            b.Height = 40;
            b.Margin = new Padding(0, 0, 0, 8);
            b.FlatStyle = FlatStyle.System;
            b.Click += onClick;
        }

        private static void ConfigureGrid(DataGridView g)
        {
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToOrderColumns = true;
            g.ReadOnly = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.RowHeadersVisible = false;
            g.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            g.AutoGenerateColumns = true;
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = Color.White;
            g.EnableHeadersVisualStyles = false;
        }

        private static void ApplyGridTheme(DataGridView g)
        {
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            g.DefaultCellStyle.BackColor = Color.White;
            g.DefaultCellStyle.ForeColor = Color.Black;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 240, 254);
            g.DefaultCellStyle.SelectionForeColor = Color.Black;
            g.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        private static void FitColumns(DataGridView g, int extraPadding, int? max)
        {
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            g.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            foreach (DataGridViewColumn c in g.Columns)
            {
                var w = c.Width + extraPadding;
                if (max.HasValue && w > max.Value) w = max.Value;
                c.Width = w;
            }
        }

        private static int ReadInt(DataRow r, string name) =>
            (!r.Table.Columns.Contains(name) || r[name] == DBNull.Value) ? 0 : Convert.ToInt32(r[name]);
        private static double ReadDouble(DataRow r, string name) =>
            (!r.Table.Columns.Contains(name) || r[name] == DBNull.Value) ? 0.0 : Convert.ToDouble(r[name]);
        private static string ReadString(DataRow r, string name) =>
            (!r.Table.Columns.Contains(name) || r[name] == DBNull.Value) ? "" : Convert.ToString(r[name]) ?? "";
    }
}