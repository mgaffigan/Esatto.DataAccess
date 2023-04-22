using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Data.SqlClient;
using System.Data;

namespace Esatto.DataAccess
{
    public class StoredProcedure : DbCommand
    {
        public StoredProcedure(DbConf con, string proc)
            : base(con, con.FormatObject(proc))
        {
        }

        protected override void FillCommand(SqlCommand sqc)
        {
            sqc.CommandType = System.Data.CommandType.StoredProcedure;
        }
    }
}
