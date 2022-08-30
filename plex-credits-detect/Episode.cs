using plexCreditsDetect.Database;
using Spreads.DataTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect
{
    [DebuggerDisplay("Episode: {id}")]
    public class Episode
    {

        // Saved in database
        bool _idInLocalDB_set = false;
        string _idInLocalDB = "";
        public string idInLocalDB
        {
            get
            {
                return _idInLocalDB;
            }
            set
            {
                if (!onlySetIfUnset || !_idInLocalDB_set)
                {
                    _idInLocalDB = value;
                    _idInLocalDB_set = true;
                }
            }
        }


        bool _id_set = false;
        string _id = "";
        public string id
        { 
            get
            {
                return _id;
            }
            set
            {
                if (!onlySetIfUnset || !_id_set)
                {
                    _id = value;
                    _id_set = true;
                }
            }
        }

        bool _name_set = false;
        string _name = "";
        public string name
        {
            get
            {
                return _name;
            }
            set
            {
                if (!onlySetIfUnset || !_name_set)
                {
                    _name = value;
                    _name_set = true;
                }
            }
        }

        bool _dir_set = false;
        string _dir = "";
        public string dir
        {
            get
            {
                return _dir;
            }
            set
            {
                if (!onlySetIfUnset || !_dir_set)
                {
                    _dir = value;
                    _dir_set = true;
                }
            }
        }

        public bool onlySetIfUnset = false;

        bool? _BlackframeDetectionPending = null;
        public bool BlackframeDetectionPending
        {
            get
            {
                if (!_BlackframeDetectionPending.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_BlackframeDetectionPending.HasValue)
                    {
                        _BlackframeDetectionPending = false;
                    }
                }
                return _BlackframeDetectionPending.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_BlackframeDetectionPending.HasValue)
                {
                    _BlackframeDetectionPending = value;
                }
            }
        }

        bool? _SilenceDetectionPending = null;
        public bool SilenceDetectionPending
        {
            get
            {
                if (!_SilenceDetectionPending.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_SilenceDetectionPending.HasValue)
                    {
                        _SilenceDetectionPending = false;
                    }
                }
                return _SilenceDetectionPending.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_SilenceDetectionPending.HasValue)
                {
                    _SilenceDetectionPending = value;
                }
            }
        }

        bool? _DetectionPending = null;
        public bool DetectionPending
        {
            get
            {
                if (!_DetectionPending.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_DetectionPending.HasValue)
                    {
                        _DetectionPending = false;
                    }
                }
                return _DetectionPending.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_DetectionPending.HasValue)
                {
                    _DetectionPending = value;
                }
            }
        }

        bool? _SilenceDetectionDone = null;
        public bool SilenceDetectionDone
        {
            get
            {
                if (!_SilenceDetectionDone.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_SilenceDetectionDone.HasValue)
                    {
                        _SilenceDetectionDone = false;
                    }
                }
                return _SilenceDetectionDone.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_SilenceDetectionDone.HasValue)
                {
                    _SilenceDetectionDone = value;
                }
            }
        }

        bool? _BlackframeDetectionDone = null;
        public bool BlackframeDetectionDone
        {
            get
            {
                if (!_BlackframeDetectionDone.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_BlackframeDetectionDone.HasValue)
                    {
                        _BlackframeDetectionDone = false;
                    }
                }
                return _BlackframeDetectionDone.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_BlackframeDetectionDone.HasValue)
                {
                    _BlackframeDetectionDone = value;
                }
            }
        }

        DateTime? _LastWriteTimeUtcInDB = null;
        public DateTime LastWriteTimeUtcInDB
        {
            get
            {
                if (!_LastWriteTimeUtcInDB.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_LastWriteTimeUtcInDB.HasValue)
                    {
                        _LastWriteTimeUtcInDB = DateTime.MinValue;
                    }
                }
                return _LastWriteTimeUtcInDB.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_LastWriteTimeUtcInDB.HasValue)
                {
                    _LastWriteTimeUtcInDB = value;
                }
            }
        }

        long? _FileSizeInDB = null;
        public long FileSizeInDB
        {
            get
            {
                if (!_FileSizeInDB.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_FileSizeInDB.HasValue)
                    {
                        _FileSizeInDB = -1;
                    }
                }
                return _FileSizeInDB.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_FileSizeInDB.HasValue)
                {
                    _FileSizeInDB = value;
                }
            }
        }




        // Extra info
        bool _didPopulateFromLocal_set = false;
        bool _didPopulateFromLocal = false;
        public bool didPopulateFromLocal
        {
            get
            {
                return _didPopulateFromLocal;
            }
            set
            {
                if (!onlySetIfUnset || !_didPopulateFromLocal_set)
                {
                    _didPopulateFromLocal = value;
                    _didPopulateFromLocal_set = true;
                }
            }
        }

        bool _passed_set = false;
        bool _passed = false;
        public bool passed
        {
            get
            {
                return _passed;
            }
            set
            {
                if (!onlySetIfUnset || !_passed_set)
                {
                    _passed = value;
                    _passed_set = true;
                }
            }
        }

        bool _needsScanning_set = false;
        bool _needsScanning = false;
        public bool needsScanning
        {
            get
            {
                return _needsScanning;
            }
            set
            {
                if (!onlySetIfUnset || !_needsScanning_set)
                {
                    _needsScanning = value;
                    _needsScanning_set = true;
                }
            }
        }

        bool _needsSilenceScanning_set = false;
        bool _needsSilenceScanning = false;
        public bool needsSilenceScanning
        {
            get
            {
                return _needsSilenceScanning;
            }
            set
            {
                if (!onlySetIfUnset || !_needsSilenceScanning_set)
                {
                    _needsSilenceScanning = value;
                    _needsSilenceScanning_set = true;
                }
            }
        }

        bool _needsBlackframeScanning_set = false;
        bool _needsBlackframeScanning = false;
        public bool needsBlackframeScanning
        {
            get
            {
                return _needsBlackframeScanning;
            }
            set
            {
                if (!onlySetIfUnset || !_needsBlackframeScanning_set)
                {
                    _needsBlackframeScanning = value;
                    _needsBlackframeScanning_set = true;
                }
            }
        }





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

        bool _fullPath_set = false;
        string _fullPath = "";
        public string fullPath
        {
            get
            {
                return _fullPath;
            }
            set
            {
                if (!onlySetIfUnset || !_fullPath_set)
                {
                    _fullPath = value;
                    _fullPath_set = true;
                }
            }
        }

        bool _fullDirPath_set = false;
        string _fullDirPath = "";
        public string fullDirPath
        {
            get
            {
                return _fullDirPath;
            }
            set
            {
                if (!onlySetIfUnset || !_fullDirPath_set)
                {
                    _fullDirPath = value;
                    _fullDirPath_set = true;
                }
            }
        }

        public Segments segments { get; set; } = new Segments();
        public long FileSizeOnDisk { get; set; } = 0;
        public DateTime LastWriteTimeUtcOnDisk { get; set; } = DateTime.MinValue;
        public bool EpisodeNameChanged { get; set; } = false;


        bool? _InPlexDB = null;
        public bool InPlexDB
        {
            get
            {
                if (!_InPlexDB.HasValue)
                {
                    _InPlexDB = metadata_item_id >= 0;
                }
                return _InPlexDB.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_InPlexDB.HasValue)
                {
                    _InPlexDB = value;
                }
            }
        }

        bool? _InPrivateDB = null;
        public bool InPrivateDB
        {
            get
            {
                if (!_InPrivateDB.HasValue)
                {
                    PopulateFromPrivateDB();
                    if (!_InPrivateDB.HasValue)
                    {
                        _InPrivateDB = false;
                    }
                }
                return _InPrivateDB.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_InPrivateDB.HasValue)
                {
                    _InPrivateDB = value;
                    didPopulateFromLocal = true;
                }
            }
        }

        public bool isSet_metadata_item_id { get; set; } = false;

        long? _metadata_item_id = null;
        public long metadata_item_id
        {
            get
            {
                if (!_metadata_item_id.HasValue)
                {
                    if (!InPrivateDB && !_metadata_item_id.HasValue) // InPrivateDB may populate _metadata_item_id from local database
                    {
                        _metadata_item_id = Scanner.plexDB.GetMetadataID(this);
                        isSet_metadata_item_id = true;
                    }
                }
                return _metadata_item_id.Value;
            }
            set
            {
                if (!onlySetIfUnset || !_metadata_item_id.HasValue)
                {
                    _metadata_item_id = value;
                    isSet_metadata_item_id = true;
                }
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
                    _plexTimings = Scanner.plexDB.GetPlexIntroTimings(metadata_item_id);
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


        public void PopulateFromPrivateDB(bool fillOnly = true, long metadata_id = -1)
        {
            didPopulateFromLocal = true;
            Scanner.db.GetEpisode(this, fillOnly, metadata_id);
        }
        public void Save()
        {
            Scanner.db.Insert(this);
        }
        public void Delete()
        {
            Scanner.db.DeleteEpisode(this);
        }

        public Episode()
        {

        }
        public Episode(string path)
        {
            ParseInfoFromPath(path);
        }
        public Episode(long metadata_id)
        {
            ParseInfoFromMetadataID(metadata_id);
        }

        void ParseInfoFromMetadataID(long metadata_id)
        {
            if (Scanner.db.GetEpisode(this, true, metadata_id) == null)
            {
                if (Scanner.plexDB.GetEpisodeForMetaID(this, metadata_id) != null)
                {
                    Scanner.db.GetEpisode(this, true);
                }
            }
        }

        void ParseInfoFromPath(string pPath)
        {
            id = pPath;
            Validate();
        }

        public void Validate()
        {
            string path = Program.getRelativePath(id);
            Exists = false;

            id = Program.GetDBStylePath(path);

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
