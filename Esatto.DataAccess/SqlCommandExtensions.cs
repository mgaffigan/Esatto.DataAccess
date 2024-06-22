using System.Data;
using System.Data.SqlClient;

namespace Esatto.DataAccess;

public static class SqlCommandExtensions
{
    public static SqlParameter AddReturnParameter(this SqlCommand cmd)
    {
        var result = new SqlParameter();
        result.Direction = ParameterDirection.ReturnValue;
        cmd.Parameters.Add(result);
        return result;
    }

    public static string GetFullCommandText(this SqlCommand sqc)
    {
        if (sqc.CommandType != CommandType.StoredProcedure)
        {
            return SqlCommandDumper.GetCommandText(sqc);
        }

        return sqc.CommandText;
    }
}
