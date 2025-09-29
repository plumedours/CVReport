using CVReport.Data.Sql;
using RapportCVVentes.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CVReport.Services; // ✅ ajouté pour TranslationService

namespace CVReport.Forms
{
    public abstract class ReportBaseForm : Form
    {
        protected readonly SqlDb _db;

        // ====== Header UI ======
        protected readonly TableLayoutPanel _root = new();
        protected readonly TableLayoutPanel _header = new();
        protected readonly Label reportTitleLabel = new();
        protected readonly FlowLayoutPanel _headerButtons = new();
        protected readonly System.Windows.Forms.Button pdfBtn = new();
        protected readonly System.Windows.Forms.Button printBtn = new();
        protected readonly System.Windows.Forms.Button expandAllBtn = new();
        protected readonly Panel _contentHost = new();
        private readonly ToolTip _tooltip = new();

        private readonly PrintDocument _printDoc = new();
        private bool _printingWired = false;

        private enum PrintStage { Main, Details, Done }
        private PrintStage _printStage = PrintStage.Main;

        private int _printRowIndexMain = 0;
        private int _printRowIndexDetails = 0;
        private bool _printHeaderDone = false;

        private readonly List<(string Title, DataGridView Grid)> _printBlocks = new();
        private int _printBlockIndex = 0;
        private int _printRowIndexInBlock = 0;

        private string? _currentJobNumber;

        protected ReportBaseForm(SqlDb db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            BuildHeaderAndRoot();
        }

        protected abstract string ReportKindLabel { get; }
        protected abstract DataGridView? GetMainGrid();
        protected abstract DataGridView? GetDetailGrid();
        protected virtual FlowLayoutPanel? GetExpandBlocksFlow() => null;

        protected async Task InitializeHeaderAsync(bool showExpandAll = false)
        {
            expandAllBtn.Visible = showExpandAll;
            await LoadReportTitleAsync();
            WirePrintingUi();
        }

        /// <summary>Récupère le titre via i18n + Job Number.</summary>
        private async Task LoadReportTitleAsync()
        {
            try
            {
                var dt = await _db.QueryAsync(Queries.JobInfo.GetLatestHeader);
                if (dt.Rows.Count == 0)
                {
                    _currentJobNumber = null;
                    reportTitleLabel.Text = TranslationService.T("main.title.noProject");
                    return;
                }

                var row = dt.Rows[0];
                _currentJobNumber = (row["Job Number"]?.ToString() ?? "").Trim();

                if (string.IsNullOrWhiteSpace(_currentJobNumber))
                {
                    reportTitleLabel.Text = TranslationService.T("main.title.noProject");
                }
                else
                {
                    reportTitleLabel.Text = string.Format(
                        TranslationService.T("main.title.project"), _currentJobNumber);
                }
            }
            catch
            {
                reportTitleLabel.Text = TranslationService.T("main.title.error");
            }
        }

        // ================== Header / Layout ==================
        private void BuildHeaderAndRoot()
        {
            SuspendLayout();

            _root.Dock = DockStyle.Fill;
            _root.ColumnCount = 1;
            _root.RowCount = 2;
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _header.Dock = DockStyle.Top;
            _header.AutoSize = true;
            _header.ColumnCount = 1;
            _header.RowCount = 2;
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            reportTitleLabel.AutoSize = true;
            reportTitleLabel.Font = new Font("Segoe UI Semibold", 14f);
            reportTitleLabel.Text = TranslationService.T("main.title.loading"); // ✅ clé "chargement..."

            _headerButtons.AutoSize = true;
            _headerButtons.FlowDirection = FlowDirection.LeftToRight;
            _headerButtons.WrapContents = false;
            _headerButtons.Margin = new Padding(0, 4, 0, 0);

            ConfigureIconButton(printBtn, Properties.Resources.print,
                TranslationService.T("main.print")); // ✅ trad bouton
            ConfigureIconButton(pdfBtn, Properties.Resources.pdf,
                TranslationService.T("main.exportPdf"));

            expandAllBtn.Tag = "main.expandAll"; // ✅ trad via Translator
            ConfigureTextButton(expandAllBtn, leftMargin: 8);
            expandAllBtn.Visible = false;

            _headerButtons.Controls.Add(pdfBtn);
            _headerButtons.Controls.Add(printBtn);
            _headerButtons.Controls.Add(expandAllBtn);

            _header.Controls.Add(reportTitleLabel, 0, 0);
            _header.Controls.Add(_headerButtons, 0, 1);

            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = Color.White;

            _root.Controls.Add(_header, 0, 0);
            _root.Controls.Add(_contentHost, 0, 1);

            Controls.Add(_root);

            ResumeLayout(true);
        }

        private void ConfigureIconButton(System.Windows.Forms.Button b, Image img, string tooltipText)
        {
            b.Size = new Size(35, 35);
            b.MinimumSize = new Size(35, 35);
            b.MaximumSize = new Size(35, 35);
            b.BackColor = Color.Gainsboro;
            b.UseVisualStyleBackColor = false;
            b.BackgroundImage = img;
            b.BackgroundImageLayout = ImageLayout.Center;
            b.Cursor = Cursors.Hand;
            b.Text = string.Empty;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;

            _tooltip.SetToolTip(b, tooltipText);
            b.AccessibleName = tooltipText;
            b.Margin = new Padding(0, 0, 8, 0);
        }

        private void ConfigureTextButton(System.Windows.Forms.Button b, int leftMargin = 0)
        {
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(0, 35);
            b.Padding = new Padding(10, 6, 10, 6);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(leftMargin, 0, 0, 0);
            b.FlatStyle = FlatStyle.System;
            b.UseVisualStyleBackColor = true;
        }

        protected void SetContent(Control content)
        {
            _contentHost.Controls.Clear();
            content.Dock = DockStyle.Fill;
            _contentHost.Controls.Add(content);
        }

        // ================== Impression / PDF ==================
        private void WirePrintingUi()
        {
            if (_printingWired) return;
            _printingWired = true;

            pdfBtn.Click += async (_, __) => await ExportPdfAsync();
            printBtn.Click += (_, __) => PrintReport();

            _printDoc.DefaultPageSettings.Margins = new Margins(50, 40, 50, 50);
            _printDoc.PrintPage += PrintDocOnPrintPage;
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

            // blocs “Tout déplier” (si fournis par la forme)
            var expand = GetExpandBlocksFlow();
            if (expand != null && expand.Visible && expand.Controls.Count > 0)
            {
                foreach (Control card in expand.Controls)
                {
                    var (title, grid) = ExtractCardTitleAndGrid(card);
                    if (grid != null) _printBlocks.Add((string.IsNullOrWhiteSpace(title) ? "Détails" : title, grid));
                }
            }
        }

        private void PrintReport()
        {
            var main = GetMainGrid();
            if (main == null || main.DataSource == null || main.Rows.Count == 0)
            {
                MessageBox.Show(this, "Aucun rapport à imprimer.", "Imprimer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ResetPrintState();

            var prefix = ReportKindLabel;
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
            var main = GetMainGrid();
            if (main == null || main.DataSource == null || main.Rows.Count == 0)
            {
                MessageBox.Show(this, "Aucun rapport à exporter.", "Export PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"{ReportKindLabel.Replace(' ', '_')}" +
                           $"{(string.IsNullOrWhiteSpace(_currentJobNumber) ? "" : $"_{_currentJobNumber}")}" +
                           $"_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                OverwritePrompt = true
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var pdfPrinter = FindPdfPrinterName();
            if (pdfPrinter == null)
            {
                MessageBox.Show(this,
                    "Aucune imprimante PDF détectée (ex: \"Microsoft Print to PDF\").\n" +
                    "Installez-en une ou utilisez Imprimer et choisissez une imprimante PDF.",
                    "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResetPrintState();

            try
            {
                var prefix = ReportKindLabel;
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

        private static string? FindPdfPrinterName()
        {
            foreach (string p in PrinterSettings.InstalledPrinters)
                if (string.Equals(p, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase)) return p;

            foreach (string p in PrinterSettings.InstalledPrinters)
                if (p.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0) return p;

            return null;
        }

        private void PrintDocOnPrintPage(object? sender, PrintPageEventArgs e)
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

            // Titre (1re page)
            if (!_printHeaderDone)
            {
                var title = string.IsNullOrWhiteSpace(reportTitleLabel?.Text)
                    ? ReportKindLabel
                    : reportTitleLabel.Text;
                g.DrawString(title, fontTitle, Brushes.Black, left, y);
                y += fontTitle.GetHeight(g) + 8;
                _printHeaderDone = true;
            }

            // 1) Main
            if (_printStage == PrintStage.Main)
            {
                var main = GetMainGrid();
                if (main == null || main.Columns.Count == 0 || main.Rows.Count == 0)
                {
                    AdvanceToDetails(e, false);
                    return;
                }

                y = DrawGridHeader(g, main, left, y, totalWidth, rowH, fontHead);

                for (; _printRowIndexMain < main.Rows.Count; _printRowIndexMain++)
                {
                    if (y + rowH > bounds.Bottom) { e.HasMorePages = true; return; }
                    var r = main.Rows[_printRowIndexMain];
                    DrawGridRow(g, main, r, left, y, totalWidth, rowH, fontCell);
                    y += rowH;
                }

                AdvanceToDetails(e, true);
                return;
            }

            // 2) Détails
            if (_printStage == PrintStage.Details)
            {
                // Blocs “Tout déplier”
                var expand = GetExpandBlocksFlow();
                if (expand != null && expand.Visible && _printBlocks.Count > 0)
                {
                    while (_printBlockIndex < _printBlocks.Count)
                    {
                        var (title, grid) = _printBlocks[_printBlockIndex];

                        if (_printRowIndexInBlock == 0)
                        {
                            y += 8;
                            g.DrawString(title, fontHead, Brushes.Black, left, y);
                            y += fontHead.GetHeight(g) + 4;
                            y = DrawGridHeader(g, grid, left, y, totalWidth, rowH, fontHead);
                        }

                        for (; _printRowIndexInBlock < grid.Rows.Count; _printRowIndexInBlock++)
                        {
                            if (y + rowH > bounds.Bottom) { e.HasMorePages = true; return; }
                            var rr = grid.Rows[_printRowIndexInBlock];
                            DrawGridRow(g, grid, rr, left, y, totalWidth, rowH, fontCell);
                            y += rowH;
                        }

                        _printBlockIndex++;
                        _printRowIndexInBlock = 0;

                        if (_printBlockIndex < _printBlocks.Count)
                        {
                            float needed = fontHead.GetHeight(g) + 4 + rowH * 2;
                            if (y + needed > bounds.Bottom) { e.HasMorePages = true; return; }
                        }
                    }

                    _printStage = PrintStage.Done;
                }
                // Détail simple (seconde grille)
                else
                {
                    var details = GetDetailGrid();
                    if (details != null && details.Visible && details.DataSource != null && details.Rows.Count > 0)
                    {
                        y += 8;
                        g.DrawString("Détails", fontHead, Brushes.Black, left, y);
                        y += fontHead.GetHeight(g) + 4;

                        y = DrawGridHeader(g, details, left, y, totalWidth, rowH, fontHead);

                        for (; _printRowIndexDetails < details.Rows.Count; _printRowIndexDetails++)
                        {
                            if (y + rowH > bounds.Bottom) { e.HasMorePages = true; return; }
                            var r = details.Rows[_printRowIndexDetails];
                            DrawGridRow(g, details, r, left, y, totalWidth, rowH, fontCell);
                            y += rowH;
                        }

                        _printStage = PrintStage.Done;
                    }
                    else
                    {
                        _printStage = PrintStage.Done;
                    }
                }
            }

            e.HasMorePages = (_printStage != PrintStage.Done);
        }

        private void AdvanceToDetails(PrintPageEventArgs e, bool forceNewPage)
        {
            bool hasBlocks = (GetExpandBlocksFlow() != null && GetExpandBlocksFlow()!.Visible && _printBlocks.Count > 0);
            var details = GetDetailGrid();
            bool hasSimple = (details != null && details.Visible && details.DataSource != null && details.Rows.Count > 0);

            if (hasBlocks || hasSimple)
            {
                _printStage = PrintStage.Details;
                if (forceNewPage) { e.HasMorePages = true; return; }
            }
            else
            {
                _printStage = PrintStage.Done;
            }
        }

        private static (string Title, DataGridView? Grid) ExtractCardTitleAndGrid(Control card)
        {
            var lbl = card.Controls.OfType<Label>().FirstOrDefault()
                      ?? card.Controls.OfType<TableLayoutPanel>().SelectMany(t => t.Controls.OfType<Label>()).FirstOrDefault();

            var grid = card.Controls.OfType<DataGridView>().FirstOrDefault()
                       ?? card.Controls.OfType<TableLayoutPanel>().SelectMany(t => t.Controls.OfType<DataGridView>()).FirstOrDefault();

            return (lbl?.Text ?? "Détails", grid);
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
                g.DrawString(cols[i].HeaderText, fontHead, Brushes.Black,
                    new RectangleF(x + 6, y + 4, w - 12, rowH - 8));
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
                g.DrawString(text, fontCell, Brushes.Black,
                    new RectangleF(x + 6, y + 4, w - 12, rowH - 8));
                x += w;
            }
        }
    }
}