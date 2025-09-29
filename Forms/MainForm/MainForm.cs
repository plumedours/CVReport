using CVReport.Forms;
using CVReport.Forms.Reports;
using CVReport.Services;
using RapportCVVentes.Data;
using RDPChecker.Forms;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using WinFormsToolTip = System.Windows.Forms.ToolTip;

namespace CVReport
{
    public partial class MainForm : Form
    {
        private readonly SqlDb _db;
        private static readonly System.Threading.SemaphoreSlim _cxTablesGate = new(1, 1);

        private enum ReportMode { None, MaterialsSummary, ByCabinet, Soumission }
        private ReportMode _mode = ReportMode.None;

        private FlowLayoutPanel? _expandFlow;
        private bool _isExpanded = false;

        // wrapper pour header+content afin d’éviter tout chevauchement
        private TableLayoutPanel? _rootLayout;

        private bool _suppressDetails = false;

        // évite de câbler plusieurs fois les boutons d'impression/PDF
        private bool _printingUiWired = false;

        private bool _eventsWired;

        private ByCabinetForm? _byCabinetForm;
        private MaterialsSummaryForm? _materialsForm;
        private SoumissionForm? _soumissionForm;

        public MainForm(SqlDb db)
        {
            InitializeComponent();
            
            //// 1) initialiser i18n avant d’appliquer les textes
            TranslationService.InitializeFromSettings();

            //// 2) construire le menu Langue en fonction des fichiers présents
            BuildLanguageMenu();

            //// 3) réappliquer automatiquement quand on change de langue
            TranslationService.LanguageChanged += (_, __) =>
            {
                BuildLanguageMenu();
                Translator.Apply(this, tooltip);
            };

            this.Icon = Properties.Resources.favicon;
            this.Text = "CV Report";
            _db = db ?? throw new ArgumentNullException(nameof(db));

            Translator.Apply(this, tooltip);
        }

        private void BuildLanguageMenu()
        {
            var ms = this.Controls.OfType<MenuStrip>().FirstOrDefault();
            if (ms == null) return;

            var langRoot = ms.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(i =>
                    string.Equals(i.Tag as string, "menu.language", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.Text?.Trim(), "menu.language", StringComparison.OrdinalIgnoreCase));
            if (langRoot == null) return;

            langRoot.DropDownItems.Clear();

            foreach (var code in CVReport.Services.TranslationService.AvailableLanguages().OrderBy(c => c))
            {
                // libellé localisé pour fr/en ; fallback => code en majuscules
                var display = code.ToUpperInvariant();

                var mi = new ToolStripMenuItem(display)
                {
                    Tag = "lang:" + code,
                    Checked = code.Equals(CVReport.Services.TranslationService.CurrentLanguage, StringComparison.OrdinalIgnoreCase),
                    CheckOnClick = false
                };
                mi.Click += (_, __) =>
                {
                    CVReport.Services.TranslationService.SetLanguage(code);

                    // Recocher le bon item
                    foreach (ToolStripItem s in langRoot.DropDownItems)
                        if (s is ToolStripMenuItem smi)
                            smi.Checked = ((string?)smi.Tag == "lang:" + CVReport.Services.TranslationService.CurrentLanguage);

                    // Recréer le menu (pour changer "Français"/"English" eux-mêmes)
                    BuildLanguageMenu();
                };
                langRoot.DropDownItems.Add(mi);
            }

            langRoot.DropDownItems.Add(new ToolStripSeparator());
            var add = new ToolStripMenuItem(CVReport.Services.TranslationService.T("menu.language.add"))
            {
                Tag = "menu.language.add"
            };
            add.Click += (s, e) =>
            {
                new FormTraduction().ShowDialog();
                BuildLanguageMenu();
            };
            langRoot.DropDownItems.Add(add);
        }

        private static System.Collections.Generic.IEnumerable<Control> GetAllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var cc in GetAllControls(c))
                    yield return cc;
            }
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            using var f = new AboutForm();
            f.ShowDialog(this);
        }

        private void helpMenuItem_Click(object sender, EventArgs e)
        {
            using var f = new HelpForm();
            f.ShowDialog(this);
        }

        private void quitMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void cabReportBtn_Click(object sender, EventArgs e)
        {
            if (_byCabinetForm == null || _byCabinetForm.IsDisposed)
            {
                _byCabinetForm = new ByCabinetForm(_db, EnsureCxTablesAsync);
                _byCabinetForm.FormClosed += (_, __) => _byCabinetForm = null;
                _byCabinetForm.Show(this);
            }
            else
            {
                _byCabinetForm.Activate();
            }
        }

        private async void matReportBtn_Click(object sender, EventArgs e)
        {
            if (_materialsForm == null || _materialsForm.IsDisposed)
            {
                _materialsForm = new MaterialsSummaryForm(_db, EnsureCxTablesAsync);
                _materialsForm.FormClosed += (_, __) => _materialsForm = null;
                _materialsForm.Show(this);
            }
            else
            {
                _materialsForm.Activate();
            }
        }

        private void soumReportBtn_Click(object sender, EventArgs e)
        {
            if (_soumissionForm == null || _soumissionForm.IsDisposed)
            {
                _soumissionForm = new SoumissionForm(_db, EnsureCxTablesAsync);
                _soumissionForm.FormClosed += (_, __) => _soumissionForm = null;
                _soumissionForm.Show(this);
            }
            else
            {
                _soumissionForm.Activate();
            }
        }
    }
}