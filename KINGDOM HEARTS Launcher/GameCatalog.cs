using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kingdom_Hearts_Launcher
{
    public class Collection
    {
        public string Name { get; }
        public string SteamAppId { get; }
        public Func<string, bool> Matches { get; }

        public Collection(string name, string steamAppId, Func<string, bool> matches)
        {
            Name = name;
            SteamAppId = steamAppId;
            Matches = matches;
        }
    }

    public enum CopyrightSkip
    {
        None,
        KH1,
        Movies
    }

    public class GameDef
    {
        public string Id { get; }
        public string Title { get; }
        public Collection Collection { get; }
        // Relative to the collection folder; null means the exe is located by GameCatalog.FindKh3Exe.
        public string ExePath { get; }
        public CopyrightSkip Skip { get; }

        public GameDef(string id, string title, Collection collection, string exePath, CopyrightSkip skip = CopyrightSkip.None)
        {
            Id = id;
            Title = title;
            Collection = collection;
            ExePath = exePath;
            Skip = skip;
        }
    }

    public static class GameCatalog
    {
        public static readonly Collection Hd1525 = new Collection(
            "1.5+2.5 ReMIX", "2552430",
            dir => File.Exists(Path.Combine(dir, "KINGDOM HEARTS FINAL MIX.exe")));

        public static readonly Collection Hd28 = new Collection(
            "2.8 Final Chapter Prologue", "2552440",
            dir => File.Exists(Path.Combine(dir, "KINGDOM HEARTS Dream Drop Distance.exe"))
                || File.Exists(Path.Combine(dir, "KINGDOM HEARTS HD 2.8 Launcher.exe")));

        public static readonly Collection Kh3 = new Collection(
            "III + Re Mind", null,
            dir => Path.GetFileName(dir.TrimEnd('\\', '/')).StartsWith("KINGDOM HEARTS", StringComparison.OrdinalIgnoreCase)
                && FindKh3Exe(dir) != null);

        public static readonly Collection[] Collections = { Hd1525, Hd28, Kh3 };

        // Listed in the official order: 1.5+2.5, then 2.8, then III.
        public static readonly GameDef[] Games =
        {
            new GameDef("kh1", "KINGDOM HEARTS Final Mix", Hd1525, "KINGDOM HEARTS FINAL MIX.exe", CopyrightSkip.KH1),
            new GameDef("recom", "KINGDOM HEARTS Re:Chain of Memories", Hd1525, "KINGDOM HEARTS Re_Chain of Memories.exe"),
            new GameDef("days", "KINGDOM HEARTS 358/2 Days", Hd1525, "KINGDOM HEARTS HD 1.5+2.5 Launcher.exe", CopyrightSkip.Movies),
            new GameDef("kh2", "KINGDOM HEARTS II Final Mix", Hd1525, "KINGDOM HEARTS II FINAL MIX.exe"),
            new GameDef("bbs", "KINGDOM HEARTS Birth by Sleep Final Mix", Hd1525, "KINGDOM HEARTS Birth by Sleep FINAL MIX.exe"),
            new GameDef("recoded", "KINGDOM HEARTS Re:coded", Hd1525, "KINGDOM HEARTS HD 1.5+2.5 Launcher.exe", CopyrightSkip.Movies),
            new GameDef("ddd", "KINGDOM HEARTS Dream Drop Distance HD", Hd28, "KINGDOM HEARTS Dream Drop Distance.exe"),
            new GameDef("bbs02", "KINGDOM HEARTS 0.2: Birth by Sleep", Hd28, "KINGDOM HEARTS 0.2 Birth by Sleep/Binaries/Win64/KINGDOM HEARTS 0.2 Birth by Sleep.exe"),
            new GameDef("backcover", "KINGDOM HEARTS χ Back Cover (Movie)", Hd28, "KINGDOM HEARTS HD 2.8 Launcher.exe", CopyrightSkip.Movies),
            new GameDef("kh3", "KINGDOM HEARTS III", Kh3, null),
            // Has no collection: its exe is chosen by the user, since copies of it can sit anywhere.
            new GameDef("mom", "KINGDOM HEARTS Melody of Memory", null, null),
        };

        public static GameDef Find(string id)
        {
            return Games.FirstOrDefault(g => g.Id == id);
        }

        public static string FindCollectionDir(string parentDir, Collection collection)
        {
            if (string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
            {
                return null;
            }

            // The user may have pointed us straight at a game folder instead of the parent.
            if (collection.Matches(parentDir))
            {
                return parentDir;
            }

            try
            {
                return Directory.GetDirectories(parentDir).FirstOrDefault(collection.Matches);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return null;
            }
        }

        public static string FindKh3Exe(string dir)
        {
            string root = Path.Combine(dir, "KINGDOM HEARTS III.exe");
            if (File.Exists(root))
            {
                return root;
            }

            try
            {
                return Directory.EnumerateFiles(dir, "KINGDOM HEARTS III*.exe", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).ToLowerInvariant().Contains("crash"))
                    .Where(f => f.Substring(dir.Length).Split('\\', '/').Length <= 5)
                    .OrderBy(f => f.Length)
                    .FirstOrDefault();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return null;
            }
        }

        public static readonly string[] Presets =
        {
            "Official", "Release", "Chronological", "Alphabetical", "Alphabetical 2",
            "Official reverse", "Release reverse", "Chronological reverse", "Alphabetical reverse", "Alphabetical 2 reverse",
            "Random"
        };

        public static List<string> BuildOrder(string preset)
        {
            bool reverse = preset != null && preset.EndsWith(" reverse");
            string name = reverse ? preset.Substring(0, preset.Length - " reverse".Length) : preset;

            List<string> order;
            switch (name)
            {
                case "Release":
                    order = new List<string> { "kh1", "recom", "kh2", "days", "bbs", "recoded", "ddd", "backcover", "bbs02", "kh3", "mom" };
                    break;
                case "Chronological":
                    order = new List<string> { "backcover", "bbs", "kh1", "recom", "days", "kh2", "recoded", "ddd", "bbs02", "kh3", "mom" };
                    break;
                case "Alphabetical":
                    order = Games.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase).Select(g => g.Id).ToList();
                    break;
                case "Alphabetical 2":
                    order = Games.OrderBy(g => SubtitleKey(g.Title), StringComparer.OrdinalIgnoreCase).Select(g => g.Id).ToList();
                    break;
                case "Random":
                    Random rng = new Random();
                    order = Games.Select(g => g.Id).OrderBy(id => rng.Next()).ToList();
                    break;
                default:
                    order = Games.Select(g => g.Id).ToList();
                    break;
            }

            if (reverse)
            {
                order.Reverse();
            }
            return order;
        }

        // Sorts by the part of the title after "KINGDOM HEARTS", so the first game ("KINGDOM HEARTS") comes first.
        private static string SubtitleKey(string title)
        {
            const string prefix = "KINGDOM HEARTS";
            return title.StartsWith(prefix) ? title.Substring(prefix.Length).Trim() : title;
        }
    }
}
