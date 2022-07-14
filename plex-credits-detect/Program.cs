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

        static void Main(string[] args)
        {
            if (!settings.CheckGlobalSettingFile())
            {
                return;
            }

            settings.Load();

            Scanner.audioService = new FFmpegAudioService();

            //Scanner.db = new LMDBFingerprintDatabase(settings.databasePath);
            Scanner.db = new InMemoryFingerprintDatabase(settings.databasePath);

            Scanner scanner = new Scanner();

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


            foreach (var path in settings.paths)
            {
                scanner.CheckDirectory(path);
            }

            while (true)
            {
                Episode ep = Scanner.db.GetOnePendingEpisode();

                while (ep != null)
                {
                    scanner.ScanDirectory(ep.fullDirPath);

                    Thread.Sleep(1000);

                    ep = Scanner.db.GetOnePendingEpisode();
                }

                Thread.Sleep(10000);
            }

            Scanner.db.CloseDatabase();
        }

        private static void File_Renamed(object sender, RenamedEventArgs e)
        {
            Episode ep;

            if (Scanner.IsVideoExtension(e.OldFullPath))
            {
                ep = new Episode(e.OldFullPath);
                Scanner.db.DeleteEpisode(ep.id);
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
            Scanner.db.DeleteEpisode(ep.id);
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

        public static string PathCombine(string p1, string p2)
        {
            return Path.Combine(p1, p2.Trim(new char[] { '/', '\\', ':' }));
        }

        public static string getRelativePath(string path)
        {
            string ret = path;

            foreach (string p in settings.paths)
            {
                ret = ret.Replace(p, "");
            }

            ret = Path.DirectorySeparatorChar + ret.Trim(new char[] { '/', '\\', ':' });

            return ret;
        }
        public static string getRelativeDirectory(string path)
        {
            string ret = Path.GetDirectoryName(path);

            foreach (string p in settings.paths)
            {
                ret = ret.Replace(p, "");
            }

            ret = Path.DirectorySeparatorChar + ret.Trim(new char[] { '/', '\\', ':' });

            return ret;
        }


        public static void Exit()
        {
            try
            {
                Scanner.db.CloseDatabase();
            }
            catch { }
            Environment.Exit(0);
        }
    }
}
