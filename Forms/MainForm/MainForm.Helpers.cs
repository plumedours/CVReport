using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace CVReport
{
    public partial class MainForm
    {
        // =========================
        // Utilitaires génériques
        // =========================

        private static void EnableDoubleBuffering(DataGridView grid)
        {
            try
            {
                var prop = typeof(DataGridView).GetProperty("DoubleBuffered",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                prop?.SetValue(grid, true, null);
            }
            catch { /* ignore */ }
        }

        private static void FitColumnsToContent(DataGridView g, bool includeHeader = true, int extraPadding = 24, int? maxColWidth = null)
        {
            if (g.Columns.Count == 0) return;

            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            g.AutoResizeColumns(includeHeader
                ? DataGridViewAutoSizeColumnsMode.AllCells
                : DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader);

            foreach (DataGridViewColumn col in g.Columns)
            {
                var w = col.Width + extraPadding;
                if (maxColWidth.HasValue) w = Math.Min(w, maxColWidth.Value);
                col.Width = Math.Max(w, 40);
            }

            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }

        private static int ComputeTightGridWidth(DataGridView g, int min = 300, int extra = 4)
        {
            int rowHeaders = g.RowHeadersVisible ? g.RowHeadersWidth : 0;
            int colsWidth = g.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width);
            bool vScroll = g.Controls.OfType<VScrollBar>().FirstOrDefault()?.Visible == true;
            int vScrollW = vScroll ? SystemInformation.VerticalScrollBarWidth : 0;
            return Math.Max(min, colsWidth + rowHeaders + vScrollW + extra);
        }

        // =========================
        // Thème/Selection
        // =========================

        private static void ApplyGridTheme(
            DataGridView g,
            Color? selBack = null, Color? selFore = null,
            bool headerFlat = true, Color? headerBack = null, Color? headerFore = null,
            bool noAlternatingZebra = true, Color? gridColor = null, Color? back = null)
        {
            var bg = back ?? Color.White;
            var gc = gridColor ?? Color.Silver;
            var sb = selBack ?? Color.FromArgb(232, 240, 254);
            var sf = selFore ?? Color.Black;
            var hbg = headerBack ?? bg;
            var hfg = headerFore ?? Color.Black;

            g.BackgroundColor = bg;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = gc;

            g.DefaultCellStyle.BackColor = bg;
            g.RowsDefaultCellStyle.BackColor = bg;
            if (noAlternatingZebra) g.AlternatingRowsDefaultCellStyle.BackColor = bg;

            // sélection ligne complète
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.DefaultCellStyle.SelectionBackColor = sb;
            g.DefaultCellStyle.SelectionForeColor = sf;
            g.RowsDefaultCellStyle.SelectionBackColor = sb;
            g.RowsDefaultCellStyle.SelectionForeColor = sf;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = sb;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = sf;
            g.RowHeadersDefaultCellStyle.SelectionBackColor = sb;
            g.RowHeadersDefaultCellStyle.SelectionForeColor = sf;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = hbg;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = hfg;

            g.EnableHeadersVisualStyles = !headerFlat ? true : false;
            g.ColumnHeadersDefaultCellStyle.BackColor = hbg;
            g.ColumnHeadersDefaultCellStyle.ForeColor = hfg;
            if (headerFlat) g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        }

        // === Supprime la barre H de n’importe quelle grille en “étirant” une colonne souple ===
        protected void RemoveHorizontalScroll(DataGridView g, string flexibleColumnName)
        {
            g.ScrollBars = ScrollBars.Vertical;

            void Stretch()
            {
                if (!g.Columns.Contains(flexibleColumnName)) return;

                int rowHeadersW = g.RowHeadersVisible ? g.RowHeadersWidth : 0;
                bool vScroll = g.Controls.OfType<VScrollBar>().FirstOrDefault()?.Visible == true;
                int vScrollW = vScroll ? SystemInformation.VerticalScrollBarWidth : 0;
                int others = g.Columns.Cast<DataGridViewColumn>()
                                         .Where(c => c.Visible && c.Name != flexibleColumnName)
                                         .Sum(c => c.Width);
                int available = g.ClientSize.Width - rowHeadersW - vScrollW - 2 - others;
                if (available > 60)
                    g.Columns[flexibleColumnName].Width = available;
            }

            Stretch();
            g.SizeChanged += (_, __) => Stretch();
        }

        // =========================
        // Conversions & colonnes
        // =========================

        private static decimal SafeDec(object? v)
            => v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v, CultureInfo.InvariantCulture);

        // ===== Unités – détection et formatage =====
        private enum UnitKind { Unknown, Each, LinearFt, SquareFt, BoardFt, Sheet, CubicFt }

        // Ajuste ces correspondances selon ta base (IDs de dbo.CxMaterial.UnitOfIssueID)
        private static readonly System.Collections.Generic.Dictionary<int, UnitKind> UnitIdMap =
            new System.Collections.Generic.Dictionary<int, UnitKind>
        {
    { 1, UnitKind.Each },      // ex: "Each"
    { 2, UnitKind.LinearFt },  // ex: "Lin. Ft"
    { 3, UnitKind.SquareFt },  // ex: "Sq. Ft"
    { 4, UnitKind.BoardFt },   // ex: "Bd. Ft"
    { 5, UnitKind.Sheet },     // ex: "Sheet"
    { 6, UnitKind.CubicFt }    // ex: "Cubic Ft"
        };

        // Essaie d’inférer l’unité à partir de UnitOfIssueID, puis mots-clefs, puis métriques.
        private static UnitKind InferUnitKind(string name, string? desc, int? unitOfIssueId,
                                              decimal each, decimal linFt, decimal sqFt, decimal bdFt,
                                              decimal sheets, decimal cubicFt)
        {
            // 1) D’abord l’unité déclarée
            if (unitOfIssueId.HasValue && UnitIdMap.TryGetValue(unitOfIssueId.Value, out var kindFromId))
                return kindFromId;

            // 2) Heuristique par mots-clés
            string all = ((name ?? "") + " " + (desc ?? "")).ToLowerInvariant();

            bool Any(params string[] tokens) => tokens.Any(t => all.Contains(t));
            if (Any("bande", "chant", "edge", "edgeband", "tape", "ruban", "moulure", "molding", "profil", "profile", "trim"))
                return UnitKind.LinearFt;
            if (Any("panneau", "sheet", "melamine", "mélamine", "mdf", "plywood", "uniboard", "laminé", "laminate"))
                return UnitKind.Sheet;
            if (Any("vis", "screw", "clip", "agrafe", "hinge", "charni", "support", "embout", "patte", "bracket", "gliss", "rail"))
                return UnitKind.Each;

            // 3) Heuristique par métriques (seuils anti-bruit)
            if (sheets > 0.01m) return UnitKind.Sheet;
            if (sqFt > 0.01m) return UnitKind.SquareFt;
            if (linFt > 0.01m) return UnitKind.LinearFt;
            if (bdFt > 0.01m) return UnitKind.BoardFt;
            if (cubicFt > 0.01m) return UnitKind.CubicFt;

            return each > 0 ? UnitKind.Each : UnitKind.Unknown;
        }

        // Formate la quantité/unité selon le “kind” choisi (avec ou sans déchets)
        private static string FormatQtyByKind(
            UnitKind kind, bool useWaste,
            decimal each, decimal linFt, decimal sqFt, decimal bdFt, decimal sheets, decimal cubicFt,
            decimal eachW, decimal linFtW, decimal sqFtW, decimal bdFtW, decimal sheetsW, decimal cubicFtW)
        {
            decimal v; string u;

            switch (kind)
            {
                case UnitKind.Sheet: v = useWaste ? sheetsW : sheets; u = "feuilles"; break;
                case UnitKind.SquareFt: v = useWaste ? sqFtW : sqFt; u = "pi²"; break;
                case UnitKind.LinearFt: v = useWaste ? linFtW : linFt; u = "pi. lin."; break;
                case UnitKind.BoardFt: v = useWaste ? bdFtW : bdFt; u = "bd.ft"; break;
                case UnitKind.CubicFt: v = useWaste ? cubicFtW : cubicFt; u = "ft³"; break;
                case UnitKind.Each:
                case UnitKind.Unknown:
                default: v = useWaste ? eachW : each; u = "pièces"; break;
            }

            return v == Math.Truncate(v) ? $"{v:0} ({u})" : $"{v:0.##} ({u})";
        }

        // =========================
        // Helpers spécifiques ExpandAll
        // =========================

        private static object? GetRowValueByProperty(DataGridView grid, DataGridViewRow row, string dataPropertyName, int fallbackIndex = -1)
        {
            var col = grid.Columns
                          .Cast<DataGridViewColumn>()
                          .FirstOrDefault(c => string.Equals(c.DataPropertyName, dataPropertyName, StringComparison.OrdinalIgnoreCase));
            if (col != null)
                return row.Cells[col.Index]?.Value;

            if (fallbackIndex >= 0 && fallbackIndex < row.Cells.Count)
                return row.Cells[fallbackIndex]?.Value;

            return null;
        }
    }
}