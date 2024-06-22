using System.Xml.Linq;
using System.Xml;
using System;
using System.Data.SqlClient;

namespace Esatto.DataAccess;

public static class SqlDataReaderExtensions
{
    public static int GetInt(this SqlDataReader sdr, string name)
    {
        return sdr.GetInt32(sdr.GetOrdinal(name));
    }

    public static long GetLong(this SqlDataReader sdr, string name)
    {
        return sdr.GetInt64(sdr.GetOrdinal(name));
    }

    public static TimeSpan GetTimeSpan(this SqlDataReader sdr, string name)
    {
        return sdr.GetTimeSpan(sdr.GetOrdinal(name));
    }

    public static int GetIntNull(this SqlDataReader sdr, string name, int nullvalue)
    {
        int index = sdr.GetOrdinal(name);
        if (sdr.IsDBNull(index))
        {
            return nullvalue;
        }
        return sdr.GetInt32(index);
    }

    public static int? GetIntNull(this SqlDataReader sdr, string name)
    {
        int index = sdr.GetOrdinal(name);
        if (sdr.IsDBNull(index))
        {
            return null;
        }
        return sdr.GetInt32(index);
    }

    public static bool IsNull(this SqlDataReader sdr, string name)
    {
        return sdr.IsDBNull(sdr.GetOrdinal(name));
    }

    public static decimal GetDecimal(this SqlDataReader sdr, string p)
    {
        return sdr.GetDecimal(sdr.GetOrdinal(p));
    }

    public static string GetStringNotNull(this SqlDataReader sdr, string p) 
        => sdr.GetString(p) ?? throw new InvalidOperationException($"Unexpected null for {p}");

    public static string? GetString(this SqlDataReader sdr, string p)
    {
        int index = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(index))
        {
            return null;
        }

        return sdr.GetString(index);
    }

    public static Guid GetGuid(this SqlDataReader sdr, string p)
    {
        return sdr.GetGuid(sdr.GetOrdinal(p));
    }

    public static Guid? GetGuidNull(this SqlDataReader sdr, string p)
    {
        int index = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(index))
        {
            return null;
        }
        return sdr.GetGuid(index);
    }

    public static bool GetBool(this SqlDataReader sdr, string p)
    {
        int index = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(index))
        {
            throw new InvalidOperationException($"Unexpected null for {p}");
        }
        return sdr.GetBoolean(index);
    }

    public static bool GetBoolNull(this SqlDataReader sdr, string p)
    {
        int index = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(index))
        {
            return false;
        }
        return sdr.GetBoolean(index);
    }

    public static byte[] GetBinary(this SqlDataReader sdr, string p)
    {
        return sdr.GetSqlBinary(sdr.GetOrdinal(p)).Value;
    }

    public static DateTime GetDateTime(this SqlDataReader sdr, string p)
    {
        return sdr.GetDateTime(sdr.GetOrdinal(p));
    }

    public static DateTime? GetDateTimeNull(this SqlDataReader sdr, string p)
    {
        int idx = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(idx))
        {
            return null;
        }

        return sdr.GetDateTime(idx);
    }

    public static DateTimeOffset GetDateTimeOffset(this SqlDataReader sdr, string p)
    {
        return sdr.GetDateTimeOffset(sdr.GetOrdinal(p));
    }

    public static DateTimeOffset? GetDateTimeOffsetNull(this SqlDataReader sdr, string p)
    {
        int idx = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(idx))
        {
            return null;
        }
        return sdr.GetDateTimeOffset(idx);
    }

    public static bool HasColumn(this SqlDataReader sdr, string p)
    {
        int fieldCount = sdr.FieldCount;

        for (int i = 0; i < fieldCount; i++)
        {
            if (sdr.GetName(i).Equals(p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static XmlElement GetXml(this SqlDataReader sdr, string p)
    {
        var idx = sdr.GetOrdinal(p);
        if (sdr.IsDBNull(idx))
        {
            return null;
        }

        var reader = sdr.GetSqlXml(idx).CreateReader();
        XmlDocument xd = new XmlDocument();
        xd.Load(reader);
        return xd.DocumentElement;
    }

    public static TResult GetXmlSerializedObject<TResult>(this SqlDataReader sdr, string column, bool throwIfNull = true)
        where TResult : class
    {
        var idx = sdr.GetOrdinal(column);
        if (sdr.IsDBNull(idx))
        {
            if (throwIfNull)
            {
                throw new InvalidOperationException(column + " is null");
            }
            return null;
        }

        var xs = new System.Xml.Serialization.XmlSerializer(typeof(TResult));
        using (var xr = sdr.GetXmlReader(idx))
        {
            return (TResult)xs.Deserialize(xr);
        }
    }

    public static ObjectFiller<T> GetObjectFiller<T>(this SqlDataReader sdr, 
        string? prefix = null, PropertyValueMapper? valueMapper = null)
        where T : new()
    {
        return new ObjectFiller<T>(sdr, prefix, valueMapper);
    }

    public static T GetPartialObject<T>(this SqlDataReader sdr,
        string? prefix = null, PropertyValueMapper? valueMapper = null)
        where T : new()
    {
        return sdr.GetObjectFiller<T>(prefix, valueMapper).GetPartialObject(sdr);
    }
}
