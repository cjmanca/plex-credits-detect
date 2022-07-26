using plexCreditsDetect.Database;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Emy;
using System;
using System.Diagnostics;

internal class Root
{

}

namespace plexCreditsDetect
{
    internal class Program
    {
        public static Settings settings = new Settings();
        static Dictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>();
        public static bool firstLoop = true;

        static void Main(string[] args)
        {
            if (!settings.CheckGlobalSettingFile())
            {
                return;
            }

            Scanner.audioService = new FFmpegAudioService();

            //Scanner.db = new LMDBFingerprintDatabase(settings.databasePath);
            Scanner.db = new InMemoryFingerprintDatabase(settings.databasePath);
            Scanner.plexDB.LoadDatabase(settings.PlexDatabasePath);

            Scanner scanner = new Scanner();


            if (Scanner.db.lastPlexIntroAdded == DateTime.MinValue)
            {
                Console.WriteLine("First time run detected. The first run can take a long time to build up the database from your plex database. It may appear to be frozen, but give it time.");
            }

            // Now detecting changes by monitoring Plex adding intros to the plex database.
            // No longer need to scan/monitor file changes ourselves.
            /*
            foreach (var path in settings.paths)
            {
                watchers[path] = new FileSystemWatcher(path);
                watchers[path].Path = path;
                watchers[path].NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
                watchers[path].Changed += File_Changed;
                watchers[path].Created += File_Created;
                watchers[path].Deleted += File_Deleted;
                watchers[path].Renamed += File_Renamed;
                watchers[path].IncludeSubdirectories = true;
                watchers[path].InternalBufferSize = 64 * 1024;
                watchers[path].Filter = "*.*";
                watchers[path].EnableRaisingEvents = true;
            }
            */


            if (settings.recheckUndetectedOnStartup || settings.recheckSilenceOnStartup)
            {
                Console.WriteLine("Crawling library paths to find episodes that don't meet the desired credit and intro numbers");
                foreach (var path in settings.paths)
                {
                    scanner.CheckDirectory(path.Key);
                }
            }


            Console.WriteLine($"\nSyncing newly added episodes from plex...\n");

            firstLoop = true;
            while (true)
            {

                scanner.CheckForNewPlexIntros();

                if (firstLoop)
                {
                    Console.WriteLine($"\nCompiling list of pending seasons...\n");
                }

                var dirs = Scanner.db.GetPendingDirectories();

                int count = 0;

                if (dirs != null)
                {
                    dirs.Sort();
                    foreach (var item in dirs)
                    {
                        count++;
                        Console.WriteLine($"");
                        Console.WriteLine($"Processing season {count} of {dirs.Count}");
                        scanner.ScanDirectory(item);
                    }
                }

                firstLoop = false;

                Thread.Sleep(60000);
            }

            Scanner.db.CloseDatabase();
            Scanner.plexDB.CloseDatabase();
        }

        private static void File_Renamed(object sender, RenamedEventArgs e)
        {
            Episode ep;

            if (Scanner.IsVideoExtension(e.OldFullPath))
            {
                ep = new Episode(e.OldFullPath);
                Scanner.db.DeleteEpisode(ep);
            }
            if (Scanner.IsVideoExtension(e.FullPath))
            {
                ep = new Episode(e.FullPath);
                ep.DetectionPending = true;
                Scanner.db.Insert(ep);
            }
        }

        private static void File_Deleted(object sender, FileSystemEventArgs e)
        {
            if (!Scanner.IsVideoExtension(e.FullPath))
            {
                return;
            }
            Episode ep = new Episode(e.FullPath);
            Scanner.db.DeleteEpisode(ep);
        }

        private static void File_Created(object sender, FileSystemEventArgs e)
        {
            if (!Scanner.IsVideoExtension(e.FullPath))
            {
                return;
            }
            Episode ep = new Episode(e.FullPath);
            ep.DetectionPending = true;
            Scanner.db.Insert(ep);
        }

        private static void File_Changed(object sender, FileSystemEventArgs e)
        {
            if (!Scanner.IsVideoExtension(e.FullPath))
            {
                return;
            }
            Episode ep = new Episode(e.FullPath);
            ep.DetectionPending = true;
            Scanner.db.Insert(ep);
        }

        public static string GetWinStylePath(string path)
        {
            return path.Replace('/', '\\');
        }
        public static string GetLinuxStylePath(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string FixPath(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        public static string PathCombine(string p1, string p2)
        {
            return Path.Combine(p1, p2.Trim(new char[] { '/', '\\' }));
        }

        public static string getRelativePath(string path)
        {
            string ret = path;


            foreach (var p in settings.paths)
            {
                if (path.StartsWith(p.Key))
                {
                    ret = ret.Replace(p.Key, "");
                    break;
                }
                if (path.StartsWith(p.Value))
                {
                    ret = ret.Replace(p.Value, "");
                    break;
                }
            }

            ret = Path.DirectorySeparatorChar + FixPath(ret).Trim(new char[] { '/', '\\' });

            return ret;
        }
        public static string getRelativeDirectory(string path)
        {
            string ret = Path.GetDirectoryName(path);

            foreach (var p in settings.paths)
            {
                if (path.StartsWith(p.Key))
                {
                    ret = ret.Replace(p.Key, "");
                    break;
                }
                if (path.StartsWith(p.Value))
                {
                    ret = ret.Replace(p.Value, "");
                    break;
                }
            }

            ret = Path.DirectorySeparatorChar + ret.Trim(new char[] { '/', '\\' });

            return ret;
        }

        public static string getFullDirectory(string path)
        {
            foreach (var bPath in Program.settings.paths)
            {
                string p = Program.PathCombine(bPath.Key, path);

                if (Directory.Exists(p))
                {
                    return p;
                }
            }

            return path;
        }

        public static void Exit()
        {
            if (Scanner.db != null)
            {
                Scanner.db.CloseDatabase();
            }
            if (Scanner.plexDB != null)
            {
                Scanner.plexDB.CloseDatabase();
            }
            Environment.Exit(0);
        }
    }
}
