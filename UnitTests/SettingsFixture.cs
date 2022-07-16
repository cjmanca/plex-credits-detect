using Microsoft.Extensions.FileProviders;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;

namespace plexCreditsDetect.Tests
{
    [TestFixture]
    public class SettingsFixture
    {
        string tmpFilePath = "";
        string tmpFile = "";
        Settings settings = new Settings();

        Dictionary<string, string> settingsDict = new Dictionary<string, string>();

        [SetUp]
        public void Setup()
        {
            tmpFilePath = Path.GetTempFileName();
            tmpFile = Path.Combine(tmpFilePath, "fingerprint.ini");

            File.Delete(tmpFilePath);
            Directory.CreateDirectory(tmpFilePath);

            settingsDict.Add(Path.GetFullPath(settings.globalSettingsPath + Path.DirectorySeparatorChar), "" +
                "[default]" + Environment.NewLine +
                "introStart = 0.1" + Environment.NewLine +
                "introEnd = 0.9" + Environment.NewLine +
                "introMatchCount = 0" + Environment.NewLine +
                "creditsMatchCount = 1" + Environment.NewLine +
                "[directories]" + Environment.NewLine +
                "d1 = " + Path.GetFullPath(Path.DirectorySeparatorChar + "Videos" + Path.DirectorySeparatorChar) + Environment.NewLine);

            settingsDict.Add(Path.GetFullPath(Path.DirectorySeparatorChar + "Videos" + Path.DirectorySeparatorChar), "" +
                "[default]" + Environment.NewLine +
                "introStart = 0.3" + Environment.NewLine);

            settingsDict.Add(Path.GetFullPath(Path.Combine(Path.DirectorySeparatorChar + "Videos", "Some Series") + Path.DirectorySeparatorChar), "" +
                "[default]" + Environment.NewLine +
                "introEnd = 0.7" + Environment.NewLine);

            settings.FileProvider = GetFakeFileProvider;

        }

        public IFileProvider GetFakeFileProvider(string path)
        {
            path = Path.GetFullPath(path + Path.DirectorySeparatorChar);
            if (settingsDict.ContainsKey(path))
            {
                File.WriteAllText(tmpFile, settingsDict[path]);
            }
            else if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
            return new PhysicalFileProvider(tmpFilePath);
        }

        [Test]
        public void TestHierarchicalLoad()
        {
            settings.Load(Path.GetFullPath(Path.Combine(Path.DirectorySeparatorChar + "Videos", "Some Series") + Path.DirectorySeparatorChar));

            Assert.That(settings.introStart, Is.EqualTo(0.3));
            Assert.That(settings.introEnd, Is.EqualTo(0.7));
            Assert.That(settings.introMatchCount, Is.EqualTo(0));
            Assert.That(settings.creditsMatchCount, Is.EqualTo(1));
        }
    }
}