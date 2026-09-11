using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SysMonitor
{
    public static class AppSettings
    {
        public static event Action SettingsChanged;

        private static string _settingsFile;
        private static ThemeMode _theme = ThemeMode.Dark;
        private static AppLanguage _lang = AppLanguage.Zh;

        public static ThemeMode Theme
        {
            get { return _theme; }
        }

        public static AppLanguage Language
        {
            get { return _lang; }
        }

        static AppSettings()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysMonitor");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                _settingsFile = Path.Combine(dir, "app_settings.json");
            }
            catch
            {
                _settingsFile = "app_settings.json";
            }
            Load();
        }

        public static readonly string ModulePower = "power";
        public static readonly string ModuleNetwork = "net";
        public static readonly string ModuleZeroTier = "zt";
        public static readonly string ModuleCompute = "compute";

        public static readonly string[] AllModuleKeys = new string[] { "power", "net", "zt", "compute" };

        private static List<string> _moduleOrder = new List<string> { "power", "net", "zt", "compute" };
        private static List<string> _moduleEnabled = new List<string> { "power", "net", "zt" };

        public static List<string> ModuleOrder
        {
            get { return new List<string>(_moduleOrder); }
        }

        public static List<string> ModuleEnabled
        {
            get { return new List<string>(_moduleEnabled); }
        }

        public static bool IsValidModuleKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < AllModuleKeys.Length; i++)
            {
                if (AllModuleKeys[i] == key) return true;
            }
            return false;
        }

        public static List<string> GetActiveModules()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < _moduleOrder.Count; i++)
            {
                string m = _moduleOrder[i];
                if (_moduleEnabled.Contains(m))
                {
                    list.Add(m);
                }
            }
            if (list.Count == 0)
            {
                list.Add(ModulePower);
            }
            return list;
        }

        public static void SetWidgetModules(List<string> order, List<string> enabled)
        {
            if (order != null && order.Count > 0)
            {
                List<string> cleanOrder = new List<string>();
                for (int i = 0; i < order.Count; i++)
                {
                    if (IsValidModuleKey(order[i]) && !cleanOrder.Contains(order[i]))
                        cleanOrder.Add(order[i]);
                }
                for (int i = 0; i < AllModuleKeys.Length; i++)
                {
                    string k = AllModuleKeys[i];
                    if (!cleanOrder.Contains(k)) cleanOrder.Add(k);
                }
                _moduleOrder = cleanOrder;
            }

            if (enabled != null && enabled.Count > 0)
            {
                List<string> cleanEnabled = new List<string>();
                for (int i = 0; i < enabled.Count; i++)
                {
                    if (IsValidModuleKey(enabled[i]) && !cleanEnabled.Contains(enabled[i]))
                        cleanEnabled.Add(enabled[i]);
                }
                if (cleanEnabled.Count > 0)
                {
                    _moduleEnabled = cleanEnabled;
                }
            }

            Save();
            if (SettingsChanged != null) SettingsChanged();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = File.ReadAllText(_settingsFile, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> dict = ser.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("theme"))
                        {
                            string t = dict["theme"].ToString().Trim().ToLowerInvariant();
                            _theme = t == "light" ? ThemeMode.Light : ThemeMode.Dark;
                        }
                        if (dict.ContainsKey("language"))
                        {
                            string l = dict["language"].ToString().Trim().ToLowerInvariant();
                            _lang = l == "en" ? AppLanguage.En : AppLanguage.Zh;
                        }
                        if (dict.ContainsKey("module_order"))
                        {
                            System.Collections.IEnumerable list = dict["module_order"] as System.Collections.IEnumerable;
                            if (list != null)
                            {
                                List<string> parsed = new List<string>();
                                foreach (object item in list)
                                {
                                    string s = item != null ? item.ToString().Trim().ToLowerInvariant() : "";
                                    if (IsValidModuleKey(s) && !parsed.Contains(s))
                                        parsed.Add(s);
                                }
                                if (parsed.Count > 0)
                                {
                                    for (int i = 0; i < AllModuleKeys.Length; i++)
                                    {
                                        string k = AllModuleKeys[i];
                                        if (!parsed.Contains(k)) parsed.Add(k);
                                    }
                                    _moduleOrder = parsed;
                                }
                            }
                        }
                        if (dict.ContainsKey("module_enabled"))
                        {
                            System.Collections.IEnumerable list = dict["module_enabled"] as System.Collections.IEnumerable;
                            if (list != null)
                            {
                                List<string> parsed = new List<string>();
                                foreach (object item in list)
                                {
                                    string s = item != null ? item.ToString().Trim().ToLowerInvariant() : "";
                                    if (IsValidModuleKey(s) && !parsed.Contains(s))
                                        parsed.Add(s);
                                }
                                if (parsed.Count > 0)
                                {
                                    _moduleEnabled = parsed;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            AppTheme.SetTheme(_theme);
            I18n.SetLanguage(_lang);
        }

        public static void Save()
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> dict = new Dictionary<string, object>
                {
                    { "theme", _theme == ThemeMode.Light ? "light" : "dark" },
                    { "language", _lang == AppLanguage.En ? "en" : "zh" },
                    { "module_order", _moduleOrder },
                    { "module_enabled", _moduleEnabled }
                };
                string json = ser.Serialize(dict);
                File.WriteAllText(_settingsFile, json, Encoding.UTF8);
            }
            catch { }
        }

        public static void SetTheme(ThemeMode mode)
        {
            if (_theme == mode) return;
            _theme = mode;
            AppTheme.SetTheme(mode);
            Save();
            if (SettingsChanged != null) SettingsChanged();
        }

        public static void SetLanguage(AppLanguage lang)
        {
            if (_lang == lang) return;
            _lang = lang;
            I18n.SetLanguage(lang);
            Save();
            if (SettingsChanged != null) SettingsChanged();
        }
    }
}
