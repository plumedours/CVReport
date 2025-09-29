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
    /// Fenêtre: Sommaire par cabinet
    /// Gauche: liste des cabinets (firstList)
    /// Droite: matériaux du cabinet (secondList) OU blocs “Tout déplier”
    /// </summary>
    public sealed class ByCabinetForm : ReportBaseForm
    {
        private readonly Func<Task>? _ensureCxTablesAsync;

        // Layout contenu
        private readonly Panel _content = new();
        private readonly DataGridView firstList = new();
        private readonly Panel spacerPanel = new();
        private readonly Panel rightPanel = new();
        private readonly DataGridView secondList = new();
        private readonly FlowLayoutPanel _expandFlow = new();   // blocs “Tout déplier”

        private bool _isExpanded = false;
        private bool _expandResizeHooked = false;

        public ByCabinetForm(SqlDb db, Func<Task>? ensureCxTablesAsync) : base(db)
        {
            _ensureCxTablesAsync = ensureCxTablesAsync;

            Tag = "report.byCabinets.title";
            //Text = "CV Report — Sommaire par cabinet";
            Size = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BuildContentLayout();

            // Grids
            ConfigureGrid(firstList);
            ConfigureGrid(secondList);

            // Evénements
            firstList.CellClick += FirstList_CellClick;

            // Bouton “Tout déplier” du header
            expandAllBtn.Tag = "report.byCabinets.expandAll";
            expandAllBtn.Visible = true;
            expandAllBtn.Click += async (_, __) =>
            {
                if (!_isExpanded) await ExpandAllAsync();
                else CollapseAll();
            };

            Shown += async (_, __) =>
            {
                UseWaitCursor = true;
                try
                {
                    if (_ensureCxTablesAsync != null) await _ensureCxTablesAsync();
                    var dt = await _db.QueryAsync(Queries.Cabinets.ListAll);

                    firstList.DataSource = null;
                    firstList.Columns.Clear();
                    firstList.DataSource = dt;

                    ApplyGridTheme(firstList);
                    FitColumns(firstList, 12, 520);

                    secondList.DataSource = null;
                    secondList.Columns.Clear();
                    ApplyGridTheme(secondList);
                }
                finally
                {
                    UseWaitCursor = false;
                }

                Translator.Apply(this);
                await InitializeHeaderAsync(showExpandAll: true);
            };
        }

        protected override string ReportKindLabel =>
            CVReport.Services.TranslationService.T("report.byCabinet.kind");
        protected override DataGridView? GetMainGrid() => firstList;
        protected override DataGridView? GetDetailGrid() => secondList;
        protected override FlowLayoutPanel? GetExpandBlocksFlow() => _expandFlow;

        private void BuildContentLayout()
        {
            // Gauche
            firstList.Dock = DockStyle.Left;
            firstList.Width = 500;

            spacerPanel.Dock = DockStyle.Left;
            spacerPanel.Width = 8;

            // Droite
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.BackColor = Color.White;

            secondList.Dock = DockStyle.Fill;

            _expandFlow.Dock = DockStyle.Fill;
            _expandFlow.FlowDirection = FlowDirection.TopDown;
            _expandFlow.WrapContents = false;
            _expandFlow.AutoScroll = true;
            _expandFlow.Visible = false; // activé lorsque “Tout déplier”
            rightPanel.Controls.Add(secondList);
            rightPanel.Controls.Add(_expandFlow);
            rightPanel.Controls.SetChildIndex(_expandFlow, 0); // blocs au-dessus quand visibles

            // Empilement
            _content.Dock = DockStyle.Fill;
            _content.Controls.Add(rightPanel);
            _content.Controls.Add(spacerPanel);
            _content.Controls.Add(firstList);

            SetContent(_content);
        }

        // =================== Interactions ===================
        private async void FirstList_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (firstList.Rows[e.RowIndex].DataBoundItem is not DataRowView drv) return;
            if (!int.TryParse(drv["CabinetID"]?.ToString(), out int cabinetId)) return;

            try
            {
                UseWaitCursor = true;

                var p = new SqlParameter("@CabinetId", cabinetId);
                var dt = await _db.QueryAsync(Queries.Cabinets.MaterialsByCabinet, p);

                secondList.DataSource = null;
                secondList.Columns.Clear();
                secondList.DataSource = dt;

                Translator.GridTranslator.TranslateColumns(secondList);

                ApplyGridTheme(secondList);
                FitColumns(secondList, 16, 900);

                // Quand on clique, on revient en mode “détail simple”
                if (_isExpanded) CollapseAll();
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        // =================== “Tout déplier” ===================
        private async Task ExpandAllAsync()
        {
            _isExpanded = true;
            expandAllBtn.Text = CVReport.Services.TranslationService.T("report.byCabinets.collapseAll");

            // cache le détail simple, montre le flow
            secondList.Visible = false;
            _expandFlow.SuspendLayout();
            _expandFlow.Controls.Clear();
            _expandFlow.Visible = true;

            try
            {
                UseWaitCursor = true;

                // Source: table master
                DataTable? master = TryGetMasterTable(firstList.DataSource);
                if (master != null && master.Rows.Count > 0)
                {
                    foreach (DataRow r in master.Rows)
                    {
                        if (!int.TryParse(r["CabinetID"]?.ToString(), out int cabId)) continue;
                        string cabName = r["CabinetName"]?.ToString() ?? $"Cabinet {cabId}";
                        await AddCabinetCardAsync(cabId, cabName);
                    }
                }
                else
                {
                    // fallback via lignes visibles
                    foreach (DataGridViewRow row in firstList.Rows)
                    {
                        if (row.IsNewRow) continue;

                        int cabId = 0;
                        string cabName = "Cabinet";
                        if (firstList.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName.Equals("CabinetID", StringComparison.OrdinalIgnoreCase)))
                            int.TryParse(Convert.ToString(row.Cells[firstList.Columns
                                .Cast<DataGridViewColumn>().First(c => c.DataPropertyName.Equals("CabinetID", StringComparison.OrdinalIgnoreCase)).Index].Value), out cabId);
                        if (firstList.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName.Equals("CabinetName", StringComparison.OrdinalIgnoreCase)))
                            cabName = Convert.ToString(row.Cells[firstList.Columns
                                .Cast<DataGridViewColumn>().First(c => c.DataPropertyName.Equals("CabinetName", StringComparison.OrdinalIgnoreCase)).Index].Value) ?? $"Cabinet {cabId}";

                        if (cabId > 0) await AddCabinetCardAsync(cabId, cabName);
                    }
                }

                HookExpandFlowResizeOnce();
                AdjustAllCardWidths();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erreur 'Tout déplier'", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _expandFlow.ResumeLayout(true);
                UseWaitCursor = false;
            }
        }

        private void CollapseAll()
        {
            _isExpanded = false;
            expandAllBtn.Text = CVReport.Services.TranslationService.T("report.byCabinets.expandAll");

            _expandFlow.Visible = false;
            secondList.Visible = true;
            rightPanel.Controls.SetChildIndex(secondList, 0);
        }

        private static DataTable? TryGetMasterTable(object? dataSource)
        {
            if (dataSource is DataTable dt) return dt;
            if (dataSource is DataView dv) return dv.ToTable();
            if (dataSource is BindingSource bs)
            {
                if (bs.DataSource is DataTable t) return t;
                if (bs.List is DataView dv2) return dv2.ToTable();
            }
            return null;
        }

        private async Task AddCabinetCardAsync(int cabId, string cabName)
        {
            var p = new SqlParameter("@CabinetId", cabId);
            var dt = await _db.QueryAsync(Queries.Cabinets.MaterialsByCabinet, p);

            // sécurité: filtre côté client si besoin
            if (dt.Columns.Contains("CabinetID"))
            {
                var rows = dt.Select($"CabinetID = {cabId}");
                if (rows.Length != dt.Rows.Count)
                {
                    var filtered = dt.Clone();
                    foreach (var r in rows) filtered.ImportRow(r);
                    dt = filtered;
                }
            }

            _expandFlow.Controls.Add(BuildCabinetCard(cabName, dt));
        }

        private Panel BuildCabinetCard(string title, DataTable details)
        {
            var lbl = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            };

            var grid = new DataGridView
            {
                Name = "CardGrid",
                Dock = DockStyle.Top,
                ReadOnly = true,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = true,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Horizontal,
                Margin = new Padding(0, 0, 0, 8),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                DataSource = details
            };

            Translator.GridTranslator.TranslateColumns(grid);
            ApplyGridTheme(grid);
            FitColumns(grid, 16, 1000);
            FitGridHeightToContent(grid);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(lbl, 0, 0);
            layout.Controls.Add(grid, 0, 1);

            var card = new Panel
            {
                Padding = new Padding(0),
                Margin = new Padding(0, 0, 0, 12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White
            };
            card.Controls.Add(layout);

            int initW = GetExpandInnerWidth();
            card.MinimumSize = new Size(initW, 0);
            card.Width = initW;

            int inner = initW - card.Padding.Horizontal;
            grid.Width = Math.Max(60, inner);
            FitGridHeightToContent(grid);

            return card;
        }

        private int GetExpandInnerWidth()
        {
            int w = _expandFlow.ClientSize.Width - _expandFlow.Padding.Horizontal;
            if (_expandFlow.VerticalScroll.Visible) w -= SystemInformation.VerticalScrollBarWidth;
            return Math.Max(120, w);
        }

        private void AdjustAllCardWidths()
        {
            int w = GetExpandInnerWidth();

            foreach (Control c in _expandFlow.Controls)
            {
                c.MinimumSize = new Size(w, 0);
                c.Width = w;

                var grid = c.Controls
                            .OfType<TableLayoutPanel>()
                            .SelectMany(t => t.Controls.OfType<DataGridView>())
                            .FirstOrDefault();
                if (grid != null)
                {
                    var card = c as Panel;
                    int inner = w - (card?.Padding.Horizontal ?? 0);
                    grid.Width = Math.Max(60, inner);
                    FitGridHeightToContent(grid);
                }
            }
        }

        private void HookExpandFlowResizeOnce()
        {
            if (_expandResizeHooked) return;
            _expandFlow.Resize += (_, __) => AdjustAllCardWidths();
            _expandResizeHooked = true;
        }

        // ====== Utils visuels ======
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

        private static void FitGridHeightToContent(DataGridView g)
        {
            int rowsHeight = 0;
            foreach (DataGridViewRow r in g.Rows)
                if (r.Visible && !r.IsNewRow)
                    rowsHeight += r.Height;

            int header = g.ColumnHeadersVisible ? g.ColumnHeadersHeight : 0;
            int hScroll = g.Controls.OfType<HScrollBar>().FirstOrDefault()?.Visible == true
                ? SystemInformation.HorizontalScrollBarHeight : 0;

            const int fudge = 2;
            int h = header + rowsHeight + hScroll + fudge;
            g.Height = Math.Max(60, Math.Min(8000, h));
        }
    }
}
