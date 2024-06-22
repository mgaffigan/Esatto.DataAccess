using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    public delegate object? PropertyValueMapper(PropertyInfo pi, object? value);

    public class ObjectFiller<T>
        where T : new()
    {
        private readonly PropertyInfo[] Mapping;
        private readonly PropertyValueMapper MapValue;

        public ObjectFiller(SqlDataReader sdr, string? prefix, PropertyValueMapper? valueMapper)
        {
            this.MapValue = valueMapper ?? new((_, value) => value);

            var T = typeof(T);
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

            this.Mapping = mapping;
        }

        public T GetPartialObject(SqlDataReader sdr)
        {
            var obj = new T();

            for (int i = 0; i < Mapping.Length; i++)
            {
                var propertyInfo = Mapping[i];
                try
                {
                    if (propertyInfo == null || sdr.IsDBNull(i)) continue;

                    var value = sdr[i];
                    value = MapValue(propertyInfo, value);
                    Mapping[i].SetValue(obj, value, null);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(string.Format("Exception while mapping column '{0}'", Mapping[i].Name), ex);
                }
            }

            return obj;
        }
    }
}
