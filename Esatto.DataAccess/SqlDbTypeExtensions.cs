using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    internal class SqlDbTypeExtensions
    {
        static Dictionary<Type, SqlDbType> TypeToSqlMap = new Dictionary<Type, SqlDbType>()
        {
            { typeof(bool), SqlDbType.Bit },
            { typeof(byte), SqlDbType.TinyInt },
            { typeof(sbyte), SqlDbType.TinyInt },
            { typeof(short), SqlDbType.SmallInt },
            { typeof(ushort), SqlDbType.SmallInt },
            { typeof(int), SqlDbType.Int },
            { typeof(uint), SqlDbType.Int },
            { typeof(long), SqlDbType.BigInt },
            { typeof(ulong), SqlDbType.BigInt },
            { typeof(float), SqlDbType.Real },
            { typeof(double), SqlDbType.Float },
            { typeof(decimal), SqlDbType.Money },
            { typeof(string), SqlDbType.NVarChar },
            { typeof(char), SqlDbType.NChar  },
            { typeof(char[]), SqlDbType.NVarChar  },
            { typeof(byte[]), SqlDbType.VarBinary },
            { typeof(Guid), SqlDbType.UniqueIdentifier },
            { typeof(DateTime), SqlDbType.DateTime2 },
            { typeof(DateTimeOffset), SqlDbType.DateTimeOffset },
            { typeof(TimeSpan), SqlDbType.Time },
        };

        public static SqlDbType ToSqlDbType(Type t)
        {
            if (TypeToSqlMap.TryGetValue(t, out var result))
            {
                return result;
            }

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return ToSqlDbType(Nullable.GetUnderlyingType(t));
            }

            throw new ArgumentException($"Type {t} is not a supported SqlDbType");
        }
    }
}
