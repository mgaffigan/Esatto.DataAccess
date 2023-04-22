using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Xml;

namespace Esatto.DataAccess
{
    public class ResultSet : IDisposable
    {
        private readonly SqlDataReader sdr;
        private readonly SqlCommand sqc;
        private readonly SqlConnection con;
        private readonly DbCommand dbcommand;
        internal readonly DataReaderOptions ReaderOptions;

        public SqlDataReader DataReader => sdr;

        internal ResultSet(SqlDataReader sdr, SqlConnection con, SqlCommand sqc, DbCommand command, DataReaderOptions readerOptions)
        {
            this.sdr = sdr;
            this.con = con;
            this.sqc = sqc;
            this.dbcommand = command;
            this.ReaderOptions = readerOptions;
        }

        public ResultSet FillObject<T>(out T obj, string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
        {
            //check that we have data to load
            if (!sdr.Read())
            {
                obj = default(T);
            }
            else
            {
                obj = ObjectFiller.GetSingleResult<T>(sdr, prefix, action, this);
            }

            return this;
        }

        public ResultSet FillPartialObject<T>(out T obj, string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
        {
            obj = ObjectFiller.GetSingleResult<T>(sdr, prefix, action, this);

            return this;
        }

        public ResultSet FillList<T>(out List<T> obj, string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
        {
            obj = ObjectFiller.GetResults<T>(sdr, prefix, action, this);

            return this;
        }

        public List<T> GetList<T>(Func<ResultSet, T> ctor)
        {
            var results = new List<T>();
            while (sdr.Read())
            {
                results.Add(ctor(this));
            }
            return results;
        }

        public Dictionary<TKey, List<TValue>> GetMappedList<TKey, TValue>(
            Func<ResultSet, TKey> keyCtor, Func<ResultSet, TValue> valueCtor, 
            IEqualityComparer<TKey>? keyComparer = null)
        {
            var lookup = new Dictionary<TKey, List<TValue>>(keyComparer);
            while (sdr.Read())
            {
                var key = keyCtor(this);
                List<TValue> list;
                if (!lookup.TryGetValue(key, out list))
                {
                    lookup[key] = list = new List<TValue>();
                }

                list.Add(valueCtor(this));
            }
            return lookup;
        }

        public List<T> GetList<T>(string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
        {
            List<T> results;
            FillList<T>(out results, prefix, action);

            return results;
        }

        public ResultSet ForEachRow(Action<ResultSet> action)
        {
            while (sdr.Read())
            {
                action(this);
            }

            return this;
        }

        public IEnumerable<T> ForEachRow<T>(Func<ResultSet, T> action)
        {
            while (sdr.Read())
            {
                yield return action(this);
            }
        }

        public ResultSet ForEachObject<T>(string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
        {
            ObjectFiller.GetResults<T>(sdr, prefix, action, this);

            return this;
        }

        public T GetPartialObject<T>(string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
        {
            T obj = default(T);
            FillPartialObject<T>(out obj, prefix, action);
            return obj;
        }

        public T GetObject<TException, T>(string? prefix = null, Action<T, ResultSet>? action = null) 
            where T : new()
            where TException : Exception, new()
        {
            ReadOrFail<TException>();

            return ObjectFiller.GetSingleResult<T>(sdr, prefix, action, this);
        }

        public ResultSet ReadOrFail<TException>()
             where TException : Exception, new()
        {
            if (!sdr.Read())
            {
                throw new TException();
            }

            return this;
        }

        public bool Read()
        {
            return sdr.Read();
        }

        public ResultSet NextResult()
        {
            sdr.NextResult();

            return this;
        }

        /// <summary>
        /// Use closereader before end to close the sqldatareader but not the connection.  This will also load output parameters.
        /// </summary>
        public void CloseReader()
        {
            if (sdr != null && !sdr.IsClosed)
            {
                sdr.Close();
                if (dbcommand != null)
                {
                    dbcommand.LoadParameters(sqc);
                }
            }
        }

        public void End()
        {
            if (sdr != null && !sdr.IsClosed)
            {
                sdr.Close();
            }
            if (con != null)
            {
                con.Close();
            }
        }

        public int GetInt(string name)
        {
            return sdr.GetInt32(sdr.GetOrdinal(name));
        }

        public long GetLong(string name)
        {
            return sdr.GetInt64(sdr.GetOrdinal(name));
        }

        public TimeSpan GetTimeSpan(string name)
        {
            return sdr.GetTimeSpan(sdr.GetOrdinal(name));
        }

        public object Get(string name)
        {
            return sdr[name];
        }

        public int GetIntNull(string name, int nullvalue)
        {
            int index = sdr.GetOrdinal(name);
            if (sdr.IsDBNull(index))
            {
                return nullvalue;
            }
            return sdr.GetInt32(index);
        }

        public int? GetIntNull(string name)
        {
            int index = sdr.GetOrdinal(name);
            if (sdr.IsDBNull(index))
            {
                return null;
            }
            return sdr.GetInt32(index);
        }

        public bool IsNull(string name)
        {
            return sdr.IsDBNull(sdr.GetOrdinal(name));
        }

        void IDisposable.Dispose()
        {
            End();
        }

        public decimal GetDecimal(string p)
        {
            return sdr.GetDecimal(sdr.GetOrdinal(p));
        }

        public string GetStringNotNull(string p) => GetString(p) ?? throw new InvalidOperationException($"Unexpected null for {p}");

        public string? GetString(string p)
        {
            int index = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(index))
            {
                return null;
            }

            var result = sdr.GetString(index);
            if (ReaderOptions.HasFlag(DataReaderOptions.TrimStringValues))
            {
                return result.Trim();
            }
            else
            {
                return result;
            }
        }

        public Guid GetGuid(string p)
        {
            return sdr.GetGuid(sdr.GetOrdinal(p));
        }

        public Guid? GetGuidNull(string p)
        {
            int index = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(index))
            {
                return null;
            }
            return sdr.GetGuid(index);
        }

        public bool GetBool(string p)
        {
            int index = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(index))
            {
                 throw new InvalidOperationException($"Unexpected null for {p}");
            }
            return sdr.GetBoolean(index);
        }

        public bool GetBoolNull(string p)
        {
            int index = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(index))
            {
                return false;
            }
            return sdr.GetBoolean(index);
        }

        public byte[] GetBinary(string p)
        {
            return sdr.GetSqlBinary(sdr.GetOrdinal(p)).Value;
        }

        public DateTime GetDateTime(string p)
        {
            return sdr.GetDateTime(sdr.GetOrdinal(p));
        }

        public DateTime? GetDateTimeNull(string p)
        {
            int idx = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(idx))
            {
                return null;
            }

            var value = sdr.GetDateTime(idx);
            if (ReaderOptions.HasFlag(DataReaderOptions.Interpret19000101AsNull)
                && value == new DateTime(1900, 1, 1))
            {
                return null;
            }

            return value;
        }

        public DateTimeOffset GetDateTimeOffset(string p)
        {
            return sdr.GetDateTimeOffset(sdr.GetOrdinal(p));
        }

        public DateTimeOffset? GetDateTimeOffsetNull(string p)
        {
            int idx = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(idx))
            {
                return null;
            }
            return sdr.GetDateTimeOffset(idx);
        }

        public bool HasColumn(string p)
        {
            int fieldCount = sdr.FieldCount;

            for (int i = 0; i < fieldCount; i++)
            {
                if (sdr.GetName(i).Equals(p, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public XmlElement GetXml(string p)
        {
            var idx = sdr.GetOrdinal(p);
            if (sdr.IsDBNull(idx))
            {
                return null;
            }

            var reader = sdr.GetSqlXml(idx).CreateReader();
            XmlDocument xd = new XmlDocument();
            xd.Load(reader);
            return xd.DocumentElement;
        }

        public TResult GetXmlSerializedObject<TResult>(string column, bool throwIfNull = true)
            where TResult : class
        {
            var idx = sdr.GetOrdinal(column);
            if (sdr.IsDBNull(idx))
            {
                if (throwIfNull)
                {
                    throw new InvalidOperationException(column + " is null");
                }
                return null;
            }

            var xs = new System.Xml.Serialization.XmlSerializer(typeof(TResult));
            using (var xr = sdr.GetXmlReader(idx))
            {
                return (TResult)xs.Deserialize(xr);
            }
        }
    }
}
