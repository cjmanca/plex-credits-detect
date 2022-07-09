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

            settingsDict.Add(Path.GetFullPath(settings.globalSettingsPath + "\\"), "" +
                "[default]" + Environment.NewLine +
                "detectionStart = 0.1" + Environment.NewLine +
                "detectionEnd = 0.9" + Environment.NewLine +
                "maximumSegments = 98" + Environment.NewLine +
                "minimumFilesForMatch = 3" + Environment.NewLine +
                "[directories]" + Environment.NewLine +
                "d1 = C:\\Videos" + Environment.NewLine);

            settingsDict.Add(Path.GetFullPath("C:\\Videos\\"), "" +
                "[default]" + Environment.NewLine +
                "detectionStart = 0.3" + Environment.NewLine);

            settingsDict.Add(Path.GetFullPath("C:\\Videos\\Some Series\\"), "" +
                "[default]" + Environment.NewLine +
                "detectionEnd = 0.7" + Environment.NewLine);

            settings.FileProvider = GetFakeFileProvider;

        }

        public IFileProvider GetFakeFileProvider(string path)
        {
            path = Path.GetFullPath(path + "\\");
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
            settings.Load("C:\\Videos\\Some Series\\");

            Assert.That(settings.detectionStart, Is.EqualTo(0.3));
            Assert.That(settings.detectionEnd, Is.EqualTo(0.7));
            Assert.That(settings.maximumSegments, Is.EqualTo(98));
            Assert.That(settings.minimumFilesForMatch, Is.EqualTo(3));
        }
    }
}