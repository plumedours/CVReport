using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

// Utilise l'énum partagée du moteur d'impression
using PrintStage = CVReport.ReportPrinting.PrintStage;

namespace CVReport
{
    public partial class MainForm
    {
        // ================================
        // Impression / export PDF (MainForm)
        // ================================
        private readonly PrintDocument _printDoc = new PrintDocument();
        private bool _printingWired = false;

        // On réutilise l'énum du moteur partagé
        private PrintStage _printStage = PrintStage.Main;

        private int _printRowIndexMain = 0;           // firstList
        private int _printRowIndexDetails = 0;        // secondList (mode simple)
        private bool _printHeaderDone = false;        // titre (première page uniquement)

        // ⚠️ Liste non passée par ref → peut rester readonly
        private readonly List<(string Title, DataGridView Grid)> _printBlocks = new();
        private int _printBlockIndex = 0;             // index du bloc courant
        private int _printRowIndexInBlock = 0;        // ligne courante dans le bloc

        private void InitPrinting() => EnsurePrintingWired();

        private void PrintReport()
        {

            EnsurePrintingWired();
            ResetPrintState();

            var prefix = _mode == ReportMode.ByCabinet ? "Rapport par cabinet" : "Rapport des matériaux";
            var job = string.IsNullOrWhiteSpace(_currentJobNumber) ? "" : $" — {_currentJobNumber}";
            _printDoc.DocumentName = $"{prefix}{job}";

            using var dlg = new PrintDialog { Document = _printDoc, UseEXDialog = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try { _printDoc.Print(); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Erreur d’impression",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ExportPdfAsync()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName =
                    $"{(_mode == ReportMode.ByCabinet ? "Rapport_Par_Cabinet" : "Rapport_Materiaux")}" +
                    $"{(string.IsNullOrWhiteSpace(_currentJobNumber) ? "" : $"_{_currentJobNumber}")}" +
                    $"_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                OverwritePrompt = true
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var pdfPrinter = ReportPrinting.FindPdfPrinterName();
            if (pdfPrinter == null)
            {
                MessageBox.Show(this,
                    "Aucune imprimante PDF détectée (ex: \"Microsoft Print to PDF\").\n" +
                    "Installez-en une ou utilisez Imprimer et choisissez une imprimante PDF.",
                    "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EnsurePrintingWired();
            ResetPrintState();

            try
            {
                var prefix = _mode == ReportMode.ByCabinet ? "Rapport par cabinet" : "Rapport des matériaux";
                var job = string.IsNullOrWhiteSpace(_currentJobNumber) ? "" : $" — {_currentJobNumber}";
                _printDoc.DocumentName = $"{prefix}{job}";

                _printDoc.PrinterSettings = new PrinterSettings
                {
                    PrinterName = pdfPrinter,
                    PrintToFile = true,
                    PrintFileName = sfd.FileName
                };

                _printDoc.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            await Task.CompletedTask;
        }

        private void EnsurePrintingWired()
        {
            if (_printingWired) return;

            _printDoc.DefaultPageSettings.Margins = new Margins(50, 40, 50, 50);

            _printingWired = true;
        }

        private void ResetPrintState()
        {
            _printStage = PrintStage.Main;
            _printRowIndexMain = 0;
            _printRowIndexDetails = 0;
            _printBlockIndex = 0;
            _printRowIndexInBlock = 0;
            _printBlocks.Clear();
            _printHeaderDone = false;
        }
    }

    // ==========================================================
    // Mutualisation : utilitaires d'impression réutilisables
    // ==========================================================
    public static class ReportPrinting
    {
        public sealed class Context
        {
            public Form Owner { get; init; } = default!;
            public Label TitleLabel { get; init; } = default!;
            public DataGridView MainGrid { get; init; } = default!;
            public DataGridView? DetailGrid { get; init; }
            public FlowLayoutPanel? ExpandFlow { get; init; }
            public string DocumentPrefix { get; init; } = "Rapport";
            public string? JobNumber { get; init; }
        }

        // Rendre l'énum PUBLIC pour être cohérent avec les méthodes publiques
        public enum PrintStage { Main, Details, Done }

        public static void Print(Context ctx)
        {
            if (ctx.MainGrid.DataSource == null || ctx.MainGrid.Rows.Count == 0)
            {
                MessageBox.Show(ctx.Owner, "Aucun rapport à imprimer.", "Imprimer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var doc = new PrintDocument();
            doc.DefaultPageSettings.Margins = new Margins(50, 40, 50, 50);

            var stage = PrintStage.Main;
            int rowMain = 0, rowDetails = 0, blockIndex = 0, rowInBlock = 0;
            bool headerDone = false;
            var blocks = new List<(string Title, DataGridView Grid)>();

            if (ctx.ExpandFlow != null && ctx.ExpandFlow.Visible && ctx.ExpandFlow.Controls.Count > 0)
            {
                foreach (Control card in ctx.ExpandFlow.Controls)
                {
                    var (title, cardGrid) = ExtractCardTitleAndGrid(card);
                    if (cardGrid != null) blocks.Add((string.IsNullOrWhiteSpace(title) ? "Détails" : title, cardGrid));
                }
            }

            doc.PrintPage += (s, e) =>
                RenderPrintPage(
                    e, ctx.TitleLabel, ctx.MainGrid, ctx.DetailGrid,
                    ctx.ExpandFlow, ref headerDone, ref stage,
                    ref rowMain, ref rowDetails, blocks, ref blockIndex, ref rowInBlock);

            var job = string.IsNullOrWhiteSpace(ctx.JobNumber) ? "" : $" — {ctx.JobNumber}";
            doc.DocumentName = $"{ctx.DocumentPrefix}{job}";

            using var dlg = new PrintDialog { Document = doc, UseEXDialog = true };
            if (dlg.ShowDialog(ctx.Owner) == DialogResult.OK)
            {
                try { doc.Print(); }
                catch (Exception ex) { MessageBox.Show(ctx.Owner, ex.Message, "Erreur d’impression", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        public static async Task ExportPdfAsync(Context ctx)
        {
            if (ctx.MainGrid.DataSource == null || ctx.MainGrid.Rows.Count == 0)
            {
                MessageBox.Show(ctx.Owner, "Aucun rapport à exporter.", "Export PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName =
                    $"{ctx.DocumentPrefix.Replace(' ', '_')}" +
                    $"{(string.IsNullOrWhiteSpace(ctx.JobNumber) ? "" : $"_{ctx.JobNumber}")}" +
                    $"_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                OverwritePrompt = true
            };
            if (sfd.ShowDialog(ctx.Owner) != DialogResult.OK) return;

            var pdfPrinter = FindPdfPrinterName();
            if (pdfPrinter == null)
            {
                MessageBox.Show(ctx.Owner,
                    "Aucune imprimante PDF détectée (ex: \"Microsoft Print to PDF\").",
                    "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var doc = new PrintDocument();
            doc.DefaultPageSettings.Margins = new Margins(50, 40, 50, 50);

            var stage = PrintStage.Main;
            int rowMain = 0, rowDetails = 0, blockIndex = 0, rowInBlock = 0;
            bool headerDone = false;
            var blocks = new List<(string Title, DataGridView Grid)>();

            if (ctx.ExpandFlow != null && ctx.ExpandFlow.Visible && ctx.ExpandFlow.Controls.Count > 0)
            {
                foreach (Control card in ctx.ExpandFlow.Controls)
                {
                    var (title, cardGrid) = ExtractCardTitleAndGrid(card);
                    if (cardGrid != null) blocks.Add((string.IsNullOrWhiteSpace(title) ? "Détails" : title, cardGrid));
                }
            }

            doc.PrintPage += (s, e) =>
                RenderPrintPage(
                    e, ctx.TitleLabel, ctx.MainGrid, ctx.DetailGrid,
                    ctx.ExpandFlow, ref headerDone, ref stage,
                    ref rowMain, ref rowDetails, blocks, ref blockIndex, ref rowInBlock);

            var job = string.IsNullOrWhiteSpace(ctx.JobNumber) ? "" : $" — {ctx.JobNumber}";
            doc.DocumentName = $"{ctx.DocumentPrefix}{job}";
            doc.PrinterSettings = new PrinterSettings
            {
                PrinterName = pdfPrinter,
                PrintToFile = true,
                PrintFileName = sfd.FileName
            };

            try { doc.Print(); }
            catch (Exception ex) { MessageBox.Show(ctx.Owner, ex.Message, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Error); }

            await Task.CompletedTask;
        }

        // ---- primitives partagées (rendu + helpers) ----

        public static string? FindPdfPrinterName()
        {
            foreach (string p in PrinterSettings.InstalledPrinters)
                if (string.Equals(p, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase))
                    return p;
            foreach (string p in PrinterSettings.InstalledPrinters)
                if (p.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                    return p;
            return null;
        }

        public static (string Title, DataGridView? Grid) ExtractCardTitleAndGrid(Control card)
        {
            var lbl = card.Controls.OfType<Label>().FirstOrDefault()
                      ?? card.Controls.OfType<TableLayoutPanel>()
                                      .SelectMany(t => t.Controls.OfType<Label>())
                                      .FirstOrDefault();

            var grid = card.Controls.OfType<DataGridView>().FirstOrDefault()
                       ?? card.Controls.OfType<TableLayoutPanel>()
                                       .SelectMany(t => t.Controls.OfType<DataGridView>())
                                       .FirstOrDefault();

            return (lbl?.Text ?? "Détails", grid);
        }

        public static void RenderPrintPage(
            PrintPageEventArgs e,
            Label titleLabel,
            DataGridView mainGrid,
            DataGridView? detailsGrid,
            FlowLayoutPanel? expandFlow,
            ref bool headerDone,
            ref PrintStage stage,
            ref int rowIndexMain,
            ref int rowIndexDetails,
            List<(string Title, DataGridView Grid)> blocks,  // ✅ plus de 'ref'
            ref int blockIndex,
            ref int rowIndexInBlock)
        {
            RectangleF bounds = e.MarginBounds;
            var g = e.Graphics;

            using var fontTitle = new Font("Segoe UI Semibold", 14f);
            using var fontHead = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            using var fontCell = new Font("Segoe UI", 9.5f);

            float y = bounds.Top;
            float left = bounds.Left;
            float totalWidth = bounds.Width;
            float rowH = fontCell.GetHeight(g) + 8;

            if (!headerDone)
            {
                var title = string.IsNullOrWhiteSpace(titleLabel?.Text)
                    ? "Rapport"
                    : titleLabel.Text;
                g.DrawString(title, fontTitle, Brushes.Black, left, y);
                y += fontTitle.GetHeight(g) + 8;
                headerDone = true;
            }

            if (stage == PrintStage.Main)
            {
                if (mainGrid.Columns.Count == 0 || mainGrid.Rows.Count == 0)
                {
                    AdvanceToDetails(e, expandFlow, detailsGrid, ref stage);
                    return;
                }

                y = DrawGridHeader(g, mainGrid, left, y, totalWidth, rowH, fontHead);

                for (; rowIndexMain < mainGrid.Rows.Count; rowIndexMain++)
                {
                    if (y + rowH > bounds.Bottom) { e.HasMorePages = true; return; }
                    var r = mainGrid.Rows[rowIndexMain];
                    DrawGridRow(g, mainGrid, r, left, y, totalWidth, rowH, fontCell);
                    y += rowH;
                }

                stage = PrintStage.Details;
                e.HasMorePages = true; // nouvelle page pour les détails
                return;
            }

            if (stage == PrintStage.Details)
            {
                if (expandFlow != null && expandFlow.Visible && blocks.Count > 0)
                {
                    while (blockIndex < blocks.Count)
                    {
                        var (title, grid) = blocks[blockIndex];

                        if (rowIndexInBlock == 0)
                        {
                            y += 8;
                            g.DrawString(title, fontHead, Brushes.Black, left, y);
                            y += fontHead.GetHeight(g) + 4;
                            y = DrawGridHeader(g, grid, left, y, totalWidth, rowH, fontHead);
                        }

                        for (; rowIndexInBlock < grid.Rows.Count; rowIndexInBlock++)
                        {
                            if (y + rowH > bounds.Bottom) { e.HasMorePages = true; return; }
                            var rr = grid.Rows[rowIndexInBlock];
                            DrawGridRow(g, grid, rr, left, y, totalWidth, rowH, fontCell);
                            y += rowH;
                        }

                        blockIndex++;
                        rowIndexInBlock = 0;

                        if (blockIndex < blocks.Count)
                        {
                            float needed = fontHead.GetHeight(g) + 4 + rowH * 2;
                            if (y + needed > bounds.Bottom) { e.HasMorePages = true; return; }
                        }
                    }

                    stage = PrintStage.Done;
                }
                else if (detailsGrid != null && detailsGrid.Visible && detailsGrid.DataSource != null && detailsGrid.Rows.Count > 0)
                {
                    y += 8;
                    g.DrawString("Détails", fontHead, Brushes.Black, left, y);
                    y += fontHead.GetHeight(g) + 4;

                    y = DrawGridHeader(g, detailsGrid, left, y, totalWidth, rowH, fontHead);

                    for (; rowIndexDetails < detailsGrid.Rows.Count; rowIndexDetails++)
                    {
                        if (y + rowH > bounds.Bottom) { e.HasMorePages = true; return; }
                        var r = detailsGrid.Rows[rowIndexDetails];
                        DrawGridRow(g, detailsGrid, r, left, y, totalWidth, rowH, fontCell);
                        y += rowH;
                    }

                    stage = PrintStage.Done;
                }
                else
                {
                    stage = PrintStage.Done;
                }
            }

            e.HasMorePages = (stage != PrintStage.Done);
        }

        private static void AdvanceToDetails(PrintPageEventArgs e, FlowLayoutPanel? expandFlow, DataGridView? detailsGrid, ref PrintStage stage)
        {
            bool hasBlocks = (expandFlow != null && expandFlow.Visible && expandFlow.Controls.Count > 0);
            bool hasSimple = (detailsGrid != null && detailsGrid.Visible && detailsGrid.DataSource != null && detailsGrid.Rows.Count > 0);

            if (hasBlocks || hasSimple)
            {
                stage = PrintStage.Details;
                e.HasMorePages = true;
            }
            else stage = PrintStage.Done;
        }

        private static float DrawGridHeader(Graphics g, DataGridView grid, float left, float y, float totalWidth, float rowH, Font fontHead)
        {
            var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
            if (cols.Count == 0) return y;

            float sum = cols.Sum(c => Math.Max(c.Width, 1));
            var widths = cols.Select(c => (totalWidth * (Math.Max(c.Width, 1) / sum))).ToArray();

            using (var back = new SolidBrush(Color.FromArgb(245, 245, 245)))
                g.FillRectangle(back, left, y, totalWidth, rowH);
            g.DrawRectangle(Pens.Gainsboro, left, y, totalWidth, rowH);

            float x = left;
            for (int i = 0; i < cols.Count; i++)
            {
                float w = widths[i];
                g.DrawString(cols[i].HeaderText, fontHead, Brushes.Black, new RectangleF(x + 6, y + 4, w - 12, rowH - 8));
                g.DrawRectangle(Pens.Gainsboro, x, y, w, rowH);
                x += w;
            }
            return y + rowH;
        }

        private static void DrawGridRow(Graphics g, DataGridView grid, DataGridViewRow row, float left, float y, float totalWidth, float rowH, Font fontCell)
        {
            var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
            float sum = cols.Sum(c => Math.Max(c.Width, 1));
            var widths = cols.Select(c => (totalWidth * (Math.Max(c.Width, 1) / sum))).ToArray();

            float x = left;
            for (int i = 0; i < cols.Count; i++)
            {
                float w = widths[i];
                var cell = row.Cells[cols[i].Index];
                string text = cell?.Value?.ToString() ?? "";

                g.DrawRectangle(Pens.Gainsboro, x, y, w, rowH);
                g.DrawString(text, fontCell, Brushes.Black, new RectangleF(x + 6, y + 4, w - 12, rowH - 8));
                x += w;
            }
        }
    }
}