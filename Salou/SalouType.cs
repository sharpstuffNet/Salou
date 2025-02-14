using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql
{
    /// <summary>
    /// Internal used Types = Dbtype but could be extended
    /// </summary>
    public enum SalouType 
    {
        Unknown,
        Binary,
        Byte,
        Boolean,
        Decimal,
        Date,
        DateTime,
        Double,
        Guid,
        Int16,
        Int32,
        Int64,
        //Object,
        Single,
        String,
        Time,
        TimeSpan,
        Xml,
        DateTimeOffset,
        DBNull,
        NULL
    }
}

