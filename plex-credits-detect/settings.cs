using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;

namespace plexCreditsDetect
{
    public class Settings : ICloneable
    {
        public string currentlyLoadedSettingsPath = "";
        public string globalSettingsPath => Program.PathCombine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plex-credits-detect");

        public List<string> paths = new List<string>();
        public string databasePath = "";
        public string PlexDatabasePath = "";
        public string TempDirectoryPath = "";
        public string ffmpegPath = "";

        public bool useAudio = true;
        public bool useVideo = false;

        public int introMatchCount = 0;
        public int creditsMatchCount = 1;
        public int maximumMatches => introMatchCount + creditsMatchCount;

        public double introStart = 0;
        public double introEnd = 0.5;
        public double introMaxSearchPeriod = 15 * 60;

        public double creditsStart = 0.7;
        public double creditsEnd = 1.0;
        public double creditsMaxSearchPeriod = 10 * 60;

        public double shiftSegmentBySeconds = 2;

        public double minimumMatchSeconds = 20;
        public double PermittedGap = 5;
        public double PermittedGapWithMinimumEnclosure = 10;

        public int audioAccuracy = 4;
        public int stride = 1024;
        public int sampleRate = 5512;
        public ushort minFrequency = 200;
        public ushort maxFrequency = 2000;

        public int videoAccuracy = 2;
        public double videoSizeDivisor = 50;
        public int frameRate = 1;

        public bool forceRedetect = false;

        public delegate IFileProvider FileProviderDelegate(string path);

        public FileProviderDelegate FileProvider;

        object ICloneable.Clone()
        {
            Settings ret = new Settings();

            ret.paths = paths.ToList();
            ret.databasePath = databasePath;
            ret.PlexDatabasePath = PlexDatabasePath;
            ret.TempDirectoryPath = TempDirectoryPath;
            ret.ffmpegPath = ffmpegPath;

            ret.useAudio = useAudio;
            ret.useVideo = useVideo;

            ret.introMatchCount = introMatchCount;
            ret.creditsMatchCount = creditsMatchCount;

            ret.introStart = introStart;
            ret.introEnd = introEnd;
            ret.introMaxSearchPeriod = introMaxSearchPeriod;

            ret.creditsStart = creditsStart;
            ret.creditsEnd = creditsEnd;
            ret.creditsMaxSearchPeriod = creditsMaxSearchPeriod;

            ret.shiftSegmentBySeconds = shiftSegmentBySeconds;

            ret.minimumMatchSeconds = minimumMatchSeconds;
            ret.PermittedGap = PermittedGap;
            ret.PermittedGapWithMinimumEnclosure = PermittedGapWithMinimumEnclosure;

            ret.audioAccuracy = audioAccuracy;
            ret.stride = stride;
            ret.sampleRate = sampleRate;
            ret.minFrequency = minFrequency;
            ret.maxFrequency = maxFrequency;

            ret.videoAccuracy = videoAccuracy;
            ret.videoSizeDivisor = videoSizeDivisor;
            ret.frameRate = frameRate;

            ret.forceRedetect = forceRedetect;

            return ret;
        }

        private void LoadSingle(string path)
        {
            PhysicalFileProvider fp = (PhysicalFileProvider)FileProvider(path);

            if (!fp.GetFileInfo("fingerprint.ini").Exists)
            {
                return;
            }

            IniConfigurationSource iniConfig = new IniConfigurationSource { Path = "fingerprint.ini", Optional = true, FileProvider = fp };
            IniConfigurationProvider iniProvider = new IniConfigurationProvider(iniConfig);
            iniProvider.Load();

            TryGet(iniProvider, "default:databasePath", ref databasePath);
            TryGet(iniProvider, "default:PlexDatabasePath", ref PlexDatabasePath);
            TryGet(iniProvider, "default:TempDirectoryPath", ref TempDirectoryPath);
            TryGet(iniProvider, "default:ffmpegPath", ref ffmpegPath);

            TryGet(iniProvider, "default:useAudio", ref useAudio);
            TryGet(iniProvider, "default:useVideo", ref useVideo);

            TryGet(iniProvider, "default:introMatchCount", ref introMatchCount);
            TryGet(iniProvider, "default:creditsMatchCount", ref creditsMatchCount);

            TryGet(iniProvider, "default:introStart", ref introStart);
            TryGet(iniProvider, "default:introEnd", ref introEnd);
            TryGet(iniProvider, "default:introMaxSearchPeriod", ref introMaxSearchPeriod);

            TryGet(iniProvider, "default:creditsStart", ref creditsStart);
            TryGet(iniProvider, "default:creditsEnd", ref creditsEnd);
            TryGet(iniProvider, "default:creditsMaxSearchPeriod", ref creditsMaxSearchPeriod);

            TryGet(iniProvider, "default:shiftSegmentBySeconds", ref shiftSegmentBySeconds);

            TryGet(iniProvider, "default:minimumMatchSeconds", ref minimumMatchSeconds);
            TryGet(iniProvider, "default:PermittedGap", ref PermittedGap);
            TryGet(iniProvider, "default:PermittedGapWithMinimumEnclosure", ref PermittedGapWithMinimumEnclosure);

            TryGet(iniProvider, "default:audioAccuracy", ref audioAccuracy);
            TryGet(iniProvider, "default:stride", ref stride);
            TryGet(iniProvider, "default:sampleRate", ref sampleRate);
            TryGet(iniProvider, "default:minFrequency", ref minFrequency);
            TryGet(iniProvider, "default:maxFrequency", ref maxFrequency);

            TryGet(iniProvider, "default:videoAccuracy", ref videoAccuracy);
            TryGet(iniProvider, "default:videoSizeDivisor", ref videoSizeDivisor);
            TryGet(iniProvider, "default:frameRate", ref frameRate);

            TryGet(iniProvider, "default:forceRedetect", ref forceRedetect);

            string temp;
            var dirs = iniProvider.GetChildKeys(new List<string>(), "directories");
            foreach (var dir in dirs)
            {
                if (dir.Length > 0 && iniProvider.TryGet("directories:" + dir, out temp))
                {
                    paths.Add(temp);
                }
            }
        }

        public Settings()
        {
            FileProvider = GetFileProvider;
            databasePath = Program.PathCombine(globalSettingsPath, "database");
        }
        public Settings(string path)
        {
            FileProvider = GetFileProvider;
            databasePath = Program.PathCombine(globalSettingsPath, "database");
            Load(path);
        }

        private IFileProvider GetFileProvider(string path)
        {
            return new PhysicalFileProvider(path);
        }

        public bool CheckGlobalSettingFile()
        {
            Directory.CreateDirectory(globalSettingsPath);

            Thread.Sleep(10);

            if (!File.Exists(Program.PathCombine(globalSettingsPath, "fingerprint.ini")))
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (Stream stream = assembly.GetManifestResourceStream("plexCreditsDetect.resources.fingerprint.ini"))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string defaultPlexDataDir = Program.PathCombine(Program.PathCombine(Program.PathCombine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Plex Media Server"), "Plug-in Support"), "Databases");

                        string output = reader.ReadToEnd() + Environment.NewLine +
                            "databasePath = " + Program.PathCombine(globalSettingsPath, "database") + Environment.NewLine +
                            "PlexDatabasePath = " + Program.PathCombine(defaultPlexDataDir, "com.plexapp.plugins.library.db") + Environment.NewLine +
                            "TempDirectoryPath = " + Program.PathCombine(globalSettingsPath, "temp");

                        File.WriteAllText(Program.PathCombine(globalSettingsPath, "fingerprint.ini"), output);

                        Console.WriteLine("Created default config file: " + Program.PathCombine(globalSettingsPath, "fingerprint.ini"));

                        Program.Exit();
                        return false;
                    }
                }
            }

            Console.WriteLine("Loading global config file: " + Program.PathCombine(globalSettingsPath, "fingerprint.ini"));

            Load();

            if (TempDirectoryPath == "")
            {
                Console.WriteLine("TempDirectoryPath not set. Make sure TempDirectoryPath is an empty directory outside of your media paths.");
                return false;
            }

            DirectoryInfo d = new DirectoryInfo(TempDirectoryPath);
            if (d.Parent == null)
            {
                Console.WriteLine("TempDirectoryPath appears to be a directory root! This could result in unintended deletion when temp is cleared. Make sure TempDirectoryPath is an empty directory outside of your media paths.");
                return false;
            }

            foreach (var p in paths)
            {
                if (p == @"C:\path\to\library")
                {
                    Console.WriteLine("[directories] section not yet configured. Please remove the default example directory and add your own library paths.");
                    return false;
                }

                if (p.Contains(TempDirectoryPath) || TempDirectoryPath.Contains(p))
                {
                    Console.WriteLine("TempDirectoryPath appears to be in your media paths! This could cause unintended issues. Make sure TempDirectoryPath is an empty directory outside of your media paths.");
                    return false;
                }
            }

            Directory.CreateDirectory(databasePath);
            Directory.CreateDirectory(TempDirectoryPath);

            if (ffmpegPath == "")
            {
                ffmpegPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            }

            return true;
        }

        public void Load(string path = "")
        {
            currentlyLoadedSettingsPath = Path.GetDirectoryName(Program.PathCombine(path, "nothing"));

            LoadSingle(globalSettingsPath);

            if (path == "")
            {
                return;
            }

            List<string> pathParts = new List<string>();
            DirectoryInfo di = new DirectoryInfo(path);

            while (di != null)
            {
                pathParts.Add(di.FullName);
                di = di.Parent;
            }

            pathParts.Reverse();

            foreach (var part in pathParts)
            {
                LoadSingle(part);
            }
        }

        void TryGet<T>(IniConfigurationProvider iniProvider, string key, ref T assign)
        {
            if (typeof(bool) == assign.GetType())
            {
                bool tmp = false;
                if (TryGetIniBool(iniProvider, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(int) == assign.GetType())
            {
                int tmp = 0;
                if (TryGetIniInt(iniProvider, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(ushort) == assign.GetType())
            {
                ushort tmp = 0;
                if (TryGetIniUshort(iniProvider, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(double) == assign.GetType())
            {
                double tmp = 0;
                if (TryGetIniDouble(iniProvider, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(string) == assign.GetType())
            {
                string tmp = "";
                if (TryGetIniString(iniProvider, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
        }

        bool TryGetIniInt(IniConfigurationProvider iniProvider, string key, ref int assign)
        {
            string temp;
            int iTemp;
            if (iniProvider.TryGet(key, out temp))
            {
                if (int.TryParse(temp, out iTemp))
                {
                    assign = iTemp;
                    return true;
                }
            }
            return false;
        }

        bool TryGetIniUshort(IniConfigurationProvider iniProvider, string key, ref ushort assign)
        {
            string temp;
            ushort iTemp;
            if (iniProvider.TryGet(key, out temp))
            {
                if (ushort.TryParse(temp, out iTemp))
                {
                    assign = iTemp;
                    return true;
                }
            }
            return false;
        }

        bool TryGetIniDouble(IniConfigurationProvider iniProvider, string key, ref double assign)
        {
            string temp;
            double dTemp;
            if (iniProvider.TryGet(key, out temp))
            {
                if (double.TryParse(temp, out dTemp))
                {
                    assign = dTemp;
                    return true;
                }
            }
            return false;
        }

        bool TryGetIniString(IniConfigurationProvider iniProvider, string key, ref string assign)
        {
            string temp;
            if (iniProvider.TryGet(key, out temp))
            {
                assign = temp;
                return true;
            }
            return false;
        }

        bool TryGetIniBool(IniConfigurationProvider iniProvider, string key, ref bool assign)
        {
            string temp;
            bool dTemp;
            if (iniProvider.TryGet(key, out temp))
            {
                if (bool.TryParse(temp, out dTemp))
                {
                    assign = dTemp;
                    return true;
                }
            }
            return false;
        }
    }
}
