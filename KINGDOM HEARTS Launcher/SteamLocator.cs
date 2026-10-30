using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Kingdom_Hearts_Launcher
{
    public static class SteamLocator
    {
        // Suggests a games folder (steamapps\common) so first-time users don't have to hunt for it.
        public static string GuessGamesDirectory()
        {
            // Installed the recommended way (steamapps\common\KINGDOM HEARTS Launcher), the games are right next to us.
            DirectoryInfo exeDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'));
            DirectoryInfo common = exeDir.Parent;
            if (common != null && common.Parent != null
                && common.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                && common.Parent.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                return common.FullName;
            }

            List<string> libraries = new List<string>();
            try
            {
                string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (!string.IsNullOrEmpty(steam))
                {
                    steam = steam.Replace('/', '\\');
                    libraries.Add(steam);

                    string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        {
                            libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
            }

            libraries.Add(@"C:\Program Files (x86)\Steam");

            List<string> commonDirs = libraries
                .Select(l => Path.Combine(l, "steamapps", "common"))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return commonDirs.FirstOrDefault(c => GameCatalog.Collections.Any(col => GameCatalog.FindCollectionDir(c, col) != null))
                ?? commonDirs.FirstOrDefault();
        }
    }
}
