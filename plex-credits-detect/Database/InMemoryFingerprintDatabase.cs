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

        public AVHashes GetTrackHash(string id)
        {
            return modelService.ReadHashesByTrackId(id);
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

        public void DeleteEpisode(string id)
        {
            ExecuteDBCommand("DELETE FROM ScannedMedia WHERE id = '@id';", new Dictionary<string, object>()
            {
                { "id", id }
            });
            try
            {
                modelService.DeleteTrack(id);
            }
            catch { }
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

        public void InsertHash(Episode ep, AVHashes hashes)
        {
            TrackInfo trackinfo = new TrackInfo(ep.id, ep.name, ep.dir, new Dictionary<string, string>()
            {
                { "name", ep.name },
                { "dir", ep.dir },
                { "LastWriteTimeUtc", ep.LastWriteTimeUtc.ToFileTimeUtc().ToString() },
                { "FileSize", ep.FileSize.ToString() },
                { "DetectionPending", ep.DetectionPending.ToString() }
            });

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
