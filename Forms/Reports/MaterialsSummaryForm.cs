using CVReport.Data.Sql;
using CVReport.Services;
using CVReport.Forms;
using RapportCVVentes.Data;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CVReport.Forms.Reports
{
    /// <summary>Fenêtre: Sommaire des matériaux (une grille plein écran)</summary>
    public sealed class MaterialsSummaryForm : ReportBaseForm
    {
        private readonly Func<Task>? _ensureCxTablesAsync;
        private readonly DataGridView grid = new();

        public MaterialsSummaryForm(SqlDb db, Func<Task>? ensureCxTablesAsync) : base(db)
        {
            _ensureCxTablesAsync = ensureCxTablesAsync;

            // Titre i18n du Form
            this.Tag = "report.materials.title";
            Size = new Size(1100, 800);
            StartPosition = FormStartPosition.CenterScreen;

            grid.Dock = DockStyle.Fill;
            ConfigureGrid(grid);

            var host = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add(grid);
            SetContent(host);

            Shown += async (_, __) =>
            {
                UseWaitCursor = true;
                try
                {
                    if (_ensureCxTablesAsync != null) await _ensureCxTablesAsync();

                    // ======= Agrégat matériaux =======
                    var excluded = await _db.QueryAsync(Queries.Materials.ExcludedByCxParam);
                    var excludedIds = new System.Collections.Generic.HashSet<int>();
                    foreach (DataRow r in excluded.Rows)
                        if (r[0] != DBNull.Value) excludedIds.Add(Convert.ToInt32(r[0]));

                    var dtParts = await _db.QueryAsync(Queries.Materials.PartsAgg);
                    var dtMol = await _db.QueryAsync(Queries.Materials.MoldingAgg);

                    var map = new System.Collections.Generic.Dictionary<int, Agg>();
                    foreach (DataRow r in dtParts.Rows)
                    {
                        if (r["MaterialID"] == DBNull.Value) continue;
                        int id = Convert.ToInt32(r["MaterialID"]);
                        if (excludedIds.Contains(id)) continue;

                        var a = new Agg
                        {
                            Name = (r["Product"]?.ToString() ?? $"Matériau #{id}").Trim(),
                            Desc = (r["Description"]?.ToString() ?? "").Trim(),
                            UnitId = r["UnitOfIssueID"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["UnitOfIssueID"]),
                            Sheets = SafeDec(r["NoOfSheets"]),
                            SheetsW = SafeDec(r["NoOfSheetsWaste"]),
                            SqFt = SafeDec(r["SqFt"]),
                            SqFtW = SafeDec(r["SqFtWaste"]),
                            LinFt = SafeDec(r["LinFt"]),
                            LinFtW = SafeDec(r["LinFtWaste"]),
                            Each = SafeDec(r["Each"]),
                            EachW = SafeDec(r["EachWaste"])
                        };
                        map[id] = a;
                    }

                    foreach (DataRow r in dtMol.Rows)
                    {
                        if (r["MaterialID"] == DBNull.Value) continue;
                        int id = Convert.ToInt32(r["MaterialID"]);
                        if (excludedIds.Contains(id)) continue;
                        if (!map.TryGetValue(id, out var a))
                        {
                            a = new Agg { Name = $"Matériau #{id}" };
                            map[id] = a;
                        }
                        var lin = SafeDec(r["LinFt"]);
                        a.LinFt += lin;
                        a.LinFtW += lin;
                    }

                    // Table rapport avec colonnes stables (pour i18n des headers)
                    var report = new DataTable();
                    report.Columns.Add("MaterialName", typeof(string));
                    report.Columns.Add("Quantity", typeof(string));
                    report.Columns.Add("PartDescription", typeof(string));

                    foreach (var a in map.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var (val, unitKey) = PickBest(a, useWaste: true);
                        if (val <= 0) continue;

                        report.Rows.Add(
                            string.IsNullOrWhiteSpace(a.Name) ? "(Sans nom)" : a.Name,
                            FormatQty(val, unitKey),
                            a.Desc ?? ""
                        );
                    }

                    grid.DataSource = null;
                    grid.Columns.Clear();
                    grid.DataSource = report;

                    // Traduire les en-têtes de colonnes
                    TranslateGridColumns(grid);

                    ApplyGridTheme(grid);
                    FitColumns(grid, 24, null);
                }
                finally
                {
                    UseWaitCursor = false;
                }

                // Appliquer la traduction du Form (titre, menus/labels éventuels)
                Translator.Apply(this);

                await InitializeHeaderAsync(showExpandAll: false);
            };
        }

        // Titre de la zone d’en-tête (header) du rapport
        protected override string ReportKindLabel => TranslationService.T("report.materials.kind");
        protected override DataGridView? GetMainGrid() => grid;
        protected override DataGridView? GetDetailGrid() => null;

        // ======= Helpers =======
        private sealed class Agg
        {
            public string Name { get; set; } = "";
            public string? Desc { get; set; }
            public int? UnitId { get; set; }
            public decimal Each, LinFt, SqFt, Sheets;
            public decimal EachW, LinFtW, SqFtW, SheetsW;
        }

        private static (decimal val, string unitKey) PickBest(Agg a, bool useWaste)
        {
            if (useWaste)
            {
                if (a.SheetsW > 0) return (a.SheetsW, "unit.sheets");
                if (a.SqFtW > 0) return (a.SqFtW, "unit.sqft");
                if (a.LinFtW > 0) return (a.LinFtW, "unit.lft");
                if (a.EachW > 0) return (a.EachW, "unit.each");
            }
            else
            {
                if (a.Sheets > 0) return (a.Sheets, "unit.sheets");
                if (a.SqFt > 0) return (a.SqFt, "unit.sqft");
                if (a.LinFt > 0) return (a.LinFt, "unit.lft");
                if (a.Each > 0) return (a.Each, "unit.each");
            }
            return (0m, "");
        }

        private static string FormatQty(decimal val, string unitKey)
        {
            if (val <= 0) return "";
            var s = (val == Math.Truncate(val)) ? val.ToString("0") : val.ToString("0.##");

            if (string.IsNullOrEmpty(unitKey)) return s;
            var unit = TranslationService.T(unitKey); // traduit l’unité
            return $"{s} ({unit})";
        }

        private static decimal SafeDec(object? v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(v); } catch { return 0m; }
        }

        private static void ConfigureGrid(DataGridView g)
        {
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.ReadOnly = true;
            g.RowHeadersVisible = false;
            g.AutoGenerateColumns = true;
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = Color.White;
            g.EnableHeadersVisualStyles = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
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

        /// <summary>
        /// Traduit les en-têtes de colonnes de la grille à partir de leurs DataPropertyName.
        /// </summary>
        private static void TranslateGridColumns(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                var prop = col.DataPropertyName;
                if (string.IsNullOrEmpty(prop)) continue;

                string? key = prop switch
                {
                    "MaterialName" => "col.materialName",
                    "Quantity" => "col.quantity",
                    "PartDescription" => "col.partDescription",
                    _ => null
                };

                if (key != null)
                    col.HeaderText = TranslationService.T(key);
            }
        }
    }
}