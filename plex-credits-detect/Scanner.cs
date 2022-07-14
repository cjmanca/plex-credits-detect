using plexCreditsDetect.Database;
using SoundFingerprinting;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB;
using SoundFingerprinting.InMemory;
using SoundFingerprinting.Query;
using SoundFingerprinting.LCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.Configuration.Frames;

namespace plexCreditsDetect
{
    public class Scanner
    {
        internal static PlexDB plexDB = new PlexDB();
        internal static IFingerprintDatabase db = null;
        internal static SoundFingerprinting.Emy.FFmpegAudioService audioService = null;

        private static readonly string[] allowedExtensions = new string[] { ".3g2", ".3gp", ".amv", ".asf", ".avi", ".flv", ".f4v", ".f4p", ".f4a", ".f4b", ".m4v", ".mkv", ".mov", ".qt", ".mp4", ".m4p", ".mpg", ".mp2", ".mpeg", ".mpe", ".mpv", ".m2v", ".mts", ".m2ts", ".ts", ".ogv", ".ogg", ".rm", ".rmvb", ".viv", ".vob", ".webm", ".wmv" };


        internal bool CheckIfFileNeedsScanning(Episode ep)
        {
            if (!ep.Exists) // can't scan something that doesn't exist
            {
                return false;
            }

            if (!IsVideoExtension(ep))
            {
                return false;
            }

            var info = db.GetEpisode(ep.id);

            if (info == null)
            {
                return true;
            }

            if (ep.FileSize != info.FileSize)
            {
                return true;
            }

            return false;
        }

        public void FingerprintFile(string path, Settings settings = null)
        {
            Episode ep = new Episode(path);
            FingerprintFile(ep, settings);
        }

        internal void FingerprintFile(Episode ep, Settings settings = null)
        {
            if (settings == null)
            {
                settings = new Settings(ep.fullPath);
            }

            Console.WriteLine("Fingerprinting: " + ep.id);

            AVHashes hashes = db.GetTrackHash(ep.id);

            if (hashes == null || hashes.IsEmpty || CheckIfFileNeedsScanning(ep))
            {
                try
                {
                    // create hashed fingerprint
                    var hashedFingerprint = FingerprintCommandBuilder.Instance
                                                .BuildFingerprintCommand()
                                                .From(ep.fullPath, MediaType.Audio)
                                                .WithFingerprintConfig(config =>
                                                {
                                                // audio configuration
                                                    config.Audio = new DefaultFingerprintConfiguration();
                                                    return config;
                                                })
                                                .UsingServices(audioService)
                                                .Hash()
                                                .Result;

                    // store hashes in the database for later retrieval
                    db.InsertHash(ep, hashedFingerprint);
                }
                catch (Exception e)
                {
                    Console.WriteLine("FingerprintFile Exception: ", e.Message);
                }
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
                    FingerprintFile(ep, settings);
                }
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
                        Console.WriteLine("\n");
                        Console.WriteLine("Matching: " + ep.id);

                        var result = QueryCommandBuilder.Instance
                            .BuildQueryCommand()
                            .From(ep.fullPath, MediaType.Audio)
                            .WithQueryConfig(config =>
                            {
                                config.Audio.FingerprintConfiguration = new DefaultFingerprintConfiguration();
                                config.Audio.MaxTracksToReturn = 9999;
                                config.Audio.ThresholdVotes = settings.audioAccuracy;
                                config.Audio.PermittedGap = settings.PermittedGap;
                                config.Audio.AllowMultipleMatchesOfTheSameTrackInQuery = true;
                                config.Audio.YesMetaFieldsFilters = new Dictionary<string, string> { { "dir", ep.dir } };
                                config.Audio.NoMetaFieldsFilters = new Dictionary<string, string> { { "name", ep.name } };
                                return config;
                            })
                            .UsingServices(db.GetModelService(), audioService)
                            .Query()
                            .Result;

                        List<ResultEntry> sortedRes;

                        if (result != null)
                        {
                            sortedRes = result.Audio.ResultEntries.ToList();
                            sortedRes.Sort((a, b) => a.QueryMatchStartsAt.CompareTo(b.QueryMatchStartsAt));

                            foreach (var entry in sortedRes)
                            {
                                if (entry.TrackCoverageWithPermittedGapsLength >= settings.minimumMatchSeconds)
                                {
                                    ep.segments.AddSegment(new Episode.Segment(entry.QueryMatchStartsAt, entry.QueryMatchStartsAt + entry.TrackCoverageWithPermittedGapsLength), settings.PermittedGap);
                                }
                            }
                        }


                        //OutputMatches(result?.Audio.ResultEntries, MediaType.Audio);
                        OutputSegments(ep, settings);

                        // TODO: consider other segments other than just intro/credits?
                        // Considerations: sometimes it matches portions of the actual episode that uses "mood music" with no dialogue/sfx
                        Episode.Segment credits = ep.segments.allSegments.Last();
                        Episode.Segment intro = ep.segments.allSegments.Where(x => x.end < credits.start).MaxBy(x => x.duration);


                        plexDB.LoadDatabase(settings.PlexDatabasePath);

                        // TODO: insert the matches into the Plex database
                        long metaID = plexDB.GetMetadataID(ep);
                        plexDB.DeleteExistingIntros(metaID);

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


                        ep.DetectionPending = false;
                        db.Insert(ep);


                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ScanDirectory Exception: ", e.Message);
                }
                finally
                {
                    plexDB.CloseDatabase();
                }
            }

        }

        public void OutputSegments(Episode ep, Settings settings)
        {
            foreach (var resultEntry in ep.segments.allSegments)
            {
                if (resultEntry.duration >= settings.minimumMatchSeconds)
                {
                    Console.WriteLine($"Match from {resultEntry.start:0.00} to {resultEntry.end:0.00}. Duration: {resultEntry.duration:0.00}.");

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

                            if (CheckIfFileNeedsScanning(ep))
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
    }
}
