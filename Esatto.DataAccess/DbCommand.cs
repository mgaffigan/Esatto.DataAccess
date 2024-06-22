using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    public class DbCommand : DynamicObject
    {
        private DbConf conf = null;
        public string CommandText { get; set; }
        public string CommandName { get; private set; }
        private Dictionary<string, object> paramvals = new Dictionary<string, object>();
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IReadOnlyDictionary<string, object> Parameters => paramvals;
        private bool IsCancelled = false;
        private SqlCommand lastCommand = null;
        private int cParamNumber = 1000;
        private int cTableNumber = 1000;

        private Exception UserException;

        public DbCommand(DbConf con, string? command, string commandName)
        {
            if (con == null)
                throw new ArgumentNullException("con");
            if (string.IsNullOrWhiteSpace(commandName) && commandName != null)
                throw new ArgumentOutOfRangeException("commandName", "CommandName cannot be whitespace");

            this.Timeout = -1;
            this.conf = con;
            this.CommandText = command;
            this.CommandName = commandName;
        }

        public DbCommand(DbConf con, string? command)
            : this(con, command, null)
        {
        }

        public DbCommand(DbConf con)
            : this(con, null, null)
        {
        }

        #region progress reporting

        /// <summary>
        /// Gets fired when dbo.ReportProgress sends progress information
        /// </summary>
        public event EventHandler<ProgressReportEventArgs> Progress;

        private void hookupProgressEvent(SqlConnection con)
        {
            UserException = null;
            if (Progress != null)
            {
                con.FireInfoMessageEventOnUserErrors = true;
                con.InfoMessage += new SqlInfoMessageEventHandler(con_InfoMessage);
            }
        }

        private void con_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            ProgressReport info;
            if (ProgressReport.TryParse(e.Message, out info))
            {
                info.DBSource = e.Source;
                Progress?.Invoke(this, new ProgressReportEventArgs(info.Progress, info.Total, info.Status));
            }
            else if (e.Errors.Cast<SqlError>().Any(c => c.Class >= 16))
            {
                // critical error, throw back to main thread
                UserException = e.GetException();
            }
            else
            {
                conf.Logger.LogDebug("Received output from SQL Command '{0}': {1}", CommandName ?? CommandText, e.Message);
            }
        }

        #endregion progress reporting

        /// <summary>
        /// Get an auto-incremented parameter name
        /// </summary>
        /// <returns>Parameter name, e.g. @p1001</returns>
        public string GetNewParamName()
        {
            return string.Format("@p{0}", cParamNumber++);
        }

        public void ApplyParamsObject(object? paramObject)
        {
            if (paramObject != null)
            {
                var paramList = paramObject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
                foreach (var p in paramList)
                {
                    this[p.Name] = p.GetValue(paramObject);
                }
            }
        }

        /// <summary>
        /// Get an auto-incremented table name
        /// </summary>
        /// <returns>Table name, e.g. t1005</returns>
        public string GetNewTableAlias()
        {
            return string.Format("t{0}", cTableNumber++);
        }

        public ResultSet ExecuteReader()
        {
            SqlDataReader sdr = null;
            SqlConnection con = null;
            string? fullCommandText = null;

            try
            {
                con = conf.GetConnection();

                hookupProgressEvent(con);

                var sqc = createDynamicCommand(con);

                //exec
                fullCommandText = sqc.GetFullCommandText();

                logPreExec(sqc, fullCommandText);
                sdr = sqc.ExecuteReader();

                return new ResultSet(sdr, con, sqc, this, conf.ReaderOptions);
            }
            catch (Exception ex)
            {
                if (sdr != null && !sdr.IsClosed) sdr.Close();
                con?.Close();

                TranslateException(fullCommandText, ex);
                throw;
            }
        }

        public async Task<ResultSet> ExecuteReaderAsync()
        {
            SqlDataReader sdr = null;
            SqlConnection con = null;
            string fullCommandText = null;

            try
            {
                con = await conf.GetConnectionAsync().ConfigureAwait(false);

                hookupProgressEvent(con);

                var sqc = createDynamicCommand(con);

                //exec
                fullCommandText = sqc.GetFullCommandText();

                logPreExec(sqc, fullCommandText);
                sdr = await sqc.ExecuteReaderAsync().ConfigureAwait(false);

                return new ResultSet(sdr, con, sqc, this, conf.ReaderOptions);
            }
            catch (Exception ex)
            {
                if (sdr != null && !sdr.IsClosed) sdr.Close();
                con?.Close();

                TranslateException(fullCommandText, ex);
                throw;
            }
        }

        public class PreExecEventArgs : EventArgs
        {
            public SqlCommand Command { get; }

            public string CommandText { get; }

            public PreExecEventArgs(SqlCommand sqc, string commandText)
            {
                this.Command = sqc;
                this.CommandText = commandText;
            }
        }

        public event EventHandler<PreExecEventArgs> PreExec;

        private void logPreExec(SqlCommand sqc, string commandText)
        {
            PreExec?.Invoke(this, new PreExecEventArgs(sqc, commandText));
            if (CommandName != null)
            {
                conf.Logger.LogDebug("Executing command '{0}' against '{1}' (Schema: '{2}'):\r\n{3}",
                    CommandName, conf.ConnectionString, conf.Schema, commandText);
            }
        }

        public void LoadParameters(SqlCommand sqc)
        {
            //update any output vars
            foreach (SqlParameter p in sqc.Parameters)
            {
                if (p.Direction == ParameterDirection.ReturnValue)
                {
                    if (paramvals.ContainsKey("ZR_Return"))
                        paramvals["ZR_Return"] = (int)(p.Value ?? 0);
                    else
                        paramvals.Add("ZR_Return", (int)(p.Value ?? 0));
                }
                else if (p.Direction == ParameterDirection.InputOutput)
                {
                    paramvals["ZX_" + p.ParameterName.Substring(1)] = (p.Value == System.DBNull.Value) ? null : p.Value;
                }
                else if (p.Direction == ParameterDirection.Output)
                {
                    paramvals["ZO_" + p.ParameterName.Substring(1)] = (p.Value == System.DBNull.Value) ? null : p.Value;
                }
            }

            // throw if there was a user exception
            var e = UserException;
            if (e != null)
            {
                UserException = null;
                throw e;
            }
        }

        public int Execute()
        {
            string fullCommandText = null;
            try
            {
                using (var con = conf.GetConnection())
                using (var sqc = createDynamicCommand(con))
                {
                    // hook progress if need be
                    hookupProgressEvent(con);

                    //add return var
                    var pReturn = sqc.AddReturnParameter();

                    //exec
                    fullCommandText = sqc.GetFullCommandText();

                    logPreExec(sqc, fullCommandText);
                    sqc.ExecuteNonQuery();

                    //update any output vars
                    LoadParameters(sqc);

                    //return result
                    int val = (int)pReturn.Value;

                    //clean up
                    con.Close();

                    return val;
                }
            }
            catch (Exception ex)
            {
                TranslateException(fullCommandText, ex);
                throw;
            }
        }

        public async Task<int> ExecuteAsync()
        {
            string fullCommandText = null;
            try
            {
                using (var con = conf.GetConnection())
                using (var sqc = createDynamicCommand(con))
                {
                    // hook progress if need be
                    hookupProgressEvent(con);

                    //add return var
                    var pReturn = sqc.AddReturnParameter();

                    //exec
                    fullCommandText = sqc.GetFullCommandText();

                    logPreExec(sqc, fullCommandText);
                    await sqc.ExecuteNonQueryAsync().ConfigureAwait(false);

                    //update any output vars
                    LoadParameters(sqc);

                    //return result
                    int val = (int)pReturn.Value;

                    //clean up
#if NET
                    await con.CloseAsync().ConfigureAwait(false);
#else
                    con.Close();
#endif

                    return val;
                }
            }
            catch (Exception ex)
            {
                TranslateException(fullCommandText, ex);
                throw;
            }
        }

        private void TranslateException(string fullCommandText, Exception ex)
        {
            var sEx = ex as SqlException;
            // there is no good way to identify this error.  See 
            //   https://referencesource.microsoft.com/#System.Data/System/Data/SqlClient/TdsParser.cs,2332
            //   https://stackoverflow.com/questions/10226314/what-is-the-best-way-to-catch-operation-cancelled-by-user-exception
            if (sEx != null && sEx.Number == 0 && sEx.State == 0 && sEx.Class == 11 && IsCancelled)
            {
                // no-op, no need to log since it is a user initated exception
                throw new OperationCanceledException(ex.Message, ex);
            }
            if (sEx != null && sEx.Number == 50000)
            {
                // no-op, no need to log since it is a user thrown exception
            }
            else
            {
                conf.Logger.LogWarning(ex, "Exception occurred while executing command against schema '{1}':\r\n{0}", fullCommandText ?? CommandText, conf.Schema);
            }
        }

        protected virtual void FillCommand(SqlCommand sqc)
        {
            //no-op for normal command
        }

        private SqlCommand createDynamicCommand(SqlConnection con)
        {
            SqlCommand sqc = new SqlCommand(CommandText, con);
            IsCancelled = false;
            lastCommand = sqc;

            FillCommand(sqc);

            if (Timeout >= 0)
                sqc.CommandTimeout = Timeout;

            //add all the paramaters
            foreach (var p in paramvals)
            {
                var dbValue = p.Value ?? DBNull.Value;

                if (p.Value is DbType)
                    throw new NotImplementedException("Did you mean SqlDbType?");
                if (p.Key.StartsWith("ZO_") || ((p.Key.StartsWith("ZX_") && p.Value is SqlDbType)))
                {
                    var param = sqc.Parameters.Add(new SqlParameter("@" + p.Key.Substring(3), (SqlDbType)p.Value)
                    {
                        IsNullable = true,
                        //specify the paramaeter direction based off of the key prefix
                        Direction = (p.Key.StartsWith("ZX_")
                            ? ParameterDirection.InputOutput
                            : ParameterDirection.Output)
                    });
                    if (param.SqlDbType == SqlDbType.NVarChar || param.SqlDbType == SqlDbType.VarChar || param.SqlDbType == SqlDbType.VarBinary)
                        param.Size = 4000;
                }
                else if (p.Key.StartsWith("ZX_"))
                {
                    sqc.Parameters.Add(new SqlParameter("@" + p.Key.Substring(3), dbValue)
                    {
                        IsNullable = true,
                        Direction = ParameterDirection.InputOutput
                    });
                }
                else if (p.Key.StartsWith("ZS_") || p.Value is DataTable)
                {
                    var key = p.Key;
                    if (key.StartsWith("ZS_"))
                    {
                        key = key.Substring(3);
                    }

                    var stp = new SqlParameter("@" + key, p.Value)
                    {
                        SqlDbType = SqlDbType.Structured
                    };
                    var dt = p.Value as DataTable;
                    var tableName = dt?.TableName;
                    if (!string.IsNullOrWhiteSpace(tableName))
                    {
                        stp.TypeName = tableName;
                    }
                    sqc.Parameters.Add(stp);
                }
                else
                {
                    var autoParam = sqc.Parameters.AddWithValue("@" + p.Key, dbValue);
                    autoParam.IsNullable = p.Value == null;
                    // avoid nvarchar since most indexes are on the SBCS format 
                    // https://blogs.msmvps.com/jcoehoorn/blog/2014/05/12/can-we-stop-using-addwithvalue-already/
                    if (conf.ReaderOptions.HasFlag(DataReaderOptions.DefaultParametersToSbcs))
                    {
                        var sValue = dbValue as string;
                        if (sValue != null && !sValue.Any(c => c > 127))
                        {
                            // string has only ASCII characters, use varchar
                            autoParam.SqlDbType = SqlDbType.VarChar;
                        }
                    }
                }
            }

            return sqc;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var paramName = binder.Name;
            setParam(value, paramName);
            return true;
        }

        public object? this[string paramname]
        {
            get
            {
                //remove starting @
                if (paramname.StartsWith("@"))
                    paramname = paramname.Substring(1);

                return paramvals[paramname];
            }
            set
            {
                //remove starting @
                if (paramname.StartsWith("@"))
                    paramname = paramname.Substring(1);

                setParam(value, paramname);
            }
        }

        private void setParam(object? value, string paramName)
        {
            if (paramvals.ContainsKey(paramName))
            {
                paramvals[paramName] = value;
            }
            else
            {
                paramvals.Add(paramName, value);
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var paramName = binder.Name;

            //init result to null
            result = null;

            //check for the existance of a key
            if (!paramvals.ContainsKey(paramName))
                return false;

            //handle nulls
            if (paramvals[paramName] == null)
            {
                if (binder.ReturnType.IsValueType)
                {
                    //if it is a value type (int, bool, etc...) then lets return default
                    result = Activator.CreateInstance(binder.ReturnType);
                }
                else
                {
                    //if it is a value type, we already assume null, so just return
                }
                return true;
            }

            //check for type compatability
            if (!binder.ReturnType.IsAssignableFrom(paramvals[paramName].GetType()))
                return false;

            //type matches, return value
            result = paramvals[paramName];
            return true;
        }

        /// <summary>
        /// Gets or sets the timeout allowed (in seconds) for the command to be completed
        /// See SqlCommand.CommandTimeout
        /// </summary>
        public int Timeout { get; set; }

        public void Cancel()
        {
            IsCancelled = true;
            if (lastCommand != null)
                lastCommand.Cancel();
        }
    }
}