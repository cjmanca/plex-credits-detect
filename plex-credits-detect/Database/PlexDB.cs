using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect.Database
{
    internal class PlexDB
    {
        SQLiteConnection sqlite_conn = null;
        long TagID = -1;

        long parentActivityID = -1;
        long currentActivityID = -1;

        string databasePath = "";

        public class RootDirectory
        {
            public string path = "";
            public long library_section_id = -1;
            public long section_type = -1;
        }


        Dictionary<long, RootDirectory> _RootDirectories = null;
        public Dictionary<long, RootDirectory> RootDirectories
        {
            get
            {
                if (_RootDirectories == null)
                {
                    _RootDirectories = GetRootDirectories();
                    if (_RootDirectories == null)
                    {
                        _RootDirectories = new Dictionary<long, RootDirectory>();
                    }
                    else
                    {
                        GetMovieRoots();
                    }
                }
                return _RootDirectories;
            }
        }


        public PlexDB()
        {
        }
        public PlexDB(string path)
        {
            LoadDatabase(path);
        }

        public void LoadDatabase(string path)
        {
            databasePath = path;
            if (path == null || path == "")
            {
                throw new ArgumentException("PlexDB.LoadDatabase - Invalid database path");
            }

            if (!File.Exists(path))
            {
                Console.WriteLine("PlexDB.LoadDatabase - Invalid database path");
                throw new ArgumentException("Invalid database path");
            }
            SQLiteConnectionStringBuilder sb = new SQLiteConnectionStringBuilder();
            sb.DataSource = path;
            sb.Version = 3;
            sb.FailIfMissing = true;
            sb.CacheSize = 0;

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
                        Console.WriteLine($"PlexDB LoadDatabase SQLite error code {e.ErrorCode}: {e.Message}");
                        Program.Exit();
                    }
                    Thread.Sleep(10);
                }
            }
            ExecuteDBCommand("CREATE INDEX IF NOT EXISTS index_taggings_on_created_at ON taggings(created_at);");
        }


        public int ExecuteDBCommand(string cmd, Dictionary<string, object> p = null)
        {
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
                    return sqlite_cmd.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    if (count > 10)
                    {
                        Console.WriteLine($"PlexDB ExecuteDBCommand Database has been locked for a long time. Attempting to re-connect.");
                        CloseDatabase();
                        LoadDatabase(databasePath);
                        count = 0;
                    }

                    if ((SQLiteErrorCode)e.ErrorCode != SQLiteErrorCode.Busy)
                    {
                        Console.WriteLine($"PlexDB ExecuteDBCommand SQLite error code {e.ErrorCode}: {e.Message}");
                        Program.Exit();
                        return -1;
                    }
                    Thread.Sleep(10);
                }
            }
        }

        public SQLResultInfo ExecuteDBQuery(string cmd, Dictionary<string, object> p = null)
        {
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
                catch (SQLiteException e)
                {
                    if (count > 10)
                    {
                        Console.WriteLine($"PlexDB ExecuteDBCommand Database has been locked for a long time. Attempting to re-connect.");
                        CloseDatabase();
                        LoadDatabase(databasePath);
                        count = 0;
                    }

                    if ((SQLiteErrorCode)e.ErrorCode != SQLiteErrorCode.Busy)
                    {
                        Console.WriteLine($"PlexDB ExecuteDBQuery SQLite error code {e.ErrorCode}: {e.Message}");
                    }
                    Thread.Sleep(10);
                }
            }
        }

        public void CloseDatabase()
        {
            try
            {
                if (sqlite_conn != null && sqlite_conn.State != System.Data.ConnectionState.Closed)
                {
                    sqlite_conn.Close();
                    sqlite_conn.Dispose();
                    sqlite_conn = null;
                }
            }
            catch { }
        }


        public long GetTagID()
        {
            if (TagID < 0)
            {
                var result = ExecuteDBQuery("SELECT id FROM tags WHERE tag_type = 12 LIMIT 1;");

                if (!result.Read())
                {
                    Console.WriteLine("Couldn't get tag_id from Plex Database. Make sure you've set up intro scanning and Plex has scanned at least one show before turning off Plex's default scanning.");
                    Program.Exit();
                    return -1;
                }

                TagID = result.Get<long>("id");

                if (TagID < 0)
                {
                    Console.WriteLine("Couldn't get tag_id from Plex Database. Make sure you've set up intro scanning and Plex has scanned at least one show before turning off Plex's default scanning.");
                    Program.Exit();
                    return -1;
                }
            }

            return TagID;
        }

        public long GetMetadataID(Episode ep)
        {
            var result = ExecuteDBQuery("SELECT metadata_item_id " +
                "FROM media_items " +
                "LEFT JOIN media_parts " +
                "ON media_items.id = media_parts.media_item_id " +
                "WHERE " +
                "media_parts.file LIKE @fileWin OR media_parts.file LIKE @fileLin LIMIT 1;", new Dictionary<string, object>()
                {
                    { "fileWin", $"%{Program.GetWinStylePath(ep.id)}" },
                    { "fileLin", $"%{Program.GetLinuxStylePath(ep.id)}" },
                });

            if (!result.Read())
            {
                return -1;
            }

            return result.Get<long>("metadata_item_id");
        }

        public Episode GetEpisodeForMetaID(long metadata_item_id)
        {
            var result = ExecuteDBQuery("SELECT file " +
                "FROM media_parts " +
                "LEFT JOIN media_items " +
                "ON media_items.id = media_parts.media_item_id " +
                "WHERE " +
                "media_items.metadata_item_id = @metadata_item_id LIMIT 1;", new Dictionary<string, object>()
                {
                    { "metadata_item_id", metadata_item_id }
                });

            if (!result.Read())
            {
                return null;
            }

            Episode ep = new Episode(result.Get<string>("file"));
            ep.meta_id = metadata_item_id;
            ep.InPlexDB = true;

            return ep;
        }

        public void DeleteExistingIntros(Episode ep)
        {
            DeleteExistingIntros(ep.meta_id);
        }

        public void DeleteExistingIntros(long epMetaID)
        {
            long tagid = GetTagID();
            if (epMetaID < 0 || tagid < 0)
            {
                return;
            }
            ExecuteDBCommand("DELETE FROM taggings WHERE `metadata_item_id` = @metadata_item_id AND `tag_id` = @tag_id AND `index` > 0;", new Dictionary<string, object>()
            {
                { "metadata_item_id", epMetaID },
                { "tag_id", tagid }
            });
        }


        public void Insert(long metadata_item_id, Segment segment, int index)
        {
            long tagid = GetTagID();

            if (tagid < 0 || metadata_item_id < 0)
            {
                return;
            }

            ExecuteDBCommand("INSERT INTO taggings " +
                "(`metadata_item_id`, `tag_id`, `index`, `text`, `time_offset`, `end_time_offset`, `thumb_url`, `created_at`,       `extra_data`) VALUES " +
                "(@metadata_item_id,  @tag_id,  @index,  @text,  @time_offset,  @end_time_offset,  @thumb_url,   CURRENT_TIMESTAMP, @extra_data);", new Dictionary<string, object>()
            {
                { "metadata_item_id", metadata_item_id },
                { "tag_id", tagid },
                { "index", index },
                { "text", "intro" },
                { "time_offset", (int)(segment.start * 1000) },
                { "end_time_offset", (int)(segment.end * 1000) },
                { "thumb_url", "" },
                { "extra_data", "pv%3Aversion=5" }
            });

        }

        public int GetNonPlexIntroTimingsCount(long metadata_item_id)
        {
            long tagid = GetTagID();

            if (tagid < 0 || metadata_item_id < 0)
            {
                return 0;
            }

            var result = ExecuteDBQuery("SELECT COUNT(*) as counted FROM taggings WHERE `metadata_item_id` = @metadata_item_id AND `tag_id` = @tag_id AND `index` > 0;", new Dictionary<string, object>()
            {
                { "metadata_item_id", metadata_item_id },
                { "tag_id", tagid }
            });

            if (!result.Read())
            {
                return 0;
            }

            return result.Get<int>("counted");
        }


        public Segment GetPlexIntroTimings(long metadata_item_id)
        {
            long tagid = GetTagID();

            if (tagid < 0 || metadata_item_id < 0)
            {
                return null;
            }

            var result = ExecuteDBQuery("SELECT `time_offset`, `end_time_offset` FROM taggings WHERE `metadata_item_id` = @metadata_item_id AND `tag_id` = @tag_id AND `index` = @index LIMIT 1;", new Dictionary<string, object>()
            {
                { "metadata_item_id", metadata_item_id },
                { "tag_id", tagid },
                { "index", 0 }
            });

            if (!result.Read())
            {
                return null;
            }

            Segment segment = new Segment();
            segment.isCredits = false;
            segment.isSilence = false;
            segment.isBlackframes = false;

            segment.start = result.Get<double>("time_offset") / 1000.0;
            segment.end = result.Get<double>("end_time_offset") / 1000.0;

            return segment;
        }

        public class RecentIntroData
        {
            public long metadata_item_id;
            public Episode episode;
            public Segment segment = new Segment();
            public DateTime created;
        }

        public List<RecentIntroData> GetRecentPlexIntroTimingsSingleQuery(DateTime since)
        {
            long tagid = GetTagID();

            if (tagid < 0)
            {
                return null;
            }

            var result = ExecuteDBQuery("SELECT taggings.metadata_item_id as metadata_item_id, media_parts.`file` as `file`, taggings.`time_offset` as `time_offset`, taggings.`end_time_offset` as `end_time_offset`, taggings.`created_at` as `created_at` " +
                " FROM taggings " +
                " INNER JOIN media_items ON taggings.metadata_item_id = media_items.metadata_item_id " +
                " INNER JOIN media_parts ON media_items.id = media_parts.media_item_id " +
                " WHERE taggings.`created_at` > @created_at AND taggings.`tag_id` = @tag_id AND taggings.`index` = @index " +
                " ORDER BY created_at ASC LIMIT 100;", new Dictionary<string, object>()
            {
                { "created_at", since },
                { "tag_id", tagid },
                { "index", 0 }
            });

            if (!result.HasRows)
            {
                return null;
            }

            List<RecentIntroData> ret = new List<RecentIntroData>();

            while (result.Read())
            {
                RecentIntroData data = new RecentIntroData();

                data.metadata_item_id = result.Get<long>("metadata_item_id");

                data.segment.start = result.Get<double>("time_offset") / 1000.0;
                data.segment.end = result.Get<double>("end_time_offset") / 1000.0;
                data.segment.isCredits = false;
                data.segment.isSilence = false;
                data.segment.isBlackframes = false;

                data.episode = new Episode(result.Get<string>("file"));

                data.episode.meta_id = data.metadata_item_id;
                data.episode.InPlexDB = true;

                data.episode.plexTimings = data.segment;

                data.created = result.Get<DateTime>("created_at");

                ret.Add(data);
            }

            ret.Sort((a, b) => a.created.CompareTo(b.created)); // oldest to newest


            return ret;
        }
        public List<RecentIntroData> GetRecentPlexIntroTimings(DateTime since)
        {
            long tagid = GetTagID();

            if (tagid < 0)
            {
                return null;
            }

            var result = ExecuteDBQuery("SELECT metadata_item_id, `time_offset`, `end_time_offset`, `created_at` " +
                "FROM taggings " +
                "WHERE `created_at` > @created_at AND `tag_id` = @tag_id AND `index` = @index ORDER BY created_at ASC LIMIT 100;", new Dictionary<string, object>()
            {
                { "created_at", since },
                { "tag_id", tagid },
                { "index", 0 }
            });

            if (!result.HasRows)
            {
                return null;
            }

            List<RecentIntroData> ret = new List<RecentIntroData>();

            while (result.Read())
            {
                RecentIntroData data = new RecentIntroData();

                data.metadata_item_id = result.Get<long>("metadata_item_id");

                data.segment.start = result.Get<double>("time_offset") / 1000.0;
                data.segment.end = result.Get<double>("end_time_offset") / 1000.0;
                data.segment.isCredits = false;
                data.segment.isSilence = false;
                data.segment.isBlackframes = false;

                data.created = result.Get<DateTime>("created_at");

                data.episode = GetEpisodeForMetaID(data.metadata_item_id);

                ret.Add(data);
            }

            ret.Sort((a, b) => a.created.CompareTo(b.created)); // oldest to newest


            return ret;
        }



        public void NewParentActivity()
        {
            EndActivity();

            ExecuteDBCommand("INSERT INTO activities " +
                "(`type`, `title`, `subtitle`, `started_at`, `cancelled`) VALUES " +
                "(@type,  @title,  @subtitle,  @started_at,  @cancelled);", new Dictionary<string, object>()
            {
                { "type", "media.generate.intros" },
                { "title", "Detecting credits" },
                { "subtitle", "Detecting credits" },
                { "started_at", (new DateTimeOffset(DateTime.Now)).ToUnixTimeSeconds() },
                { "cancelled", 0 }
            });

            parentActivityID = sqlite_conn.LastInsertRowId;
        }

        public void NewActivity(string subtitle)
        {
            EndActivity();

            if (parentActivityID < 0)
            {
                NewParentActivity();
            }

            ExecuteDBCommand("INSERT INTO activities " +
                "(`parent_id`, `type`, `title`, `subtitle`, `started_at`, `cancelled`) VALUES " +
                "(@parent_id,  @type,  @title,  @subtitle,  @started_at,  @cancelled);", new Dictionary<string, object>()
            {
                { "parent_id", parentActivityID },
                { "type", "media.generate.intros" },
                { "title", "Detecting credits" },
                { "subtitle", subtitle },
                { "started_at", (new DateTimeOffset(DateTime.Now)).ToUnixTimeSeconds() },
                { "cancelled", 0 }
            });

            currentActivityID = sqlite_conn.LastInsertRowId;
        }

        public void EndActivity()
        {

            if (currentActivityID < 0)
            {
                return;
            }

            ExecuteDBCommand(" UPDATE activities SET " +
                " `finished_at` =  @finished_at " +
                " WHERE `id` = @id;", new Dictionary<string, object>()
            {
                { "finished_at", (new DateTimeOffset(DateTime.Now)).ToUnixTimeSeconds() },
                { "id", currentActivityID }
            });

            currentActivityID = -1;
        }


        public class ShowSeasonInfo
        {
            public long showID = -1;
            public long seasonID = -1;

            public string showName = "";
            public int seasonNumber = -1;
            public string seasonName = "";
        }

        private bool GetMetadataItemByID(long metadata_item_id, ShowSeasonInfo ret)
        {
            if (metadata_item_id < 0)
            {
                return false;
            }

            var result = ExecuteDBQuery("SELECT `parent_id`, `metadata_type`, `title`, `index` FROM metadata_items WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", metadata_item_id }
            });

            if (!result.Read())
            {
                return false;
            }

            int type = result.Get<int>("metadata_type");
            string title = result.Get<string>("title");
            int index = result.Get<int>("index");

            switch (type)
            {
                case 2: // show
                    ret.showID = metadata_item_id;
                    ret.showName = title;
                    break;
                case 3: // season
                    ret.showID = result.Get<int>("parent_id");
                    ret.seasonID = metadata_item_id;
                    ret.seasonName = title;
                    ret.seasonNumber = index;
                    break;
                case 4: //episode
                    ret.seasonID = result.Get<int>("parent_id");
                    break;
                default:
                    return false;
            }

            return true;

        }

        private Dictionary<long, RootDirectory> GetRootDirectories()
        {
            Dictionary<long, RootDirectory> ret = new Dictionary<long, RootDirectory>();

            var result = ExecuteDBQuery("SELECT * FROM section_locations;");

            if (!result.HasRows)
            {
                return ret;
            }

            while (result.Read())
            {
                var root = new RootDirectory();
                root.path = result.Get<string>("root_path");
                root.library_section_id = result.Get<long>("library_section_id");
                ret[root.library_section_id] = root;
            }

            return ret;
        }

        private Dictionary<long, string> GetMovieRoots()
        {
            Dictionary<long, string> ret = new Dictionary<long, string>();

            var result = ExecuteDBQuery("SELECT * FROM library_sections;");

            if (!result.HasRows)
            {
                return ret;
            }

            while (result.Read())
            {
                long id = result.Get<long>("id");

                if (RootDirectories.ContainsKey(id))
                {
                    RootDirectories[id].section_type = result.Get<int>("section_type");
                }
            }

            return ret;
        }

        public Dictionary<string, DateTime> GetRecentlyModifiedDirectories(DateTime since)
        {
            Dictionary<string, DateTime> ret = new Dictionary<string, DateTime>();

            var result = ExecuteDBQuery("SELECT * FROM directories " +
                " WHERE `updated_at` > @updated_at ORDER BY updated_at ASC LIMIT 100;", new Dictionary<string, object>()
            {
                { "updated_at", since }
            });

            if (!result.HasRows)
            {
                return ret;
            }

            while (result.Read())
            {
                string dir = result.Get<string>("path");
                long id = result.Get<long>("library_section_id");
                bool valid = true;

                if (dir == "")
                {
                    continue;
                }

                if (RootDirectories.ContainsKey(id))
                {
                    if (RootDirectories[id].section_type > 2)
                    {
                        valid = false;
                    }
                    else
                    {
                        dir = Program.PathCombine(Program.plexBasePathToLocalBasePath(RootDirectories[id].path), dir);
                    }
                }
                else
                {
                    dir = Program.getFullDirectory(dir);
                }

                if (valid)
                {
                    ret.Add(dir, result.Get<DateTime>("updated_at"));
                }
            }
            return ret;
        }

        public ShowSeasonInfo GetShowAndSeason(long metadata_item_id)
        {
            ShowSeasonInfo ret = new ShowSeasonInfo();

            if (GetMetadataItemByID(metadata_item_id, ret))
            {
                if (GetMetadataItemByID(ret.seasonID, ret))
                {
                    GetMetadataItemByID(ret.showID, ret);
                }
            }

            return ret;
        }


    }
}
