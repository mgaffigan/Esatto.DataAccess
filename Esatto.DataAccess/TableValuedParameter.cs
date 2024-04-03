using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Esatto.DataAccess
{
    public class TableValuedParameter
    {
        private string[] columns = null;
        private List<object[]> rows = new List<object[]>();

        public TableValuedParameter(params string[] columns)
        {
            this.columns = columns;
        }

        public static DataTable FromEnumerable<T>(IEnumerable<T> list)
        {
            if (typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T) == typeof(Guid)
                || typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTimeOffset) 
                || typeof(T) == typeof(byte[]))
            {
                return FromSingle(list);
            }

            var paramList = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
            var res = new TableValuedParameter(paramList.Select(e => e.Name).ToArray());
            foreach (var row in list)
            {
                res.AddRow(paramList.Select(pi => pi.GetValue(row)).ToArray<object>());
            }
            return res.GetDataTable();
        }

        private static DataTable FromSingle<T>(IEnumerable<T> list)
        {
            var res = new TableValuedParameter(new[] { "t" });
            foreach (var row in list)
            {
                res.AddRow(new object[] { row });
            }
            return res.GetDataTable();
        }

        [Obsolete("Use Select with anonymous type")]
        public static DataTable FromEnumerable<T>(IEnumerable<T> list, params Expression<Func<T, object>>[] map)
        {
            if (map == null || !map.Any())
            {
                if (typeof(T).IsPrimitive || typeof(T) == typeof(string))
                {
                    return FromSingle(list);
                }
                else
                {
                    var paramList = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
                    map = paramList.Select(p => ExpressionForParam<T>(p)).ToArray();
                }
            }

            var funcMap = map.Select(e => new { ParamName = e.Parameters.Single().Name, Functor = e.Compile() });

            var res = new TableValuedParameter(funcMap.Select(e => e.ParamName).ToArray());
            foreach (var row in list)
            {
                res.AddRow(funcMap.Select(f => f.Functor(row)).ToArray());
            }

            return res.GetDataTable();
        }

        [Obsolete("Use Select with anonymous type")]
        private static Expression<Func<T, object>> ExpressionForParam<T>(PropertyInfo arg)
        {
            var param = Expression.Parameter(arg.DeclaringType, arg.Name);
            Expression accessor = Expression.Property(param, arg);
            accessor = Expression.Convert(accessor, typeof(object));

            return Expression.Lambda<Func<T, object>>(accessor, param);
        }

        public void AddRow(params object[] rowValues)
        {
            if (rowValues.Length != columns.Length)
                throw new ArgumentException("invalid count of row values");

            for (int i = 0; i < rowValues.Length; i++)
            {
                object o = rowValues[i];
                if (o is Enum)
                {
                    rowValues[i] = Convert.ToInt32(o);
                }
            }

            rows.Add(rowValues);
        }

        public DataTable GetDataTable()
        {
            DataTable table = new DataTable();

            //add columns
            foreach (string s in columns)
                table.Columns.Add(s);

            foreach (var r in rows)
                table.Rows.Add(r);

            //add rows
            return table;
        }

        public void AddList(IEnumerable list)
        {
            foreach (object o in list)
            {
                rows.Add(new object[] { o });
            }
        }

        public static DataTable FromList(string columnName, IEnumerable list)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add(columnName);

            foreach (var r in list)
                dt.Rows.Add(r);

            return dt;
        }

        public static DataTable FromSortedList(string keycolumnName, string valueColumnName, SortedList<string, string> list)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add(keycolumnName);
            dt.Columns.Add(valueColumnName);

            if (list != null)
            {
                foreach (var r in list)
                {
                    dt.Rows.Add(r.Key, r.Value);
                }
            }

            return dt;
        }
    }

    public static class TableValuedParameterExtensions
    {
        /// <summary>
        /// Converts basic types (string, int, etc...) to a single column data table, and other types
        /// to a multi-column data table with one column per prop
        /// </summary>
        public static DataTable AsDataTable<TValue>(this IEnumerable<TValue> @this)
            => TableValuedParameter.FromEnumerable<TValue>(@this);

        [Obsolete("Use Select with AsDataTable()")]
        public static DataTable AsDataTable<TValue>(this IEnumerable<TValue> @this, params Expression<Func<TValue, object>>[] map)
            => TableValuedParameter.FromEnumerable<TValue>(@this, map);
    }
}