using plexCreditsDetect.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect
{
    public class Episode
    {

        // Saved in database
        public string id { get; set; }
        public string name { get; set; }
        public string dir { get; set; }

        bool? _BlackframeDetectionPending = null;
        public bool BlackframeDetectionPending
        {
            get
            {
                if (!_BlackframeDetectionPending.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_BlackframeDetectionPending.HasValue)
                    {
                        _BlackframeDetectionPending = false;
                    }
                }
                return _BlackframeDetectionPending.Value;
            }
            set
            {
                _BlackframeDetectionPending = value;
            }
        }

        bool? _SilenceDetectionPending = null;
        public bool SilenceDetectionPending
        {
            get
            {
                if (!_SilenceDetectionPending.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_SilenceDetectionPending.HasValue)
                    {
                        _SilenceDetectionPending = false;
                    }
                }
                return _SilenceDetectionPending.Value;
            }
            set
            {
                _SilenceDetectionPending = value;
            }
        }

        bool? _DetectionPending = null;
        public bool DetectionPending
        {
            get
            {
                if (!_DetectionPending.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_DetectionPending.HasValue)
                    {
                        _DetectionPending = false;
                    }
                }
                return _DetectionPending.Value;
            }
            set
            {
                _DetectionPending = value;
            }
        }

        bool? _SilenceDetectionDone = null;
        public bool SilenceDetectionDone
        {
            get
            {
                if (!_SilenceDetectionDone.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_SilenceDetectionDone.HasValue)
                    {
                        _SilenceDetectionDone = false;
                    }
                }
                return _SilenceDetectionDone.Value;
            }
            set
            {
                _SilenceDetectionDone = value;
            }
        }

        bool? _BlackframeDetectionDone = null;
        public bool BlackframeDetectionDone
        {
            get
            {
                if (!_BlackframeDetectionDone.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_BlackframeDetectionDone.HasValue)
                    {
                        _BlackframeDetectionDone = false;
                    }
                }
                return _BlackframeDetectionDone.Value;
            }
            set
            {
                _BlackframeDetectionDone = value;
            }
        }

        DateTime? _LastWriteTimeUtcInDB = null;
        public DateTime LastWriteTimeUtcInDB
        {
            get
            {
                if (!_LastWriteTimeUtcInDB.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_LastWriteTimeUtcInDB.HasValue)
                    {
                        _LastWriteTimeUtcInDB = DateTime.MinValue;
                    }
                }
                return _LastWriteTimeUtcInDB.Value;
            }
            set
            {
                _LastWriteTimeUtcInDB = value;
            }
        }

        long? _FileSizeInDB = null;
        public long FileSizeInDB
        {
            get
            {
                if (!_FileSizeInDB.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_FileSizeInDB.HasValue)
                    {
                        _FileSizeInDB = -1;
                    }
                }
                return _FileSizeInDB.Value;
            }
            set
            {
                _FileSizeInDB = value;
            }
        }




        // Extra info
        public bool passed = false;
        public bool needsScanning = false;
        public bool needsSilenceScanning = false;
        public bool needsBlackframeScanning = false;
        bool? _isMovie = null;
        public bool isMovie
        {
            get
            {
                if (!_isMovie.HasValue)
                {
                    _isMovie = false;
                    foreach (var item in Scanner.plexDB.RootDirectories)
                    {
                        if (fullDirPath.Contains(Program.plexBasePathToLocalBasePath(item.Value.path)) && item.Value.section_type == 1)
                        {
                            _isMovie = true;
                        }
                    }
                }
                return _isMovie.Value;
            }
        }

        public bool Exists { get; set; } = false;
        public string fullPath { get; set; }
        public string fullDirPath { get; set; }
        public Segments segments { get; set; } = new Segments();
        public long FileSizeOnDisk { get; set; }
        public DateTime LastWriteTimeUtcOnDisk { get; set; }


        bool? _InPlexDB = null;
        public bool InPlexDB
        {
            get
            {
                if (!_InPlexDB.HasValue)
                {
                    _InPlexDB = meta_id >= 0;
                }
                return _InPlexDB.Value;
            }
            set
            {
                _InPlexDB = value;
            }
        }

        bool? _InPrivateDB = null;
        public bool InPrivateDB
        {
            get
            {
                if (!_InPrivateDB.HasValue)
                {
                    Scanner.db.GetEpisode(this);
                    if (!_InPrivateDB.HasValue)
                    {
                        _InPrivateDB = false;
                    }
                }
                return _InPrivateDB.Value;
            }
            set
            {
                _InPrivateDB = value;
            }
        }
        long? _meta_id = null;
        public long meta_id
        {
            get
            {
                if (!_meta_id.HasValue)
                {
                    _meta_id = Scanner.plexDB.GetMetadataID(this);
                }
                return _meta_id.Value;
            }
            set
            {
                _meta_id = value;
            }
        }

        bool checkedForPlexTimings = false;
        Segment _plexTimings = null;
        public Segment plexTimings
        {
            get
            {
                if (!checkedForPlexTimings)
                {
                    _plexTimings = Scanner.plexDB.GetPlexIntroTimings(meta_id);
                    checkedForPlexTimings = true;
                }
                return _plexTimings;
            }
            set
            {
                _plexTimings = value;
                if (_plexTimings != null)
                {
                    checkedForPlexTimings = true;
                }
            }
        }

        double? _duration = null;
        public double duration
        {
            get
            {
                if (!_duration.HasValue)
                {
                    _duration = ffmpeghelper.GetDuration(fullPath);
                }

                return _duration.Value;
            }
        }

        public Episode()
        {

        }
        public Episode(string path)
        {
            ParseInfoFromPath(path);
        }

        void ParseInfoFromPath(string pPath)
        {
            
            string path = Program.getRelativePath(pPath);
            Exists = false;

            id = path;

            FileInfo fi;

            foreach (var bPath in Settings.paths)
            {
                string p = Program.PathCombine(bPath.Key, path);
                fi = new FileInfo(p);

                if (fi.Exists)
                {
                    try
                    {
                        Exists = true;
                        LastWriteTimeUtcOnDisk = fi.LastWriteTimeUtc;
                        FileSizeOnDisk = fi.Length;
                        path = fi.FullName;

                        break;
                    }
                    catch (Exception e) // Issue #24: a file that exists but with an invalid LastWriteTimeUtc. Possible filesystem corruption, ignore the offending file.
                    {
                        Exists = false;
                        Console.WriteLine("File found with an invalid LastWriteTimeUtc index. Ignoring: " + p);
                    }
                }
            }


            fullPath = path;
            fullDirPath = Path.GetDirectoryName(path);

            name = Path.GetFileName(path);
            dir = Program.getRelativeDirectory(path);

        }

    }
}
