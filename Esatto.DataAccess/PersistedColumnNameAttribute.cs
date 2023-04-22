using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esatto.DataAccess
{
    public class PersistedColumnNameAttribute : Attribute
    {
        public string ColumnName { get; set; }
    }
}
