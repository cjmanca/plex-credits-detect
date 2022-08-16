using SoundFingerprinting.Data;
using SoundFingerprinting.InMemory;
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SoundFingerprinting;
using System.Data;
using Spreads.DataTypes;

namespace plexCreditsDetect.Database
{
    internal class InMemoryFingerprintDatabase
    {
        InMemoryModelService modelService = null;
        SQLiteConnection sqlite_conn = null;

        string databasePath = "";

        public int dbversion
        {
            get
            {
                return GetIntData("DBVersion");
            }
            set
            {
                SetIntData("DBVersion", value);
            }
        }

        public DateTime lastPlexIntroAdded
        {
            get
            {
                return GetDateData("lastPlexIntroAdded");
            }
            set
            {
                SetDateData("lastPlexIntroAdded", value);
            }
        }

        public DateTime lastPlexDirectoryChanged
        {
            get
            {
                return GetDateData("lastPlexDirectoryChanged");
            }
            set
            {
                SetDateData("lastPlexDirectoryChanged", value);
            }
        }


        public InMemoryFingerprintDatabase()
        {
        }
        public InMemoryFingerprintDatabase(string path)
        {
            LoadDatabase(path);
        }

        public void LoadDatabase(string path)
        {
            if (path == null || path == "")
            {
                throw new ArgumentException("Invalid database path");
            }

            if (File.Exists(path))
            {
                throw new ArgumentException("Invalid database path");
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            SetupNewScan();

            databasePath = path;

            SQLiteConnectionStringBuilder sb = new SQLiteConnectionStringBuilder();
            sb.DataSource = Program.PathCombine(path, "fingerprintMedia.db");
            sb.Version = 3;
            sb.FailIfMissing = false;
            sb.ForeignKeys = true;
            sb.JournalMode = SQLiteJournalModeEnum.Wal;

            sqlite_conn = new SQLiteConnection(sb.ToString());

            while (true)
            {
                try
                {
                    sqlite_conn.Open();
                    break;
                }
                catch (SQLiteException e)
                {
                    if ((SQLiteErrorCode)e.ErrorCode != SQLiteErrorCode.Busy)
                    {
                        Console.WriteLine($"InMemoryFingerprintDatabase LoadDatabase SQLite error code {e.ErrorCode}: {e.Message}");
                        Program.Exit(-1);
                    }
                    Thread.Sleep(10);
                }
            }

            ExecuteDBCommand("VACUUM;");

            ExecuteDBCommand("CREATE TABLE IF NOT EXISTS GlobalData (id TEXT PRIMARY KEY, StringData TEXT, DateData DATETIME, DoubleData DOUBLE, IntData INTEGER);");

            if (dbversion < 0)
            {
                using (var transaction = sqlite_conn.BeginTransaction())
                {
                    ExecuteDBCommand("CREATE TABLE IF NOT EXISTS ScannedMedia (id TEXT NOT NULL PRIMARY KEY, name TEXT NOT NULL, dir TEXT NOT NULL, LastWriteTimeUtc INTEGER, FileSize INTEGER, DetectionPending BOOLEAN);");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_dir ON ScannedMedia(dir);");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_DetectionPending ON ScannedMedia(DetectionPending);");

                    ExecuteDBCommand("CREATE TABLE IF NOT EXISTS ScannedMedia_Timings (id INTEGER PRIMARY KEY, ScannedMedia_id TEXT NOT NULL REFERENCES ScannedMedia(id), is_plex_intro BOOLEAN NOT NULL, time_offset DOUBLE NOT NULL, end_time_offset DOUBLE NOT NULL, isCredits BOOLEAN);");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_Timings_ScannedMedia_id ON ScannedMedia_Timings(ScannedMedia_id);");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_Timings_is_plex_intro ON ScannedMedia_Timings(is_plex_intro);");

                    dbversion = 0;
                    transaction.Commit();
                }
            }

            if (dbversion < 1)
            {
                using (var transaction = sqlite_conn.BeginTransaction())
                {
                    ExecuteDBCommand("ALTER TABLE ScannedMedia ADD COLUMN SilenceDetectionDone BOOLEAN DEFAULT FALSE;");
                    ExecuteDBCommand("ALTER TABLE ScannedMedia_Timings ADD COLUMN isSilence BOOLEAN DEFAULT FALSE;");

                    dbversion = 1;
                    transaction.Commit();
                }
            }

            if (dbversion < 2)
            {
                using (var transaction = sqlite_conn.BeginTransaction())
                {
                    ExecuteDBCommand("ALTER TABLE ScannedMedia ADD COLUMN SilenceDetectionPending BOOLEAN DEFAULT FALSE;");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_SilenceDetectionPending ON ScannedMedia(SilenceDetectionPending);");

                    dbversion = 2;
                    transaction.Commit();
                }
            }

            if (dbversion < 3)
            {
                using (var transaction = sqlite_conn.BeginTransaction())
                {
                    ExecuteDBCommand("ALTER TABLE ScannedMedia ADD COLUMN BlackframeDetectionPending BOOLEAN DEFAULT FALSE;");
                    ExecuteDBCommand("ALTER TABLE ScannedMedia ADD COLUMN BlackframeDetectionDone BOOLEAN DEFAULT FALSE;");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_BlackframeDetectionPending ON ScannedMedia(BlackframeDetectionPending);");
                    ExecuteDBCommand("ALTER TABLE ScannedMedia_Timings ADD COLUMN isBlackframes BOOLEAN DEFAULT FALSE;");

                    dbversion = 3;
                    transaction.Commit();
                }
            }

            if (dbversion < 4)
            {
                using (var transaction = sqlite_conn.BeginTransaction())
                {
                    ExecuteDBCommand("DELETE FROM ScannedMedia_Timings WHERE ScannedMedia_id NOT IN (SELECT id FROM ScannedMedia WHERE ScannedMedia.id = ScannedMedia_id);");

                    ExecuteDBCommand("ALTER TABLE ScannedMedia ADD COLUMN metadata_item_id INTEGER DEFAULT -2;");
                    ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_metadata_item_id ON ScannedMedia(metadata_item_id);");


                    ExecuteDBCommand("PRAGMA foreign_keys = false;");

                    

                    ExecuteDBCommand("ALTER TABLE ScannedMedia_Timings RENAME TO temp_table_; ");

                    ExecuteDBCommand("CREATE TABLE ScannedMedia_Timings(" +
                    "     id              INTEGER PRIMARY KEY, " +
                    "     ScannedMedia_id TEXT    NOT NULL " +
                    "                             REFERENCES ScannedMedia (id) ON DELETE CASCADE " +
                    "                                                          ON UPDATE CASCADE, " +
                    "     is_plex_intro   BOOLEAN NOT NULL, " +
                    "     time_offset     DOUBLE  NOT NULL, " +
                    "     end_time_offset DOUBLE  NOT NULL, " +
                    "     isCredits       BOOLEAN, " +
                    "     isSilence       BOOLEAN DEFAULT FALSE, " +
                    "     isBlackframes   BOOLEAN DEFAULT FALSE " +
                    " ); ");

                    ExecuteDBCommand("INSERT INTO ScannedMedia_Timings(" +
                    "     id, ScannedMedia_id, is_plex_intro, time_offset, end_time_offset, isCredits, isSilence, isBlackframes ) " +
                    "     SELECT id, ScannedMedia_id, is_plex_intro, time_offset, end_time_offset, isCredits, isSilence, isBlackframes FROM temp_table_; ");

                    ExecuteDBCommand("DROP TABLE temp_table_; ");
                    ExecuteDBCommand("CREATE INDEX idx_ScannedMedia_Timings_ScannedMedia_id ON ScannedMedia_Timings(ScannedMedia_id); ");
                    ExecuteDBCommand("CREATE INDEX idx_ScannedMedia_Timings_is_plex_intro ON ScannedMedia_Timings(is_plex_intro); ");

                    ExecuteDBCommand("PRAGMA foreign_keys = true;");


                    dbversion = 4;
                    transaction.Commit();
                }
            }

            if (GetIntData("dbv4migrationdone") < 1)
            {
                Console.WriteLine("Starting DB migration. This could take several minutes. Please wait...");

                bool didSomething = true;

                using (var countRes = ExecuteDBQuery("SELECT COUNT(*) as cnt FROM ScannedMedia WHERE metadata_item_id = -2;"))
                {
                    if (countRes.Read())
                    {
                        int count = countRes.Get<int>("cnt");
                        int processed = 0;

                        while (didSomething)
                        {
                            didSomething = false;
                            Console.WriteLine($"Total items to migrate: {count - processed}");

                            using (var result = ExecuteDBQuery("SELECT `id`, `dir` FROM ScannedMedia WHERE metadata_item_id = -2 LIMIT 100;"))
                            { 

                                while (result.Read())
                                {
                                    processed++;
                                    didSomething = true;

                                    string id = result.Get<string>("id");
                                    string dir = result.Get<string>("dir");

                                    long metadata_item_id = Scanner.plexDB.GetMetadataID(id);

                                    ExecuteDBCommand("UPDATE ScannedMedia SET id = @newid, dir = @dir, metadata_item_id = @metadata_item_id WHERE `id` = @id;", new Dictionary<string, object>()
                                    {
                                        { "metadata_item_id", metadata_item_id },
                                        { "id", id },
                                        { "newid", Program.GetDBStylePath(id) },
                                        { "dir", Program.GetDBStylePath(dir) }
                                    });

                                }
                            }
                        }
                    }
                }

                ExecuteDBCommand("DELETE FROM ScannedMedia WHERE metadata_item_id = -1;");
                SetIntData("dbv4migrationdone", 1);
            }


        }

        public int GetIntData(string id)
        {
            using (var result = ExecuteDBQuery("SELECT IntData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            }))
            {

                if (!result.Read())
                {
                    return -1;
                }

                return result.Get<int>("IntData");
            }
        }
        public void SetIntData(string id, int data)
        {
            ExecuteDBCommand("REPLACE INTO GlobalData (`id`, IntData) VALUES (@id, @data);", new Dictionary<string, object>()
            {
                { "id", id },
                { "data", data },
            });
        }
        public string GetStringData(string id)
        {
            using (var result = ExecuteDBQuery("SELECT StringData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            }))
            {

                if (!result.Read())
                {
                    return "";
                }

                return result.Get<string>("StringData");
            }
        }
        public void SetStringData(string id, string data)
        {
            ExecuteDBCommand("REPLACE INTO GlobalData (`id`, StringData) VALUES (@id, @data);", new Dictionary<string, object>()
            {
                { "id", id },
                { "data", data },
            });
        }
        public DateTime GetDateData(string id)
        {
            using (var result = ExecuteDBQuery("SELECT DateData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            }))
            {

                if (!result.Read())
                {
                    return DateTime.MinValue;
                }

                return result.Get<DateTime>("DateData");
            }
        }
        public void SetDateData(string id, DateTime data)
        {
            ExecuteDBCommand("REPLACE INTO GlobalData (`id`, DateData) VALUES (@id, @data);", new Dictionary<string, object>()
            {
                { "id", id },
                { "data", data },
            });
        }

        public double GetDoubleData(string id)
        {
            using (var result = ExecuteDBQuery("SELECT DoubleData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            }))
            {

                if (!result.Read())
                {
                    return -1;
                }

                return result.Get<double>("DoubleData");
            }
        }

        public void SetDoubleData(string id, double data)
        {
            ExecuteDBCommand("REPLACE INTO GlobalData (`id`, DoubleData) VALUES (@id, @data);", new Dictionary<string, object>()
            {
                { "id", id },
                { "data", data },
            });
        }


        public int ExecuteDBCommand(string cmd, Dictionary<string, object> p = null, int recursionCount = 0)
        {
            using (var sqlite_cmd = sqlite_conn.CreateCommand())
            {
                sqlite_cmd.CommandText = cmd;
                sqlite_cmd.CommandType = System.Data.CommandType.Text;

                if (p != null)
                {
                    foreach (var row in p)
                    {
                        sqlite_cmd.Parameters.Add(new SQLiteParameter("@" + row.Key, row.Value));
                    }
                }

                int count = 0;

                while (true)
                {
                    try
                    {
                        count++;
                        int ret = sqlite_cmd.ExecuteNonQuery();
                        return ret;
                    }
                    catch (SQLiteException ex)
                    {
                        if (count >= 2 + recursionCount)
                        {
                            Console.WriteLine($"plex-credits-detect Database ExecuteDBCommand Database has been locked for a long time. Attempting to re-connect.");
                            CloseDatabase();
                            LoadDatabase(databasePath);
                            return ExecuteDBCommand(cmd, p, recursionCount + 1);
                        }

                        if ((SQLiteErrorCode)ex.ErrorCode != SQLiteErrorCode.Busy)
                        {
                            Console.WriteLine("plex-credits-detect Database.ExecuteDBCommand exception: " + ex.Message + "" +
                                " while executing SQL: " + cmd);
                            if (p != null && p.Count > 0)
                            {
                                Console.WriteLine("With data: ");

                                foreach (var x in p)
                                {
                                    Console.WriteLine($"{x.Key} = {x.Value}");
                                }
                            }
                            Program.Exit(-1);
                            return -1;
                        }
                        Thread.Sleep(10);
                    }
                }
            }
        }

        public SQLResultInfo ExecuteDBQuery(string cmd, Dictionary<string, object> p = null, int recursionCount = 0)
        {
            if (recursionCount > 20)
            {
                Console.WriteLine($"plex-credits-detect Database not accessible. Retry count exceeded. Exiting.");
                Program.Exit(-1);
                return null;
            }
            SQLResultInfo ret = new SQLResultInfo();

            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = cmd;
            sqlite_cmd.CommandType = System.Data.CommandType.Text;

            if (p != null)
            {
                foreach (var row in p)
                {
                    sqlite_cmd.Parameters.Add(new SQLiteParameter("@" + row.Key, row.Value));
                }
            }

            int count = 0;

            while (true)
            {
                try
                {
                    count++;
                    //var reader = sqlite_cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo);
                    ret.reader = sqlite_cmd.ExecuteReader();

                    for (var i = 0; i < ret.reader.FieldCount; i++)
                    {
                        ret.columns[ret.reader.GetName(i)] = i;
                    }

                    return ret;
                }
                catch (SQLiteException ex)
                {
                    if (count >= 2 + recursionCount)
                    {
                        Console.WriteLine($"plex-credits-detect Database ExecuteDBQuery Database has been locked for a long time. Attempting to re-connect.");

                        sqlite_cmd.Reset();
                        sqlite_cmd.Dispose();

                        CloseDatabase();
                        LoadDatabase(databasePath);
                        return ExecuteDBQuery(cmd, p, recursionCount + 1);
                    }

                    if ((SQLiteErrorCode)ex.ErrorCode != SQLiteErrorCode.Busy)
                    {
                        sqlite_cmd.Reset();
                        sqlite_cmd.Dispose();

                        Console.WriteLine("plex-credits-detect Database.ExecuteDBQuery exception: " + ex.Message + "" +
                            " while executing SQL: " + cmd);
                        if (p != null && p.Count > 0)
                        {
                            Console.WriteLine("With data: ");

                            foreach (var x in p)
                            {
                                Console.WriteLine($"{x.Key} = {x.Value}");
                            }
                        }
                        Program.Exit(-1);
                        return null;
                    }
                    Thread.Sleep(10);
                }
            }
        }

        public void CloseDatabase()
        {
            try
            {
                sqlite_conn.Close();
                sqlite_conn.Dispose();
            }
            catch { }

            sqlite_conn = null;
            modelService = null;
        }

        public AVHashes GetTrackHash(string id, bool isCredits, int partNum = -1)
        {
            return modelService.ReadHashesByTrackId(id + isCredits.ToString() + partNum);
        }

        public Episode GetEpisode(string id)
        {
            return GetEpisode(new Episode(id));
        }
        public Episode GetEpisode(long metadata_id)
        {
            return GetEpisode(new Episode(), metadata_id);
        }
        public Episode GetEpisode(Episode ep, long metadata_id = -1)
        {
            Dictionary<string, object> p = null;
            string cmd = "";

            if (metadata_id >= 0)
            {
                cmd = "SELECT * FROM ScannedMedia WHERE metadata_item_id = @metadata_item_id LIMIT 1;";
                p = new Dictionary<string, object>()
                {
                    { "metadata_item_id", metadata_id }
                };
            }
            else
            {
                cmd = "SELECT * FROM ScannedMedia WHERE id = @id LIMIT 1;";
                p = new Dictionary<string, object>()
                {
                    { "id", Program.GetDBStylePath(ep.id) }
                };
            }

            using (var result = ExecuteDBQuery(cmd, p))
            {

                if (!result.Read())
                {
                    ep.InPrivateDB = false;
                    ep.FileSizeInDB = -1;
                    ep.LastWriteTimeUtcInDB = DateTime.MinValue;

                    if (metadata_id < 0 && ep.isSet_metadata_item_id && ep.metadata_item_id > 0)
                    {
                        return GetEpisode(ep, ep.metadata_item_id);
                    }

                    return null;
                }

                string sourceID = Program.GetDBStylePath(ep.id);
                long sourceMetaData = -1;

                if (ep.isSet_metadata_item_id)
                {
                    sourceMetaData = ep.metadata_item_id;
                }


                ep.InPrivateDB = true;
                ep.idInLocalDB = result.Get<string>("id");
                ep.id = ep.idInLocalDB;
                ep.metadata_item_id = result.Get<long>("metadata_item_id");
                ep.name = result.Get<string>("name");
                ep.dir = result.Get<string>("dir");
                ep.LastWriteTimeUtcInDB = result.GetUnixDateTime("LastWriteTimeUtc");
                ep.FileSizeInDB = result.Get<long>("FileSize");
                ep.DetectionPending = result.Get<bool>("DetectionPending");
                ep.SilenceDetectionPending = result.Get<bool>("SilenceDetectionPending");
                ep.BlackframeDetectionPending = result.Get<bool>("BlackframeDetectionPending");
                ep.SilenceDetectionDone = result.Get<bool>("SilenceDetectionDone");
                ep.BlackframeDetectionDone = result.Get<bool>("BlackframeDetectionDone");

                ep.Validate();

                if (sourceMetaData >= 0 && sourceMetaData != ep.metadata_item_id) // episode originally populated from plex DB, and metadata_item_id changed. Probably plex dance.
                {
                    ep.metadata_item_id = sourceMetaData;
                    if (ep.Exists)
                    {
                        ep.Save();
                    }
                }

                if (metadata_id > 0 && sourceID.Length > 1 && sourceID != Program.GetDBStylePath(ep.id)) // episode originally populated from plex DB, and file name/location changed
                {
                    ep.id = sourceID;
                    ep.EpisodeNameChanged = true;
                    ep.Validate();
                    if (ep.Exists)
                    {
                        ep.Save();
                    }
                }

                return ep;
            }
        }

        public bool EpisodeExists(string id)
        {
            using (var result = ExecuteDBQuery("SELECT COUNT(*) as cnt " +
                " FROM ScannedMedia WHERE id = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", Program.GetDBStylePath(id) }
            }))
            {
                if (!result.Read())
                {
                    return false;
                }

                int count = result.Get<int>("cnt");

                return count > 0;
            }
        }

        public DateTime GetEpisodeLastWriteTimeUtc(string id)
        {
            using (var result = ExecuteDBQuery("SELECT LastWriteTimeUtc " +
                " FROM ScannedMedia WHERE id = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", Program.GetDBStylePath(id) }
            }))
            {

                if (!result.Read())
                {
                    return DateTime.MinValue;
                }

                return result.GetUnixDateTime("LastWriteTimeUtc");
            }
        }
        public long GetEpisodeFileSize(string id)
        {
            using (var result = ExecuteDBQuery("SELECT FileSize " +
                " FROM ScannedMedia WHERE id = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", Program.GetDBStylePath(id) }
            }))
            {

                if (!result.Read())
                {
                    return -1;
                }

                return result.Get<long>("FileSize");
            }
        }

        public List<Episode> GetPendingEpisodesForSeason(string dir)
        {
            List<Episode> eps = new List<Episode>();


            using (var result = ExecuteDBQuery("SELECT * FROM ScannedMedia " +
                " WHERE dir = @dir AND (DetectionPending = TRUE OR SilenceDetectionPending = TRUE OR BlackframeDetectionPending = TRUE);", new Dictionary<string, object>()
            {
                { "dir", Program.GetDBStylePath(dir) }
            }))
            {
                if (!result.HasRows)
                {
                    return null;
                }

                while (result.Read())
                {
                    string id = result.Get<string>("id");

                    Episode ep = new Episode();
                    ep.InPrivateDB = true;
                    ep.id = id;
                    ep.idInLocalDB = id;
                    ep.metadata_item_id = result.Get<long>("metadata_item_id");
                    ep.name = result.Get<string>("name");
                    ep.dir = result.Get<string>("dir");
                    ep.LastWriteTimeUtcInDB = result.GetUnixDateTime("LastWriteTimeUtc");
                    ep.FileSizeInDB = result.Get<long>("FileSize");
                    ep.DetectionPending = result.Get<bool>("DetectionPending");
                    ep.SilenceDetectionPending = result.Get<bool>("SilenceDetectionPending");
                    ep.BlackframeDetectionPending = result.Get<bool>("BlackframeDetectionPending");
                    ep.SilenceDetectionDone = result.Get<bool>("SilenceDetectionDone");
                    ep.BlackframeDetectionDone = result.Get<bool>("BlackframeDetectionDone");

                    ep.Validate();

                    eps.Add(ep);
                }

                return eps;
            }
        }

        public void ClearDetectionPendingForDirectory(string dir)
        {
            var result = ExecuteDBCommand("UPDATE ScannedMedia " +
                " SET DetectionPending = FALSE, SilenceDetectionPending = FALSE, BlackframeDetectionPending = FALSE " +
                " WHERE dir = @dir;", new Dictionary<string, object>()
            {
                { "dir", Program.GetDBStylePath(dir) }
            });
        }

        public List<Episode> GetPendingEpisodes()
        {
            List<Episode> eps = new List<Episode>();


            using (var result = ExecuteDBQuery("SELECT * FROM ScannedMedia " +
                " WHERE DetectionPending = TRUE OR SilenceDetectionPending = TRUE OR BlackframeDetectionPending = TRUE;"))
            {

                if (!result.HasRows)
                {
                    return null;
                }

                while (result.Read())
                {
                    string id = result.Get<string>("id");

                    Episode ep = new Episode();
                    ep.InPrivateDB = true;
                    ep.id = id;
                    ep.idInLocalDB = id;
                    ep.metadata_item_id = result.Get<long>("metadata_item_id");
                    ep.name = result.Get<string>("name");
                    ep.dir = result.Get<string>("dir");
                    ep.LastWriteTimeUtcInDB = result.GetUnixDateTime("LastWriteTimeUtc");
                    ep.FileSizeInDB = result.Get<long>("FileSize");
                    ep.DetectionPending = result.Get<bool>("DetectionPending");
                    ep.SilenceDetectionPending = result.Get<bool>("SilenceDetectionPending");
                    ep.BlackframeDetectionPending = result.Get<bool>("BlackframeDetectionPending");
                    ep.SilenceDetectionDone = result.Get<bool>("SilenceDetectionDone");
                    ep.BlackframeDetectionDone = result.Get<bool>("BlackframeDetectionDone");

                    ep.Validate();

                    eps.Add(ep);
                }

                return eps;
            }
        }
        public Episode GetOnePendingEpisode()
        {
            using (var result = ExecuteDBQuery("SELECT * FROM ScannedMedia " +
                " WHERE DetectionPending = TRUE OR SilenceDetectionPending = TRUE OR BlackframeDetectionPending = TRUE LIMIT 1;"))
            {
                if (!result.Read())
                {
                    return null;
                }

                string id = result.Get<string>("id");

                Episode ep = new Episode();
                ep.InPrivateDB = true;
                ep.id = id;
                ep.idInLocalDB = id;
                ep.metadata_item_id = result.Get<long>("metadata_item_id");
                ep.name = result.Get<string>("name");
                ep.dir = result.Get<string>("dir");
                ep.LastWriteTimeUtcInDB = result.GetUnixDateTime("LastWriteTimeUtc");
                ep.FileSizeInDB = result.Get<long>("FileSize");
                ep.DetectionPending = result.Get<bool>("DetectionPending");
                ep.SilenceDetectionPending = result.Get<bool>("SilenceDetectionPending");
                ep.BlackframeDetectionPending = result.Get<bool>("BlackframeDetectionPending");
                ep.SilenceDetectionDone = result.Get<bool>("SilenceDetectionDone");
                ep.BlackframeDetectionDone = result.Get<bool>("BlackframeDetectionDone");

                ep.Validate();

                return ep;
            }
        }

        public List<string> GetPendingDirectories()
        {
            List<string> dirs = new List<string>();
            string dir;

            using (var result = ExecuteDBQuery("SELECT distinct dir " +
                " FROM ScannedMedia WHERE DetectionPending = TRUE OR SilenceDetectionPending = TRUE OR BlackframeDetectionPending = TRUE;"))
            {

                if (!result.HasRows)
                {
                    return null;
                }

                while (result.Read())
                {
                    dir = Program.FixPath(result.Get<string>("dir"));
                    if (!Scanner.ignoreDirectories.Contains(dir))
                    {
                        dirs.Add(Program.getFullDirectory(dir));
                    }
                }

                return dirs;
            }
        }
        public void DeleteEpisode(Episode ep)
        {
            try
            {
                DeleteEpisodeTimings(ep);
                ExecuteDBCommand("DELETE FROM ScannedMedia WHERE id = @id;", new Dictionary<string, object>()
                {
                    { "id", Program.GetDBStylePath(ep.id) }
                });
                modelService.DeleteTrack(ep.id + true.ToString());
                modelService.DeleteTrack(ep.id + false.ToString());
            }
            catch { }


            ep.InPrivateDB = false;
        }

        public void DeleteEpisodeTimings(Episode ep)
        {
            ExecuteDBCommand("DELETE FROM ScannedMedia_Timings WHERE ScannedMedia_id = @ScannedMedia_id;", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", Program.GetDBStylePath(ep.id) }
            });
        }
        public void DeleteEpisodePlexTimings(Episode ep)
        {
            ExecuteDBCommand("DELETE FROM ScannedMedia_Timings WHERE ScannedMedia_id = @ScannedMedia_id AND is_plex_intro = @is_plex_intro;", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", Program.GetDBStylePath(ep.id) },
                { "is_plex_intro", true }
            });
        }

        public List<Segment> GetNonPlexTimings(Episode ep, bool addToEpisode = false)
        {
            List<Segment> segments = new List<Segment>();

            using (var result = ExecuteDBQuery("SELECT time_offset, end_time_offset, isCredits, isSilence, isBlackframes " +
                " FROM ScannedMedia_Timings WHERE ScannedMedia_id = @ScannedMedia_id AND is_plex_intro = @is_plex_intro;", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", Program.GetDBStylePath(ep.id) },
                { "is_plex_intro", false }
            }))
            {

                if (!result.HasRows)
                {
                    return null;
                }

                while (result.Read())
                {
                    Segment seg = new Segment();

                    seg.start = result.Get<double>("time_offset");
                    seg.end = result.Get<double>("end_time_offset");

                    seg.isCredits = result.Get<bool>("isCredits");
                    seg.isSilence = result.Get<bool>("isSilence");
                    seg.isBlackframes = result.Get<bool>("isBlackframes");

                    segments.Add(seg);

                    if (addToEpisode)
                    {
                        ep.segments.allSegments.Add(seg);
                    }
                }

                return segments;
            }
        }

        public List<Episode> GetNonPlexTimingsForDir(string dir)
        {
            List<Episode> episodes = new List<Episode>();
            Dictionary<string, int> columns;

            using (var result = ExecuteDBQuery("SELECT m.id as id, LastWriteTimeUtc, FileSize, DetectionPending, SilenceDetectionPending, BlackframeDetectionPending, SilenceDetectionDone, BlackframeDetectionDone, time_offset, end_time_offset, isCredits, isSilence, isBlackframes, is_plex_intro, metadata_item_id " +
                " FROM ScannedMedia as m LEFT JOIN ScannedMedia_Timings as t ON t.ScannedMedia_id = m.id " +
                " WHERE m.dir = @dir; ", new Dictionary<string, object>()
            {
                { "dir", Program.GetDBStylePath(dir) },
                { "is_plex_intro", false }
            }))
            {

                if (!result.HasRows)
                {
                    return episodes;
                }

                Episode ep;

                while (result.Read())
                {
                    bool isPlexIntro = false;
                    Segment seg = null;

                    string id = result.Get<string>("id");
                    ep = episodes.FirstOrDefault(x => x.id == id);
                    if (ep == null)
                    {
                        ep = new Episode();
                        ep.id = id;
                        ep.idInLocalDB = id;
                        ep.metadata_item_id = result.Get<long>("metadata_item_id");
                        ep.InPrivateDB = true;
                        ep.LastWriteTimeUtcInDB = result.GetUnixDateTime("LastWriteTimeUtc");
                        ep.FileSizeInDB = result.Get<long>("FileSize");

                        ep.Validate();

                        episodes.Add(ep);
                    }

                    ep.DetectionPending = result.Get<bool>("DetectionPending");
                    ep.SilenceDetectionPending = result.Get<bool>("SilenceDetectionPending");
                    ep.BlackframeDetectionPending = result.Get<bool>("BlackframeDetectionPending");
                    ep.SilenceDetectionDone = result.Get<bool>("SilenceDetectionDone");
                    ep.BlackframeDetectionDone = result.Get<bool>("BlackframeDetectionDone");

                    if (!result.IsDBNull("time_offset"))
                    {
                        seg = new Segment();
                        seg.start = result.Get<double>("time_offset");
                        seg.end = result.Get<double>("end_time_offset");
                        seg.isCredits = result.Get<bool>("isCredits");
                        seg.isSilence = result.Get<bool>("isSilence");
                        seg.isBlackframes = result.Get<bool>("isBlackframes");
                        isPlexIntro = result.Get<bool>("is_plex_intro");

                        if (!isPlexIntro)
                        {
                            ep.segments.allSegments.Add(seg);
                        }
                    }

                }
                episodes.ForEach(x => x.segments.allSegments.Sort((a, b) => a.start.CompareTo(b.start)));

                return episodes;
            }
        }

        public void Insert(Episode ep)
        {
            string cmd = "";

            if (!ep.didPopulateFromLocal && ep.idInLocalDB == "")
            {
                if (EpisodeExists(ep.id))
                {
                    ep.idInLocalDB = Program.GetDBStylePath(ep.id);
                }
            }

            if (ep.idInLocalDB != "")
            {
                cmd = "UPDATE ScannedMedia SET " +
                    " id = @id, " +
                    " name = @name, " +
                    " dir = @dir, " +
                    " LastWriteTimeUtc = @LastWriteTimeUtc, " +
                    " FileSize = @FileSize, " +
                    " DetectionPending = @DetectionPending, " +
                    " SilenceDetectionDone = @SilenceDetectionDone, " +
                    " SilenceDetectionPending = @SilenceDetectionPending, " +
                    " BlackframeDetectionPending = @BlackframeDetectionPending, " +
                    " BlackframeDetectionDone = @BlackframeDetectionDone, " +
                    " metadata_item_id = @metadata_item_id " +
                    " WHERE id = @idInLocalDB; ";
            }
            else
            {
                cmd = "INSERT INTO ScannedMedia " +
                " (id, name, dir, LastWriteTimeUtc, FileSize, DetectionPending, SilenceDetectionPending, BlackframeDetectionPending, SilenceDetectionDone, BlackframeDetectionDone, metadata_item_id) VALUES " +
                " (@id, @name, @dir, @LastWriteTimeUtc, @FileSize, @DetectionPending, @SilenceDetectionPending, @BlackframeDetectionPending, @SilenceDetectionDone, @BlackframeDetectionDone, @metadata_item_id);";
            }

            ExecuteDBCommand(cmd, new Dictionary<string, object>()
            {
                { "id", Program.GetDBStylePath(ep.id) },
                { "name", ep.name },
                { "dir", Program.GetDBStylePath(ep.dir) },
                { "LastWriteTimeUtc", ep.LastWriteTimeUtcOnDisk.ToFileTimeUtc() },
                { "FileSize", ep.FileSizeOnDisk },
                { "DetectionPending", ep.DetectionPending },
                { "SilenceDetectionPending", ep.SilenceDetectionPending },
                { "BlackframeDetectionPending", ep.BlackframeDetectionPending },
                { "SilenceDetectionDone", ep.SilenceDetectionDone },
                { "BlackframeDetectionDone", ep.BlackframeDetectionDone },
                { "metadata_item_id", ep.metadata_item_id },
                { "idInLocalDB", Program.GetDBStylePath(ep.idInLocalDB) }
            });
            ep.idInLocalDB = Program.GetDBStylePath(ep.id);
            ep.InPrivateDB = true;
            ep.LastWriteTimeUtcInDB = ep.LastWriteTimeUtcOnDisk;
            ep.FileSizeInDB = ep.FileSizeOnDisk;
        }

        public void InsertTiming(Episode ep, Segment segment, bool isPlexIntro)
        {
            ExecuteDBCommand("INSERT INTO ScannedMedia_Timings " +
                " (ScannedMedia_id, is_plex_intro, time_offset, end_time_offset, isCredits, isSilence, isBlackframes) VALUES " +
                " (@ScannedMedia_id, @is_plex_intro, @time_offset, @end_time_offset, @isCredits, @isSilence, @isBlackframes);", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", Program.GetDBStylePath(ep.id) },
                { "is_plex_intro", isPlexIntro },
                { "time_offset", segment.start },
                { "end_time_offset", segment.end },
                { "isCredits", segment.isCredits },
                { "isSilence", segment.isSilence },
                { "isBlackframes", segment.isBlackframes }
            });
        }


        public void InsertHash(Episode ep, AVHashes hashes, MediaType avtype, bool isCredits, double start, int partNum = -1)
        {
            TrackInfo trackinfo = new TrackInfo(ep.id + isCredits.ToString() + partNum, ep.name, ep.dir, new Dictionary<string, string>()
            {
                { "name", ep.name },
                { "dir", ep.dir },
                { "LastWriteTimeUtc", ep.LastWriteTimeUtcOnDisk.ToFileTimeUtc().ToString() },
                { "FileSize", ep.FileSizeOnDisk.ToString() },
                { "start", start.ToString() },
                { "isCredits", isCredits.ToString() },
                { "DetectionPending", ep.DetectionPending.ToString() },
                { "SilenceDetectionPending", ep.SilenceDetectionPending.ToString() },
                { "BlackframeDetectionPending", ep.BlackframeDetectionPending.ToString() },
                { "SilenceDetectionDone", ep.SilenceDetectionDone.ToString() },
                { "BlackframeDetectionDone", ep.BlackframeDetectionDone.ToString() }
            }, avtype);


            modelService.Insert(trackinfo, hashes);
        }

        public void SetupNewScan()
        {
            modelService = new InMemoryModelService();
        }

        public IModelService GetModelService()
        {
            return modelService;
        }
    }
}
