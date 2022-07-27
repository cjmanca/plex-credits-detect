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
        public bool detectSilenceAfterCredits = true;

        public int introMatchCount = 0;
        public int creditsMatchCount = 1;

        public int maximumMatches => introMatchCount + creditsMatchCount;

        public int quickDetectFingerprintSamples = 5;
        public int fullDetectFingerprintMaxSamples = 10;


        public double introStart = 0;
        public double introEnd = 0.5;
        public double introMaxSearchPeriod = 15 * 60;

        public double creditsStart = 0.7;
        public double creditsEnd = 1.0;
        public double creditsMaxSearchPeriod = 10 * 60;

        public double shiftSegmentBySeconds = 2;

        public double minimumMatchSeconds = 20;
        public double PermittedGap = 3;
        public double PermittedGapWithMinimumEnclosure = 10;

        public int audioAccuracy = 4;
        public int stride = 512;
        public int sampleRate = 5512;
        public ushort minFrequency = 200;
        public ushort maxFrequency = 2000;
        public int silenceDecibels = -55;

        public int videoAccuracy = 2;
        public double videoSizeDivisor = 50;
        public int frameRate = 1;

        public bool recheckSilenceOnStartup = false;
        public bool recheckUndetectedOnStartup = false;
        public bool forceRedetect = false;

        public bool redetectIfFileSizeChanges = true;

        public Func<string, string> pathOverride = null;

        bool anyMissingGlobalIniSettings = false;

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
            ret.detectSilenceAfterCredits = detectSilenceAfterCredits;

            ret.introMatchCount = introMatchCount;
            ret.creditsMatchCount = creditsMatchCount;

            ret.quickDetectFingerprintSamples = quickDetectFingerprintSamples;
            ret.fullDetectFingerprintMaxSamples = fullDetectFingerprintMaxSamples;

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
            ret.silenceDecibels = silenceDecibels;

            ret.videoAccuracy = videoAccuracy;
            ret.videoSizeDivisor = videoSizeDivisor;
            ret.frameRate = frameRate;

            ret.recheckSilenceOnStartup = recheckSilenceOnStartup;
            ret.recheckUndetectedOnStartup = recheckUndetectedOnStartup;
            ret.forceRedetect = forceRedetect;

            ret.redetectIfFileSizeChanges = redetectIfFileSizeChanges;

            return ret;
        }

        private void LoadSingle(string path, bool warnOnMissing = false)
        {
            Encoding encoding = Encoding.UTF8;

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
                encoding = reader.CurrentEncoding;
            }

            data = parser.ReadFile(iniPath, encoding);

            TryGet(data, warnOnMissing, "default", "databasePath", ref databasePath);
            TryGet(data, warnOnMissing, "default", "PlexDatabasePath", ref PlexDatabasePath);
            TryGet(data, warnOnMissing, "default", "TempDirectoryPath", ref TempDirectoryPath);
            TryGet(data, warnOnMissing, "default", "ffmpegPath", ref ffmpegPath);

            TryGet(data, warnOnMissing, "default", "useAudio", ref useAudio);
            TryGet(data, warnOnMissing, "default", "useVideo", ref useVideo);
            TryGet(data, warnOnMissing, "default", "detectSilenceAfterCredits", ref detectSilenceAfterCredits);

            TryGet(data, warnOnMissing, "default", "introMatchCount", ref introMatchCount);
            TryGet(data, warnOnMissing, "default", "creditsMatchCount", ref creditsMatchCount);

            TryGet(data, warnOnMissing, "default", "quickDetectFingerprintSamples", ref quickDetectFingerprintSamples);
            TryGet(data, warnOnMissing, "default", "fullDetectFingerprintMaxSamples", ref fullDetectFingerprintMaxSamples);

            TryGet(data, warnOnMissing, "default", "introStart", ref introStart);
            TryGet(data, warnOnMissing, "default", "introEnd", ref introEnd);
            TryGet(data, warnOnMissing, "default", "introMaxSearchPeriod", ref introMaxSearchPeriod);

            TryGet(data, warnOnMissing, "default", "creditsStart", ref creditsStart);
            TryGet(data, warnOnMissing, "default", "creditsEnd", ref creditsEnd);
            TryGet(data, warnOnMissing, "default", "creditsMaxSearchPeriod", ref creditsMaxSearchPeriod);

            TryGet(data, warnOnMissing, "default", "shiftSegmentBySeconds", ref shiftSegmentBySeconds);

            TryGet(data, warnOnMissing, "default", "minimumMatchSeconds", ref minimumMatchSeconds);
            TryGet(data, warnOnMissing, "default", "PermittedGap", ref PermittedGap);
            TryGet(data, warnOnMissing, "default", "PermittedGapWithMinimumEnclosure", ref PermittedGapWithMinimumEnclosure);

            TryGet(data, warnOnMissing, "default", "audioAccuracy", ref audioAccuracy);
            TryGet(data, warnOnMissing, "default", "stride", ref stride);
            TryGet(data, warnOnMissing, "default", "sampleRate", ref sampleRate);
            TryGet(data, warnOnMissing, "default", "minFrequency", ref minFrequency);
            TryGet(data, warnOnMissing, "default", "maxFrequency", ref maxFrequency);
            TryGet(data, warnOnMissing, "default", "silenceDecibels", ref silenceDecibels);

            TryGet(data, warnOnMissing, "default", "videoAccuracy", ref videoAccuracy);
            TryGet(data, warnOnMissing, "default", "videoSizeDivisor", ref videoSizeDivisor);
            TryGet(data, warnOnMissing, "default", "frameRate", ref frameRate);

            TryGet(data, warnOnMissing, "default", "recheckSilenceOnStartup", ref recheckSilenceOnStartup);
            TryGet(data, warnOnMissing, "default", "recheckUndetectedOnStartup", ref recheckUndetectedOnStartup);
            TryGet(data, warnOnMissing, "default", "forceRedetect", ref forceRedetect);

            TryGet(data, warnOnMissing, "default", "redetectIfFileSizeChanges", ref redetectIfFileSizeChanges);

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

            if (anyMissingGlobalIniSettings)
            {
                anyMissingGlobalIniSettings = false;
                
                Console.WriteLine("");
                Console.WriteLine("Missing settings have been added to your global ini. Consult github for information on these settings:");
                Console.WriteLine("https://github.com/cjmanca/plex-credits-detect");
                Console.WriteLine("");

                File.WriteAllText(iniPath, data.ToString(), encoding);

                Thread.Sleep(10000);
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

            Load("", true);

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
            Directory.CreateDirectory(Program.PathCombine(TempDirectoryPath, "plex-credits-detect-temp"));

            if (ffmpegPath == "")
            {
                ffmpegPath = Program.PathCombine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "ffmpeg.exe");
            }

            return true;
        }

        public void Load(string path = "", bool warnOnMissing = false)
        {
            currentlyLoadedSettingsPath = Path.GetDirectoryName(Program.PathCombine(path, "nothing"));

            LoadSingle(globalSettingsPath, warnOnMissing);

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

        void TryGet<T>(IniData data, bool warnOnMissing, string section, string key, ref T assign)
        {
            if (warnOnMissing && (!data.Sections.ContainsSection(section) || !data[section].ContainsKey(key)))
            {
                data[section][key] = assign.ToString();

                Console.WriteLine("");
                Console.WriteLine($"!!! MISSING Ini key in global config file. Section: {section}, Key: {key}");
                anyMissingGlobalIniSettings = true;
            }

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
                if (int.TryParse(sanitizeRawValue(data[section][key]), out iTemp))
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
                if (ushort.TryParse(sanitizeRawValue(data[section][key]), out iTemp))
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
                if (double.TryParse(sanitizeRawValue(data[section][key]), out dTemp))
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
                assign = sanitizeRawValue(data[section][key]);
                return true;
            }
            return false;
        }

        bool TryGetIniBool(IniData data, string section, string key, ref bool assign)
        {
            bool dTemp;
            if (data.Sections.ContainsSection(section) && data[section].ContainsKey(key))
            {
                if (bool.TryParse(sanitizeRawValue(data[section][key]), out dTemp))
                {
                    assign = dTemp;
                    return true;
                }
            }
            return false;
        }


        string sanitizeRawValue(string raw)
        {
            int idx = raw.IndexOf('#');
            if (idx < 0)
            {
                return raw;
            }
            return raw.Substring(0, idx).Trim();
        }
    }
}
