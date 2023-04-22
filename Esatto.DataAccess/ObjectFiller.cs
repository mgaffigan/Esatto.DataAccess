using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Reflection;

namespace Esatto.DataAccess
{
    class ObjectFiller
    {
        public static List<T> GetResults<T>(SqlDataReader sdr, string Prefix, Action<T, ResultSet> action, ResultSet res) where T : new()
        {
            var mapping = GetMapping(typeof(T), sdr, Prefix ?? string.Empty);
            List<T> tls = new List<T>();

            while (sdr.Read())
            {
                T obj = FillResult<T>(sdr, action, res, mapping, res.ReaderOptions);

                tls.Add(obj);
            }

            return tls;
        }

        public static T GetSingleResult<T>(SqlDataReader sdr, string Prefix, Action<T, ResultSet> action, ResultSet res) where T : new()
        {
            var mapping = GetMapping(typeof(T), sdr, Prefix ?? string.Empty);

            T obj = FillResult(sdr, action, res, mapping, res.ReaderOptions);
            return obj;
        }

        private static T FillResult<T>(SqlDataReader sdr, Action<T, ResultSet> action, ResultSet res, PropertyInfo[] mapping, DataReaderOptions options) where T : new()
        {
            T obj = new T();

            for (int i = 0; i < mapping.Length; i++)
            {
                var propertyInfo = mapping[i];
                try
                {
                    if (propertyInfo != null && !sdr.IsDBNull(i))
                    {
                        var value = sdr[i];

                        if (propertyInfo.PropertyType == typeof(string)
                            && value is string
                            && options.HasFlag(DataReaderOptions.TrimStringValues))
                        {
                            value = ((string)value).Trim();
                        }
                        else if (propertyInfo.PropertyType == typeof(DateTime?)
                            && value is DateTime && ((DateTime)value) == new DateTime(1900, 1, 1)
                            && options.HasFlag(DataReaderOptions.Interpret19000101AsNull))
                        {
                            value = null;
                        }

                        mapping[i].SetValue(obj, value, null);
                    }
                    /*else
                        System.Diagnostics.Debugger.Error("Bindings", "Invalid mapping for column " + sdr.GetName(i) + " on object " + typeof(T).Name + "\n");*/
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(string.Format("Exception while mapping column '{0}'", mapping[i].Name), ex);
                }
            }

            if (action != null)
                action(obj, res);
            return obj;
        }

        private static PropertyInfo[] GetMapping(Type T, SqlDataReader sdr, string prefix)
        {
            PropertyInfo[] props = T.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo[] sprops = T.GetProperties(BindingFlags.Static | BindingFlags.Public);
            PropertyInfo[] mapping = new PropertyInfo[sdr.FieldCount];
            //figure out what column we will be looking for
            string PrimaryKey = T.Name + "ID";

            if (string.IsNullOrWhiteSpace(prefix))
                prefix = null;

            for (int i = 0; i < sdr.FieldCount; i++)
            {
                string fieldName = sdr.GetName(i);

                //if we are prefixed, remove it
                if (prefix != null)
                {
                    //there is a prefix, and we match
                    if (fieldName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        fieldName = fieldName.Substring(prefix.Length);
                    }
                    //there is a prefix, this is an unrelated column
                    else
                    {
                        //go to the next column
                        continue;
                    }
                }

                //check for the primary key and adjust accordingly
                if (fieldName.Equals(PrimaryKey, StringComparison.OrdinalIgnoreCase))
                    fieldName = "ID";

                for (int j = 0; j < props.Length; j++)
                {
                    if (props[j].CanWrite
                        && props[j].Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                        && !sprops.Any(pi => pi.Name == fieldName + "Field"))
                    {
                        mapping[i] = props[j];
                        break;
                    }
                }
            }

            return mapping;
        }
    }
}
