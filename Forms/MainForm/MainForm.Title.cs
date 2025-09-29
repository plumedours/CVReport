using CVReport.Data.Sql;
using CVReport.Services;
using RapportCVVentes.Data;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CVReport
{
    public partial class MainForm
    {
        // Une seule source de vérité pour le Job Number
        private string? _currentJobNumber;

        /// <summary>Charge le Job Number et met à jour le titre (reportTitleLabel).</summary>
        private async Task LoadReportTitleAsync()
        {
            try
            {
                var dt = await _db.QueryAsync(Queries.JobInfo.GetLatestHeader);

                if (dt.Rows.Count == 0)
                {
                    _currentJobNumber = null;
                    //reportTitleLabel.Text = "Rapport des matériaux (aucun projet)";
                    return;
                }

                var row = dt.Rows[0];
                _currentJobNumber = (row["Job Number"]?.ToString() ?? "").Trim();

                //UpdateReportTitleForCurrentMode();
            }
            catch (Exception ex)
            {
                //reportTitleLabel.Text = "Rapport des matériaux (erreur)";
                MessageBox.Show(this, ex.Message, "Erreur lors du chargement du titre",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // =============================================
    // Mutualisation : utilitaire de titre réutilisable
    // =============================================
    public static class ReportTitle
    {
        public static async Task<string?> GetJobNumberAsync(SqlDb db)
        {
            var dt = await db.QueryAsync(Queries.JobInfo.GetLatestHeader);
            if (dt.Rows.Count == 0) return null;
            return (dt.Rows[0]["Job Number"]?.ToString() ?? "").Trim();
        }

        public static async Task SetHeaderAsync(SqlDb db, Label titleLabel)
        {
            try
            {
                var job = await GetJobNumberAsync(db);

                if (string.IsNullOrWhiteSpace(job))
                {
                    // Aucun projet
                    titleLabel.Text = TranslationService.T("main.title.noProject");
                }
                else
                {
                    // Projet trouvé
                    titleLabel.Text = string.Format(
                        TranslationService.T("main.title.project"), job);
                }
            }
            catch
            {
                titleLabel.Text = TranslationService.T("main.title.error");
            }
        }
    }
}