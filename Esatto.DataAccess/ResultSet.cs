using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Xml;
using System.Threading.Tasks;
using System.Reflection;

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

        [Obsolete("Use Read+GetPartialObject or GetObject instead")]
        public ResultSet FillObject<T>(out T? obj, string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            //check that we have data to load
            if (!sdr.Read())
            {
                obj = default(T);
            }
            else
            {
                obj = GetPartialObject<T>(prefix, action);
            }

            return this;
        }

        [Obsolete("Use GetPartialObject<T> instead")]
        public ResultSet FillPartialObject<T>(out T obj, string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            obj = GetPartialObject<T>(prefix, action);

            return this;
        }

        [Obsolete("Use GetList<T> instead")]
        public ResultSet FillList<T>(out List<T> obj, string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            obj = GetList<T>(prefix, action);

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

        public async Task<List<T>> GetListAsync<T>(Func<ResultSet, T> ctor)
        {
            var results = new List<T>();
            while (await sdr.ReadAsync().ConfigureAwait(false))
            {
                results.Add(ctor(this));
            }
            return results;
        }

        public List<T> GetList<T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            var mapping = sdr.GetObjectFiller<T>(prefix, MapValue);

            var tls = new List<T>();
            while (sdr.Read())
            {
                T obj = mapping.GetPartialObject(sdr);
                action?.Invoke(obj, this);

                tls.Add(obj);
            }

            return tls;
        }

        public async Task<List<T>> GetListAsync<T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            var mapping = sdr.GetObjectFiller<T>(prefix, MapValue);

            var tls = new List<T>();
            while (await sdr.ReadAsync().ConfigureAwait(false))
            {
                T obj = mapping.GetPartialObject(sdr);
                action?.Invoke(obj, this);

                tls.Add(obj);
            }

            return tls;
        }

        public ResultSet ForEachRow(Action<ResultSet> action)
        {
            while (sdr.Read())
            {
                action(this);
            }

            return this;
        }

        public async Task ForEachRowAsync(Action<ResultSet> action)
        {
            while (await sdr.ReadAsync().ConfigureAwait(false))
            {
                action(this);
            }
        }

        public IEnumerable<T> ForEachRow<T>(Func<ResultSet, T> action)
        {
            while (sdr.Read())
            {
                yield return action(this);
            }
        }

        public async IAsyncEnumerable<T> ForEachRowAsync<T>(Func<ResultSet, T> action)
        {
            while (await sdr.ReadAsync().ConfigureAwait(false))
            {
                yield return action(this);
            }
        }

        public ResultSet ForEachObject<T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            var mapping = sdr.GetObjectFiller<T>(prefix, MapValue);

            while (sdr.Read())
            {
                T obj = mapping.GetPartialObject(sdr);
                action?.Invoke(obj, this);
            }

            return this;
        }

        public async Task ForEachObjectAsync<T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            var mapping = sdr.GetObjectFiller<T>(prefix, MapValue);

            while (await sdr.ReadAsync().ConfigureAwait(false))
            {
                T obj = mapping.GetPartialObject(sdr);
                action?.Invoke(obj, this);
            }
        }

        public T GetPartialObject<T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
        {
            var obj = sdr.GetPartialObject<T>(prefix, MapValue);
            action?.Invoke(obj, this);

            return obj;
        }

        public T GetObject<TException, T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
            where TException : Exception, new()
        {
            ReadOrFail<TException>();

            return GetPartialObject<T>(prefix, action);
        }

        public async Task<T> GetObjectAsync<TException, T>(string? prefix = null, Action<T, ResultSet>? action = null)
            where T : new()
            where TException : Exception, new()
        {
            await ReadOrFailAsync<TException>().ConfigureAwait(false);

            return GetPartialObject<T>(prefix, action);
        }

        private object MapValue(PropertyInfo propertyInfo, object value)
        {
            if (ReaderOptions.HasFlag(DataReaderOptions.TrimStringValues)
                && propertyInfo.PropertyType == typeof(string)
                && value is string s)
            {
                return s.Trim();
            }
            else if (ReaderOptions.HasFlag(DataReaderOptions.Interpret19000101AsNull)
                && propertyInfo.PropertyType == typeof(DateTime?)
                && value is DateTime dt && dt == new DateTime(1900, 1, 1))
            {
                return null;
            }
            else return value;
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

        public async Task ReadOrFailAsync<TException>()
            where TException : Exception, new()
        {
            if (!await sdr.ReadAsync().ConfigureAwait(false))
            {
                throw new TException();
            }
        }

        public bool Read()
        {
            return sdr.Read();
        }

        public Task<bool> ReadAsync() => sdr.ReadAsync();

        public ResultSet NextResult()
        {
            sdr.NextResult();

            return this;
        }

        public Task<bool> NextResultAsync() => sdr.NextResultAsync();

        /// <summary>
        /// Use closereader before end to close the sqldatareader but not the connection.  This will also load output parameters.
        /// </summary>
        public void CloseReader()
        {
            if (sdr != null && !sdr.IsClosed)
            {
                sdr.Close();
                dbcommand?.LoadParameters(sqc);
            }
        }

        void IDisposable.Dispose() => End();

        public void End()
        {
            if (sdr != null && !sdr.IsClosed)
            {
                sdr.Close();
            }
            con?.Close();
        }

        public int GetInt(string name) => sdr.GetInt(name);

        public long GetLong(string name) => sdr.GetLong(name);

        public TimeSpan GetTimeSpan(string name) => sdr.GetTimeSpan(name);

        public object Get(string name) => sdr[name];

        public int GetIntNull(string name, int nullvalue) => sdr.GetIntNull(name, nullvalue);

        public int? GetIntNull(string name) => sdr.GetIntNull(name);

        public bool IsNull(string name) => sdr.IsNull(name);

        public decimal GetDecimal(string p) => sdr.GetDecimal(p);

        public string GetStringNotNull(string p) => GetString(p) ?? throw new InvalidOperationException($"Unexpected null for {p}");

        public string? GetString(string p)
        {
            var result = sdr.GetString(p);
            if (ReaderOptions.HasFlag(DataReaderOptions.TrimStringValues))
            {
                result = result?.Trim();
            }
            return result;
        }

        public Guid GetGuid(string p) => sdr.GetGuid(p);

        public Guid? GetGuidNull(string p) => sdr.GetGuidNull(p);

        public bool GetBool(string p) => sdr.GetBool(p);

        public bool GetBoolNull(string p) => sdr.GetBoolNull(p);

        public byte[] GetBinary(string p) => sdr.GetBinary(p);

        public DateTime GetDateTime(string p) => sdr.GetDateTime(p);

        public DateTime? GetDateTimeNull(string p)
        {
            var value = sdr.GetDateTimeNull(p);
            if (ReaderOptions.HasFlag(DataReaderOptions.Interpret19000101AsNull)
                && value == new DateTime(1900, 1, 1))
            {
                return null;
            }
            return value;
        }

        public DateTimeOffset GetDateTimeOffset(string p) => sdr.GetDateTimeOffset(p);

        public DateTimeOffset? GetDateTimeOffsetNull(string p) => sdr.GetDateTimeOffsetNull(p);

        public bool HasColumn(string p) => sdr.HasColumn(p);

        public XmlElement GetXml(string p) => sdr.GetXml(p);

        public TResult GetXmlSerializedObject<TResult>(string column, bool throwIfNull = true)
            where TResult : class
        {
            return sdr.GetXmlSerializedObject<TResult>(column, throwIfNull);
        }
    }
}
