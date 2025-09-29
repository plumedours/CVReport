using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CVReport.Services
{
    public static class TranslationService
    {
        private static readonly object _gate = new();
        private static Dictionary<string, string> _curr = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _fallback = new(StringComparer.OrdinalIgnoreCase);

        public static string Folder { get; private set; } = "Languages";
        public static string DefaultLanguage { get; private set; } = "en";
        public static string CurrentLanguage { get; private set; } = "en";

        public static event EventHandler? LanguageChanged;

        public static void InitializeFromSettings()
        {
            // lit d’abord appsettings.Development.json puis appsettings.json
            var baseDir = AppContext.BaseDirectory;
            var roots = new[] { "appsettings.Development.json", "appsettings.json" }
                        .Select(f => Path.Combine(baseDir, f))
                        .Where(File.Exists)
                        .Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement);

            string? folder = null, def = null, curr = null;

            foreach (var r in roots)
            {
                if (r.TryGetProperty("Localization", out var loc))
                {
                    if (folder is null && loc.TryGetProperty("LanguagesFolder", out var lf))
                        folder = lf.GetString();
                    if (def is null && loc.TryGetProperty("DefaultLanguage", out var dl))
                        def = dl.GetString();
                    if (curr is null && loc.TryGetProperty("CurrentLanguage", out var cl))
                        curr = cl.GetString();
                }
            }

            Folder = string.IsNullOrWhiteSpace(folder) ? "Languages" : folder!;
            DefaultLanguage = string.IsNullOrWhiteSpace(def) ? "en" : def!;
            var lang = string.IsNullOrWhiteSpace(curr) ? DefaultLanguage : curr!;
            SetLanguage(lang); // charge tout de suite
        }

        public static IEnumerable<string> AvailableLanguages()
        {
            if (!Directory.Exists(Folder)) yield break;
            foreach (var f in Directory.EnumerateFiles(Folder, "*.json"))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        public static void SetLanguage(string code)
        {
            lock (_gate)
            {
                CurrentLanguage = code;
                _curr = LoadDict(code);
                _fallback = DefaultLanguage.Equals(code, StringComparison.OrdinalIgnoreCase)
                          ? _curr
                          : LoadDict(DefaultLanguage);
            }
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string T(string key, params object[]? args)
        {
            string raw;
            lock (_gate)
            {
                if (!_curr.TryGetValue(key, out raw!) && !_fallback.TryGetValue(key, out raw!))
                    raw = "[" + key + "]";
            }
            return (args is { Length: > 0 }) ? string.Format(raw, args) : raw;
        }

        private static Dictionary<string, string> LoadDict(string code)
        {
            var path = Path.Combine(Folder, code + ".json");
            if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var json = JsonDocument.Parse(File.ReadAllText(path));
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in json.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.GetString() ?? "";
            return dict;
        }
    }
}