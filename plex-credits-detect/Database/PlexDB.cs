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


        public PlexDB()
        {
        }
        public PlexDB(string path)
        {
            LoadDatabase(path);
        }

        public void LoadDatabase(string path)
        {
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

            try
            {
                sqlite_conn.Open();
            }
            catch (SQLiteException e)
            {
                Console.WriteLine($"PlexDB LoadDatabase SQLite error code {e.ErrorCode}: {e.Message}");
                Thread.Sleep(10);
                Program.Exit();
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

            while (true)
            {
                try
                {
                    return sqlite_cmd.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Console.WriteLine($"PlexDB ExecuteDBCommand SQLite error code {e.ErrorCode}: {e.Message}");
                    Thread.Sleep(10);
                }
            }
        }

        public SQLiteDataReader ExecuteDBQuery(string cmd, Dictionary<string, object> p = null)
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

            while (true)
            {
                try
                {
                    return sqlite_cmd.ExecuteReader();
                }
                catch (SQLiteException e)
                {
                    Console.WriteLine($"PlexDB ExecuteDBQuery SQLite error code {e.ErrorCode}: {e.Message}");
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

                if (result == null || !result.HasRows || !result.Read())
                {
                    Console.WriteLine("Couldn't get tag_id from Plex Database. Make sure you've set up intro scanning and Plex has scanned at least one show before turning off Plex's default scanning.");
                    Program.Exit();
                    return -1;
                }

                TagID = result.GetInt64(0);

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

            if (result == null || !result.HasRows || !result.Read())
            {
                return -1;
            }

            return result.GetInt64(0);
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

            if (result == null || !result.HasRows || !result.Read())
            {
                return null;
            }

            return new Episode(result.GetString(0));
        }

        public void DeleteExistingIntros(Episode ep)
        {
            DeleteExistingIntros(GetMetadataID(ep));
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


        public void Insert(long metadata_item_id, Episode.Segment segment, int index)
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

            if (result == null || !result.HasRows || !result.Read())
            {
                return 0;
            }

            return result.GetInt32(0);
        }


        public Episode.Segment GetPlexIntroTimings(long metadata_item_id)
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

            if (result == null || !result.HasRows || !result.Read())
            {
                return null;
            }

            Episode.Segment segment = new Episode.Segment();
            segment.isCredits = false;

            segment.start = result.GetDouble(0) / 1000.0;
            segment.end = result.GetDouble(1) / 1000.0;

            return segment;
        }

        public class RecentIntroData
        {
            public long metadata_item_id;
            public Episode episode;
            public Episode.Segment segment = new Episode.Segment();
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
                "FROM taggings " +
                "INNER JOIN media_items ON taggings.metadata_item_id = media_items.metadata_item_id " +
                "INNER JOIN media_parts ON media_items.id = media_parts.media_item_id " +
                "WHERE taggings.`created_at` > @created_at AND taggings.`tag_id` = @tag_id AND taggings.`index` = @index " +
                "ORDER BY created_at ASC LIMIT 100;", new Dictionary<string, object>()
            {
                { "created_at", since },
                { "tag_id", tagid },
                { "index", 0 }
            });

            if (result == null || !result.HasRows)
            {
                return null;
            }

            List<RecentIntroData> ret = new List<RecentIntroData>();

            while (result.Read())
            {
                RecentIntroData data = new RecentIntroData();

                data.metadata_item_id = result.GetInt64(0);

                data.episode = new Episode(result.GetString(1));

                data.segment.start = result.GetDouble(2) / 1000.0;
                data.segment.end = result.GetDouble(3) / 1000.0;
                data.segment.isCredits = false;

                data.created = result.GetDateTime(4);

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

            if (result == null || !result.HasRows)
            {
                return null;
            }

            List<RecentIntroData> ret = new List<RecentIntroData>();

            while (result.Read())
            {
                RecentIntroData data = new RecentIntroData();

                data.metadata_item_id = result.GetInt64(0);

                data.segment.start = result.GetDouble(1) / 1000.0;
                data.segment.end = result.GetDouble(2) / 1000.0;
                data.segment.isCredits = false;

                data.created = result.GetDateTime(3);

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

            if (result == null || !result.HasRows || !result.Read())
            {
                return false;
            }

            int type = result.GetInt32(1);
            string title = result.GetString(2);
            int index = result.GetInt32(3);

            switch (type)
            {
                case 2: // show
                    ret.showID = metadata_item_id;
                    ret.showName = title;
                    break;
                case 3: // season
                    ret.showID = result.GetInt32(0);
                    ret.seasonID = metadata_item_id;
                    ret.seasonName = title;
                    ret.seasonNumber = index;
                    break;
                case 4: //episode
                    ret.seasonID = result.GetInt32(0);
                    break;
                default:
                    return false;
            }

            return true;

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
