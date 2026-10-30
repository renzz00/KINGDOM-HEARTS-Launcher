using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Kingdom_Hearts_Launcher
{
    // Adds this launcher to Steam as a non-Steam game by editing the binary shortcuts.vdf, and installs its artwork.
    public static class SteamShortcuts
    {
        public const string AppName = "KINGDOM HEARTS Launcher";

        // Artwork looked up by name (.png / .jpg) in a "Steam Assets" folder next to the exe, then in the exe's
        // embedded Assets/Steam resources. Value is the suffix Steam expects in userdata\<id>\config\grid.
        private static readonly Dictionary<string, string> ArtworkSuffixes = new Dictionary<string, string>
        {
            { "capsule", "p" },    // portrait capsule, 600x900
            { "wide", "" },        // wide capsule, 460x215
            { "hero", "_hero" },   // hero, 3840x1240
            { "logo", "_logo" },   // logo overlaid on the hero
            { "icon", "_icon" },   // library icon, 512x512
        };

        // Optional logo.json (Steam's logo position on the hero) is installed as <id>.json.
        private const string LogoPositionAsset = "logo.json";

        private static readonly string[] ArtworkExtensions = { ".png", ".jpg", ".jpeg" };

        public class Result
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        private static string FindSteamDir()
        {
            string steamDir = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            return !string.IsNullOrEmpty(steamDir) && Directory.Exists(steamDir) ? steamDir.Replace('/', '\\') : null;
        }

        public static bool IsSteamInstalled
        {
            get { return FindSteamDir() != null; }
        }

        private static bool IsSteamRunning
        {
            get { return Process.GetProcessesByName("steam").Length > 0; }
        }

        // True if Steam already has a shortcut pointing at this exe, so the first-run offer isn't shown for it.
        public static bool IsLauncherAdded()
        {
            try
            {
                string steamDir = FindSteamDir();
                string configDir = steamDir == null ? null : FindConfigDir(steamDir);
                string path = configDir == null ? null : Path.Combine(configDir, "shortcuts.vdf");
                if (path == null || !File.Exists(path))
                {
                    return false;
                }

                string quotedExe = "\"" + Process.GetCurrentProcess().MainModule.FileName + "\"";
                VdfMap shortcuts = Vdf.Read(File.ReadAllBytes(path)).Get<VdfMap>("shortcuts");
                return shortcuts != null && shortcuts.Values.OfType<VdfMap>()
                    .Any(e => string.Equals(e.Get<string>("Exe"), quotedExe, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                return false;
            }
        }

        // Steam rewrites shortcuts.vdf from memory when it exits, so it has to be closed while we edit the file.
        // With closeSteam, a running Steam is shut down cleanly first and started again afterwards.
        public static Result AddLauncher(bool closeSteam)
        {
            string steamDir = FindSteamDir();
            if (steamDir == null)
            {
                return Fail("Steam installation not found.");
            }

            string steamExe = Path.Combine(steamDir, "steam.exe");
            bool wasRunning = IsSteamRunning;
            if (wasRunning)
            {
                if (!closeSteam)
                {
                    return Fail("Close Steam completely (including its tray icon), then try again. Steam overwrites its shortcuts when it exits.");
                }
                if (!ShutdownSteam(steamExe))
                {
                    return Fail("Steam didn't close. Close it yourself (including its tray icon), then try again.");
                }
            }

            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string assetsDir = Path.Combine(Path.GetDirectoryName(exe), "Steam Assets");
            Result result = AddTo(steamDir, exe, assetsDir);

            if (wasRunning)
            {
                StartSteam(steamExe);
            }
            return result;
        }

        private static bool ShutdownSteam(string steamExe)
        {
            try
            {
                Process.Start(new ProcessStartInfo(steamExe, "-shutdown") { UseShellExecute = false });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }

            for (int i = 0; i < 90 && IsSteamRunning; i++)
            {
                System.Threading.Thread.Sleep(500);
            }
            if (IsSteamRunning)
            {
                return false;
            }
            System.Threading.Thread.Sleep(1500); // let Steam finish writing its files
            return true;
        }

        private static void StartSteam(string steamExe)
        {
            try
            {
                Process.Start(new ProcessStartInfo(steamExe) { UseShellExecute = false });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Nothing more to do; the user can start Steam themselves.
            }
        }

        public static Result AddTo(string steamDir, string exe, string assetsDir)
        {
            string configDir = FindConfigDir(steamDir);
            if (configDir == null)
            {
                return Fail("No Steam account folder found. Sign in to Steam once, then try again.");
            }

            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            try
            {
                VdfMap root = File.Exists(shortcutsPath) ? Vdf.Read(File.ReadAllBytes(shortcutsPath)) : new VdfMap();
                VdfMap shortcuts = root.Get<VdfMap>("shortcuts");
                if (shortcuts == null)
                {
                    shortcuts = new VdfMap();
                    root.Set("shortcuts", shortcuts);
                }

                string quotedExe = "\"" + exe + "\"";
                uint id = ShortcutId(quotedExe, AppName);

                // Match by exe or by name, so moving or renaming the launcher updates its entry instead of adding another.
                VdfMap entry = shortcuts.Values.OfType<VdfMap>().FirstOrDefault(e =>
                    string.Equals(e.Get<string>("Exe"), quotedExe, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.Get<string>("AppName"), AppName, StringComparison.OrdinalIgnoreCase));
                bool existed = entry != null;
                if (!existed)
                {
                    entry = new VdfMap();
                    shortcuts.Set(NextIndex(shortcuts), entry);
                }

                string gridDir = Path.Combine(configDir, "grid");
                int artwork = InstallArtwork(gridDir, id, assetsDir);
                string iconFile = ArtworkExtensions.Select(ext => Path.Combine(gridDir, id + "_icon" + ext)).FirstOrDefault(File.Exists);

                entry.Set("appid", unchecked((int)id));
                entry.Set("AppName", AppName);
                entry.Set("Exe", quotedExe);
                entry.Set("StartDir", "\"" + Path.GetDirectoryName(exe) + "\"");
                entry.Set("icon", iconFile ?? exe);
                if (entry.Get<string>("ShortcutPath") == null) entry.Set("ShortcutPath", "");
                if (entry.Get<string>("LaunchOptions") == null) entry.Set("LaunchOptions", "");
                if (!entry.Has("IsHidden")) entry.Set("IsHidden", 0);
                if (!entry.Has("AllowDesktopConfig")) entry.Set("AllowDesktopConfig", 1);
                if (!entry.Has("AllowOverlay")) entry.Set("AllowOverlay", 1);
                if (!entry.Has("OpenVR")) entry.Set("OpenVR", 0);
                if (!entry.Has("LastPlayTime")) entry.Set("LastPlayTime", 0);
                if (!entry.Has("tags")) entry.Set("tags", new VdfMap());

                if (File.Exists(shortcutsPath))
                {
                    File.Copy(shortcutsPath, shortcutsPath + ".khbackup", true);
                }
                File.WriteAllBytes(shortcutsPath, Vdf.Write(root));

                return new Result
                {
                    Success = true,
                    Message = (existed ? "Updated" : "Added") + " in Steam"
                        + (artwork > 0 ? " with " + artwork + " artwork file(s)" : "")
                        + ". Start Steam to see it in your library."
                };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                return Fail("Could not update Steam's shortcuts: " + ex.Message);
            }
        }

        // The account folder that was used most recently.
        private static string FindConfigDir(string steamDir)
        {
            string userdata = Path.Combine(steamDir, "userdata");
            if (!Directory.Exists(userdata))
            {
                return null;
            }

            return Directory.GetDirectories(userdata)
                .Where(d => Path.GetFileName(d).All(char.IsDigit) && Path.GetFileName(d) != "0")
                .Select(d => Path.Combine(d, "config"))
                .Where(Directory.Exists)
                .OrderByDescending(c => File.Exists(Path.Combine(c, "localconfig.vdf")) ? File.GetLastWriteTimeUtc(Path.Combine(c, "localconfig.vdf")) : DateTime.MinValue)
                .FirstOrDefault();
        }

        private static string NextIndex(VdfMap shortcuts)
        {
            int next = shortcuts.Keys.Select(k => { int n; return int.TryParse(k, out n) ? n + 1 : 0; }).DefaultIfEmpty(0).Max();
            return next.ToString();
        }

        // Steam derives a non-Steam game's id from a CRC of its quoted exe path and name; artwork is filed under it.
        public static uint ShortcutId(string quotedExe, string appName)
        {
            return Crc32(Encoding.UTF8.GetBytes(quotedExe + appName)) | 0x80000000;
        }

        private static int InstallArtwork(string gridDir, uint id, string assetsDir)
        {
            int count = 0;
            foreach (KeyValuePair<string, string> art in ArtworkSuffixes)
            {
                foreach (string ext in ArtworkExtensions)
                {
                    byte[] data = LoadAsset(assetsDir, art.Key + ext);
                    if (data == null)
                    {
                        continue;
                    }

                    Directory.CreateDirectory(gridDir);
                    foreach (string old in ArtworkExtensions)
                    {
                        string stale = Path.Combine(gridDir, id + art.Value + old);
                        if (File.Exists(stale)) File.Delete(stale);
                    }
                    File.WriteAllBytes(Path.Combine(gridDir, id + art.Value + ext), data);
                    count++;
                    break;
                }
            }

            byte[] logoPosition = LoadAsset(assetsDir, LogoPositionAsset);
            if (logoPosition != null)
            {
                Directory.CreateDirectory(gridDir);
                File.WriteAllBytes(Path.Combine(gridDir, id + ".json"), logoPosition);
                count++;
            }
            return count;
        }

        private static byte[] LoadAsset(string assetsDir, string fileName)
        {
            string path = Path.Combine(assetsDir, fileName);
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }

            try
            {
                string assembly = Uri.EscapeDataString(typeof(SteamShortcuts).Assembly.GetName().Name);
                var info = Application.GetResourceStream(new Uri("pack://application:,,,/" + assembly + ";component/Assets/Steam/" + fileName));
                if (info == null)
                {
                    return null;
                }
                using (Stream s = info.Stream)
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UriFormatException)
            {
                return null;
            }
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
            }
            return ~crc;
        }

        private static Result Fail(string message)
        {
            return new Result { Success = false, Message = message };
        }
    }

    // Ordered key/value map of Steam's binary KeyValues format.
    public class VdfMap
    {
        private readonly List<KeyValuePair<string, object>> items = new List<KeyValuePair<string, object>>();

        public IEnumerable<KeyValuePair<string, object>> Items { get { return items; } }
        public IEnumerable<string> Keys { get { return items.Select(i => i.Key); } }
        public IEnumerable<object> Values { get { return items.Select(i => i.Value); } }

        public bool Has(string key)
        {
            return items.Any(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public T Get<T>(string key) where T : class
        {
            return items.Where(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)).Select(i => i.Value).OfType<T>().FirstOrDefault();
        }

        public void Set(string key, object value)
        {
            int index = items.FindIndex(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                items[index] = new KeyValuePair<string, object>(items[index].Key, value);
            }
            else
            {
                items.Add(new KeyValuePair<string, object>(key, value));
            }
        }
    }

    public static class Vdf
    {
        private const byte TypeMap = 0, TypeString = 1, TypeInt = 2, TypeEnd = 8;

        public static VdfMap Read(byte[] data)
        {
            using (BinaryReader r = new BinaryReader(new MemoryStream(data)))
            {
                return ReadMap(r, true);
            }
        }

        private static VdfMap ReadMap(BinaryReader r, bool isRoot)
        {
            VdfMap map = new VdfMap();
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                byte type = r.ReadByte();
                if (type == TypeEnd)
                {
                    return map;
                }

                string key = ReadString(r);
                switch (type)
                {
                    case TypeMap:
                        map.Set(key, ReadMap(r, false));
                        break;
                    case TypeString:
                        map.Set(key, ReadString(r));
                        break;
                    case TypeInt:
                        map.Set(key, r.ReadInt32());
                        break;
                    default:
                        throw new InvalidDataException("Unsupported value type " + type + " in shortcuts.vdf");
                }
            }

            if (!isRoot)
            {
                throw new InvalidDataException("shortcuts.vdf ended unexpectedly");
            }
            return map;
        }

        private static string ReadString(BinaryReader r)
        {
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = r.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static byte[] Write(VdfMap root)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                WriteMap(w, root);
                return ms.ToArray();
            }
        }

        private static void WriteMap(BinaryWriter w, VdfMap map)
        {
            foreach (KeyValuePair<string, object> item in map.Items)
            {
                if (item.Value is VdfMap)
                {
                    w.Write(TypeMap);
                    WriteString(w, item.Key);
                    WriteMap(w, (VdfMap)item.Value);
                }
                else if (item.Value is int)
                {
                    w.Write(TypeInt);
                    WriteString(w, item.Key);
                    w.Write((int)item.Value);
                }
                else
                {
                    w.Write(TypeString);
                    WriteString(w, item.Key);
                    WriteString(w, (string)item.Value);
                }
            }
            w.Write(TypeEnd);
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            w.Write(Encoding.UTF8.GetBytes(s));
            w.Write((byte)0);
        }
    }
}
