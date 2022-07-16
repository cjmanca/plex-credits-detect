using SoundFingerprinting.Data;
using SoundFingerprinting.InMemory;
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoundFingerprinting;

namespace plexCreditsDetect.Database
{
    internal class InMemoryFingerprintDatabase : IFingerprintDatabase
    {
        InMemoryModelService modelService = null;
        SQLiteConnection sqlite_conn = null;


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

            try
            {
                SQLiteConnectionStringBuilder sb = new SQLiteConnectionStringBuilder();
                sb.DataSource = Program.PathCombine(path, "fingerprintMedia.db");
                sb.Version = 3;
                sb.FailIfMissing = false;

                sqlite_conn = new SQLiteConnection(sb.ToString());

                sqlite_conn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("InMemoryFingerprintDatabase.LoadDatabase exception: " + ex.Message);
            }

            ExecuteDBCommand("CREATE TABLE IF NOT EXISTS ScannedMedia (id TEXT NOT NULL PRIMARY KEY, name TEXT NOT NULL, dir TEXT NOT NULL, LastWriteTimeUtc INTEGER, FileSize INTEGER, DetectionPending BOOLEAN);");
            ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_dir ON ScannedMedia(dir);");
            ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_DetectionPending ON ScannedMedia(DetectionPending);");

            ExecuteDBCommand("CREATE TABLE IF NOT EXISTS ScannedMedia_Timings (id INTEGER PRIMARY KEY, ScannedMedia_id TEXT NOT NULL REFERENCES ScannedMedia(id), is_plex_intro BOOLEAN NOT NULL, time_offset DOUBLE NOT NULL, end_time_offset DOUBLE NOT NULL, isCredits BOOLEAN);");
            ExecuteDBCommand("CREATE INDEX IF NOT EXISTS idx_ScannedMedia_Timings_ScannedMedia_id ON ScannedMedia_Timings(ScannedMedia_id);");

            ExecuteDBCommand("CREATE TABLE IF NOT EXISTS GlobalData (id TEXT PRIMARY KEY, StringData TEXT, DateData DATETIME, DoubleData DOUBLE, IntData INTEGER);");

        }

        public int GetIntData(string id)
        {
            var result = ExecuteDBQuery("SELECT IntData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            });

            if (result == null || !result.HasRows || !result.Read())
            {
                return -1;
            }

            return result.GetInt32(0);
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
            var result = ExecuteDBQuery("SELECT StringData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            });

            if (result == null || !result.HasRows || !result.Read())
            {
                return "";
            }

            return result.GetString(0);
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
            var result = ExecuteDBQuery("SELECT DateData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            });

            if (result == null || !result.HasRows || !result.Read())
            {
                return DateTime.MinValue;
            }

            return result.GetDateTime(0);
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
            var result = ExecuteDBQuery("SELECT DoubleData FROM GlobalData WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>()
            {
                { "id", id }
            });

            if (result == null || !result.HasRows || !result.Read())
            {
                return -1;
            }

            return result.GetDouble(0);
        }

        public void SetDoubleData(string id, double data)
        {
            ExecuteDBCommand("REPLACE INTO GlobalData (`id`, DoubleData) VALUES (@id, @data);", new Dictionary<string, object>()
            {
                { "id", id },
                { "data", data },
            });
        }


        public int ExecuteDBCommand(string cmd, Dictionary<string, object> p = null)
        {
            try
            {
                var sqlite_cmd = sqlite_conn.CreateCommand();
                sqlite_cmd.CommandText = cmd;
                sqlite_cmd.CommandType = System.Data.CommandType.Text;

                if (p != null)
                {
                    foreach(var row in p)
                    {
                        sqlite_cmd.Parameters.Add(new SQLiteParameter("@" + row.Key, row.Value));
                    }
                }

                return sqlite_cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("InMemoryFingerprintDatabase.ExecuteDBCommand exception: " + ex.Message + "" +
                    " while executing SQL: " + cmd);
                if (p != null && p.Count > 0)
                {
                    Console.WriteLine("With data: ");

                    foreach (var x in p)
                    {
                        Console.WriteLine($"{x.Key} = {x.Value}");
                    }
                }
            }
            return -1;
        }

        public SQLiteDataReader ExecuteDBQuery(string cmd, Dictionary<string, object> p = null)
        {
            try
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

                return sqlite_cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                Console.WriteLine("InMemoryFingerprintDatabase.ExecuteDBCommand exception: " + ex.Message + "" +
                    " while executing SQL: " + cmd);
                if (p != null && p.Count > 0)
                {
                    Console.WriteLine("With data: ");

                    foreach (var x in p)
                    {
                        Console.WriteLine($"{x.Key} = {x.Value}");
                    }
                }
            }
            return null;
        }

        public void CloseDatabase()
        {
            try
            {
                sqlite_conn.Close();
                sqlite_conn.Dispose();
            }
            catch { }

            if (modelService != null)
            {
                modelService = null;
            }
        }

        public AVHashes GetTrackHash(string id, bool isCredits)
        {
            return modelService.ReadHashesByTrackId(id + isCredits.ToString());
        }
        public Episode GetEpisode(string id)
        {
            var result = ExecuteDBQuery("SELECT id, name, dir, LastWriteTimeUtc, FileSize, DetectionPending " +
                "FROM ScannedMedia WHERE id = @id;", new Dictionary<string, object>()
            {
                { "id", id }
            });

            if (result == null || !result.HasRows || !result.Read())
            {
                return null;
            }

            Episode ep = new Episode(id);
            ep.id = result.GetString(0);
            ep.name = result.GetString(1);
            ep.dir = result.GetString(2);
            ep.LastWriteTimeUtc = DateTime.FromFileTimeUtc(result.GetInt64(3));
            ep.FileSize = result.GetInt64(4);
            ep.DetectionPending = result.GetBoolean(5);

            return ep;
        }

        public List<Episode> GetPendingEpisodes()
        {
            List<Episode> eps = new List<Episode>();


            var result = ExecuteDBQuery("SELECT id, name, dir, LastWriteTimeUtc, FileSize, DetectionPending " +
                "FROM ScannedMedia WHERE DetectionPending = TRUE;");

            if (result == null || !result.HasRows)
            {
                return null;
            }

            while (result.Read())
            {
                string id = result.GetString(0);

                Episode ep = new Episode(id);
                ep.id = id;
                ep.name = result.GetString(1);
                ep.dir = result.GetString(2);
                ep.LastWriteTimeUtc = DateTime.FromFileTimeUtc(result.GetInt64(3));
                ep.FileSize = result.GetInt64(4);
                ep.DetectionPending = result.GetBoolean(5);

                eps.Add(ep);
            }

            return eps;
        }
        public Episode GetOnePendingEpisode()
        {
            while (true)
            {
                var result = ExecuteDBQuery("SELECT id, name, dir, LastWriteTimeUtc, FileSize, DetectionPending " +
                    "FROM ScannedMedia WHERE DetectionPending = TRUE LIMIT 1;");

                if (result == null || !result.HasRows || !result.Read())
                {
                    return null;
                }

                string id = result.GetString(0);

                Episode ep = new Episode(id);
                ep.id = id;
                ep.name = result.GetString(1);
                ep.dir = result.GetString(2);
                ep.LastWriteTimeUtc = DateTime.FromFileTimeUtc(result.GetInt64(3));
                ep.FileSize = result.GetInt64(4);
                ep.DetectionPending = result.GetBoolean(5);

                return ep;
            }
        }

        public Dictionary<string, Episode> GetPendingDirectories()
        {
            Dictionary <string, Episode> episodes = new Dictionary <string, Episode>();

            var result = ExecuteDBQuery("SELECT id, name, dir, LastWriteTimeUtc, FileSize, DetectionPending " +
                "FROM ScannedMedia WHERE DetectionPending = TRUE;");

            if (result == null || !result.HasRows)
            {
                return null;
            }

            while (result.Read())
            {
                string id = result.GetString(0);

                Episode ep = new Episode(id);
                ep.id = id;
                ep.name = result.GetString(1);
                ep.dir = result.GetString(2);
                ep.LastWriteTimeUtc = DateTime.FromFileTimeUtc(result.GetInt64(3));
                ep.FileSize = result.GetInt64(4);
                ep.DetectionPending = result.GetBoolean(5);

                if (ep.Exists && !episodes.ContainsKey(ep.fullDirPath))
                {
                    episodes[ep.fullDirPath] = ep;
                }
            }

            return episodes;
        }
        public void DeleteEpisode(string id)
        {
            ExecuteDBCommand("DELETE FROM ScannedMedia WHERE id = @id;", new Dictionary<string, object>()
            {
                { "id", id }
            });
            try
            {
                modelService.DeleteTrack(id + true.ToString());
                modelService.DeleteTrack(id + false.ToString());
                DeleteEpisodeTimings(id);
            }
            catch { }
        }

        public void DeleteEpisodeTimings(string id)
        {
            ExecuteDBCommand("DELETE FROM ScannedMedia_Timings WHERE ScannedMedia_id = @ScannedMedia_id;", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", id }
            });
        }
        public void DeleteEpisodePlexTimings(string id)
        {
            ExecuteDBCommand("DELETE FROM ScannedMedia_Timings WHERE ScannedMedia_id = @ScannedMedia_id AND is_plex_intro = @is_plex_intro;", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", id },
                { "is_plex_intro", true }
            });
        }

        public List<Episode.Segment> GetNonPlexTimings(Episode ep)
        {
            List<Episode.Segment> segments = new List<Episode.Segment>();

            var result = ExecuteDBQuery("SELECT time_offset, end_time_offset, isCredits " +
                "FROM ScannedMedia_Timings WHERE ScannedMedia_id = @ScannedMedia_id AND is_plex_intro = @is_plex_intro;", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", ep.id },
                { "is_plex_intro", false }
            });

            if (result == null || !result.HasRows)
            {
                return null;
            }

            while (result.Read())
            {
                Episode.Segment seg = new Episode.Segment();

                seg.start = result.GetDouble(0);
                seg.end = result.GetDouble(1);

                seg.isCredits = result.GetBoolean(2);

                segments.Add(seg);
            }

            return segments;
        }

        public void Insert(Episode ep)
        {
            ExecuteDBCommand("REPLACE INTO ScannedMedia " +
                "(id, name, dir, LastWriteTimeUtc, FileSize, DetectionPending) VALUES " +
                "(@id, @name, @dir, @LastWriteTimeUtc, @FileSize, @DetectionPending);", new Dictionary<string, object>()
            {
                { "id", ep.id },
                { "name", ep.name },
                { "dir", ep.dir },
                { "LastWriteTimeUtc", ep.LastWriteTimeUtc.ToFileTimeUtc() },
                { "FileSize", ep.FileSize },
                { "DetectionPending", ep.DetectionPending }
            });
        }

        public void InsertTiming(Episode ep, Episode.Segment segment, bool isPlexIntro)
        {
            ExecuteDBCommand("INSERT INTO ScannedMedia_Timings " +
                "(ScannedMedia_id, is_plex_intro, time_offset, end_time_offset, isCredits) VALUES " +
                "(@ScannedMedia_id, @is_plex_intro, @time_offset, @end_time_offset, @isCredits);", new Dictionary<string, object>()
            {
                { "ScannedMedia_id", ep.id },
                { "is_plex_intro", isPlexIntro },
                { "time_offset", segment.start },
                { "end_time_offset", segment.end },
                { "isCredits", segment.isCredits }
            });
        }


        public void InsertHash(Episode ep, AVHashes hashes, MediaType avtype, bool isCredits, double start)
        {
            TrackInfo trackinfo = new TrackInfo(ep.id + isCredits.ToString(), ep.name, ep.dir, new Dictionary<string, string>()
            {
                { "name", ep.name },
                { "dir", ep.dir },
                { "LastWriteTimeUtc", ep.LastWriteTimeUtc.ToFileTimeUtc().ToString() },
                { "FileSize", ep.FileSize.ToString() },
                { "start", start.ToString() },
                { "isCredits", isCredits.ToString() },
                { "DetectionPending", ep.DetectionPending.ToString() }
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
