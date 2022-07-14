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

            sqlite_conn = new SQLiteConnection(sb.ToString());

            while (sqlite_conn.State == System.Data.ConnectionState.Closed)
            {
                try
                {
                    sqlite_conn.Open();
                    return;
                }
                catch (SQLiteException e)
                {
                    Console.WriteLine($"LoadDatabase SQLite error code {e.ErrorCode}: {e.Message}");
                    Thread.Sleep(10);
                }
            }
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
                    Console.WriteLine($"ExecuteDBCommand SQLite error code {e.ErrorCode}: {e.Message}");
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
                    Console.WriteLine($"ExecuteDBQuery SQLite error code {e.ErrorCode}: {e.Message}");
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
                "media_parts.file = @file LIMIT 1;", new Dictionary<string, object>()
                {
                    { "file", ep.fullPath }
                });

            if (result == null || !result.HasRows || !result.Read())
            {
                Console.WriteLine("Couldn't find episode in Plex database: " + ep.id);

                return -1;
            }

            return result.GetInt64(0);
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
            ExecuteDBCommand("DELETE FROM taggings WHERE metadata_item_id = @metadata_item_id AND tag_id = @tag_id;", new Dictionary<string, object>()
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
                "(`metadata_item_id`, `tag_id`, `index`, `text`, `time_offset`, `end_time_offset`, `created_at`,      `extra_data`) VALUES " +
                "(@metadata_item_id,  @tag_id,  @index,  @text,  @time_offset,  @end_time_offset,  CURRENT_TIMESTAMP, @extra_data);", new Dictionary<string, object>()
            {
                { "metadata_item_id", metadata_item_id },
                { "tag_id", tagid },
                { "index", index },
                { "text", "intro" },
                { "time_offset", (int)(segment.start * 1000) },
                { "end_time_offset", (int)(segment.end * 1000) },
                { "extra_data", "pv%3Aversion=5" }
            });

        }
    }
}
