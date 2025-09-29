using System;
using System.Linq;
using System.Windows.Forms;

namespace CVReport.Services
{
    public static class Translator
    {
        public static void Apply(Form form, ToolTip? tooltip = null)
        {
            // Traduit le titre de la fenêtre si Tag est défini
            if (form.Tag is string formKey && !string.IsNullOrWhiteSpace(formKey))
                form.Text = TranslationService.T(formKey);

            // Traduit tous les contrôles
            TranslateControls(form.Controls);

            // Traduit les MenuStrip
            foreach (var ms in GetAllControls(form).OfType<MenuStrip>())
                TranslateToolStrip(ms);

            // Traduit les ToolTips
            if (tooltip != null)
                TranslateToolTips(form.Controls, tooltip);
        }

        private static void TranslateControls(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                if (control.Tag is string key && !string.IsNullOrEmpty(key))
                    control.Text = TranslationService.T(key);

                if (control.HasChildren)
                    TranslateControls(control.Controls);
            }
        }

        private static void TranslateToolStrip(ToolStrip ts)
        {
            foreach (ToolStripItem it in ts.Items)
                TranslateToolStripItemRec(it);
        }

        private static void TranslateToolStripItemRec(ToolStripItem it)
        {
            string? key = null;

            if (it.Tag is string tagStr)
            {
                if (tagStr.Contains(".") && !tagStr.StartsWith("lang:", StringComparison.OrdinalIgnoreCase))
                    key = tagStr;
            }

            if (key == null)
            {
                var txt = it.Text?.Trim();
                if (!string.IsNullOrEmpty(txt) && txt.Contains('.'))
                    key = txt;
            }

            if (key != null)
                it.Text = TranslationService.T(key);

            if (it is ToolStripDropDownItem dd && dd.HasDropDownItems)
                foreach (ToolStripItem child in dd.DropDownItems)
                    TranslateToolStripItemRec(child);
        }

        private static void TranslateToolTips(Control.ControlCollection controls, ToolTip tooltip)
        {
            foreach (Control control in controls)
            {
                if (control.Tag is string key && !string.IsNullOrEmpty(key))
                    tooltip.SetToolTip(control, TranslationService.T(key));

                if (control.HasChildren)
                    TranslateToolTips(control.Controls, tooltip);
            }
        }

        private static System.Collections.Generic.IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                yield return control;
                foreach (var child in GetAllControls(control))
                    yield return child;
            }
        }

        public static class GridTranslator
        {
            public static void TranslateColumns(DataGridView g)
            {
                foreach (DataGridViewColumn col in g.Columns)
                {
                    // On part de la DataPropertyName comme clé
                    var prop = col.DataPropertyName;
                    if (!string.IsNullOrEmpty(prop))
                    {
                        var key = prop switch
                        {
                            "CabinetID" => "col.cabinetId",
                            "MaterialID" => "col.materialId",
                            "PartDescription" => "col.partDescription",
                            "MaterialName" => "col.materialName",
                            "Quantity" => "col.quantity",

                            // Soumission / Matériaux (unités & agrégats)
                            "TotalSqFt" => "col.totalSqft",
                            "RealSheets" => "col.realSheets",
                            "RoundedSheets" => "col.roundedSheets",

                            _ => null
                        };

                        if (key != null)
                            col.HeaderText = TranslationService.T(key);
                    }
                }
            }
        }
    }
}