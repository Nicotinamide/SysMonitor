using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace SysMonitor
{
    // ==========================================
    // Windows DPAPI Native Credential Encryption
    // ==========================================
    public static class SecretStorage
    {
        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(cipherBytes);
            }
            catch
            {
                return "";
            }
        }

        public static string Unprotect(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return "";
            }
        }
    }

    public class MemberDirectoryConfig
    {
        public string ControllerUrl { get; set; }
        public string NetworkId { get; set; }
        public string ApiToken { get; set; }

        public MemberDirectoryConfig()
        {
            ControllerUrl = "";
            NetworkId = "";
            ApiToken = "";
        }
    }

    public enum MemberSyncState
    {
        None,
        Syncing,
        Synced,
        Cached,
        NoToken,
        Unauthorized,
        Forbidden,
        Failed
    }

    public class MemberDirectoryEngine
    {
        public event Action<List<MemberNode>> MembersUpdated;
        public event Action<string> StatusChanged;
        public event Action ConfigChanged;
        public event Action<MemberSyncState, string> SyncError;

        private MemberDirectoryConfig _config = new MemberDirectoryConfig();
        private List<MemberNode> _members = new List<MemberNode>();
        private readonly object _lock = new object();
        private Timer _timerSync;
        private bool _isFetching = false;
        private MemberSyncState _lastSyncState = MemberSyncState.None;
        private int _lastSyncCount = 0;

        private string _appDataDir;
        private string _encryptedCredFile;
        private string _cacheFile;

        public MemberDirectoryConfig CurrentConfig
        {
            get
            {
                lock (_lock)
                {
                    return new MemberDirectoryConfig
                    {
                        ControllerUrl = _config.ControllerUrl,
                        NetworkId = _config.NetworkId,
                        ApiToken = _config.ApiToken
                    };
                }
            }
        }

        public bool HasToken
        {
            get
            {
                lock (_lock)
                {
                    return !string.IsNullOrEmpty(_config.ApiToken) && !string.IsNullOrEmpty(_config.ApiToken.Trim());
                }
            }
        }

        public string GetCurrentStatusText()
        {
            lock (_lock)
            {
                TranslationSet i18n = I18n.Current;
                switch (_lastSyncState)
                {
                    case MemberSyncState.Syncing:
                        return i18n.Syncing;
                    case MemberSyncState.Synced:
                        return string.Format(i18n.SyncedNodesFormat, _lastSyncCount);
                    case MemberSyncState.Cached:
                        return string.Format(i18n.CachedNodesFormat, _lastSyncCount);
                    case MemberSyncState.NoToken:
                        return i18n.NoTokenHint;
                    case MemberSyncState.Unauthorized:
                        return _members.Count > 0
                            ? string.Format(i18n.Lang == AppLanguage.Zh ? "⚠️ 401未授权 (已缓存 {0})" : "⚠️ 401 Unauthorized (Cached {0})", _members.Count)
                            : ("⚠️ " + i18n.Unauthorized401);
                    case MemberSyncState.Forbidden:
                        return _members.Count > 0
                            ? string.Format(i18n.Lang == AppLanguage.Zh ? "⚠️ 403拒绝访问 (已缓存 {0})" : "⚠️ 403 Forbidden (Cached {0})", _members.Count)
                            : ("⚠️ " + i18n.Forbidden403);
                    case MemberSyncState.Failed:
                        return _members.Count > 0
                            ? string.Format(i18n.Lang == AppLanguage.Zh ? "⚠️ 同步失败 (已缓存 {0})" : "⚠️ Sync Failed (Cached {0})", _members.Count)
                            : ("⚠️ " + (i18n.Lang == AppLanguage.Zh ? "同步失败" : "Sync Failed"));
                    default:
                        if (_members != null && _members.Count > 0)
                            return string.Format(i18n.CachedNodesFormat, _members.Count);
                        return !string.IsNullOrEmpty(_config.ApiToken) ? i18n.Syncing : i18n.NoTokenHint;
                }
            }
        }

        private void SetSyncState(MemberSyncState state, int count = 0)
        {
            lock (_lock)
            {
                _lastSyncState = state;
                _lastSyncCount = count;
            }
            string text = GetCurrentStatusText();
            if (StatusChanged != null) StatusChanged(text);
        }

        public MemberDirectoryEngine()
        {
            _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysMonitor");
            if (!Directory.Exists(_appDataDir))
            {
                try { Directory.CreateDirectory(_appDataDir); } catch { }
            }

            _encryptedCredFile = Path.Combine(_appDataDir, "credentials.dat");
            _cacheFile = Path.Combine(_appDataDir, "members_cache.json");

            LoadConfig();
            LoadOfflineCache();

            AppSettings.SettingsChanged += delegate
            {
                string text = GetCurrentStatusText();
                if (!string.IsNullOrEmpty(text) && StatusChanged != null)
                {
                    StatusChanged(text);
                }
            };

            // Background sync timer: every 5 minutes (300 seconds)
            _timerSync = new Timer(OnTimerSync, null, 1500, 300000);
        }

        private void LoadConfig()
        {
            try
            {
                // 1. Try loading DPAPI encrypted credentials
                if (File.Exists(_encryptedCredFile))
                {
                    string cipher = File.ReadAllText(_encryptedCredFile, Encoding.UTF8);
                    string plainJson = SecretStorage.Unprotect(cipher);
                    if (!string.IsNullOrEmpty(plainJson))
                    {
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        Dictionary<string, object> dict = ser.Deserialize<Dictionary<string, object>>(plainJson);
                        if (dict != null)
                        {
                            if (dict.ContainsKey("controllerUrl")) _config.ControllerUrl = dict["controllerUrl"].ToString().Trim();
                            if (dict.ContainsKey("networkId")) _config.NetworkId = dict["networkId"].ToString().Trim();
                            if (dict.ContainsKey("apiToken")) _config.ApiToken = dict["apiToken"].ToString().Trim();
                            return;
                        }
                    }
                }

                // 2. Migration from old plain text config.json if present
                string oldConfig = Path.Combine(_appDataDir, "config.json");
                if (File.Exists(oldConfig))
                {
                    try
                    {
                        string oldJson = File.ReadAllText(oldConfig, Encoding.UTF8);
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        Dictionary<string, object> dict = ser.Deserialize<Dictionary<string, object>>(oldJson);
                        if (dict != null)
                        {
                            if (dict.ContainsKey("controllerUrl")) _config.ControllerUrl = dict["controllerUrl"].ToString().Trim();
                            if (dict.ContainsKey("networkId")) _config.NetworkId = dict["networkId"].ToString().Trim();
                            if (dict.ContainsKey("apiToken")) _config.ApiToken = dict["apiToken"].ToString().Trim();
                        }
                        // Save to encrypted and delete plain text
                        SaveEncryptedConfig(_config.ControllerUrl, _config.NetworkId, _config.ApiToken);
                        File.Delete(oldConfig);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public void SaveEncryptedConfig(string controllerUrl, string networkId, string apiToken)
        {
            lock (_lock)
            {
                _config.ControllerUrl = controllerUrl != null ? controllerUrl.Trim() : "";
                _config.NetworkId = networkId != null ? networkId.Trim() : "";
                _config.ApiToken = apiToken != null ? apiToken.Trim() : "";

                try
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    string json = ser.Serialize(new Dictionary<string, string>
                    {
                        { "controllerUrl", _config.ControllerUrl },
                        { "networkId", _config.NetworkId },
                        { "apiToken", _config.ApiToken }
                    });

                    string cipher = SecretStorage.Protect(json);
                    File.WriteAllText(_encryptedCredFile, cipher, Encoding.UTF8);
                }
                catch { }
            }

            // Immediately trigger background refresh with new config
            TriggerRefresh();
            if (ConfigChanged != null) ConfigChanged();
        }

        // Live async test probe for the UI settings panel
        public void TestConnectionAsync(string url, string nwid, string token, Action<bool, int, string> callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    try
                    {
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                    }
                    catch { }

                    string baseUrl = (url ?? "").TrimEnd('/');
                    string testUrl = string.Format("{0}/api/v1/networks/{1}/members", baseUrl, nwid ?? "");

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(testUrl);
                    req.Method = "GET";
                    req.Headers.Add("Authorization", "Bearer " + (token ?? "").Trim());
                    req.Headers.Add("X-API-Token", (token ?? "").Trim());
                    req.Timeout = 15000;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        List<MemberNode> list = ParseMembersJson(json);
                        if (callback != null)
                        {
                            callback(true, list.Count, null);
                        }
                    }
                }
                catch (WebException wex)
                {
                    string msg = wex.Message;
                    if (wex.Response is HttpWebResponse)
                    {
                        HttpWebResponse httpResp = (HttpWebResponse)wex.Response;
                        if ((int)httpResp.StatusCode == 401)
                        {
                            msg = I18n.Current.Unauthorized401;
                        }
                        else if ((int)httpResp.StatusCode == 403)
                        {
                            msg = I18n.Current.Forbidden403;
                        }
                        else
                        {
                            msg = string.Format("HTTP {0} {1}", (int)httpResp.StatusCode, httpResp.StatusDescription);
                        }
                    }
                    if (callback != null)
                    {
                        callback(false, 0, msg);
                    }
                }
                catch (Exception ex)
                {
                    if (callback != null)
                    {
                        callback(false, 0, ex.Message);
                    }
                }
            });
        }

        private void LoadOfflineCache()
        {
            try
            {
                if (File.Exists(_cacheFile))
                {
                    string json = File.ReadAllText(_cacheFile, Encoding.UTF8);
                    if (!string.IsNullOrEmpty(json)) json = json.Trim().Trim('\uFEFF');
                    List<MemberNode> list = ParseMembersJson(json);
                    if (list.Count > 0)
                    {
                        lock (_lock)
                        {
                            _members = list;
                            _lastSyncState = MemberSyncState.Cached;
                            _lastSyncCount = list.Count;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveOfflineCache(string rawJson)
        {
            try
            {
                File.WriteAllText(_cacheFile, rawJson, Encoding.UTF8);
            }
            catch { }
        }

        private void OnTimerSync(object state)
        {
            TriggerRefresh();
        }

        public void TriggerRefresh()
        {
            if (_isFetching) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                FetchMembersAsync();
            });
        }

        public List<MemberNode> GetAllMembers()
        {
            lock (_lock)
            {
                return new List<MemberNode>(_members);
            }
        }

        public List<MemberNode> Search(string query)
        {
            List<MemberNode> all = GetAllMembers();
            if (string.IsNullOrEmpty(query))
            {
                all.Sort(delegate(MemberNode a, MemberNode b)
                {
                    if (a.IsOnline != b.IsOnline) return a.IsOnline ? -1 : 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
                return all;
            }

            string q = query.Trim().ToLowerInvariant();
            List<MemberNode> filtered = new List<MemberNode>();

            foreach (MemberNode m in all)
            {
                bool match = false;

                // 1. Name match
                if (!string.IsNullOrEmpty(m.Name) && m.Name.ToLowerInvariant().Contains(q))
                    match = true;

                // 2. IP match
                if (!match && !string.IsNullOrEmpty(m.Ip) && m.Ip.Contains(q))
                    match = true;

                // 3. Node ID match
                if (!match && !string.IsNullOrEmpty(m.Id) && m.Id.ToLowerInvariant().Contains(q))
                    match = true;

                // 4. Pinyin initial match (e.g. jkj -> 极空间)
                if (!match && !string.IsNullOrEmpty(m.PinyinInitials) && m.PinyinInitials.Contains(q))
                    match = true;

                if (match)
                {
                    filtered.Add(m);
                }
            }

            // Sort: exact match/prefix first, online first
            filtered.Sort(delegate(MemberNode a, MemberNode b)
            {
                bool aExact = (!string.IsNullOrEmpty(a.Name) && a.Name.ToLowerInvariant().StartsWith(q)) || (!string.IsNullOrEmpty(a.Ip) && a.Ip.StartsWith(q));
                bool bExact = (!string.IsNullOrEmpty(b.Name) && b.Name.ToLowerInvariant().StartsWith(q)) || (!string.IsNullOrEmpty(b.Ip) && b.Ip.StartsWith(q));
                if (aExact != bExact) return aExact ? -1 : 1;

                if (a.IsOnline != b.IsOnline) return a.IsOnline ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return filtered;
        }

        private void FetchMembersAsync()
        {
            _isFetching = true;

            string token;
            string baseUrl;
            string nwid;

            lock (_lock)
            {
                token = _config.ApiToken;
                baseUrl = _config.ControllerUrl.TrimEnd('/');
                nwid = _config.NetworkId;
            }

            if (string.IsNullOrEmpty(token))
            {
                if (_members.Count > 0)
                    SetSyncState(MemberSyncState.Cached, _members.Count);
                else
                    SetSyncState(MemberSyncState.NoToken);
                _isFetching = false;
                return;
            }

            SetSyncState(MemberSyncState.Syncing);

            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                }
                catch { }

                string membersUrl = string.Format("{0}/api/v1/networks/{1}/members", baseUrl, nwid);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(membersUrl);
                req.Method = "GET";
                req.Headers.Add("Authorization", "Bearer " + token);
                req.Headers.Add("X-API-Token", token);
                req.Timeout = 15000;

                string json = null;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }

                if (!string.IsNullOrEmpty(json))
                {
                    List<MemberNode> list = ParseMembersJson(json);
                    if (list.Count > 0)
                    {
                        lock (_lock)
                        {
                            _members = list;
                        }
                        SaveOfflineCache(json);

                        SetSyncState(MemberSyncState.Synced, list.Count);
                        if (MembersUpdated != null) MembersUpdated(list);
                        return;
                    }
                }

                if (_members.Count > 0)
                    SetSyncState(MemberSyncState.Cached, _members.Count);
                else
                    SetSyncState(MemberSyncState.NoToken);
            }
            catch (WebException wex)
            {
                MemberSyncState errState = MemberSyncState.Failed;
                string errMsg = wex.Message;
                if (wex.Response is HttpWebResponse)
                {
                    HttpWebResponse r = (HttpWebResponse)wex.Response;
                    if ((int)r.StatusCode == 401)
                    {
                        errState = MemberSyncState.Unauthorized;
                        errMsg = I18n.Current.Unauthorized401;
                    }
                    else if ((int)r.StatusCode == 403)
                    {
                        errState = MemberSyncState.Forbidden;
                        errMsg = I18n.Current.Forbidden403;
                    }
                    else
                    {
                        errMsg = string.Format("HTTP {0} {1}", (int)r.StatusCode, r.StatusDescription);
                    }
                }

                SetSyncState(errState, _members.Count);
                if (SyncError != null) SyncError(errState, errMsg);
            }
            catch (Exception ex)
            {
                SetSyncState(MemberSyncState.Failed, _members.Count);
                if (SyncError != null) SyncError(MemberSyncState.Failed, ex.Message);
            }
            finally
            {
                _isFetching = false;
            }
        }

        private List<MemberNode> ParseMembersJson(string json)
        {
            List<MemberNode> list = new List<MemberNode>();
            if (string.IsNullOrEmpty(json)) return list;
            json = json.Trim().Trim('\uFEFF');

            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
                if (root != null && root.ContainsKey("members"))
                {
                    IEnumerable membersArr = root["members"] as IEnumerable;
                    if (membersArr != null)
                    {
                        foreach (object obj in membersArr)
                        {
                            Dictionary<string, object> m = obj as Dictionary<string, object>;
                            if (m == null) continue;

                            MemberNode node = new MemberNode();
                            node.Id = m.ContainsKey("id") && m["id"] != null ? m["id"].ToString() : "";
                            if (string.IsNullOrEmpty(node.Id) && m.ContainsKey("address") && m["address"] != null)
                                node.Id = m["address"].ToString();

                            node.Name = m.ContainsKey("name") && m["name"] != null ? m["name"].ToString() : "";
                            if (string.IsNullOrEmpty(node.Name)) node.Name = "";

                            node.Authorized = m.ContainsKey("authorized") && m["authorized"] != null ? Convert.ToBoolean(m["authorized"]) : false;

                            // IP Assignments
                            if (m.ContainsKey("ipAssignments"))
                            {
                                IEnumerable ips = m["ipAssignments"] as IEnumerable;
                                if (ips != null)
                                {
                                    foreach (object ipObj in ips)
                                    {
                                        if (ipObj != null)
                                        {
                                            string ipStr = ipObj.ToString().Trim();
                                            node.AllIps.Add(ipStr);
                                            // Prefer IPv4
                                            if (string.IsNullOrEmpty(node.Ip) && ipStr.Contains(".") && !ipStr.Contains(":"))
                                            {
                                                node.Ip = ipStr;
                                            }
                                        }
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(node.Ip) && node.AllIps.Count > 0)
                            {
                                node.Ip = node.AllIps[0];
                            }

                            // Peer latency & online state
                            if (m.ContainsKey("peer") && m["peer"] is Dictionary<string, object>)
                            {
                                Dictionary<string, object> p = (Dictionary<string, object>)m["peer"];
                                if (p.ContainsKey("latency") && p["latency"] != null) node.Latency = Convert.ToInt32(p["latency"]);
                                if (p.ContainsKey("role") && p["role"] != null) node.Role = p["role"].ToString();

                                IEnumerable paths = p.ContainsKey("paths") ? p["paths"] as IEnumerable : null;
                                if (paths != null)
                                {
                                    foreach (object pathObj in paths)
                                    {
                                        Dictionary<string, object> pathDict = pathObj as Dictionary<string, object>;
                                        if (pathDict != null && pathDict.ContainsKey("address") && pathDict["address"] != null)
                                        {
                                            node.PhysicalAddress = pathDict["address"].ToString();
                                            break;
                                        }
                                    }
                                }
                            }

                            node.IsOnline = (node.Latency >= 0) || !string.IsNullOrEmpty(node.PhysicalAddress);
                            node.PinyinInitials = GetPinyinInitials(node.Name);

                            list.Add(node);
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        // Lightweight Simplified Chinese Pinyin Initial Extractor (GB2312)
        public static string GetPinyinInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            try
            {
                Encoding gb = Encoding.GetEncoding("GB2312");
                foreach (char c in text)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    {
                        sb.Append(char.ToLowerInvariant(c));
                    }
                    else if (c >= 0x4e00 && c <= 0x9fa5)
                    {
                        byte[] arr = gb.GetBytes(c.ToString());
                        if (arr.Length >= 2)
                        {
                            int code = (arr[0] << 8) + arr[1];
                            int[] areacode = { 45217, 45253, 45761, 46318, 46826, 47010, 47297, 47614, 48119, 48119, 49062, 49324, 49896, 50371, 50614, 50622, 50906, 51387, 51446, 52218, 52698, 52698, 52698, 52980, 53689, 54481 };
                            for (int i = 0; i < 26; i++)
                            {
                                int max = (i == 25) ? 55290 : areacode[i + 1];
                                if (areacode[i] <= code && code < max)
                                {
                                    sb.Append((char)('a' + i));
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback
                foreach (char c in text)
                {
                    if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }
    }
}
