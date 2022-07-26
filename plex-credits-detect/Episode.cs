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

        void ParseInfoFromPath(string path)
        {
            path = Program.getRelativePath(path);
            Exists = false;

            id = path;

            FileInfo fi;

            foreach (var bPath in Program.settings.paths)
            {
                string p = Program.PathCombine(bPath.Key, path);
                fi = new FileInfo(p);

                if (fi.Exists)
                {
                    Exists = true;
                    path = fi.FullName;
                    LastWriteTimeUtcOnDisk = fi.LastWriteTimeUtc;
                    FileSizeOnDisk = fi.Length;

                    break;
                }
            }


            fullPath = path;
            fullDirPath = Path.GetDirectoryName(path);

            name = Path.GetFileName(path);
            dir = Program.getRelativeDirectory(path);

        }

    }
}
