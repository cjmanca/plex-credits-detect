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

        private static readonly string[] allowedExtensions = new string[] { ".3g2", ".3gp", ".amv", ".asf", ".avi", ".flv", ".f4v", ".f4p", ".f4a", ".f4b", ".m4v", ".mkv", ".mov", ".qt", ".mp4", ".m4p", ".mpg", ".mp2", ".mpeg", ".mpe", ".mpv", ".m2v", ".mts", ".m2ts", ".ts", ".ogv", ".ogg", ".rm", ".rmvb", ".viv", ".vob", ".webm", ".wmv" };


        internal bool CheckIfFileNeedsScanning(Episode ep, Settings settings)
        {
            if (!ep.Exists) // can't scan something that doesn't exist
            {
                return false;
            }

            if (!IsVideoExtension(ep))
            {
                return false;
            }

            if (settings.forceRedetect)
            {
                return true;
            }

            var info = db.GetEpisode(ep.id);

            if (info == null)
            {
                return false;
            }

            if (ep.FileSize != info.FileSize)
            {
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
                    return true;
                }
                int intros = timings.Count(x => x.isCredits == false);
                int credits = timings.Count(x => x.isCredits == true);

                if (intros < settings.introMatchCount)
                {
                    return true;
                }
                if (credits < settings.creditsMatchCount)
                {
                    return true;
                }
            }

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

        public void FingerprintFile(string path, Episode.Segment plexTimings, bool isCredits, Settings settings = null)
        {
            Episode ep = new Episode(path);
            FingerprintFile(ep, plexTimings, isCredits, settings);
        }

        internal void FingerprintFile(Episode ep, Episode.Segment plexTimings, bool isCredits, Settings settings = null)
        {
            if (settings == null)
            {
                settings = new Settings(ep.fullPath);
            }

            AVHashes hashes = db.GetTrackHash(ep.id, isCredits);

            if (hashes == null || hashes.IsEmpty || CheckIfFileNeedsScanning(ep, settings))
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

                    if (!isCredits && start < plexTimings.end + 30)
                    {
                        start = plexTimings.end + 30;
                    }

                    var end = Math.Min(start + duration, ep.duration);





                    Console.WriteLine($"Fingerprinting: {ep.id} ({TimeSpan.FromSeconds(start):g} - {TimeSpan.FromSeconds(end):g})");

                    string creditSnippet = isCredits ? "credits" : "intro";
                    string tempFile = Path.Combine(settings.TempDirectoryPath, $"{creditSnippet}.{ep.name}");

                    if (!ffmpeghelper.CutVideo(start, end, ep.fullPath, tempFile))
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
                    db.InsertHash(ep, hashedFingerprint, avtype, isCredits, start);
                }
                catch (Exception e)
                {
                    Console.WriteLine("FingerprintFile Exception: ", e.Message);
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

            db.SetupNewScan();
            Episode ep = null;
            bool firstEntry = true;
            long metaID = 0;
            Episode.Segment plexTimings;

            Dictionary<string, long> metaIDs = new Dictionary<string, long>();
            Dictionary<string, Episode.Segment> allPlexTimings = new Dictionary<string, Episode.Segment>();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var files = Directory.EnumerateFiles(path);
            foreach (var file in files)
            {
                if (!IsVideoExtension(file))
                {
                    continue;
                }
                ep = new Episode(file);
                if (ep.Exists)
                {
                    var info = db.GetEpisode(ep.id);

                    if (info == null)
                    {
                        continue;
                    }

                    if (firstEntry)
                    {
                        firstEntry = false;
                        Console.WriteLine("");
                    }

                    metaID = plexDB.GetMetadataID(ep);
                    metaIDs[ep.id] = metaID;
                    plexTimings = plexDB.GetPlexIntroTimings(metaID);
                    allPlexTimings[ep.id] = plexTimings;

                    if (settings.introMatchCount > 0)
                    {
                        FingerprintFile(ep, plexTimings, false, settings);
                    }
                    if (settings.creditsMatchCount > 0)
                    {
                        FingerprintFile(ep, plexTimings, true, settings);
                    }
                }
            }

            if (!firstEntry)
            {
                Console.WriteLine("");
            }

            foreach (var file in files)
            {
                if (!IsVideoExtension(file))
                {
                    continue;
                }
                try
                {
                    ep = new Episode(file);
                    if (ep.Exists)
                    {
                        var info = db.GetEpisode(ep.id);

                        if (info == null)
                        {
                            continue;
                        }

                        if (metaIDs.ContainsKey(ep.id))
                        {
                            metaID = metaIDs[ep.id];
                        }
                        else
                        {
                            metaID = plexDB.GetMetadataID(ep);
                        }

                        if (allPlexTimings.ContainsKey(ep.id))
                        {
                            plexTimings = allPlexTimings[ep.id];
                        }
                        else
                        {
                            plexTimings = plexDB.GetPlexIntroTimings(metaID);
                        }

                        if (!CheckIfFileNeedsScanning(ep, settings))
                        {
                            continue;
                        }

                        Console.WriteLine("");

                        MediaType avtype = 0;

                        if (settings.useAudio)
                        {
                            avtype |= MediaType.Audio;
                        }
                        if (settings.useVideo)
                        {
                            avtype |= MediaType.Video;
                        }


                        Console.WriteLine($"Matching: {ep.id}");

                        Episode.Segments audioSegments = new Episode.Segments();
                        Episode.Segments videoSegments = new Episode.Segments();
                        Episode.Segments audioSegmentsCredits = new Episode.Segments();
                        Episode.Segments videoSegmentsCredits = new Episode.Segments();

                        if (settings.introMatchCount > 0)
                        {
                            DoSingleQuery(settings, false, ep, avtype, plexTimings, audioSegments, videoSegments);
                        }
                        if (settings.creditsMatchCount > 0)
                        {
                            DoSingleQuery(settings, true, ep, avtype, plexTimings, audioSegmentsCredits, videoSegmentsCredits);
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
                                if (validatedSegments.allSegments.Count > 0)
                                {
                                    ep.segments.AddSegment(validatedSegments.allSegments[i]);
                                }
                            }
                            for (int i = 0; i < settings.creditsMatchCount; i++)
                            {
                                if (validatedSegmentsCredits.allSegments.Count > 0)
                                {
                                    ep.segments.AddSegment(validatedSegmentsCredits.allSegments[i]);
                                }
                            }

                            ep.segments.allSegments.Sort((a, b) => a.start.CompareTo(b.start));

                            //OutputMatches(result?.Audio.ResultEntries, MediaType.Audio);
                            OutputSegments("Match", ep.segments, settings);


                            db.DeleteEpisodeTimings(ep.id);
                            plexDB.DeleteExistingIntros(metaID);

                            if (plexTimings != null)
                            {
                                db.InsertTiming(ep, plexTimings, true);
                            }


                            for (int i = 0; i < ep.segments.allSegments.Count; i++)
                            {
                                ep.segments.allSegments[i].start -= settings.shiftSegmentBySeconds;
                                ep.segments.allSegments[i].end -= settings.shiftSegmentBySeconds;

                                db.InsertTiming(ep, ep.segments.allSegments[i], false);
                                plexDB.Insert(metaID, ep.segments.allSegments[i], i + 1);
                            }

                            Console.WriteLine("");

                            /*
                            int index = 0;

                            if (intro != null && intro.duration >= settings.minimumMatchSeconds)
                            {
                                plexDB.Insert(metaID, intro, index);
                                index++;
                            }
                            if (credits != null && credits.duration >= settings.minimumMatchSeconds)
                            {
                                plexDB.Insert(metaID, credits, index);
                                index++;
                            }
                            */
                        }



                        ep.DetectionPending = false;
                        db.Insert(ep);


                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ScanDirectory Exception: ", e.Message);
                }
            }

            CleanTemp();

            Console.WriteLine($"Detection took {sw.Elapsed:g}");

        }


        void DoSingleQuery(Settings settings, bool isCredits, Episode ep, MediaType avtype, Episode.Segment plexTimings, Episode.Segments audioSegments, Episode.Segments videoSegments)
        {
            var duration = GetSearchDuration(ep, settings, isCredits);
            var start = GetSearchStartAt(ep, settings, isCredits);

            if (!isCredits && start < plexTimings.end + 30)
            {
                start = plexTimings.end + 30;
            }

            var end = Math.Min(start + duration, ep.duration);


            string creditSnippet = isCredits ? "credits" : "intro";
            string tempFile = Path.Combine(settings.TempDirectoryPath, $"{creditSnippet}.{ep.name}");

            if (!File.Exists(tempFile))
            {
                if (!ffmpeghelper.CutVideo(start, end, ep.fullPath, tempFile))
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

                            if (CheckIfFileNeedsScanning(ep, settings))
                            {
                                ep.DetectionPending = true;
                                db.Insert(ep);
                            }
                        }
                    }
                }

                var directories = Directory.EnumerateDirectories(path);
                foreach (var dir in directories)
                {
                    CheckDirectory(dir);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("CheckDirectory Exception: ", e.Message);
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
                Console.WriteLine("CheckDirectory Exception: ", e.Message);
            }

        }


        public void CheckForNewPlexIntros()
        {

            var data = plexDB.GetRecentPlexIntroTimings(db.lastPlexIntroAdded);

            if (data == null)
            {
                return;
            }

            Settings settings = null;

            foreach (var item in data)
            {
                if (settings == null || settings.currentlyLoadedSettingsPath != item.episode.fullDirPath)
                {
                    settings = new Settings(item.episode.fullDirPath);
                }


                if (CheckIfFileNeedsScanning(item.episode, settings))
                {
                    item.episode.DetectionPending = true;

                    db.Insert(item.episode);

                    db.DeleteEpisodePlexTimings(item.episode.id);
                    db.InsertTiming(item.episode, item.segment, true);
                }
                else
                {
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

        }
    }
}
