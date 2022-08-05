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

        public static Dictionary<string, string> paths = new Dictionary<string, string>();
        public static Dictionary<string, string> pathsPlexIndexed = new Dictionary<string, string>();
        public static string databasePath = "";
        public static string PlexDatabasePath = "";
        public static string TempDirectoryPath = "";
        public static string ffmpegPath = "";

        public bool useAudio = true;
        public bool useVideo = false;
        public bool detectSilenceAfterCredits = true;

        public bool detectBlackframes = true;
        public bool blackframeOnlyMovies = true;
        public bool blackframeUseMaxSearchPeriodForEpisodes = true;
        public bool blackframeUseMaxSearchPeriodForMovies = false;
        public double blackframeScreenPercentage = 75;
        public double blackframePixelPercentage = 2;

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
        public ushort minFrequency = 100;
        public ushort maxFrequency = 2750;
        public int silenceDecibels = -55;

        public int videoAccuracy = 2;
        public double videoSizeDivisor = 50;
        public int frameRate = 1;

        public bool crawlDirectoriesOnStartup = false;
        public bool recheckBlackframesOnStartup = false;
        public bool recheckSilenceOnStartup = false;
        public bool recheckUndetectedOnStartup = false;
        public bool forceRedetect = false;

        public bool redetectIfFileSizeChanges = true;

        public Func<string, string> pathOverride = null;

        bool anyMissingGlobalIniSettings = false;
        bool anyDefaultSections = false;

        object ICloneable.Clone()
        {
            Settings ret = new Settings();

            ret.useAudio = useAudio;
            ret.useVideo = useVideo;
            ret.detectSilenceAfterCredits = detectSilenceAfterCredits;

            ret.detectBlackframes = detectBlackframes;
            ret.blackframeOnlyMovies = blackframeOnlyMovies;
            ret.blackframeUseMaxSearchPeriodForEpisodes = blackframeUseMaxSearchPeriodForEpisodes;
            ret.blackframeUseMaxSearchPeriodForMovies = blackframeUseMaxSearchPeriodForMovies;
            ret.blackframeScreenPercentage = blackframeScreenPercentage;
            ret.blackframePixelPercentage = blackframePixelPercentage;

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

            ret.crawlDirectoriesOnStartup = crawlDirectoriesOnStartup;
            ret.recheckBlackframesOnStartup = recheckBlackframesOnStartup;
            ret.recheckSilenceOnStartup = recheckSilenceOnStartup;
            ret.recheckUndetectedOnStartup = recheckUndetectedOnStartup;
            ret.forceRedetect = forceRedetect;

            ret.redetectIfFileSizeChanges = redetectIfFileSizeChanges;

            return ret;
        }

        private void LoadSingle(string path, bool isGlobalConfig = false)
        {
            Encoding encoding = Encoding.UTF8;

            anyDefaultSections = false;

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


            if (isGlobalConfig) // only set directories in the global config to avoid potential issues
            {
                if (data.Sections.ContainsSection("directories"))
                {
                    foreach (var dir in data["directories"])
                    {
                        if (dir.KeyName.Length > 0)
                        {
                            paths[Path.GetFullPath(dir.KeyName.Trim())] = Path.GetFullPath(dir.Value.Trim());
                            pathsPlexIndexed[Path.GetFullPath(dir.Value.Trim())] = Path.GetFullPath(dir.KeyName.Trim());
                        }
                    }
                }
            }


            TryGet(data, isGlobalConfig, "intro", "introStart", ref introStart);
            TryGet(data, isGlobalConfig, "intro", "introEnd", ref introEnd);
            TryGet(data, isGlobalConfig, "intro", "introMaxSearchPeriod", ref introMaxSearchPeriod);


            TryGet(data, isGlobalConfig, "credits", "creditsStart", ref creditsStart);
            TryGet(data, isGlobalConfig, "credits", "creditsEnd", ref creditsEnd);
            TryGet(data, isGlobalConfig, "credits", "creditsMaxSearchPeriod", ref creditsMaxSearchPeriod);


            TryGet(data, isGlobalConfig, "matching", "useAudio", ref useAudio);
            TryGet(data, isGlobalConfig, "matching", "useVideo", ref useVideo);
            TryGet(data, isGlobalConfig, "matching", "introMatchCount", ref introMatchCount);
            TryGet(data, isGlobalConfig, "matching", "creditsMatchCount", ref creditsMatchCount);
            TryGet(data, isGlobalConfig, "matching", "quickDetectFingerprintSamples", ref quickDetectFingerprintSamples);
            TryGet(data, isGlobalConfig, "matching", "fullDetectFingerprintMaxSamples", ref fullDetectFingerprintMaxSamples);
            TryGet(data, isGlobalConfig, "matching", "audioAccuracy", ref audioAccuracy);
            TryGet(data, isGlobalConfig, "matching", "stride", ref stride);
            TryGet(data, isGlobalConfig, "matching", "sampleRate", ref sampleRate);
            TryGet(data, isGlobalConfig, "matching", "minFrequency", ref minFrequency);
            TryGet(data, isGlobalConfig, "matching", "maxFrequency", ref maxFrequency);
            TryGet(data, isGlobalConfig, "matching", "videoAccuracy", ref videoAccuracy);
            TryGet(data, isGlobalConfig, "matching", "videoSizeDivisor", ref videoSizeDivisor);
            TryGet(data, isGlobalConfig, "matching", "frameRate", ref frameRate);


            TryGet(data, isGlobalConfig, "silence", "detectSilenceAfterCredits", ref detectSilenceAfterCredits);
            TryGet(data, isGlobalConfig, "silence", "silenceDecibels", ref silenceDecibels);


            TryGet(data, isGlobalConfig, "blackframes", "detectBlackframes", ref detectBlackframes);
            TryGet(data, isGlobalConfig, "blackframes", "blackframeOnlyMovies", ref blackframeOnlyMovies);
            TryGet(data, isGlobalConfig, "blackframes", "blackframeUseMaxSearchPeriodForEpisodes", ref blackframeUseMaxSearchPeriodForEpisodes);
            TryGet(data, isGlobalConfig, "blackframes", "blackframeUseMaxSearchPeriodForMovies", ref blackframeUseMaxSearchPeriodForMovies);
            TryGet(data, isGlobalConfig, "blackframes", "blackframeScreenPercentage", ref blackframeScreenPercentage);
            TryGet(data, isGlobalConfig, "blackframes", "blackframePixelPercentage", ref blackframePixelPercentage);


            TryGet(data, isGlobalConfig, "timing", "shiftSegmentBySeconds", ref shiftSegmentBySeconds);
            TryGet(data, isGlobalConfig, "timing", "minimumMatchSeconds", ref minimumMatchSeconds);
            TryGet(data, isGlobalConfig, "timing", "PermittedGap", ref PermittedGap);
            TryGet(data, isGlobalConfig, "timing", "PermittedGapWithMinimumEnclosure", ref PermittedGapWithMinimumEnclosure);


            TryGet(data, isGlobalConfig, "redetection", "crawlDirectoriesOnStartup", ref crawlDirectoriesOnStartup);
            TryGet(data, isGlobalConfig, "redetection", "recheckBlackframesOnStartup", ref recheckBlackframesOnStartup);
            TryGet(data, isGlobalConfig, "redetection", "recheckSilenceOnStartup", ref recheckSilenceOnStartup);
            TryGet(data, isGlobalConfig, "redetection", "recheckUndetectedOnStartup", ref recheckUndetectedOnStartup);
            TryGet(data, isGlobalConfig, "redetection", "forceRedetect", ref forceRedetect);
            TryGet(data, isGlobalConfig, "redetection", "redetectIfFileSizeChanges", ref redetectIfFileSizeChanges);


            if (isGlobalConfig) // only set these in the global config to avoid potential issues
            {
                TryGet(data, isGlobalConfig, "paths", "databasePath", ref databasePath);
                TryGet(data, isGlobalConfig, "paths", "PlexDatabasePath", ref PlexDatabasePath);
                TryGet(data, isGlobalConfig, "paths", "TempDirectoryPath", ref TempDirectoryPath);
                TryGet(data, isGlobalConfig, "paths", "ffmpegPath", ref ffmpegPath);
            }


            if (anyDefaultSections)
            {
                data.Sections.RemoveSection("default");
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
            else if (anyDefaultSections)
            {
                File.WriteAllText(iniPath, data.ToString(), encoding);
            }

            anyDefaultSections = false;
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
            if (data.Sections.ContainsSection("default"))
            {
                if (data["default"].ContainsKey(key))
                {
                    anyDefaultSections = true;

                    if (!data.Sections.ContainsSection(section))
                    {
                        data.Sections.AddSection(section);
                    }

                    if (!data[section].ContainsKey(key))
                    {
                        data[section].AddKey(key);
                    }

                    data[section][key] = data["default"][key];
                }
            }

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
