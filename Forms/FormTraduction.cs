using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using CVReport.Services;

namespace RDPChecker.Forms
{
    public class FormTraduction : Form
    {
        private TextBox txtCode;
        private DataGridView grid;
        private Button btnSave, btnCancel;
        private Label lblCode;
        private string langFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");

        public FormTraduction()
        {
            InitializeComponents();

            ApplyTranslations();
            TranslationService.LanguageChanged += (s, e) => ApplyTranslations();

            // Load keys & english values as default
            LoadKeysFromEnglish();
        }

        private void InitializeComponents()
        {
            this.ClientSize = new System.Drawing.Size(560, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Icon = new Icon("Resources/favicon.ico");

            lblCode = new Label { Left = 10, Top = 10, Width = 220 };
            txtCode = new TextBox { Left = 240, Top = 10, Width = 100 };

            grid = new DataGridView
            {
                Left = 10,
                Top = 40,
                Width = 540,
                Height = 320,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false
            };

            grid.Columns.Add("Key", "Key");
            grid.Columns.Add("Value", "Traduction");
            grid.Columns[0].ReadOnly = true;

            btnSave = new Button { Left = 340, Top = 370, Width = 100 };
            btnSave.Click += (s, e) => SaveTranslation();

            btnCancel = new Button { Left = 450, Top = 370, Width = 100 };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lblCode);
            this.Controls.Add(txtCode);
            this.Controls.Add(grid);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }

        private void ApplyTranslations()
        {
            this.Text = TranslationService.T("form_translation_title");
            lblCode.Text = TranslationService.T("label_lang_code");
            btnSave.Text = TranslationService.T("btn_save") != "btn_save" ? TranslationService.T("btn_save") : TranslationService.T("btn_ok");
            btnCancel.Text = TranslationService.T("btn_cancel");
        }

        private void LoadKeysFromEnglish()
        {
            try
            {
                var enFile = Path.Combine(langFolder, "en.json");
                Dictionary<string, string> dict = null;
                if (File.Exists(enFile))
                {
                    var json = File.ReadAllText(enFile);
                    dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }

                // If no en.json, try fr.json or empty
                if (dict == null)
                {
                    var frFile = Path.Combine(langFolder, "fr.json");
                    if (File.Exists(frFile))
                    {
                        var json = File.ReadAllText(frFile);
                        dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    }
                }

                grid.Rows.Clear();
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        // Put english text as default value so user can edit it
                        grid.Rows.Add(kv.Key, kv.Value ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lecture en.json: " + ex.Message);
            }
        }

        private void SaveTranslation()
        {
            string code = txtCode.Text.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(TranslationService.T("msg_enter_lang_code") ?? "Veuillez entrer un code de langue.");
                return;
            }

            var dict = new Dictionary<string, string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                string key = row.Cells[0].Value?.ToString();
                string value = row.Cells[1].Value?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(key))
                    dict[key] = value;
            }

            try
            {
                if (!Directory.Exists(langFolder))
                    Directory.CreateDirectory(langFolder);

                string path = Path.Combine(langFolder, $"{code}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show(string.Format(TranslationService.T("msg_translation_saved") ?? "Traduction {0} sauvegardée.", code));
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur sauvegarde: " + ex.Message);
            }
        }
    }
}
