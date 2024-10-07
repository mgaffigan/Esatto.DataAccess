using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Reflection;

namespace Esatto.DataAccess
{
    internal static class SqlInfoMessageEventArgsExtensions
    {
        public static Exception GetException(this SqlInfoMessageEventArgs @this)
        {
            try
            {
                // disgusting dirty hack, but I blame them for not having an "IsHandled" member.
                var fException = typeof(SqlInfoMessageEventArgs)
#if NETFRAMEWORK
                    .GetField("exception", BindingFlags.NonPublic | BindingFlags.Instance);
#else
                    .GetField("_exception", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
                return (SqlException)fException.GetValue(@this);
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                return new InvalidOperationException($"Failed to get exception from SqlInfoMessageEventArgs: {@this.Message}", ex);
            }
        }
    }
}
