using plexCreditsDetect.Database;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Command;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.Configuration.Frames;
using SoundFingerprinting.Data;
using SoundFingerprinting.Query;
using System.Diagnostics;

namespace plexCreditsDetect
{
    public class Scanner
    {
        internal static PlexDB plexDB = new PlexDB();
        internal static InMemoryFingerprintDatabase db = null;
        internal static SoundFingerprinting.Emy.FFmpegAudioService audioService = null;
        internal static List<string> ignoreDirectories = new List<string>();

        private static readonly string[] allowedExtensions = new string[] { ".3g2", ".3gp", ".amv", ".asf", ".avi", ".flv", ".f4v", ".f4p", ".f4a", ".f4b", ".m4v", ".mkv", ".mov", ".qt", ".mp4", ".m4p", ".mpg", ".mp2", ".mpeg", ".mpe", ".mpv", ".m2v", ".mts", ".m2ts", ".ts", ".ogv", ".ogg", ".rm", ".rmvb", ".viv", ".vob", ".webm", ".wmv" };

        int processed = 0;
        int processAttempts = 0;

        bool wroteHeader = false;

        bool CheckSingleEpisode(Episode ep, bool allowInsert = true)
        {
            if (!ep.Exists)
            {
                return false;
            }

            if (!IsVideoExtension(ep.fullPath))
            {
                return false;
            }

            if (ep.meta_id < 0)
            {
                //Console.WriteLine($"{ep.id}: Metadata not found in plex db. Removing.");
                if (ep.InPrivateDB)
                {
                    db.DeleteEpisodeTimings(ep);
                    db.DeleteEpisode(ep);
                }
                ep.DetectionPending = false;
                ep.needsScanning = false;
                return false;
            }

            if (!ep.InPrivateDB)
            {
                // metadata was found in plex db, so update our db with the episode
                //Console.WriteLine($"{ep.id}: Adding to local db.");
                ep.DetectionPending = true;

                if (allowInsert)
                {
                    db.Insert(ep);
                }
            }

            ep.passed = true;
            return true;
        }

        internal bool CheckIfFileNeedsScanning(Episode ep, Settings settings, bool insertCheck = false)
        {
            bool trueValForNeedsScanning = settings.maximumMatches > 0 && (settings.useVideo || settings.useAudio) && !ep.isMovie;
            bool trueValForNeedsSilenceScanning = settings.detectSilenceAfterCredits;

            if (!ep.Exists) // can't scan something that doesn't exist
            {
                ep.needsScanning = false;
                ep.needsSilenceScanning = false;
                ep.needsBlackframeScanning = false;
                return false;
            }
            
            if (!IsVideoExtension(ep))
            {
                ep.needsScanning = false;
                ep.needsSilenceScanning = false;
                ep.needsBlackframeScanning = false;
                return false;
            }

            bool trueValForNeedsBlackframeScanning = settings.detectBlackframes && (!settings.blackframeOnlyMovies || ep.isMovie);

            if (settings.forceRedetect)
            {
                ep.needsScanning = trueValForNeedsScanning;
                ep.needsSilenceScanning = trueValForNeedsSilenceScanning;
                ep.needsBlackframeScanning = trueValForNeedsBlackframeScanning;
                return true;
            }

            if (!ep.InPrivateDB)
            {
                if (ep.meta_id < 0)
                {
                    ep.needsScanning = false;
                    ep.needsSilenceScanning = false;
                    ep.needsBlackframeScanning = false;
                    return false;
                }
                ep.needsScanning = trueValForNeedsScanning;
                ep.needsSilenceScanning = trueValForNeedsSilenceScanning;
                ep.needsBlackframeScanning = trueValForNeedsBlackframeScanning;
                return insertCheck;
            }

            if (settings.redetectIfFileSizeChanges && ep.FileSizeOnDisk != ep.FileSizeInDB)
            {
                ep.needsScanning = trueValForNeedsScanning;
                ep.needsSilenceScanning = trueValForNeedsSilenceScanning;
                ep.needsBlackframeScanning = trueValForNeedsBlackframeScanning;
                return true;
            }

            if (trueValForNeedsSilenceScanning && !ep.SilenceDetectionDone)
            {
                ep.needsSilenceScanning = trueValForNeedsSilenceScanning;
            }
            if (trueValForNeedsBlackframeScanning && !ep.BlackframeDetectionDone)
            {
                ep.needsBlackframeScanning = trueValForNeedsBlackframeScanning;
            }

            if (trueValForNeedsScanning)
            {
                var timings = db.GetNonPlexTimings(ep);

                if (timings == null)
                {
                    ep.needsScanning = true;
                    return true;
                }
                int intros = timings.Count(x => x.isCredits == false && !x.isSilence && !x.isBlackframes);
                int credits = timings.Count(x => x.isCredits == true && !x.isSilence && !x.isBlackframes);

                if (intros < settings.introMatchCount)
                {
                    ep.needsScanning = true;
                    return true;
                }
                if (credits < settings.creditsMatchCount)
                {
                    ep.needsScanning = true;
                    return true;
                }
            }

            ep.needsScanning = false;
            return ep.needsSilenceScanning || ep.needsBlackframeScanning;
        }

        FingerprintConfiguration GenerateFingerprintAudioConfig(Settings settings)
        {
            // audio configuration
            FingerprintConfiguration config = new DefaultFingerprintConfiguration();

            config.Stride = new SoundFingerprinting.Strides.IncrementalStaticStride(settings.stride);
            config.SampleRate = settings.sampleRate;
            config.FrequencyRange = new FrequencyRange(settings.minFrequency, settings.maxFrequency);

            return config;
        }
        VideoFingerprintConfiguration GenerateFingerprintVideoConfig(Settings settings)
        {
            // video configuration
            VideoFingerprintConfiguration config = new DefaultVideoFingerprintConfiguration();

            config.HashingConfig.Width = (int)(1920 / settings.videoSizeDivisor);
            config.HashingConfig.Height = (int)(1080 / settings.videoSizeDivisor);
            config.FrameRate = settings.frameRate;
            //config.TopWavelets = (int)((settings.videoWidth * settings.videoHeight) * 0.05) + 1;

            return config;
        }

        AVFingerprintConfiguration GenerateFingerprintConfig(AVFingerprintConfiguration config, Settings settings)
        {
            if (settings.useAudio)
            {
                config.Audio = GenerateFingerprintAudioConfig(settings);
            }
            if (settings.useVideo)
            {
                config.Video = GenerateFingerprintVideoConfig(settings);
            }

            return config;
        }

        internal Segment GetTimings(Episode ep, Settings settings, bool isCredits, Segment seg = null)
        {
            Segment ret = new Segment();

            double duration = GetSearchDuration(ep, settings, isCredits);
            ret.start = GetSearchStartAt(ep, settings, isCredits);

            double plexEnd = 0;

            if (ep.plexTimings != null)
            {
                plexEnd = ep.plexTimings.end + 30;
            }

            if (!isCredits && ret.start < plexEnd)
            {
                ret.start = plexEnd;
            }

            ret.end = Math.Min(ret.start + duration, ep.duration);

            if (!isCredits && ret.end > ep.duration * settings.introEnd)
            {
                ret.end = ep.duration * settings.introEnd;
            }

            if (seg != null)
            {
                ret.start = seg.start - 30;
                ret.end = seg.end + 30;
            }

            if (ret.end > ep.duration)
            {
                ret.end = ep.duration;
            }

            return ret;
        }

        internal void FingerprintFile(Episode ep, bool isCredits, Settings settings = null, Segment seg = null, int partNum = -1)
        {
            if (!ep.Exists || ep.duration <= 0)
            {
                return;
            }

            if (settings == null)
            {
                settings = new Settings(ep.fullPath);
            }

            AVHashes hashes = db.GetTrackHash(ep.id, isCredits, partNum);

            if (hashes == null || hashes.IsEmpty)
            {
                try
                {
                    MediaType avtype = 0;

                    if (settings.useAudio)
                    {
                        avtype |= MediaType.Audio;
                    }
                    if (settings.useVideo)
                    {
                        avtype |= MediaType.Video;
                    }

                    var times = GetTimings(ep, settings, isCredits, seg);

                    Console.WriteLine($"Fingerprinting: {ep.id} ({TimeSpan.FromSeconds(times.start):g} - {TimeSpan.FromSeconds(times.end):g})");

                    string creditSnippet = isCredits ? "credits" : "intro";
                    string tempFile = "";

                    if (partNum >= 0)
                    {
                        tempFile = Program.PathCombine(Program.PathCombine(Settings.TempDirectoryPath,"plex-credits-detect-temp"), $"{creditSnippet}.{partNum}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");
                    }
                    else
                    {
                        tempFile = Program.PathCombine(Program.PathCombine(Settings.TempDirectoryPath, "plex-credits-detect-temp"), $"{creditSnippet}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");
                    }

                    if (!File.Exists(tempFile))
                    {
                        if (!ffmpeghelper.CutVideo(times.start, times.end, ep.fullPath, tempFile, settings.useVideo, settings.useAudio || settings.detectSilenceAfterCredits, settings.sampleRate))
                        {
                            return;
                        }
                    }
                    // create hashed fingerprint
                    var hashedFingerprint = FingerprintCommandBuilder.Instance
                                                .BuildFingerprintCommand()
                                                //.From(ep.fullPath, duration, start, avtype)
                                                //.From(ep.fullPath, ep.duration, 0, avtype)
                                                .From(tempFile, avtype)
                                                .WithFingerprintConfig(config => GenerateFingerprintConfig(config, settings))
                                                .UsingServices(audioService)
                                                .Hash()
                                                .Result;

                    // store hashes in the database for later retrieval
                    db.InsertHash(ep, hashedFingerprint, avtype, isCredits, times.start, partNum);
                }
                catch (Exception e)
                {
                    Console.WriteLine("FingerprintFile Exception: " + e.ToString());
                }
            }
        }

        double GetSearchDuration(Episode ep, Settings settings, bool isCredits)
        {
            if (isCredits)
            {
                return Math.Min(settings.creditsMaxSearchPeriod, (ep.duration * settings.creditsEnd) - (ep.duration * settings.creditsStart));
            }
            else
            {
                return Math.Min(settings.introMaxSearchPeriod, (ep.duration * settings.introEnd) - (ep.duration * settings.introStart));
            }
        }

        double GetSearchStartAt(Episode ep, Settings settings, bool isCredits)
        {
            if (isCredits)
            {
                return GetSearchEndAt(ep, settings, isCredits) - GetSearchDuration(ep, settings, isCredits);
            }
            else
            {
                return ep.duration * settings.introStart;
            }
        }
        double GetSearchEndAt(Episode ep, Settings settings, bool isCredits)
        {
            if (isCredits)
            {
                return ep.duration * settings.creditsEnd;
            }
            else
            {
                return GetSearchStartAt(ep, settings, isCredits) + GetSearchDuration(ep, settings, isCredits);
            }
        }

        public static bool IsVideoExtension(Episode ep)
        {
            return IsVideoExtension(ep.fullPath);
        }
        public static bool IsVideoExtension(string path)
        {

            string ext = Path.GetExtension(path);

            if (allowedExtensions.Contains(ext.ToLower()))
            {
                return true;
            }

            return false;
        }




        void DoFullFingerprint(List<Episode> allEpisodes, Settings settings)
        {
            bool firstEntry = true;

            foreach (var ep in allEpisodes)
            {
                if (firstEntry)
                {
                    firstEntry = false;
                    Console.WriteLine("");

                    // unfortunately, it doesn't seem like the plex server monitors changes to the activities table
                    // so this doesn't work as I had hoped
                    //PlexDB.ShowSeasonInfo seasonInfo = plexDB.GetShowAndSeason(metaID);
                    //plexDB.NewActivity($"{seasonInfo.showName} S{seasonInfo.seasonNumber}");
                }

                if (settings.introMatchCount > 0)
                {
                    FingerprintFile(ep, false, settings);
                }
                if (settings.creditsMatchCount > 0)
                {
                    FingerprintFile(ep, true, settings);
                }
                
            }
        }


        int DetectSingleEpisode(Episode ep, Settings settings)
        {
            try
            {
                if (!ep.needsScanning)
                {
                    return 0;
                }

                if ((settings.redetectIfFileSizeChanges && ep.FileSizeInDB != ep.FileSizeOnDisk) || settings.forceRedetect)
                {
                    // episode changed, clear and start from scratch
                    ep.segments.allSegments.Clear();
                }

                MediaType avtype = 0;

                if (settings.useAudio)
                {
                    avtype |= MediaType.Audio;
                }
                if (settings.useVideo)
                {
                    avtype |= MediaType.Video;
                }


                if (!wroteHeader)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"Detecting: {ep.id}");
                    processAttempts++;
                    wroteHeader = true;
                }

                Segments audioSegments = new Segments();
                Segments videoSegments = new Segments();
                Segments audioSegmentsCredits = new Segments();
                Segments videoSegmentsCredits = new Segments();

                if (settings.introMatchCount > 0)
                {
                    DoSingleQuery(settings, false, ep, avtype, audioSegments, videoSegments);
                }
                if (settings.creditsMatchCount > 0)
                {
                    DoSingleQuery(settings, true, ep, avtype, audioSegmentsCredits, videoSegmentsCredits);
                }

                if (audioSegments.allSegments.Any() || videoSegments.allSegments.Any() || audioSegmentsCredits.allSegments.Any() || videoSegmentsCredits.allSegments.Any())
                {

                    Segments validatedSegments;
                    Segments validatedSegmentsCredits;

                    if (settings.useAudio && settings.useVideo)
                    {
                        // we only want segments where both the audio and video agree about a duplicate area
                        validatedSegments = audioSegments.FindAllOverlaps(videoSegments);
                        validatedSegmentsCredits = audioSegmentsCredits.FindAllOverlaps(videoSegments);
                    }
                    else if (settings.useAudio)
                    {
                        validatedSegments = audioSegments;
                        validatedSegmentsCredits = audioSegmentsCredits;
                    }
                    else
                    {
                        validatedSegments = videoSegments;
                        validatedSegmentsCredits = videoSegmentsCredits;
                    }


                    validatedSegments.allSegments.Sort((b, a) => a.duration.CompareTo(b.duration));
                    validatedSegmentsCredits.allSegments.Sort((b, a) => a.start.CompareTo(b.start));

                    for (int i = 0; i < settings.introMatchCount; i++)
                    {
                        if (validatedSegments.allSegments.Count > i)
                        {
                            ep.segments.AddSegment(validatedSegments.allSegments[i]);
                        }
                    }
                    for (int i = 0; i < settings.creditsMatchCount; i++)
                    {
                        if (validatedSegmentsCredits.allSegments.Count > i)
                        {
                            ep.segments.AddSegment(validatedSegmentsCredits.allSegments[i]);
                        }
                    }

                    ep.segments.allSegments.Sort((a, b) => a.start.CompareTo(b.start));

                    return validatedSegments.allSegments.Count() + validatedSegmentsCredits.allSegments.Count();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("DetectSingleEpisode Exception: " + e.ToString());
            }

            return 0;
        }


        void InsertTimings(Episode ep, Settings settings, bool verbose = true, bool doingReinsert = false)
        {
            try
            {
                if (verbose)
                {
                    OutputSegments("Match", ep.segments, settings);
                }

                if (!doingReinsert)
                {
                    db.DeleteEpisodeTimings(ep);
                }
                else
                {
                    db.DeleteEpisodePlexTimings(ep);
                }

                if (ep.plexTimings != null)
                {
                    db.InsertTiming(ep, ep.plexTimings, true);
                }

                plexDB.DeleteExistingIntros(ep.meta_id);

                Segments segments = new Segments();

                for (int i = 0; i < ep.segments.allSegments.Count; i++)
                {
                    if (!doingReinsert)
                    {
                        db.InsertTiming(ep, ep.segments.allSegments[i], false);
                    }

                    // add them all to a temporary segments list, ignoring sections to allow overlapping segments to combine
                    segments.AddSegment(ep.segments.allSegments[i], settings.PermittedGapWithMinimumEnclosure, true);
                }

                segments.allSegments.Sort((a, b) => a.start.CompareTo(b.start));

                for (int i = 0; i < segments.allSegments.Count; i++)
                {
                    segments.allSegments[i].start -= settings.shiftSegmentBySeconds;
                    if (segments.allSegments[i].start < 0)
                    {
                        segments.allSegments[i].start = 0;
                    }
                    if (segments.allSegments[i].end < ep.duration - 2)
                    {
                        segments.allSegments[i].end -= settings.shiftSegmentBySeconds;
                    }
                    else
                    {
                        segments.allSegments[i].end = ep.duration;
                    }
                    if (segments.allSegments[i].end < 0)
                    {
                        segments.allSegments[i].end = 0;
                    }

                    plexDB.Insert(ep.meta_id, segments.allSegments[i], i + 1);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("InsertTimings Exception: " + e.ToString());
            }

            processed++;
        }


        public void ScanDirectory(string path, Settings settings = null)
        {
            processed = 0;
            processAttempts = 0;
            wroteHeader = false;

            if (!Directory.Exists(path))
            {
                db.ClearDetectionPendingForDirectory(path);
                return;
            }

            if (settings == null)
            {
                settings = new Settings(path);
            }

            if (settings.maximumMatches <= 0)
            {
                db.ClearDetectionPendingForDirectory(path);
                return;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();

            string relDir = Program.getRelativeDirectory(Program.PathCombine(path, "dud"));

            try
            {

                db.SetupNewScan();

                Episode ep = null;

                var files = Directory.EnumerateFiles(path).ToList();


                List<Episode> allEpisodes;

                List<Episode> bestIntros = new List<Episode>();
                List<Segment> bestIntroSegments = new List<Segment>();
                List<Episode> bestCredits = new List<Episode>();
                List<Segment> bestCreditSegments = new List<Segment>();
                List<Episode> randomEpisodes = new List<Episode>();
                List<Episode> randomEpisodesFull = new List<Episode>();

                int totalEpisodesWithAllIntrosDetected = 0;
                int totalEpisodesWithAllCreditsDetected = 0;

                bool tryQuickDetect = false;
                int remainingToFind = 0;



                tryQuickDetect = true;
                remainingToFind = 0;

                allEpisodes = db.GetNonPlexTimingsForDir(relDir);

                foreach (var item in files)
                {
                    if (!IsVideoExtension(item))
                    {
                        continue;
                    }
                    ep = allEpisodes.FirstOrDefault(x => x.fullPath == item);
                    if (ep == null)
                    {
                        ep = new Episode(item);
                        allEpisodes.Add(ep);
                    }
                }




                for (int i = 0; i < settings.introMatchCount; i++)
                {
                    bestIntros.Add(null);
                    bestIntroSegments.Add(new Segment(0, 0));
                }
                for (int i = 0; i < settings.creditsMatchCount; i++)
                {
                    bestCredits.Add(null);
                    bestCreditSegments.Add(new Segment(0, 0));
                }

                foreach (var item in allEpisodes)
                {
                    if (!CheckSingleEpisode(item))
                    {
                        continue;
                    }

                    CheckIfFileNeedsScanning(item, settings);
                }

                allEpisodes.RemoveAll(x => !x.passed);

                int totalNeedsScanning = allEpisodes.Count(x => x.needsScanning);
                int totalToFingerprint = allEpisodes.Count();

                int totalNeedsSilenceScanning = allEpisodes.Count(x => x.needsSilenceScanning);
                int totalNeedsBlackframeScanning = allEpisodes.Count(x => x.needsBlackframeScanning);


                if (totalNeedsSilenceScanning <= 0 && totalNeedsBlackframeScanning <= 0 && (totalNeedsScanning <= 0 || totalToFingerprint < 2))
                {
                    CleanTemp();
                    db.ClearDetectionPendingForDirectory(relDir);
                    return;
                }

                if (totalNeedsScanning > totalToFingerprint / 4)
                {
                    tryQuickDetect = false;
                }

                allEpisodes.Sort((a, b) => a.name.CompareTo(b.name));


                if ((totalNeedsSilenceScanning > 0 || totalNeedsBlackframeScanning > 0) && (totalNeedsScanning <= 0 || totalToFingerprint < 2))
                {
                    foreach (var item in allEpisodes)
                    {
                        if (item.needsSilenceScanning || item.needsBlackframeScanning)
                        {
                            HandleInsert(item, settings, 0);
                        }
                    }

                    return;
                }


                int epCount = 1;

                foreach (var item in allEpisodes)
                {
                    if (randomEpisodes.Count() < (double)epCount / ((double)totalToFingerprint / (double)settings.quickDetectFingerprintSamples))
                    {
                        randomEpisodes.Add(item);
                    }
                    else if (randomEpisodes.Count() + randomEpisodesFull.Count() < (double)epCount / ((double)totalToFingerprint / (double)(settings.fullDetectFingerprintMaxSamples + settings.quickDetectFingerprintSamples)))
                    {
                        randomEpisodesFull.Add(item);
                    }


                    int introCount = item.segments.allSegments.Count(x => !x.isCredits && !x.isSilence && !x.isBlackframes);
                    int creditCount = item.segments.allSegments.Count(x => x.isCredits && !x.isSilence && !x.isBlackframes);

                    if (introCount == settings.introMatchCount)
                    {
                        totalEpisodesWithAllIntrosDetected++;
                        int count = 0;
                        foreach (var seg in item.segments.allSegments)
                        {
                            if (!seg.isCredits && !seg.isSilence && !seg.isBlackframes)
                            {
                                if (count < settings.introMatchCount && seg.duration > bestIntroSegments[count].duration)
                                {
                                    bestIntroSegments[count] = seg;
                                    bestIntros[count] = item;
                                }
                                count++;
                            }
                        }
                    }
                    if (creditCount == settings.creditsMatchCount)
                    {
                        totalEpisodesWithAllCreditsDetected++;
                        int count = 0;
                        foreach (var seg in item.segments.allSegments)
                        {
                            if (seg.isCredits && !seg.isSilence && !seg.isBlackframes)
                            {
                                if (count < settings.creditsMatchCount && seg.duration > bestCreditSegments[count].duration)
                                {
                                    bestCreditSegments[count] = seg;
                                    bestCredits[count] = item;
                                }
                                count++;
                            }
                        }
                    }

                    epCount++;
                }

                randomEpisodesFull.AddRange(randomEpisodes);
                randomEpisodesFull.Sort((a, b) => a.name.CompareTo(b.name));


                if (settings.introMatchCount > 0)
                {
                    if (totalEpisodesWithAllIntrosDetected < 8)
                    {
                        tryQuickDetect = false;
                    }
                    else
                    {
                        foreach (var item in bestIntros)
                        {
                            if (item == null)
                            {
                                tryQuickDetect = false;
                            }
                        }
                    }
                }
                if (settings.creditsMatchCount > 0)
                {
                    if (totalEpisodesWithAllCreditsDetected < 8)
                    {
                        tryQuickDetect = false;
                    }
                    else
                    {
                        foreach (var item in bestCredits)
                        {
                            if (item == null)
                            {
                                tryQuickDetect = false;
                            }
                        }
                    }
                }
                


                if (tryQuickDetect)
                {
                    for (int i = 0; i < settings.introMatchCount; i++)
                    {
                        //FingerprintFile(bestIntros[i], false, settings, bestIntroSegments[i], i);
                        FingerprintFile(bestIntros[i], false, settings);
                    }
                    for (int i = 0; i < settings.creditsMatchCount; i++)
                    {
                        //FingerprintFile(bestCredits[i], true, settings, bestCreditSegments[i], i);
                        FingerprintFile(bestCredits[i], true, settings);
                    }
                    foreach (var item in randomEpisodes)
                    {
                        if (settings.introMatchCount > 0)
                        {
                            FingerprintFile(item, false, settings);
                        }
                        if (settings.creditsMatchCount > 0)
                        {
                            FingerprintFile(item, true, settings);
                        }
                    }



                    foreach (var item in allEpisodes)
                    {
                        wroteHeader = false;
                        ep = item;
                        int detected = DetectSingleEpisode(ep, settings);

                        if (ep.segments.allSegments.Count() >= settings.maximumMatches)
                        {
                            HandleInsert(ep, settings, detected);
                        }
                        else
                        {
                            if (ep.needsScanning)
                            {
                                remainingToFind++;
                                break;
                            }
                        }
                    }
                }


                if (!tryQuickDetect || remainingToFind > 0)
                {
                    //db.SetupNewScan();

                    DoFullFingerprint(randomEpisodesFull, settings);

                    foreach (var item in allEpisodes)
                    {
                        wroteHeader = false;
                        ep = item;
                        int detected = DetectSingleEpisode(ep, settings);

                        HandleInsert(ep, settings, detected);

                    }

                }



                /*
                try
                {
                    if (processed <= 0)
                    {
                        path = Program.getRelativeDirectory(Program.PathCombine(path, "tmp"));
                        //Console.WriteLine($"No intros/credits detected in scanned files in {path}");
                        List<Episode> pending = db.GetPendingEpisodesForSeason(path);

                        if (pending != null)
                        {
                            Console.WriteLine($"List: ");
                            foreach (var item in pending)
                            {
                                Console.Write($"{item.id}: ");
                                if (plexDB.GetMetadataID(item) < 0) // pending item is no longer in the plex DB. Clean up.
                                {
                                    Console.WriteLine($" Metadata not found in plex db. Removing.");
                                    db.DeleteEpisodeTimings(item);
                                    db.DeleteEpisode(item);
                                }
                                else
                                {
                                    Console.WriteLine(" Still in Plex DB but unable to process. Ignoring until next restart.");
                                }
                            }
                        }

                        ignoreDirectories.Add(path);
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("ScanDirectory Exception (3): " + e.ToString());
                }
                */

            }
            finally
            {
                CleanTemp();
                //plexDB.EndActivity();

                db.ClearDetectionPendingForDirectory(relDir);

                if (processAttempts > 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"Detection took {sw.Elapsed:g}");
                }
            }

        }

        void HandleInsert(Episode ep, Settings settings, int detected)
        {
            bool silenceDetected = false;
            bool blackframesDetected = false;

            if (settings.detectSilenceAfterCredits && (ep.needsScanning || ep.needsSilenceScanning || ((settings.redetectIfFileSizeChanges && ep.FileSizeInDB != ep.FileSizeOnDisk) || settings.forceRedetect)))
            {
                if (!wroteHeader)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"Detecting: {ep.id}");
                    processAttempts++;
                    wroteHeader = true;
                }

                detected += DetectSilence(ep, settings);
                ep.needsSilenceScanning = false;
                ep.SilenceDetectionDone = true;
                ep.SilenceDetectionPending = false;
                silenceDetected = true;
            }
            if (settings.detectBlackframes && (ep.needsBlackframeScanning || ((settings.redetectIfFileSizeChanges && ep.FileSizeInDB != ep.FileSizeOnDisk) || settings.forceRedetect)))
            {
                if (!wroteHeader)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"Detecting: {ep.id}");
                    processAttempts++;
                    wroteHeader = true;
                }

                detected += DetectBlackframes(ep, settings);
                ep.needsBlackframeScanning = false;
                ep.BlackframeDetectionDone = true;
                ep.BlackframeDetectionPending = false;
                blackframesDetected = true;
            }
            if (ep.needsScanning || silenceDetected || blackframesDetected)
            {
                if (detected > 0)
                {
                    InsertTimings(ep, settings);
                }

                ep.needsScanning = false;
                ep.DetectionPending = false;
                db.Insert(ep);
            }
        }

        void DoSingleQuery(Settings settings, bool isCredits, Episode ep, MediaType avtype, Segments audioSegments, Segments videoSegments)
        {
            var times = GetTimings(ep, settings, isCredits);


            string creditSnippet = isCredits ? "credits" : "intro";
            string tempFile = Program.PathCombine(Program.PathCombine(Settings.TempDirectoryPath, "plex-credits-detect-temp"), $"{creditSnippet}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");

            if (!File.Exists(tempFile))
            {
                if (!ffmpeghelper.CutVideo(times.start, times.end, ep.fullPath, tempFile, settings.useVideo, settings.useAudio || settings.detectSilenceAfterCredits, settings.sampleRate))
                {
                    return;
                }
            }

            var result = QueryCommandBuilder.Instance
                .BuildQueryCommand()
                //.From(ep.fullPath, GetSearchDuration(ep, settings, isCredits), GetSearchStartAt(ep, settings, isCredits), avtype)
                //.From(ep.fullPath, ep.duration, 0, avtype)
                .From(tempFile, avtype)
                .WithQueryConfig(config =>
                {
                    if (settings.useAudio)
                    {
                        config.Audio.FingerprintConfiguration = GenerateFingerprintAudioConfig(settings);
                        config.Audio.MaxTracksToReturn = 9999;
                        config.Audio.ThresholdVotes = settings.audioAccuracy;
                        config.Audio.PermittedGap = settings.PermittedGap;
                        config.Audio.AllowMultipleMatchesOfTheSameTrackInQuery = true;
                        config.Audio.YesMetaFieldsFilters = new Dictionary<string, string> { { "dir", ep.dir } };
                        config.Audio.NoMetaFieldsFilters = new Dictionary<string, string> { { "name", ep.name }, { "isCredits", (!isCredits).ToString() } };
                    }
                    if (settings.useVideo)
                    {
                        config.Video.FingerprintConfiguration = GenerateFingerprintVideoConfig(settings);
                        config.Video.MaxTracksToReturn = 9999;
                        config.Video.ThresholdVotes = settings.videoAccuracy;
                        config.Video.PermittedGap = settings.PermittedGap;
                        config.Video.AllowMultipleMatchesOfTheSameTrackInQuery = true;
                        config.Video.YesMetaFieldsFilters = new Dictionary<string, string> { { "dir", ep.dir } };
                        config.Video.NoMetaFieldsFilters = new Dictionary<string, string> { { "name", ep.name }, { "isCredits", (!isCredits).ToString() } };
                    }
                    return config;
                })
                .UsingServices(db.GetModelService(), audioService)
                .Query()
                .Result;

            List<ResultEntry> sortedAudio;
            List<ResultEntry> sortedVideo;



            if (result != null)
            {
                if (settings.useAudio)
                {
                    sortedAudio = result.Audio.ResultEntries.ToList();
                    sortedAudio.Sort((b, a) => a.TrackCoverageWithPermittedGapsLength.CompareTo(b.TrackCoverageWithPermittedGapsLength));

                    foreach (var entry in sortedAudio)
                    {
                        Segment seg = new Segment(entry.QueryMatchStartsAt + times.start, entry.QueryMatchStartsAt + times.start + entry.TrackCoverageWithPermittedGapsLength);

                        seg.isCredits = isCredits;
                        seg.isSilence = false;
                        seg.isBlackframes = false;

                        if (seg.duration >= settings.minimumMatchSeconds)
                        {
                            audioSegments.AddSegment(seg, settings.PermittedGapWithMinimumEnclosure);
                        }
                    }

                    if (audioSegments.allSegments.Any())
                    {
                        OutputSegments($"Audio {creditSnippet} match", audioSegments, settings);
                    }
                }

                if (settings.useVideo)
                {
                    sortedVideo = result.Video.ResultEntries.ToList();
                    sortedVideo.Sort((a, b) => a.QueryMatchStartsAt.CompareTo(b.QueryMatchStartsAt));

                    foreach (var entry in sortedVideo)
                    {
                        Segment seg = new Segment(entry.QueryMatchStartsAt + times.start, entry.QueryMatchStartsAt + times.start + entry.TrackCoverageWithPermittedGapsLength);

                        seg.isCredits = isCredits;
                        seg.isSilence = false;
                        seg.isBlackframes = false;

                        if (seg.duration >= settings.minimumMatchSeconds)
                        {
                            videoSegments.AddSegment(seg, settings.PermittedGapWithMinimumEnclosure);
                        }
                    }

                    if (videoSegments.allSegments.Any())
                    {
                        OutputSegments($"Video {creditSnippet} match", videoSegments, settings);
                    }
                }
            }
        }

        public int DetectSilence(Episode ep, Settings settings)
        {
            if (!settings.detectSilenceAfterCredits)
            {
                return 0;
            }

            ep.segments.allSegments.RemoveAll(x => x.isSilence && !x.isBlackframes);

            Segment credits = ep.segments.allSegments.FirstOrDefault(x => x.isCredits && !x.isBlackframes && !x.isSilence);

            if (credits != null)
            {
                credits = new Segment(credits.end, ep.duration);
            }

            Segment times = GetTimings(ep, settings, true, credits);

            string creditSnippet = (credits == null ? "credits" : "silence");
            string tempFile = Program.PathCombine(Program.PathCombine(Settings.TempDirectoryPath, "plex-credits-detect-temp"), $"{creditSnippet}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");

            if (!File.Exists(tempFile))
            {
                if (!ffmpeghelper.CutVideo(times.start, times.end, ep.fullPath, tempFile, false, true, settings.sampleRate))
                {
                    return 0;
                }
            }
            
            Segments segments = ffmpeghelper.DetectSilence(tempFile, settings.minimumMatchSeconds, settings.silenceDecibels);
            foreach (var seg in segments.allSegments)
            {
                seg.start += times.start;
                seg.end += times.start;

                ep.segments.AddSegment(seg);
            }

            if (segments.allSegments.Any())
            {
                OutputSegments($"Silence found", segments, settings);
            }

            return segments.allSegments.Count();
        }

        public int DetectBlackframes(Episode ep, Settings settings)
        {
            if (!settings.detectBlackframes || (settings.blackframeOnlyMovies && !ep.isMovie))
            {
                return 0;
            }

            ep.segments.allSegments.RemoveAll(x => !x.isSilence && x.isBlackframes);

            Segment times = null;
            
            if ((!ep.isMovie && settings.blackframeUseMaxSearchPeriodForEpisodes) || (ep.isMovie && settings.blackframeUseMaxSearchPeriodForMovies))
            {
                times = GetTimings(ep, settings, true);
            }
            else
            {
                times = new Segment();

                times.start = ep.duration * settings.creditsStart;
                times.end = ep.duration * settings.creditsEnd;
            }

            Segments segments = ffmpeghelper.DetectBlackframes(settings, times.start, times.end, ep.fullPath, ep.isMovie ? settings.blackframeMovieMinimumMatchSeconds : settings.minimumMatchSeconds);
            
            foreach (var seg in segments.allSegments)
            {
                seg.start += times.start;
                seg.end += times.start;

                ep.segments.AddSegment(seg);
            }

            if (segments.allSegments.Any())
            {
                OutputSegments($"Black frames found", segments, settings);
            }

            return segments.allSegments.Count();
        }


        public void CleanTemp()
        {
            var files = Directory.EnumerateFiles(Program.PathCombine(Settings.TempDirectoryPath, "plex-credits-detect-temp"));
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        public void OutputSegments(string pre, Segments segs, Settings settings)
        {
            foreach (var resultEntry in segs.allSegments)
            {
                if (resultEntry.duration >= settings.minimumMatchSeconds)
                {
                    Console.WriteLine($"{pre} from {resultEntry.start:0.00} to {resultEntry.end:0.00}. Duration: {resultEntry.duration:0.00}.");

                }
            }
        }

        public void OutputMatches(IEnumerable<ResultEntry>? results, MediaType mediaType)
        {
            foreach (var resultEntry in results ?? Enumerable.Empty<ResultEntry>())
            {
                if (resultEntry.DiscreteTrackCoverageLength >= 20)
                {
                    Console.WriteLine($"Matched {resultEntry.Track.Id} on media type {mediaType} query, confidence {resultEntry.Confidence:0.00}. Match length {resultEntry.TrackCoverageWithPermittedGapsLength:0.00}.");
                    Console.WriteLine($"Track start {resultEntry.TrackMatchStartsAt:0.00}");
                    Console.WriteLine($"Query start {resultEntry.QueryMatchStartsAt:0.00}");
                    Console.WriteLine($"Track discrete coverage length {resultEntry.Coverage.TrackDiscreteCoverageLength}, with detected {resultEntry.Coverage.TrackGaps.Count()} gaps of length {resultEntry.Coverage.TrackGapsCoverageLength}");
                    Console.WriteLine($"Query discrete coverage length {resultEntry.Coverage.QueryDiscreteCoverageLength}, with detected {resultEntry.Coverage.QueryGaps.Count()} gaps");
                    
                    Console.WriteLine("\n");
                }
            }
        }

        public void CheckDirectory(string path)
        {
            try
            {
                var files = Directory.EnumerateFiles(path).ToList();
                files.Sort();
                if (files.Count() > 0)
                {
                    Settings settings = new Settings(path);

                    if ((settings.recheckBlackframesOnStartup && settings.detectBlackframes) || (settings.recheckSilenceOnStartup && settings.detectSilenceAfterCredits) || (settings.maximumMatches > 0 && settings.recheckUndetectedOnStartup))
                    {
                        foreach (var file in files)
                        {
                            if (!IsVideoExtension(file))
                            {
                                continue;
                            }
                            Episode ep = new Episode(file);

                            CheckIfFileNeedsScanning(ep, settings, true);

                            if (settings.recheckUndetectedOnStartup && ep.needsScanning)
                            {
                                ep.DetectionPending = true;
                            }
                            if (settings.recheckSilenceOnStartup && ep.needsSilenceScanning)
                            {
                                ep.SilenceDetectionPending = true;
                            }
                            if (settings.recheckBlackframesOnStartup && ep.needsBlackframeScanning)
                            {
                                ep.BlackframeDetectionPending = true;
                            }

                            if ((settings.recheckUndetectedOnStartup && ep.needsScanning) || (settings.recheckSilenceOnStartup && ep.needsSilenceScanning) || (settings.recheckBlackframesOnStartup && ep.needsBlackframeScanning))
                            {
                                Console.WriteLine("Episode needs scanning: " + ep.id);
                                db.Insert(ep);
                            }
                        }
                    }
                }

                var directories = Directory.EnumerateDirectories(path).ToList();
                directories.Sort();
                foreach (var dir in directories)
                {
                    CheckDirectory(dir);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("CheckDirectory Exception: " + e.ToString());
            }
        }

        public void InvalidateDirectory(string path)
        {
            try
            {
                var files = Directory.EnumerateFiles(path);
                if (files.Count() > 0)
                {
                    Settings settings = new Settings(path);

                    if (settings.maximumMatches > 0)
                    {
                        foreach (var file in files)
                        {
                            if (!IsVideoExtension(file))
                            {
                                continue;
                            }
                            Episode ep = new Episode(file);

                            ep.DetectionPending = true;
                            db.Insert(ep);
                        }
                    }
                }

                var directories = Directory.EnumerateDirectories(path);
                foreach (var dir in directories)
                {
                    InvalidateDirectory(dir);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("CheckDirectory Exception: " + e.ToString());
            }

        }


        public void CheckForPlexChangedDirectories()
        {
            Dictionary<string, DateTime> data = new Dictionary<string, DateTime>();

            do
            {
                data = plexDB.GetRecentlyModifiedDirectories(db.lastPlexDirectoryChanged);

                Console.WriteLine($"Found changed directories: {data.Count} \n");

                foreach (var item in data)
                {
                    if (item.Key.Length <= 1) // ignore root directory
                    {
                        continue;
                    }

                    string path = Program.getFullDirectory(item.Key);
                    DateTime time = item.Value;

                    Episode ep = new Episode();
                    ep.fullDirPath = Path.GetDirectoryName(Program.PathCombine(path, "dummy"));

                    var di = new DirectoryInfo(ep.fullDirPath);
                    if (di.Exists)
                    {

                        ep.dir = Program.getRelativeDirectory(Program.PathCombine(path, "dummy"));

                        Settings settings = new Settings(ep.fullDirPath);

                        bool doBlackframes = settings.detectBlackframes && (!settings.blackframeOnlyMovies || ep.isMovie);
                        bool doMatching = (settings.useAudio || settings.useVideo) && settings.maximumMatches > 0;

                        if (doMatching)
                        {
                            ep.DetectionPending = true;
                        }
                        if (settings.detectSilenceAfterCredits)
                        {
                            ep.SilenceDetectionPending = true;
                        }
                        if (doBlackframes)
                        {
                            ep.BlackframeDetectionPending = true;
                        }

                        if (doMatching || settings.detectSilenceAfterCredits || doBlackframes)
                        {
                            Console.WriteLine("Directory needs scanning: " + path);

                            ep.fullPath = ep.fullDirPath;
                            ep.id = ep.dir;

                            ep.name = Path.GetFileName(path);

                            ep.Exists = true;
                            ep.LastWriteTimeUtcOnDisk = di.LastWriteTimeUtc;
                            ep.FileSizeOnDisk = 0;

                            db.Insert(ep);

                            ignoreDirectories.RemoveAll(x => x == ep.dir);
                        }
                    }

                    db.lastPlexDirectoryChanged = time;
                }
            } while (data.Any());
        }

        public void CheckForNewPlexIntros()
        {
            List<PlexDB.RecentIntroData> data = new List<PlexDB.RecentIntroData>();
            Settings settings = null;
            bool firstTime = false;

            if (db.lastPlexIntroAdded == DateTime.MinValue)
            {
                firstTime = true;
            }

            do
            {
                DateTime originalLastPlexIntroAdded = db.lastPlexIntroAdded;
                //data = plexDB.GetRecentPlexIntroTimings(db.lastPlexIntroAdded);
                data = plexDB.GetRecentPlexIntroTimingsSingleQuery(originalLastPlexIntroAdded);

                if (data == null)
                {
                    if (firstTime)
                    {
                        Console.WriteLine("Data null!");
                    }
                    return;
                }

                firstTime = false;

                Console.WriteLine($"Found new plex intros: {data.Count} \n");

                int i = 0;
                int count = data.Count;



                foreach (var item in data)
                {
                    if (item.episode == null)
                    {
                        Console.WriteLine("Episode null: " + item.metadata_item_id);
                        db.lastPlexIntroAdded = item.created;
                        continue;
                    }

                    if (!item.episode.Exists)
                    {
                        i++;
                    }

                    if (!CheckSingleEpisode(item.episode, false))
                    {
                        db.lastPlexIntroAdded = item.created;
                        continue;
                    }


                    if (settings == null || settings.currentlyLoadedSettingsPath != item.episode.fullDirPath)
                    {
                        settings = new Settings(item.episode.fullDirPath);
                    }


                    if (CheckIfFileNeedsScanning(item.episode, settings, true))
                    {

                        bool doMatching = (settings.useAudio || settings.useVideo) && settings.maximumMatches > 0 && settings.recheckUndetectedOnStartup && item.episode.needsScanning;
                        bool doSilence = settings.recheckSilenceOnStartup && settings.detectSilenceAfterCredits && item.episode.needsSilenceScanning;
                        bool doBlackframes = settings.detectBlackframes && (!settings.blackframeOnlyMovies || item.episode.isMovie) && settings.recheckBlackframesOnStartup && item.episode.needsBlackframeScanning;

                        if (doMatching)
                        {
                            item.episode.DetectionPending = true;
                        }
                        if (doSilence)
                        {
                            item.episode.SilenceDetectionPending = true;
                        }
                        if (doBlackframes)
                        {
                            item.episode.BlackframeDetectionPending = true;
                        }

                        if (doMatching || doSilence || doBlackframes)
                        {
                            Console.WriteLine("Episode needs scanning: " + item.episode.id);

                            db.Insert(item.episode);

                            db.DeleteEpisodePlexTimings(item.episode);
                            db.InsertTiming(item.episode, item.segment, true);

                            ignoreDirectories.RemoveAll(x => x == item.episode.dir);
                        }

                    }
                    else
                    {
                        //Console.WriteLine($"Updating timings for episode: {item.episode.fullPath}");

                        var items = db.GetNonPlexTimings(item.episode, true);

                        if (items != null)
                        {
                            InsertTimings(item.episode, settings, false, true);
                        }
                    }

                    db.lastPlexIntroAdded = item.created;
                }

                if (firstTime && i == count)
                {
                    Console.WriteLine("No episodes could be found! Please check your media path mappings");

                    db.lastPlexIntroAdded = originalLastPlexIntroAdded;
                    return;
                }
            } while (data.Any());
        }
    }
}
