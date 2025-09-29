using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using CVReport.Data.Sql;

namespace CVReport
{
    public partial class MainForm
    {
        private async Task ExpandAllAsync()
        {
            if (_expandFlow == null) return;

            _suppressDetails = true;      // bloque tout handler parasite
            Cursor = Cursors.WaitCursor;

            try
            {
                _isExpanded = true;
                _expandFlow.SuspendLayout();
                _expandFlow.Controls.Clear();
                _expandFlow.Visible = true;

                // Assure que les cartes occupent TOUTE la largeur disponible
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
                Cursor = Cursors.Default;
                //firstList.Enabled = true;
                _suppressDetails = false;
            }
        }

        // ---------- Helpers ExpandAll ----------

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

            // filet de sécurité : filtre côté client si la requête n'a pas filtré
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

            // Renommage FR attendu par ApplyCabinetDetailsColumns
            if (dt.Columns.Contains("CabinetID")) dt.Columns["CabinetID"].ColumnName = "Cabinet ID";
            if (dt.Columns.Contains("MaterialID")) dt.Columns["MaterialID"].ColumnName = "Material ID";
            if (dt.Columns.Contains("PartDescription")) dt.Columns["PartDescription"].ColumnName = "Description";
            if (dt.Columns.Contains("MaterialName")) dt.Columns["MaterialName"].ColumnName = "Nom du matériel";
            if (dt.Columns.Contains("Quantity")) dt.Columns["Quantity"].ColumnName = "Quantité";

            _expandFlow!.Controls.Add(BuildCabinetCard(cabName, dt));
        }

        // ---- largeur totale disponible à l'intérieur du flow
        private int GetExpandInnerWidth()
        {
            if (_expandFlow == null) return 600;
            int w = _expandFlow.ClientSize.Width - _expandFlow.Padding.Horizontal;
            if (_expandFlow.VerticalScroll.Visible)
                w -= SystemInformation.VerticalScrollBarWidth;
            return Math.Max(120, w);
        }

        // Ajuste toutes les cartes à la largeur du flow
        private void AdjustAllCardWidths()
        {
            if (_expandFlow == null) return;
            int w = GetExpandInnerWidth();

            foreach (Control c in _expandFlow.Controls)
            {
                // force la largeur de la "carte"
                c.MinimumSize = new Size(w, 0);
                c.Width = w;

                // ajuste la grille interne si on la retrouve
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

        private bool _expandResizeHooked = false;
        private void HookExpandFlowResizeOnce()
        {
            if (_expandFlow == null || _expandResizeHooked) return;
            _expandFlow.Resize += (_, __) => AdjustAllCardWidths();
            _expandResizeHooked = true;
        }

        private Panel BuildCabinetCard(string title, DataTable details)
        {
            // Titre
            var lbl = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new System.Drawing.Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            };

            // Grille (pas de scroll vertical interne)
            var grid = new DataGridView
            {
                Name = "CardGrid",
                Dock = DockStyle.Top,
                ReadOnly = true,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Horizontal,
                Margin = new Padding(0, 0, 0, 8),

                // ✅ ligne entière
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            EnableDoubleBuffering(grid);

            grid.DataSource = details;
            ApplyGridTheme(grid, headerFlat: true);   // thème identique partout

            // Fit + hauteur sans scroll vertical interne
            FitColumnsToContent(grid, includeHeader: true, extraPadding: 16, maxColWidth: 1000);
            FitGridHeightToContent(grid);

            // Layout vertical (titre + grille)
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

            // Carte
            var card = new Panel
            {
                Padding = new Padding(0, 0, 0, 0),
                Margin = new Padding(0, 0, 0, 12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White
            };
            card.Controls.Add(layout);

            // largeur initiale forcée (puis maintenue via AdjustAllCardWidths)
            int initW = GetExpandInnerWidth();
            card.MinimumSize = new Size(initW, 0);
            card.Width = initW;

            // ajuste la grille à cette largeur effective
            int inner = initW - card.Padding.Horizontal;
            grid.Width = Math.Max(60, inner);
            FitGridHeightToContent(grid);

            return card;
        }

        // La grille d’un bloc doit afficher toutes ses lignes (pas de scroll vertical interne)
        private static void FitGridHeightToContent(DataGridView g)
        {
            int rowsHeight = 0;
            foreach (DataGridViewRow r in g.Rows)
                if (r.Visible && !r.IsNewRow)
                    rowsHeight += r.Height;

            int header = g.ColumnHeadersVisible ? g.ColumnHeadersHeight : 0;
            int hScroll = g.Controls.OfType<HScrollBar>().FirstOrDefault()?.Visible == true
                ? SystemInformation.HorizontalScrollBarHeight : 0;

            const int fudge = 2; // petite marge pour éviter rognage
            int h = header + rowsHeight + hScroll + fudge;
            g.Height = Math.Max(60, Math.Min(8000, h));
        }
    }
}