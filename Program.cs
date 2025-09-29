using System;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using RapportCVVentes.Data; // ton SqlDb

namespace CVReport
{
    // Ancre non statique pour UserSecrets
    internal sealed class SecretsAnchor { }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

            // On lit le nom de la connexion active, puis on résout la vraie chaîne
            var activeName = configuration["ActiveConnectionString"]; // ex: "ReportDb_ByAttach"
            var connString = string.IsNullOrWhiteSpace(activeName)
                ? null
                : configuration.GetConnectionString(activeName);

            if (string.IsNullOrWhiteSpace(connString))
            {
                MessageBox.Show(
                    "La chaîne de connexion est vide.\n" +
                    "Vérifie 'ActiveConnectionString' et l'entrée correspondante dans ConnectionStrings.",
                    "Configuration manquante",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var db = new SqlDb(connString);
            Application.Run(new MainForm(db));
        }
    }
}