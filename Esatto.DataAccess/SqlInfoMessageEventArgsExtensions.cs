using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    internal static class SqlInfoMessageEventArgsExtensions
    {
        public static SqlException GetException(this SqlInfoMessageEventArgs @this)
        {
            // disgusting dirty hack, but I blame them for not having an "IsHandled" member.
            var fException = typeof(SqlInfoMessageEventArgs)
                .GetField("exception", BindingFlags.NonPublic | BindingFlags.Instance);
            return (SqlException)fException.GetValue(@this);
        }
    }
}
