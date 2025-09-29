using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RDPChecker
{
    public static class TranslationProvider
    {
        public static Dictionary<string, string> Translations { get; private set; } = new();
        public static string LangFolder { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang");
        public static string CurrentLang { get; private set; } = "fr";

        public static event EventHandler LanguageChanged;

        public static void Load(string langCode)
        {
            CurrentLang = langCode;
            string path = Path.Combine(LangFolder, $"{langCode}.json");

            if (!Directory.Exists(LangFolder))
                Directory.CreateDirectory(LangFolder);

            if (!File.Exists(path))
            {
                Translations = new();
                LanguageChanged?.Invoke(null, EventArgs.Empty);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                Translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                Translations = new();
            }

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string T(string key)
        {
            if (Translations != null && Translations.TryGetValue(key, out var v))
                return v;
            return key; // fallback
        }
    }
}