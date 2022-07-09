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
        public string globalSettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plex-credits-detect");

        public List<string> paths { get; set; } = new List<string>();

        public double detectionStart { get; set; } = 0.0;
        public double detectionEnd { get; set; } = 0.6;
        public int maximumSegments { get; set; } = 99;
        public int minimumFilesForMatch { get; set; } = 2;

        public delegate IFileProvider FileProviderDelegate(string path);

        public FileProviderDelegate FileProvider;

        object ICloneable.Clone()
        {
            Settings ret = new Settings();

            ret.paths = paths.ToList();
            ret.detectionStart = detectionStart;
            ret.detectionEnd = detectionEnd;
            ret.maximumSegments = maximumSegments;
            ret.minimumFilesForMatch = minimumFilesForMatch;

            return ret;
        }


        public Settings()
        {
            FileProvider = GetFileProvider;
        }


        public bool CheckGlobalSettingFile()
        {
            if (!File.Exists(Path.Combine(globalSettingsPath, "fingerprint.ini")))
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (Stream stream = assembly.GetManifestResourceStream("plexCreditsDetect.resources.fingerprint.ini"))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        File.WriteAllText(Path.Combine(globalSettingsPath, "fingerprint.ini"), reader.ReadToEnd());

                        Console.WriteLine("Created default config file: " + Path.Combine(globalSettingsPath, "fingerprint.ini"));

                        Program.Exit();
                        return false;
                    }
                }
            }

            return true;
        }

        private IFileProvider GetFileProvider(string path)
        {
            return new PhysicalFileProvider(path);
        }


        public void Load(string path = "")
        {

            LoadSingle(globalSettingsPath);

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
            double dTemp;
            int iTemp;


            if (iniProvider.TryGet("default:detectionStart", out temp))
            {
                if (double.TryParse(temp, out dTemp))
                {
                    detectionStart = dTemp;
                }
            }
            if (iniProvider.TryGet("default:detectionEnd", out temp))
            {
                if (double.TryParse(temp, out dTemp))
                {
                    detectionEnd = dTemp;
                }
            }

            if (iniProvider.TryGet("default:maximumSegments", out temp))
            {
                if (int.TryParse(temp, out iTemp))
                {
                    maximumSegments = iTemp;
                }
            }

            if (iniProvider.TryGet("default:minimumFilesForMatch", out temp))
            {
                if (int.TryParse(temp, out iTemp))
                {
                    minimumFilesForMatch = iTemp;
                }
            }


            var dirs = iniProvider.GetChildKeys(new List<string>(),"directories");

            foreach (var dir in dirs)
            {
                if (dir.Length > 0 && iniProvider.TryGet("directories:" + dir, out temp))
                {
                    paths.Add(temp);
                }
            }



        }
    }
}
