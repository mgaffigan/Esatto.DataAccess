using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Data;

namespace Esatto.DataAccess
{
    public static class CommandBuilder
    {
        /// <summary>
        /// Create the inner portion of an IN Clasue ("@p1, @p2, @p3")
        /// </summary>
        /// <param name="value"></param>
        /// <param name="command"></param>
        /// <param name="sbWhere"></param>
        public static void AddParameterList(object value, DbCommand command, StringBuilder sbWhere, Func<object, object> translator = null)
        {
            //either get the single value or the set
            IEnumerable<object> values = new object[] { value };
            if (value is IEnumerable && !(value is string))
                values = ((IEnumerable)value).Cast<object>();

            // translate if specified
            if (translator != null)
                values = values.Select(translator);

            if (!values.Any())
            {
                sbWhere.Append("SELECT NULL WHERE 1 = 0");
            }
            else if (values.Count() > 50 && values.First() is int)
            {
                var tvpName = command.GetNewParamName();
                var tvp = new DataTable("ObjectID");
                tvp.Columns.Add("ObjectID", typeof(int));
                tvp.TableName = "dbo.ObjectList";
                foreach (var v in values.Cast<int>().Distinct())
                {
                    tvp.Rows.Add(v);
                }
                command[tvpName.Replace("@p", "ZS_p")] = tvp;
                sbWhere.Append("SELECT ObjectID FROM " + tvpName);
            }
            else
            {
                //add the params
                bool previous = false;
                foreach (var val in values)
                {
                    if (val != null)
                    {
                        if (previous)
                            sbWhere.Append(", ");

                        var param = command.GetNewParamName();
                        command[param] = val;
                        sbWhere.Append(param);

                        //hit the flag to add comma's
                        previous = true;
                    }
                }
            }
        }

     
        private static void addWhereIn(object value, DbCommand command, StringBuilder sbWhere, string column, bool not, Func<object, object> translator = null)
        {
            validate(value, command, sbWhere, column);

            IEnumerable<object> values = new object[] { value };
            if (value is IEnumerable && !(value is string))
                values = ((IEnumerable)value).Cast<object>();
            var hasNull = values.Any(v => v == null);

            if (not)
            {
                if (hasNull)
                {
                    sbWhere.AppendFormat("AND ({0} NOT IN (", column);
                }
                else
                {
                    sbWhere.AppendFormat("AND {0} NOT IN (", column);
                }
            }
            else
            {

                if (hasNull)
                {
                    sbWhere.AppendFormat("AND ({0} IN (", column);
                }
                else
                {
                    sbWhere.AppendFormat("AND {0} IN (", column);
                }
            }

            AddParameterList(value, command, sbWhere);

            if (!not)
            {
                if (hasNull)
                {
                    sbWhere.AppendFormat(") OR {0} IS NULL", column);
                }
            }

            sbWhere.AppendLine(")\r\n");
        }

        /// <summary>
        /// Add an IN Clause ("AND p.ColumnName IN (@p1, @p2)\r\n")
        /// </summary>
        /// <param name="value"></param>
        /// <param name="command"></param>
        /// <param name="sbWhere"></param>
        /// <param name="column"></param>
        public static void AddWhereIn(object value, DbCommand command, StringBuilder sbWhere, string column, Func<object, object> translator = null)
        {
            addWhereIn(value, command, sbWhere, column, false, translator);
        }

        /// <summary>
        /// Add an NOT IN Clause ("AND p.ColumnName NOT IN (@p1, @p2)\r\n")
        /// </summary>
        /// <param name="value"></param>
        /// <param name="command"></param>
        /// <param name="sbWhere"></param>
        /// <param name="column"></param>
        public static void AddWhereNotIn(object value, DbCommand command, StringBuilder sbWhere, string column, Func<object, object> translator = null)
        {
            addWhereIn(value, command, sbWhere, column, true, translator);   
        }

        public static void AddStartsWith(object value, DbCommand command, StringBuilder sbWhere, string column)
        {
            validate(value, command, sbWhere, column);

            IEnumerable<object> values = new object[] { value };
            if (value is IEnumerable && !(value is string))
                values = ((IEnumerable)value).Cast<object>();

            if (values.Count() == 1)
            {
                AddStartsWithInternal(" AND", values.Single(), command, sbWhere, column);
            }
            else 
            {
                sbWhere.AppendLine(" AND (1=0");
                foreach (var o in values)
                {
                    AddStartsWithInternal("    OR", o, command, sbWhere, column);
                }
                sbWhere.AppendLine(")");
            }
        }

        private static void AddStartsWithInternal(string andOr, object value, DbCommand command, StringBuilder sbWhere, string column)
        {
            string param = command.GetNewParamName();
            sbWhere.AppendFormat("{2} {0} LIKE {1} ESCAPE '\\' \r\n", column, param, andOr);
            command[param] = value.ToString()
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[") + "%";
        }

        public static void AddContains(object value, DbCommand command, StringBuilder sbWhere, string column)
        {
            validate(value, command, sbWhere, column);

            string param = command.GetNewParamName();
            sbWhere.AppendFormat("AND {0} LIKE {1} ESCAPE '\\' \r\n", column, param);
            command[param] = "%" + value.ToString()
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[") + "%";
        }

        public static void AddNotContains(object value, DbCommand command, StringBuilder sbWhere, string column)
        {

            validate(value, command, sbWhere, column);

            string param = command.GetNewParamName();
            sbWhere.AppendFormat("AND {0} NOT LIKE {1} ESCAPE '\\' \r\n", column, param);
            command[param] = "%" + value.ToString()
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[") + "%";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="command"></param>
        /// <param name="sbFrom"></param>
        /// <param name="sbWhere"></param>
        /// <param name="column"></param>
        /// <param name="schema"></param>
        /// <param name="table"></param>
        /// <param name="pkJoinFormat">0 = schema, 1 = table, 2 = table alias</param>
        public static void AddSearchTerms(IEnumerable<string> value, DbCommand command, StringBuilder sbFrom, StringBuilder sbWhere,
            string column, string schema, string table, string pkJoinFormat)
        {
            validate(value, command, sbWhere, column);
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentNullException("schema");
            if (string.IsNullOrWhiteSpace(table))
                throw new ArgumentNullException("table");

            foreach (var term in value)
            {
                var tableAlias = command.GetNewTableAlias();
                var searchParam = command.GetNewParamName();

                sbFrom.AppendFormat("INNER JOIN {0}.{1} {2} ON " + pkJoinFormat + "\r\n", schema, table, tableAlias);
                sbWhere.AppendFormat(" AND {0}.{1} LIKE {2}\r\n", tableAlias, column, searchParam);

                command[searchParam] = string.Format("{0}%", term);
            }
        }

        private static void validate(object value, DbCommand command, StringBuilder sbWhere, string column)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            if (command == null)
                throw new ArgumentNullException("command");
            if (sbWhere == null)
                throw new ArgumentNullException("sbWhere");
            if (column == null)
                throw new ArgumentNullException("column");
        }
    }
}
