using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Kingdom_Hearts_Launcher
{
    // Moves the launcher into <steamapps\common>\KINGDOM HEARTS Launcher. A running exe can't delete itself, so the
    // files are copied, the new copy is started, and a delayed command removes the old files once this process exits.
    public static class LauncherMover
    {
        public const string FolderName = "KINGDOM HEARTS Launcher";

        private static readonly string[] SidecarFiles = { "INSTALL.txt", "LICENSE.txt", "launcher_config.json" };

        private static string CurrentExe
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        public static bool IsInSteamLibrary
        {
            get
            {
                DirectoryInfo common = new DirectoryInfo(Path.GetDirectoryName(CurrentExe)).Parent;
                return common != null && common.Parent != null
                    && common.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                    && common.Parent.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase);
            }
        }

        // The steamapps\common folder to suggest: the one holding the games, else the first Steam library.
        public static string SuggestedFolder()
        {
            return SteamLocator.GuessGamesDirectory();
        }

        // Copies the launcher and its settings into <commonDir>\KINGDOM HEARTS Launcher. Returns the new exe, or null
        // with an error message.
        public static string CopyTo(string commonDir, LauncherSettings settings, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(commonDir) || !Directory.Exists(commonDir))
            {
                error = "That folder doesn't exist.";
                return null;
            }

            string exe = CurrentExe;
            string sourceDir = Path.GetDirectoryName(exe);
            string destDir = Path.Combine(Path.GetFullPath(commonDir), FolderName);
            if (SamePath(sourceDir, destDir))
            {
                error = "The launcher is already in that folder.";
                return null;
            }

            try
            {
                Directory.CreateDirectory(destDir);
                string destExe = Path.Combine(destDir, Path.GetFileName(exe));
                File.Copy(exe, destExe, true);

                foreach (string name in SidecarFiles.Where(n => n != "launcher_config.json"))
                {
                    string source = Path.Combine(sourceDir, name);
                    if (File.Exists(source))
                    {
                        File.Copy(source, Path.Combine(destDir, name), true);
                    }
                }

                string sourceAssets = Path.Combine(sourceDir, "Steam Assets");
                if (Directory.Exists(sourceAssets))
                {
                    CopyDirectory(sourceAssets, Path.Combine(destDir, "Steam Assets"));
                }

                // An existing config at the destination wins. Otherwise the new copy starts with our settings, and
                // offers to add itself to Steam again since its location changed.
                string destConfig = Path.Combine(destDir, "launcher_config.json");
                if (!File.Exists(destConfig))
                {
                    LauncherSettings copy = settings.Clone();
                    copy.MovePromptShown = true;
                    copy.SteamPromptShown = false;
                    if (string.IsNullOrEmpty(copy.ParentDirectory))
                    {
                        copy.ParentDirectory = Path.GetFullPath(commonDir);
                    }
                    copy.SaveTo(destConfig);
                }
                return destExe;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = "Could not copy the launcher there: " + ex.Message;
                return null;
            }
        }

        // Starts the new copy and removes the files we copied from the old folder (the folder itself only if it's
        // left empty). The caller should close this window right after.
        public static void Relaunch(string newExe)
        {
            string oldExe = CurrentExe;
            string oldDir = Path.GetDirectoryName(oldExe);

            Process.Start(new ProcessStartInfo(newExe) { WorkingDirectory = Path.GetDirectoryName(newExe) });

            string files = string.Join(" ", new[] { oldExe }
                .Concat(SidecarFiles.Select(n => Path.Combine(oldDir, n)))
                .Select(f => "\"" + f + "\""));
            Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 4 > nul & del /f /q " + files + " & rmdir \"" + oldDir + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            }
            foreach (string dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
            }
        }
    }
}
