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

        internal bool CheckIfFileNeedsScanning(Episode ep, Settings settings, bool insertCheck = false)
        {
            if (!ep.Exists) // can't scan something that doesn't exist
            {
                ep.needsScanning = false;
                return false;
            }
            
            if (!IsVideoExtension(ep))
            {
                ep.needsScanning = false;
                return false;
            }

            if (settings.forceRedetect)
            {
                ep.needsScanning = true;
                return true;
            }

            if (!ep.InPrivateDB)
            {
                return insertCheck;
            }

            if (ep.FileSizeOnDisk != ep.FileSizeInDB)
            {
                ep.needsScanning = true;
                return true;
            }


            if (settings == null)
            {
                settings = new Settings(ep.fullDirPath);
            }
            if (settings.maximumMatches > 0)
            {
                var timings = db.GetNonPlexTimings(ep);

                if (timings == null)
                {
                    ep.needsScanning = true;
                    return true;
                }
                int intros = timings.Count(x => x.isCredits == false);
                int credits = timings.Count(x => x.isCredits == true);

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
            return false;
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

        internal void FingerprintFile(Episode ep, bool isCredits, Settings settings = null, Episode.Segment seg = null, int partNum = -1)
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

                    var duration = GetSearchDuration(ep, settings, isCredits);
                    var start = GetSearchStartAt(ep, settings, isCredits);

                    double plexEnd = 0;

                    if (ep.plexTimings != null)
                    {
                        plexEnd = ep.plexTimings.end + 30;
                    }

                    if (!isCredits && start < plexEnd)
                    {
                        start = plexEnd;
                    }

                    var end = Math.Min(start + duration, ep.duration);

                    if (!isCredits && end > ep.duration * settings.introEnd)
                    {
                        end = ep.duration * settings.introEnd;
                    }

                    duration = end - start;

                    if (seg != null)
                    {
                        start = seg.start - 30;
                        end = seg.end + 30;
                        duration = end - start;
                    }

                    if (end > ep.duration)
                    {
                        end = ep.duration;
                    }


                    Console.WriteLine($"Fingerprinting: {ep.id} ({TimeSpan.FromSeconds(start):g} - {TimeSpan.FromSeconds(end):g})");

                    string creditSnippet = isCredits ? "credits" : "intro";
                    string tempFile = "";

                    if (partNum >= 0)
                    {
                        tempFile = Program.PathCombine(settings.TempDirectoryPath, $"{creditSnippet}.{partNum}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");
                    }
                    else
                    {
                        tempFile = Program.PathCombine(settings.TempDirectoryPath, $"{creditSnippet}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");
                    }

                    if (!ffmpeghelper.CutVideo(start, end, ep.fullPath, tempFile, settings.useVideo, settings.useAudio))
                    {
                        return;
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
                    db.InsertHash(ep, hashedFingerprint, avtype, isCredits, start, partNum);
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



        Episode CheckSingleEpisode(Episode ep, Settings settings)
        {
            if (!ep.Exists)
            {
                return null;
            }

            if (!IsVideoExtension(ep.fullPath))
            {
                return null;
            }

            if (ep.meta_id < 0)
            {
                Console.WriteLine($"{ep.id}: Metadata not found in plex db. Removing.");
                if (ep.InPrivateDB)
                {
                    db.DeleteEpisodeTimings(ep);
                    db.DeleteEpisode(ep);
                }
                return null;
            }

            if (!ep.InPrivateDB)
            {
                // metadata was found in plex db, so update our db with the episode
                Console.WriteLine($"{ep.id}: Adding to local db.");
                ep.DetectionPending = true;
                db.Insert(ep);
            }

            ep.passed = true;
            return ep;
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

                if (ep.FileSizeInDB != ep.FileSizeOnDisk)
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


                Console.WriteLine("");
                Console.WriteLine($"Matching: {ep.id}");

                Episode.Segments audioSegments = new Episode.Segments();
                Episode.Segments videoSegments = new Episode.Segments();
                Episode.Segments audioSegmentsCredits = new Episode.Segments();
                Episode.Segments videoSegmentsCredits = new Episode.Segments();

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

                    Episode.Segments validatedSegments;
                    Episode.Segments validatedSegmentsCredits;

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

                    InsertTimings(ep, settings);

                    return validatedSegments.allSegments.Count() + validatedSegmentsCredits.allSegments.Count();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("DetectSingleEpisode Exception: " + e.ToString());
            }

            return 0;
        }


        void InsertTimings(Episode ep, Settings settings)
        {
            try
            {
                //OutputMatches(result?.Audio.ResultEntries, MediaType.Audio);
                OutputSegments("Match", ep.segments, settings);

                if (ep.plexTimings != null)
                {
                    db.InsertTiming(ep, ep.plexTimings, true);
                }

                db.DeleteEpisodeTimings(ep);
                plexDB.DeleteExistingIntros(ep.meta_id);


                for (int i = 0; i < ep.segments.allSegments.Count; i++)
                {
                    ep.segments.allSegments[i].start -= settings.shiftSegmentBySeconds;
                    ep.segments.allSegments[i].end -= settings.shiftSegmentBySeconds;

                    db.InsertTiming(ep, ep.segments.allSegments[i], false);
                    plexDB.Insert(ep.meta_id, ep.segments.allSegments[i], i + 1);
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
            if (!Directory.Exists(path))
            {
                return;
            }

            if (settings == null)
            {
                settings = new Settings(path);
            }

            if (settings.maximumMatches <= 0)
            {
                return;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();

            processed = 0;

            db.SetupNewScan();

            Episode ep = null;

            var files = Directory.EnumerateFiles(path).ToList();

            string relDir = Program.getRelativeDirectory(Program.PathCombine(path, "dud"));

            List<Episode> allEpisodes;

            List<Episode> bestIntros = new List<Episode>();
            List<Episode.Segment> bestIntroSegments = new List<Episode.Segment>();
            List<Episode> bestCredits = new List<Episode>();
            List<Episode.Segment> bestCreditSegments = new List<Episode.Segment>();
            List<Episode> randomEpisodes = new List<Episode>();

            int totalEpisodesWithAllIntrosDetected = 0;
            int totalEpisodesWithAllCreditsDetected = 0;

            bool tryQuickDetect = false;
            int remainingToFind = 0;


            
            tryQuickDetect = true;
            remainingToFind = 0;

            allEpisodes = db.GetNonPlexTimingsForDir(relDir);

            foreach (var item in files)
            {
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
                bestIntroSegments.Add(new Episode.Segment(0, 0));
            }
            for (int i = 0; i < settings.creditsMatchCount; i++)
            {
                bestCredits.Add(null);
                bestCreditSegments.Add(new Episode.Segment(0, 0));
            }

            foreach (var item in allEpisodes)
            {
                ep = CheckSingleEpisode(item, settings);
                if (ep == null)
                {
                    continue;
                }

                CheckIfFileNeedsScanning(ep, settings);
            }

            allEpisodes.RemoveAll(x => !x.passed);

            int totalNeedsScanning = allEpisodes.Count(x => x.needsScanning);
            int totalToFingerprint = allEpisodes.Count();

            if (totalNeedsScanning <= 0)
            {
                CleanTemp();
                db.ClearDetectionPendingForDirectory(relDir);
                return;
            }

            int numberToQuickTry = 5;

            if (totalNeedsScanning > totalToFingerprint / 4)
            {
                tryQuickDetect = false;
            }

            allEpisodes.Sort((a, b) => a.name.CompareTo(b.name) );

            if (tryQuickDetect)
            {
                int epCount = 0;

                foreach (var item in allEpisodes)
                {
                    if (randomEpisodes.Count() < (double)epCount / ((double)totalToFingerprint / (double)numberToQuickTry))
                    {
                        randomEpisodes.Add(item);
                    }

                    int introCount = item.segments.allSegments.Count(x => !x.isCredits);
                    int creditCount = item.segments.allSegments.Count(x => x.isCredits);

                    if (introCount == settings.introMatchCount)
                    {
                        totalEpisodesWithAllIntrosDetected++;
                        int count = 0;
                        foreach (var seg in item.segments.allSegments)
                        {
                            if (!seg.isCredits)
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
                            if (seg.isCredits)
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
                    ep = item;
                    int detected = DetectSingleEpisode(ep, settings);

                    if (ep.segments.allSegments.Count() >= settings.maximumMatches)
                    {
                        if (ep.DetectionPending)
                        {
                            ep.DetectionPending = false;
                            db.Insert(ep);
                        }
                    }
                    else
                    {
                        remainingToFind++;
                    }
                }
            }
            

            if (!tryQuickDetect || remainingToFind > 0)
            {
                //db.SetupNewScan();

                DoFullFingerprint(allEpisodes, settings);

                foreach (var item in allEpisodes)
                {
                    ep = item;
                    int detected = DetectSingleEpisode(ep, settings);

                    if (ep.DetectionPending)
                    {
                        ep.DetectionPending = false;
                        db.Insert(ep);
                    }

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

            CleanTemp();
            //plexDB.EndActivity();

            db.ClearDetectionPendingForDirectory(relDir);

            Console.WriteLine("");
            Console.WriteLine($"Detection took {sw.Elapsed:g}");
        }

        
        void DoSingleQuery(Settings settings, bool isCredits, Episode ep, MediaType avtype, Episode.Segments audioSegments, Episode.Segments videoSegments)
        {
            var duration = GetSearchDuration(ep, settings, isCredits);
            var start = GetSearchStartAt(ep, settings, isCredits);

            double plexEnd = 0;

            if (ep.plexTimings != null)
            {
                plexEnd = ep.plexTimings.end + 30;
            }

            if (!isCredits && start < plexEnd)
            {
                start = plexEnd;
            }

            var end = Math.Min(start + duration, ep.duration);


            string creditSnippet = isCredits ? "credits" : "intro";
            string tempFile = Program.PathCombine(settings.TempDirectoryPath, $"{creditSnippet}.{Path.GetFileNameWithoutExtension(ep.name)}.mkv");

            if (!File.Exists(tempFile))
            {
                if (!ffmpeghelper.CutVideo(start, end, ep.fullPath, tempFile, settings.useVideo, settings.useAudio))
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
                        Episode.Segment seg = new Episode.Segment(entry.QueryMatchStartsAt + start, entry.QueryMatchStartsAt + start + entry.TrackCoverageWithPermittedGapsLength);

                        seg.isCredits = isCredits;

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
                        Episode.Segment seg = new Episode.Segment(entry.QueryMatchStartsAt + start, entry.QueryMatchStartsAt + start + entry.TrackCoverageWithPermittedGapsLength);

                        seg.isCredits = isCredits;

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

        public void CleanTemp()
        {
            var files = Directory.EnumerateFiles(Program.settings.TempDirectoryPath);
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        public bool ConstrainSegment(Episode.Segment seg, bool isCredits, Episode.Segment introPermitted, Episode.Segment creditsPermitted)
        {
            var introOverlap = seg.Overlap(introPermitted);
            var creditsOverlap = seg.Overlap(creditsPermitted);

            if (introOverlap != null && !isCredits)
            {
                seg.start = introOverlap.start;
                seg.end = introOverlap.end;
                return true;
            }
            if (creditsOverlap != null && isCredits)
            {
                seg.start = creditsOverlap.start;
                seg.end = creditsOverlap.end;
                return true;
            }

            return false;
        }

        public void OutputSegments(string pre, Episode.Segments segs, Settings settings)
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

                    if (settings.maximumMatches > 0 && settings.recheckUndetectedOnStartup)
                    {
                        foreach (var file in files)
                        {
                            if (!IsVideoExtension(file))
                            {
                                continue;
                            }
                            Episode ep = new Episode(file);

                            if (CheckIfFileNeedsScanning(ep, settings, true))
                            {
                                Console.WriteLine("Episode needs scanning: " + ep.id);

                                ep.DetectionPending = true;
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
                //data = plexDB.GetRecentPlexIntroTimings(db.lastPlexIntroAdded);
                data = plexDB.GetRecentPlexIntroTimingsSingleQuery(db.lastPlexIntroAdded);

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

                foreach (var item in data)
                {
                    if (item.episode == null)
                    {
                        Console.WriteLine("Episode null: " + item.metadata_item_id);
                        continue;
                    }

                    if (settings == null || settings.currentlyLoadedSettingsPath != item.episode.fullDirPath)
                    {
                        settings = new Settings(item.episode.fullDirPath);
                    }

                    if (CheckIfFileNeedsScanning(item.episode, settings, true))
                    {
                        Console.WriteLine("Episode needs scanning: " + item.episode.id);

                        item.episode.DetectionPending = true;

                        db.Insert(item.episode);

                        db.DeleteEpisodePlexTimings(item.episode);
                        db.InsertTiming(item.episode, item.segment, true);

                        ignoreDirectories.RemoveAll(x => x == item.episode.dir);
                    }
                    else
                    {
                        //Console.WriteLine($"Updating timings for episode: {item.episode.fullPath}");

                        var items = db.GetNonPlexTimings(item.episode);

                        if (items != null)
                        {
                            plexDB.DeleteExistingIntros(item.metadata_item_id);

                            for (int i = 0; i < items.Count; i++)
                            {
                                plexDB.Insert(item.metadata_item_id, items[i], i + 1);
                            }
                        }
                    }

                    db.lastPlexIntroAdded = item.created;
                }
            } while (data.Any());
        }
    }
}
