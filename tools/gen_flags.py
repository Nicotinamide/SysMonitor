import os
import glob
import base64

flags_dir = r"C:\Users\4955\.gemini\antigravity\scratch\sys_monitor_widget\assets\flags"
out_path = r"C:\Users\4955\.gemini\antigravity\scratch\SysMonitor_Native\src\FlagAssets.cs"

lines = [
    "using System;",
    "using System.Collections.Generic;",
    "using System.IO;",
    "using System.Windows.Media.Imaging;",
    "",
    "namespace SysMonitor",
    "{",
    "    public static class FlagAssets",
    "    {",
    "        public static readonly Dictionary<string, string> Base64Flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)",
    "        {"
]

for f in sorted(glob.glob(os.path.join(flags_dir, "*.png"))):
    code = os.path.splitext(os.path.basename(f))[0].upper()
    with open(f, "rb") as fp:
        b64 = base64.b64encode(fp.read()).decode("ascii")
    lines.append(f'            {{ "{code}", "{b64}" }},')

lines.extend([
    "        };",
    "",
    "        public static BitmapSource GetFlagImage(string countryCode)",
    "        {",
    "            if (string.IsNullOrEmpty(countryCode)) return null;",
    "            string b64;",
    "            if (!Base64Flags.TryGetValue(countryCode.Trim(), out b64)) return null;",
    "            try",
    "            {",
    "                byte[] bytes = Convert.FromBase64String(b64);",
    "                using (MemoryStream ms = new MemoryStream(bytes))",
    "                {",
    "                    BitmapImage img = new BitmapImage();",
    "                    img.BeginInit();",
    "                    img.CacheOption = BitmapCacheOption.OnLoad;",
    "                    img.StreamSource = ms;",
    "                    img.EndInit();",
    "                    img.Freeze();",
    "                    return img;",
    "                }",
    "            }",
    "            catch { return null; }",
    "        }",
    "    }",
    "}"
])

with open(out_path, "w", encoding="utf-8") as fp:
    fp.write("\n".join(lines))
print("FlagAssets.cs generated successfully!")
