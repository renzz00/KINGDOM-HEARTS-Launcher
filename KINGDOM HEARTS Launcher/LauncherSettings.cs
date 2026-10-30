using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Kingdom_Hearts_Launcher
{
    public class LauncherSettings
    {
        public const string DefaultDaysNativePath = @"C:\Program Files\Kingdom Hearts 3582 Days";

        public string ParentDirectory { get; set; }
        public List<string> Order { get; set; } = new List<string>();
        public string SelectedOrder { get; set; } = "Official";
        public string SelectedGame { get; set; }
        public bool SkipCopyrightScreenOnMovies { get; set; } = true;
        public bool SkipCopyrightScreenOnKH1 { get; set; } = true;
        public string MelonMixPath { get; set; }
        public bool UseMelonMixOnDays { get; set; }
        public bool UseMelonMixOnRecoded { get; set; }
        public string EmulatorPath { get; set; }
        public string RomPath { get; set; }
        public bool ComInsteadOfRecom { get; set; }
        // Either the install folder or the exe of the 358/2 Days native port.
        public string DaysNativePath { get; set; } = DefaultDaysNativePath;
        public bool UseNativeDays { get; set; }
        public string MelodyOfMemoryPath { get; set; }
        public bool ControllerEnabled { get; set; } = true;
        // The "Add to Steam" offer is only made once, on first run.
        public bool SteamPromptShown { get; set; }
        // Likewise the offer to move the launcher into a Steam library folder.
        public bool MovePromptShown { get; set; }
        // Escape hatch for installs that don't match the expected layout: game id -> exe path.
        public Dictionary<string, string> ExeOverrides { get; set; } = new Dictionary<string, string>();

        private const string ConfigFileName = "launcher_config.json";

        // Settings live next to the exe so the launcher stays portable (e.g. inside steamapps\common). If that folder
        // isn't writable, they go to %LOCALAPPDATA% instead.
        private static readonly string BesideExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        private static readonly string FallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KINGDOM HEARTS Launcher", ConfigFileName);
        private static string ConfigFilePath = ChooseConfigPath();

        private static string ChooseConfigPath()
        {
            if (File.Exists(BesideExePath)) return BesideExePath;
            if (File.Exists(FallbackPath)) return FallbackPath;
            return IsWritable(AppDomain.CurrentDomain.BaseDirectory) ? BesideExePath : FallbackPath;
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, Path.GetRandomFileName());
                using (File.Create(probe, 1, FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return false;
            }
        }

        public static LauncherSettings Load(out bool readFailed)
        {
            readFailed = false;
            LauncherSettings settings = null;
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    settings = new JavaScriptSerializer().Deserialize<LauncherSettings>(File.ReadAllText(ConfigFilePath));
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is IOException)
                {
                    readFailed = true;
                }
            }

            settings = settings ?? new LauncherSettings();
            settings.Normalize();
            return settings;
        }

        public void Save()
        {
            string json = JsonIndent.Format(new JavaScriptSerializer().Serialize(this));
            try
            {
                Write(ConfigFilePath, json);
            }
            catch (UnauthorizedAccessException) when (ConfigFilePath != FallbackPath)
            {
                // The folder turned out to be read-only (e.g. the exe was moved somewhere protected).
                Write(FallbackPath, json);
                ConfigFilePath = FallbackPath;
            }
        }

        // Writes these settings to an explicit file, e.g. the config of a copy of the launcher in another folder.
        public void SaveTo(string path)
        {
            Write(path, JsonIndent.Format(new JavaScriptSerializer().Serialize(this)));
        }

        private static void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
        }

        public LauncherSettings Clone()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            LauncherSettings copy = serializer.Deserialize<LauncherSettings>(serializer.Serialize(this));
            copy.Normalize();
            return copy;
        }

        private void Normalize()
        {
            MelonMixPath = ExistingFileOrNull(MelonMixPath);
            EmulatorPath = ExistingFileOrNull(EmulatorPath);
            RomPath = ExistingFileOrNull(RomPath);
            MelodyOfMemoryPath = ExistingFileOrNull(MelodyOfMemoryPath);
            UseMelonMixOnDays &= MelonMixPath != null;
            UseMelonMixOnRecoded &= MelonMixPath != null;
            ComInsteadOfRecom &= EmulatorPath != null && RomPath != null;

            ExeOverrides = ExeOverrides ?? new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(DaysNativePath))
            {
                DaysNativePath = DefaultDaysNativePath;
            }

            // Drop unknown games and append any that the saved order doesn't know about yet.
            Order = (Order ?? new List<string>()).Where(id => GameCatalog.Find(id) != null).Distinct().ToList();
            Order.AddRange(GameCatalog.Games.Select(g => g.Id).Where(id => !Order.Contains(id)));

            if (GameCatalog.Find(SelectedGame) == null)
            {
                SelectedGame = Order[0];
            }
        }

        private static string ExistingFileOrNull(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
        }
    }
}
