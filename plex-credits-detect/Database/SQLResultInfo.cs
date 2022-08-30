using System.Data.SQLite;
using System.Diagnostics;

namespace plexCreditsDetect.Database
{
    public class SQLResultInfo : IDisposable
    {
        public SQLiteDataReader reader = null;

        public Dictionary<string, int> columns = new Dictionary<string, int>();

        public bool HasRows => reader?.HasRows ?? false;

        public bool Read()
        {
            return reader?.Read() ?? false;
        }

        public bool GetBool(string v)
        {
            if (reader == null)
            {
                return false;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");
                
            return reader.GetBoolean(columns[v]);
        }
        public int GetInt(string v)
        {
            if (reader == null)
            {
                return -1;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return reader.GetInt32(columns[v]);
        }

        public long GetLong(string v)
        {
            if (reader == null)
            {
                return -1;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return reader.GetInt64(columns[v]);
        }
        public short GetShort(string v)
        {
            if (reader == null)
            {
                return -1;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return reader.GetInt16(columns[v]);
        }
        public double GetDouble(string v)
        {
            if (reader == null)
            {
                return -1;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return reader.GetDouble(columns[v]);
        }
        public string GetString(string v)
        {
            if (reader == null)
            {
                return "";
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return reader.GetString(columns[v]);
        }
        public DateTime GetDateTime(string v)
        {
            if (reader == null)
            {
                return DateTime.MinValue;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return reader.GetDateTime(columns[v]);
        }
        public DateTime GetUnixDateTime(string v)
        {
            if (reader == null)
            {
                return DateTime.MinValue;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");

            return DateTime.FromFileTimeUtc(reader.GetInt64(columns[v]));
        }

        public T Get<T>(string v)
        {
            if (typeof(bool) == typeof(T))
            {
                return (T)(object)GetBool(v);
            }
            if (typeof(int) == typeof(T))
            {
                return (T)(object)GetInt(v);
            }
            if (typeof(long) == typeof(T))
            {
                return (T)(object)GetLong(v);
            }
            if (typeof(short) == typeof(T))
            {
                return (T)(object)GetShort(v);
            }
            if (typeof(double) == typeof(T))
            {
                return (T)(object)GetDouble(v);
            }
            if (typeof(string) == typeof(T))
            {
                return (T)(object)GetString(v);
            }
            if (typeof(DateTime) == typeof(T))
            {
                return (T)(object)GetDateTime(v);
            }

            throw new NotImplementedException("Generic SQLResultInfo.Get not supported for datatype: " + typeof(T).FullName);
        }

        public bool IsDBNull(string v)
        {
            if (reader == null)
            {
                return true;
            }
            Debug.Assert(columns.ContainsKey(v), $"SQLResultInfo doesn't contain key {v}");


            return reader.IsDBNull(columns[v]);
        }

        ~SQLResultInfo() // don't rely on GC, but make sure to clean up in case it gets missed somehow
        {
            Dispose();
        }

        public void Dispose()
        {
            if (reader != null && !reader.IsClosed)
            { 
                reader.Close();
            }
            reader = null;
        }
    }
}
