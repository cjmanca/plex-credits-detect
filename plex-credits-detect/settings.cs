using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace plexCreditsDetect
{
    public class Settings : ICloneable
    {
        public string currentlyLoadedSettingsPath = "";
        public string globalSettingsPath => Program.PathCombine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plex-credits-detect");

        public Dictionary<string, string> paths = new Dictionary<string, string>();
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
        public int stride = 512;
        public int sampleRate = 5512;
        public ushort minFrequency = 200;
        public ushort maxFrequency = 2000;

        public int videoAccuracy = 2;
        public double videoSizeDivisor = 50;
        public int frameRate = 1;

        public bool recheckUndetectedOnStartup = false;
        public bool forceRedetect = false;

        public Func<string, string> pathOverride = null;

        object ICloneable.Clone()
        {
            Settings ret = new Settings();

            ret.paths = new Dictionary<string, string>(paths);
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

            ret.recheckUndetectedOnStartup = recheckUndetectedOnStartup;
            ret.forceRedetect = forceRedetect;

            return ret;
        }

        private void LoadSingle(string path)
        {
            if (pathOverride != null)
            {
                path = pathOverride(path);
            }

            string iniPath = Path.Combine(path, "fingerprint.ini");

            if (!File.Exists(iniPath))
            {
                return;
            }


            var parser = new FileIniDataParser();
            IniData data;
            
            using (var reader = new StreamReader(iniPath))
            {
                data = parser.ReadFile(iniPath, reader.CurrentEncoding);
            }

            TryGet(data, "default", "databasePath", ref databasePath);
            TryGet(data, "default", "PlexDatabasePath", ref PlexDatabasePath);
            TryGet(data, "default", "TempDirectoryPath", ref TempDirectoryPath);
            TryGet(data, "default", "ffmpegPath", ref ffmpegPath);

            TryGet(data, "default", "useAudio", ref useAudio);
            TryGet(data, "default", "useVideo", ref useVideo);

            TryGet(data, "default", "introMatchCount", ref introMatchCount);
            TryGet(data, "default", "creditsMatchCount", ref creditsMatchCount);

            TryGet(data, "default", "introStart", ref introStart);
            TryGet(data, "default", "introEnd", ref introEnd);
            TryGet(data, "default", "introMaxSearchPeriod", ref introMaxSearchPeriod);

            TryGet(data, "default", "creditsStart", ref creditsStart);
            TryGet(data, "default", "creditsEnd", ref creditsEnd);
            TryGet(data, "default", "creditsMaxSearchPeriod", ref creditsMaxSearchPeriod);

            TryGet(data, "default", "shiftSegmentBySeconds", ref shiftSegmentBySeconds);

            TryGet(data, "default", "minimumMatchSeconds", ref minimumMatchSeconds);
            TryGet(data, "default", "PermittedGap", ref PermittedGap);
            TryGet(data, "default", "PermittedGapWithMinimumEnclosure", ref PermittedGapWithMinimumEnclosure);

            TryGet(data, "default", "audioAccuracy", ref audioAccuracy);
            TryGet(data, "default", "stride", ref stride);
            TryGet(data, "default", "sampleRate", ref sampleRate);
            TryGet(data, "default", "minFrequency", ref minFrequency);
            TryGet(data, "default", "maxFrequency", ref maxFrequency);

            TryGet(data, "default", "videoAccuracy", ref videoAccuracy);
            TryGet(data, "default", "videoSizeDivisor", ref videoSizeDivisor);
            TryGet(data, "default", "frameRate", ref frameRate);

            TryGet(data, "default", "recheckUndetectedOnStartup", ref recheckUndetectedOnStartup);
            TryGet(data, "default", "forceRedetect", ref forceRedetect);

            if (data.Sections.ContainsSection("directories"))
            {
                foreach (var dir in data["directories"])
                {
                    if (dir.KeyName.Length > 0)
                    {
                        paths[dir.KeyName.Trim()] = dir.Value.Trim();
                    }
                }
            }
        }

        public Settings()
        {
            databasePath = Program.PathCombine(globalSettingsPath, "database");
        }
        public Settings(string path)
        {
            databasePath = Program.PathCombine(globalSettingsPath, "database");
            Load(path);
        }

        private bool InDocker 
        { 
            get 
            { 
                return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"; 
            } 
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

                        string defaultFfmpegPath = Program.PathCombine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///","")), "ffmpeg.exe");

                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            defaultPlexDataDir = "/var/lib/plexmediaserver/Library/Application Support/Plex Media Server/Plug-in Support/Databases/";
                            defaultFfmpegPath = "ffmpeg";
                        }
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            defaultPlexDataDir = "~/Library/Application Support/Plex Media Server/Plug-in Support/";
                            defaultFfmpegPath = "ffmpeg";
                        }
                        if (InDocker)
                        {
                            defaultPlexDataDir = "/PlexDB/";
                        }


                        string output = reader.ReadToEnd() + Environment.NewLine +
                            "databasePath = " + Program.PathCombine(globalSettingsPath, "database") + Environment.NewLine +
                            "PlexDatabasePath = " + Program.PathCombine(defaultPlexDataDir, "com.plexapp.plugins.library.db") + Environment.NewLine +
                            "TempDirectoryPath = " + Program.PathCombine(globalSettingsPath, "temp") + Environment.NewLine +
                            "ffmpegPath = " + defaultFfmpegPath;

                        File.WriteAllText(Program.PathCombine(globalSettingsPath, "fingerprint.ini"), output, Encoding.UTF8);

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
                if (p.Key == @"C:\path\this\tool\sees\to\library" || p.Value == @"C:\path\Plex\sees\to\library")
                {
                    Console.WriteLine("[directories] section not yet configured. Please remove the default example directory and add your own library paths.");
                    return false;
                }

                if (!Directory.Exists(p.Key))
                {
                    Console.WriteLine("[directories] path doesn't exist: " + p.Key);
                    return false;
                }

                if (p.Key.Contains(TempDirectoryPath) || TempDirectoryPath.Contains(p.Key))
                {
                    Console.WriteLine("TempDirectoryPath appears to be in your media paths! This could cause unintended issues. Make sure TempDirectoryPath is an empty directory outside of your media paths.");
                    return false;
                }

            }

            Directory.CreateDirectory(databasePath);
            Directory.CreateDirectory(TempDirectoryPath);

            if (ffmpegPath == "")
            {
                ffmpegPath = Program.PathCombine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "ffmpeg.exe");
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

        void TryGet<T>(IniData data, string section, string key, ref T assign)
        {
            if (typeof(bool) == assign.GetType())
            {
                bool tmp = false;
                if (TryGetIniBool(data, section, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(int) == assign.GetType())
            {
                int tmp = 0;
                if (TryGetIniInt(data, section, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(ushort) == assign.GetType())
            {
                ushort tmp = 0;
                if (TryGetIniUshort(data, section, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(double) == assign.GetType())
            {
                double tmp = 0;
                if (TryGetIniDouble(data, section, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
            if (typeof(string) == assign.GetType())
            {
                string tmp = "";
                if (TryGetIniString(data, section, key, ref tmp))
                {
                    assign = (T)(object)tmp;
                }
            }
        }

        bool TryGetIniInt(IniData data, string section, string key, ref int assign)
        {
            int iTemp;
            if (data.Sections.ContainsSection(section) && data[section].ContainsKey(key))
            {
                if (int.TryParse(data[section][key], out iTemp))
                {
                    assign = iTemp;
                    return true;
                }
            }
            return false;
        }

        bool TryGetIniUshort(IniData data, string section, string key, ref ushort assign)
        {
            ushort iTemp;
            if (data.Sections.ContainsSection(section) && data[section].ContainsKey(key))
            {
                if (ushort.TryParse(data[section][key], out iTemp))
                {
                    assign = iTemp;
                    return true;
                }
            }
            return false;
        }

        bool TryGetIniDouble(IniData data, string section, string key, ref double assign)
        {
            double dTemp;
            if (data.Sections.ContainsSection(section) && data[section].ContainsKey(key))
            {
                if (double.TryParse(data[section][key], out dTemp))
                {
                    assign = dTemp;
                    return true;
                }
            }
            return false;
        }

        bool TryGetIniString(IniData data, string section, string key, ref string assign)
        {
            if (data.Sections.ContainsSection(section) && data[section].ContainsKey(key))
            {
                assign = data[section][key];
                return true;
            }
            return false;
        }

        bool TryGetIniBool(IniData data, string section, string key, ref bool assign)
        {
            bool dTemp;
            if (data.Sections.ContainsSection(section) && data[section].ContainsKey(key))
            {
                if (bool.TryParse(data[section][key], out dTemp))
                {
                    assign = dTemp;
                    return true;
                }
            }
            return false;
        }
    }
}
