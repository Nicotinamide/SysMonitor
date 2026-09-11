using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SysMonitor.Linux
{
    /// <summary>
    /// Linux 原生安全持久化存储引擎
    /// 基于 /etc/machine-id 与当前用户上下文生成硬件级派生密钥，对 Token 进行 AES 加密保护，并严格设置 0600 文件权限
    /// </summary>
    public static class LinuxSecretStorage
    {
        private static readonly string ConfigDir;
        private static readonly string ConfigPath;

        static LinuxSecretStorage()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            ConfigDir = Path.Combine(home, ".config", "SysMonitor");
            ConfigPath = Path.Combine(ConfigDir, "config.json");
        }

        public static void SaveSecureConfig(string jsonPlain)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                byte[] key = DeriveMachineKey();
                byte[] encrypted = EncryptAes(Encoding.UTF8.GetBytes(jsonPlain), key);
                File.WriteAllBytes(ConfigPath, encrypted);

                // 尝试将文件权限限制为仅当前用户可读写 (0600)
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = "600 \"" + ConfigPath + "\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var p = System.Diagnostics.Process.Start(psi))
                        {
                            p?.WaitForExit(500);
                        }
                    }
                }
                catch { }
            }
            catch
            {
            }
        }

        public static string LoadSecureConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;

                byte[] encrypted = File.ReadAllBytes(ConfigPath);
                byte[] key = DeriveMachineKey();
                byte[] decrypted = DecryptAes(encrypted, key);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DeriveMachineKey()
        {
            string machineId = "";
            string[] idFiles = new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" };
            foreach (var f in idFiles)
            {
                if (File.Exists(f))
                {
                    try { machineId = File.ReadAllText(f).Trim(); break; } catch { }
                }
            }

            if (string.IsNullOrEmpty(machineId))
            {
                machineId = Environment.MachineName + Environment.UserName;
            }

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes("SysMonitor_Salt_" + machineId));
            }
        }

        private static byte[] EncryptAes(byte[] data, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        private static byte[] DecryptAes(byte[] encryptedData, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(encryptedData, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(encryptedData, iv.Length, encryptedData.Length - iv.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }
    }
}
