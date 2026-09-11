using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SysMonitor;
using SysMonitor.Linux.UI;

namespace SysMonitor.Linux
{
    public static class LinuxSettings
    {
        public static event Action SettingsChanged;

        private static string _settingsFile;
        private static bool _isDark = false;
        private static AppLanguage _lang = AppLanguage.Zh;

        public static bool IsDark => _isDark;
        public static AppLanguage Language => _lang;

        public const string ModulePower = "power";
        public const string ModuleNetwork = "net";
        public const string ModuleZeroTier = "zt";
        public const string ModuleCompute = "compute";

        public static readonly string[] AllModuleKeys = new string[] { "power", "net", "zt", "compute" };

        private static List<string> _moduleOrder = new List<string> { "power", "net", "zt", "compute" };
        private static List<string> _moduleEnabled = new List<string> { "power", "net", "zt" };

        public static List<string> ModuleOrder => new List<string>(_moduleOrder);
        public static List<string> ModuleEnabled => new List<string>(_moduleEnabled);

        static LinuxSettings()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "SysMonitor");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                _settingsFile = Path.Combine(dir, "app_settings.json");
            }
            catch
            {
                _settingsFile = "app_settings.json";
            }
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = File.ReadAllText(_settingsFile);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("theme", out var themeEl))
                    {
                        string themeStr = themeEl.GetString()?.ToLowerInvariant();
                        _isDark = (themeStr == "dark");
                    }

                    if (root.TryGetProperty("language", out var langEl))
                    {
                        string langStr = langEl.GetString()?.ToLowerInvariant();
                        _lang = (langStr == "en") ? AppLanguage.En : AppLanguage.Zh;
                    }

                    if (root.TryGetProperty("module_order", out var orderEl) && orderEl.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in orderEl.EnumerateArray())
                        {
                            string s = item.GetString();
                            if (!string.IsNullOrEmpty(s) && Array.IndexOf(AllModuleKeys, s) >= 0 && !list.Contains(s))
                                list.Add(s);
                        }
                        foreach (var k in AllModuleKeys)
                        {
                            if (!list.Contains(k)) list.Add(k);
                        }
                        if (list.Count > 0) _moduleOrder = list;
                    }

                    if (root.TryGetProperty("module_enabled", out var enabledEl) && enabledEl.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in enabledEl.EnumerateArray())
                        {
                            string s = item.GetString();
                            if (!string.IsNullOrEmpty(s) && Array.IndexOf(AllModuleKeys, s) >= 0 && !list.Contains(s))
                                list.Add(s);
                        }
                        if (list.Count > 0) _moduleEnabled = list;
                    }
                }
            }
            catch { }

            LinuxTheme.SetDark(_isDark);
            I18n.SetLanguage(_lang);
        }

        public static void Save()
        {
            try
            {
                var dict = new Dictionary<string, object>
                {
                    { "theme", _isDark ? "dark" : "light" },
                    { "language", _lang == AppLanguage.En ? "en" : "zh" },
                    { "module_order", _moduleOrder },
                    { "module_enabled", _moduleEnabled }
                };
                string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch { }
        }

        public static void SetTheme(bool isDark)
        {
            if (_isDark == isDark) return;
            _isDark = isDark;
            LinuxTheme.SetDark(isDark);
            Save();
            SettingsChanged?.Invoke();
        }

        public static void SetLanguage(AppLanguage lang)
        {
            if (_lang == lang) return;
            _lang = lang;
            I18n.SetLanguage(lang);
            Save();
            SettingsChanged?.Invoke();
        }

        public static bool ToggleModule(string key)
        {
            if (_moduleEnabled.Contains(key))
            {
                if (_moduleEnabled.Count <= 1) return false;
                _moduleEnabled.Remove(key);
            }
            else
            {
                _moduleEnabled.Add(key);
            }
            Save();
            SettingsChanged?.Invoke();
            return true;
        }

        public static bool IsModuleEnabled(string key) => _moduleEnabled.Contains(key);
    }
}
