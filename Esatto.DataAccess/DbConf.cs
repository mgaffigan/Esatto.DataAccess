using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
#if NETFRAMEWORK
using System.EnterpriseServices;
#endif
using System.Reflection;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    public class DbConf
    {
        public ILogger Logger { get; }
        public string ConnectionString { get; }
        public string Schema { get; }
        public DataReaderOptions ReaderOptions { get; }

        public DbConf(ILogger logger, string conStr)
            : this(logger, conStr, "dbo", DataReaderOptions.None)
        {
        }

        public DbConf(ILogger logger, string constr, string schema)
            : this(logger, constr, schema, DataReaderOptions.None)
        {
        }

        public DbConf(ILogger logger, string constr, string schema, DataReaderOptions readerOptions)
        {
            this.Logger = logger;
            this.ConnectionString = constr;
            this.Schema = schema;
            this.ReaderOptions = readerOptions;
        }

        public SqlConnection GetConnection()
        {
            var con = new SqlConnection(GetEffectiveConnectionString());
            con.Open();
            OnConnectionOpened(con);
            return con;
        }

        public async Task<SqlConnection> GetConnectionAsync()
        {
            var con = new SqlConnection(GetEffectiveConnectionString());
            await con.OpenAsync().ConfigureAwait(false);
            OnConnectionOpened(con);
            return con;
        }

        private void OnConnectionOpened(SqlConnection con)
        {
#if NETFRAMEWORK
            enlistDTC(con);
#endif
            resetIsolationLevel(con);
        }

        private string GetEffectiveConnectionString()
        {
            var sqcb = new SqlConnectionStringBuilder(ConnectionString);

            var staticName = DbClientNameScope.StaticName ?? "";
            if (!string.IsNullOrEmpty(staticName))
            {
                staticName = "-" + staticName;
            }

            var threadName = DbClientNameScope.CurrentName ?? "";
            if (!string.IsNullOrEmpty(threadName))
            {
                threadName = "-" + threadName;
            }

            sqcb.ApplicationName += staticName + threadName;
            var constr = sqcb.ConnectionString;
            return constr;
        }

        private void resetIsolationLevel(SqlConnection con)
        {
            // Isolation level leaks across pooled connections when using TransactionScope
            // https://stackoverflow.com/questions/9851415/sql-server-isolation-level-leaks-across-pooled-connections

            // reset back to READ COMMITTED if we are not in a transaction
#if NETFRAMEWORK
            if (!ContextUtil.IsInTransaction && System.Transactions.Transaction.Current == null)
#else
            if (System.Transactions.Transaction.Current == null)
#endif
            {
                var cmd = new SqlCommand("SET TRANSACTION ISOLATION LEVEL READ COMMITTED;", con);
                cmd.ExecuteNonQuery();
            }
        }

#if NETFRAMEWORK
        private void enlistDTC(SqlConnection con)
        {
            //if we are in DTC, enlist
            if (!con.ConnectionString.Contains("enlist=false") && ContextUtil.IsInTransaction)
                con.EnlistDistributedTransaction((ITransaction)ContextUtil.Transaction);
        }
#endif

        public string FormatObject(string dbobject)
        {
            return string.Format("[{0}].[{1}]", this.Schema, dbobject);
        }

        [Pure]
        public StoredProcedure StoredProcedure(string spName)
        {
            return new StoredProcedure(this, spName);
        }

        [Pure]
        public StoredProcedure StoredProcedure(string spName, object paramObject = null)
        {
            var sp = new StoredProcedure(this, spName);
            sp.ApplyParamsObject(paramObject);

            return sp;
        }

        public ResultSet ExecuteStoredProcedureReader(string spName, object paramObject = null)
        {
            var sp = new StoredProcedure(this, spName);
            sp.ApplyParamsObject(paramObject);

            return sp.ExecuteReader();
        }

        public async Task<ResultSet> ExecuteStoredProcedureReaderAsync(string spName, object paramObject = null)
        {
            var sp = new StoredProcedure(this, spName);
            sp.ApplyParamsObject(paramObject);

            return await sp.ExecuteReaderAsync().ConfigureAwait(false);
        }

        public StoredProcedure ExecuteStoredProcedure(string spName, object paramObject = null)
        {
            var sp = new StoredProcedure(this, spName);
            sp.ApplyParamsObject(paramObject);

            sp.Execute();
            return sp;
        }

        public async Task<StoredProcedure> ExecuteStoredProcedureAsync(string spName, object paramObject = null)
        {
            var sp = new StoredProcedure(this, spName);
            sp.ApplyParamsObject(paramObject);

            await sp.ExecuteAsync().ConfigureAwait(false);
            return sp;
        }
    }
}
