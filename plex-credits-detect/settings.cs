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
        public string globalSettingsPath => Program.PathCombine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plex-credits-detect");

        public List<string> paths = new List<string>();
        public string databasePath = "";
        public string PlexDatabasePath = "";

        public int startOfEpisodeMatchCount = 1;
        public int endOfEpisodeMatchCount = 1;
        public int longestMatchCount = 0;

        public int maximumMatches => startOfEpisodeMatchCount + endOfEpisodeMatchCount + longestMatchCount;

        public int audioAccuracy = 4;
        public int videoAccuracy = 4;
        public double PermittedGap = 3;
        public double minimumMatchSeconds = 20;

        public delegate IFileProvider FileProviderDelegate(string path);

        public FileProviderDelegate FileProvider;

        object ICloneable.Clone()
        {
            Settings ret = new Settings();

            ret.paths = paths.ToList();
            ret.databasePath = databasePath;

            ret.startOfEpisodeMatchCount = startOfEpisodeMatchCount;
            ret.endOfEpisodeMatchCount = endOfEpisodeMatchCount;
            ret.longestMatchCount = longestMatchCount;

            ret.audioAccuracy = audioAccuracy;
            ret.PermittedGap = PermittedGap;
            ret.minimumMatchSeconds = minimumMatchSeconds;

            return ret;
        }

        private void LoadSingle(string path)
        {
            PhysicalFileProvider fp = (PhysicalFileProvider)FileProvider(path);

            if (!fp.GetFileInfo("fingerprint.ini").Exists)
            {
                return;
            }

            //string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            IniConfigurationSource iniConfig = new IniConfigurationSource { Path = "fingerprint.ini", Optional = true, FileProvider = fp };
            IniConfigurationProvider iniProvider = new IniConfigurationProvider(iniConfig);
            iniProvider.Load();


            string temp;


            TryGet(iniProvider, "default:startOfEpisodeMatchCount", ref startOfEpisodeMatchCount);
            TryGet(iniProvider, "default:endOfEpisodeMatchCount", ref endOfEpisodeMatchCount);
            TryGet(iniProvider, "default:longestMatchCount", ref longestMatchCount);

            TryGet(iniProvider, "default:databasePath", ref databasePath);
            TryGet(iniProvider, "default:PlexDatabasePath", ref PlexDatabasePath);

            TryGet(iniProvider, "default:audioAccuracy", ref audioAccuracy);
            TryGet(iniProvider, "default:videoAccuracy", ref videoAccuracy);
            TryGet(iniProvider, "default:PermittedGap", ref PermittedGap);
            TryGet(iniProvider, "default:minimumMatchSeconds", ref minimumMatchSeconds);


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
                            "PlexDatabasePath = " + Program.PathCombine(defaultPlexDataDir, "com.plexapp.plugins.library.db");

                        File.WriteAllText(Program.PathCombine(globalSettingsPath, "fingerprint.ini"), output);

                        Console.WriteLine("Created default config file: " + Program.PathCombine(globalSettingsPath, "fingerprint.ini"));

                        Program.Exit();
                        return false;
                    }
                }
            }

            return true;
        }

        public void Load(string path = "")
        {

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
            if (typeof(int) == assign.GetType())
            {
                int tmp = 0;
                if (TryGetIniInt(iniProvider, key, ref tmp))
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

    }
}
