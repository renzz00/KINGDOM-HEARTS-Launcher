using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kingdom_Hearts_Launcher
{
    public class LaunchPlan
    {
        public string Exe { get; set; }
        public string Arguments { get; set; } = "";
        public string Error { get; set; }
        // Folder of the Steam collection the game belongs to; used to write steam_appid.txt.
        public string CollectionDir { get; set; }
        public string AppId { get; set; }
        public string Mode { get; set; }

        public bool CanLaunch => Error == null;
    }

    public static class GameLauncher
    {
        private static readonly Regex IgnoredExes =
            new Regex("unins|crash|redist|setup|dxsetup|updater|installer", RegexOptions.IgnoreCase);

        public static string GetTitle(GameDef game, LauncherSettings s)
        {
            switch (game.Id)
            {
                case "recom":
                    return s.ComInsteadOfRecom ? "KINGDOM HEARTS: Chain of Memories (GBA)" : game.Title;
                case "days":
                    return game.Title + (s.UseNativeDays ? " (Native PC Port)" : s.UseMelonMixOnDays ? " (Melon Mix)" : " (Movie)");
                case "recoded":
                    return game.Title + (s.UseMelonMixOnRecoded ? " (Melon Mix)" : " (Movie)");
                default:
                    return game.Title;
            }
        }

        public static string FindDaysNativeExe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            if (File.Exists(path))
            {
                return path;
            }
            if (!Directory.Exists(path))
            {
                return null;
            }

            try
            {
                return Directory.EnumerateFiles(path, "*.exe", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.EnumerateDirectories(path).SelectMany(d => Directory.EnumerateFiles(d, "*.exe", SearchOption.TopDirectoryOnly)))
                    .Where(f => !IgnoredExes.IsMatch(Path.GetFileName(f)))
                    .OrderByDescending(ScoreDaysExe)
                    .ThenBy(f => f.Length)
                    .FirstOrDefault();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return null;
            }
        }

        private static int ScoreDaysExe(string exe)
        {
            string name = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
            int score = 0;
            if (name.Contains("358")) score += 3;
            if (name.Contains("days")) score += 2;
            if (name.Contains("kingdom") || name.StartsWith("kh")) score += 1;
            return score;
        }

        public static LaunchPlan Plan(GameDef game, LauncherSettings s)
        {
            switch (game.Id)
            {
                case "mom":
                    return string.IsNullOrEmpty(s.MelodyOfMemoryPath)
                        ? new LaunchPlan { Error = "Set the executable in Launcher Config" }
                        : new LaunchPlan { Exe = s.MelodyOfMemoryPath, Mode = "Custom executable" };

                case "recom" when s.ComInsteadOfRecom:
                    return new LaunchPlan { Exe = s.EmulatorPath, Arguments = $"\"{s.RomPath}\"", Mode = "GBA emulator" };

                case "days" when s.UseNativeDays:
                    string nativeExe = FindDaysNativeExe(s.DaysNativePath);
                    return nativeExe != null
                        ? new LaunchPlan { Exe = nativeExe, Mode = "Native PC port" }
                        : new LaunchPlan { Error = "Native port not found: " + s.DaysNativePath };

                case "days" when s.UseMelonMixOnDays:
                    return PlanMelonMix(s, "days.nds", "358/2 Days", allowGameRom: true);

                case "recoded" when s.UseMelonMixOnRecoded:
                    return PlanMelonMix(s, "recoded.nds", "Re:coded", allowGameRom: false);
            }

            string exe;
            string collectionDir = GameCatalog.FindCollectionDir(s.ParentDirectory, game.Collection);

            string overridePath;
            if (s.ExeOverrides.TryGetValue(game.Id, out overridePath) && File.Exists(overridePath))
            {
                exe = overridePath;
            }
            else if (collectionDir == null)
            {
                return new LaunchPlan { Error = "Not found in the games folder" };
            }
            else
            {
                exe = game.ExePath != null
                    ? Path.Combine(collectionDir, game.ExePath.Replace('/', '\\'))
                    : GameCatalog.FindKh3Exe(collectionDir);

                if (exe == null || !File.Exists(exe))
                {
                    return new LaunchPlan { Error = "Executable not found" };
                }
            }

            bool skip = (game.Skip == CopyrightSkip.KH1 && s.SkipCopyrightScreenOnKH1)
                     || (game.Skip == CopyrightSkip.Movies && s.SkipCopyrightScreenOnMovies);

            return new LaunchPlan
            {
                Exe = exe,
                Arguments = skip ? "-reboot=true" : "",
                CollectionDir = collectionDir,
                AppId = game.Collection.SteamAppId,
                Mode = game.Collection.Name
            };
        }

        private static LaunchPlan PlanMelonMix(LauncherSettings s, string romName, string gameName, bool allowGameRom)
        {
            string roms = Path.Combine(Path.GetDirectoryName(s.MelonMixPath), "roms");
            if (File.Exists(Path.Combine(roms, romName)))
            {
                return new LaunchPlan { Exe = s.MelonMixPath, Arguments = "roms/" + romName, Mode = "Melon Mix" };
            }
            if (allowGameRom && File.Exists(Path.Combine(roms, "game.nds")))
            {
                return new LaunchPlan { Exe = s.MelonMixPath, Mode = "Melon Mix" };
            }
            return new LaunchPlan { Error = $"No {gameName} ROM in Melon Mix's 'roms' folder" };
        }

        // The games are started directly rather than through Steam, so they need steam_appid.txt to find the client.
        private static void EnsureSteamAppId(LaunchPlan plan)
        {
            if (plan.CollectionDir == null || plan.AppId == null)
            {
                return;
            }

            string[] dirs =
            {
                plan.CollectionDir,
                Path.Combine(plan.CollectionDir, "KINGDOM HEARTS 0.2 Birth by Sleep", "Binaries", "Win64")
            };

            foreach (string dir in dirs.Where(Directory.Exists))
            {
                string file = Path.Combine(dir, "steam_appid.txt");
                if (!File.Exists(file))
                {
                    try
                    {
                        File.WriteAllText(file, plan.AppId);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        // Best effort: a game folder we can't write to shouldn't stop the launch.
                    }
                }
            }
        }

        public static void Start(LaunchPlan plan)
        {
            EnsureSteamAppId(plan);

            // The old launchers were the game's own Steam entry, so Steam had already given them the game's app id.
            // Started from a non-Steam shortcut, Steam hands out an unrelated id instead, and the games refuse to
            // start. The game inherits this process's environment, so set the right id here.
            if (plan.AppId != null)
            {
                foreach (string name in new[] { "SteamAppId", "SteamGameId", "SteamOverlayGameId" })
                {
                    Environment.SetEnvironmentVariable(name, plan.AppId);
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = plan.Exe,
                WorkingDirectory = Path.GetDirectoryName(plan.Exe),
                Arguments = plan.Arguments
            });
        }
    }
}
