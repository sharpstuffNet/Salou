using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SalouWS4Sql.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace SalouWS4Sql
{
#nullable enable


    public static partial class Salou
    {
        /// <summary>
        /// see <see cref="SendToServerConverterDelegate"/>
        /// </summary>
        /// <param name="o"></param>
        /// <param name="dt"></param>
        /// <param name="st"></param>
        /// <returns></returns>
        /// <exception cref="SalouException"></exception>
        private static (object? value, DbType dbType, SalouType salouType) SendToServerConverterFkt(object? o, DbType? dt, SalouType? st)
        {
            // if all are null, we return null
            if (st == null && dt == null && o == null)
                return (null, DbType.Object, SalouType.NULL);

            // preference to SalouType
            if (st == null)
            {
                //then DbType
                if (dt == null)
                {
                    // if no SalouType, DbType or Type is given, we try to get the type from the object
                    if (o == null)
                        throw new SalouException("SendToServerConverter: Object is null");
                    dt = ConvertNetToDbType(o.GetType());
                }
                if (o == DBNull.Value)
                    return (o, dt.GetValueOrDefault(DbType.Object), SalouType.DBNull);

                //now switch DbType
                switch (dt.Value)
                {
                    case DbType.AnsiString:
                    case DbType.AnsiStringFixedLength:
                    case DbType.StringFixedLength:
                    case DbType.String:
                        return (o, dt.Value, SalouType.String);
                    case DbType.Object:
                    case DbType.Binary:
                        return (o, dt.Value, SalouType.Binary);
                    case DbType.SByte:
                    case DbType.Byte:
                        return (o, dt.Value, SalouType.Byte);
                    case DbType.Boolean:
                        return (o, dt.Value, SalouType.Boolean);
                    case DbType.Currency:
                    case DbType.Decimal:
                        return (o, dt.Value, SalouType.Decimal);
                    case DbType.Date:
                        return (o, dt.Value, SalouType.Date);
                    case DbType.DateTime2:
                    case DbType.DateTime:
                        return (o, dt.Value, SalouType.DateTime);
                    case DbType.Double:
                        return (o, dt.Value, SalouType.Double);
                    case DbType.Guid:
                        return (o, dt.Value, SalouType.Guid);
                    case DbType.UInt16:
                    case DbType.Int16:
                        return (o, dt.Value, SalouType.Int16);
                    case DbType.UInt32:
                    case DbType.Int32:
                        return (o, dt.Value, SalouType.Int32);
                    case DbType.UInt64:
                    case DbType.Int64:
                        return (o, dt.Value, SalouType.Int64);
                    //case DbType.Object:
                    //    return (o, dt.Value, SalouType.Unknown);
                    case DbType.Single:
                        return (o, dt.Value, SalouType.Single);
                    case DbType.Time:
                        return (o, dt.Value, SalouType.Time);
                    case DbType.DateTimeOffset:
                        return (o, dt.Value, SalouType.DateTimeOffset);
                    case DbType.Xml:
                        return (o, dt.Value, SalouType.Xml);
                    case DbType.VarNumeric:
                    default:
                        throw new SalouException($"SendToServerConverter: DbType not supported {dt}");
                }
                throw new SalouException($"SendToServerConverter: DbType not supported {dt}");
            }
            else if (dt == null)
            {
                //Switch saloutype and return
                switch (st.Value)
                {
                    case SalouType.DBNull:
                        return (o, dt.GetValueOrDefault(DbType.Object), SalouType.DBNull);
                    case SalouType.String:
                        return (o, DbType.String, st.Value);
                    case SalouType.Binary:
                        return (o, DbType.Binary, st.Value);
                    case SalouType.Byte:
                        return (o, DbType.Byte, st.Value);
                    case SalouType.Boolean:
                        return (o, DbType.Boolean, st.Value);
                    case SalouType.Decimal:
                        return (o, DbType.Decimal, st.Value);
                    case SalouType.Date:
                        return (o, DbType.Date, st.Value);
                    case SalouType.DateTime:
                        return (o, DbType.DateTime, st.Value);
                    case SalouType.Double:
                        return (o, DbType.Double, st.Value);
                    case SalouType.Guid:
                        return (o, DbType.Guid, st.Value);
                    case SalouType.Int16:
                        return (o, DbType.Int16, st.Value);
                    case SalouType.Int32:
                        return (o, DbType.Int32, st.Value);
                    case SalouType.Int64:
                        return (o, DbType.Int64, st.Value);
                    //case SalouType.Unknown:
                    //    return (o, DbType.VarNumeric, st.Value);
                    case SalouType.Single:
                        return (o, DbType.Single, st.Value);
                    case SalouType.Time:
                        return (o, DbType.Time, st.Value);
                    case SalouType.DateTimeOffset:
                        return (o, DbType.DateTimeOffset, st.Value);
                    case SalouType.Xml:
                        return (o, DbType.Xml, st.Value);
                    default:
                        throw new SalouException($"SendToServerConverter: SalouType not supported {st}");
                }

            }
            return (o, dt.Value, st.Value);
        }

        /// <summary>
        /// see <see cref="RecivedFromServerConverterDelegate"/>
        /// </summary>
        /// <param name="o"></param>
        /// <param name="dt"></param>
        /// <param name="st"></param>
        /// <returns></returns>
        /// <exception cref="SalouException"></exception>
        private static (object? value, DbType? dbType, SalouType salouType, Type ty) RecivedFromServerConverterFkt(object? o, DbType dt, SalouType st)
        {
            //Should all be there
            switch (st)
            {
                case SalouType.NULL:
                    return (null, null, st, typeof(object));
                case SalouType.DBNull:
                    return (DBNull.Value, dt, st, ConvertDbTypeToNetType(dt));
                case SalouType.Unknown:
                    return (o, null, st, typeof(object));
                case SalouType.Binary:
                    return (o, dt, st, typeof(byte[]));
                case SalouType.Byte:
                    return (o, dt, st, typeof(byte));
                case SalouType.Boolean:
                    return (o, dt, st, typeof(bool));
                case SalouType.Decimal:
                    return (o, dt, st, typeof(decimal));
                case SalouType.Date:
                    return (o, dt, st, typeof(DateOnly));
                case SalouType.DateTime:
                    return (o, dt, st, typeof(DateTime));
                case SalouType.Double:
                    return (o, dt, st, typeof(double));
                case SalouType.Guid:
                    return (o, dt, st, typeof(Guid));
                case SalouType.Int16:
                    if (dt == DbType.UInt16)
                        return (o, dt, st, typeof(UInt16));
                    else
                        return (o, dt, st, typeof(Int16));
                case SalouType.Int32:
                    if (dt == DbType.UInt32)
                        return (o, dt, st, typeof(UInt32));
                    else
                        return (o, dt, st, typeof(Int32));
                case SalouType.Int64:
                    if (dt == DbType.UInt64)
                        return (o, dt, st, typeof(UInt64));
                    else
                        return (o, dt, st, typeof(Int64));
                case SalouType.Single:
                    return (o, dt, st, typeof(Single));
                case SalouType.String:
                    return (o, dt, st, typeof(string));
                case SalouType.Time:
                    return (o, dt, st, typeof(TimeSpan));
                case SalouType.TimeSpan:
                    return (o, dt, st, typeof(TimeSpan));
                case SalouType.Xml:
                    return (o, dt, st, typeof(XmlDocument));
                case SalouType.DateTimeOffset:
                    return (o, dt, st, typeof(DateTimeOffset));

                default:
                    throw new SalouException($"RecivedFromServerConverter: SalouType not supported {st}");
            }
        }
        /// <summary>
        /// see <see cref="ServerSendToClientConverterDelegate"/>
        /// </summary>
        /// <param name="o"></param>
        /// <param name="dt"></param>
        /// <param name="type"></param>
        /// <param name="st"></param>
        /// <returns></returns>
        /// <exception cref="SalouException"></exception>
        private static (object? value, DbType dbType, SalouType salouType) ServerSendToClientConverterFkt(object? o, DbType? dt, Type? type, SalouType? st)
        {
            if (st == null && dt == null && o == null && type == null)
                return (null, DbType.Object, SalouType.NULL);
            if (o?.GetType() == typeof(DBNull))
                return (DBNull.Value, DbType.Object, SalouType.DBNull);

            //preference to SalouType
            if (st == null)
            {
                //then DbType
                if (dt == null)
                {
                    if (type == null)
                    {
                        // if no SalouType, DbType or Type is given, we try to get the type from the object
                        if (o == null)
                            throw new SalouException("SendToServerConverter: Object is null");
                        dt = ConvertNetToDbType(o.GetType());
                    }
                    else
                    {
                        dt = ConvertNetToDbType(type);
                    }
                }
                //now switch DbType
                switch (dt.Value)
                {
                    case DbType.AnsiString:
                    case DbType.AnsiStringFixedLength:
                    case DbType.StringFixedLength:
                    case DbType.String:
                        return (o, dt.Value, SalouType.String);
                    case DbType.Binary:
                        return (o, dt.Value, SalouType.Binary);
                    case DbType.Byte:
                        return (o, dt.Value, SalouType.Byte);
                    case DbType.Boolean:
                        return (o, dt.Value, SalouType.Boolean);
                    case DbType.Currency:
                    case DbType.Decimal:
                        return (o, dt.Value, SalouType.Decimal);
                    case DbType.Date:
                        return (o, dt.Value, SalouType.Date);
                    case DbType.DateTime2:
                    case DbType.DateTime:
                        return (o, dt.Value, SalouType.DateTime);
                    case DbType.Double:
                        return (o, dt.Value, SalouType.Double);
                    case DbType.Guid:
                        return (o, dt.Value, SalouType.Guid);
                    case DbType.UInt16:
                    case DbType.Int16:
                        return (o, dt.Value, SalouType.Int16);
                    case DbType.UInt32:
                    case DbType.Int32:
                        return (o, dt.Value, SalouType.Int32);
                    case DbType.UInt64:
                    case DbType.Int64:
                        return (o, dt.Value, SalouType.Int64);
                    //case DbType.Object:
                    //    return (o, dt.Value, SalouType.Unknown);
                    case DbType.Single:
                        return (o, dt.Value, SalouType.Single);
                    case DbType.Time:
                        return (o, dt.Value, SalouType.Time);
                    case DbType.DateTimeOffset:
                        return (o, dt.Value, SalouType.DateTimeOffset);
                    case DbType.Xml:
                        return (o, dt.Value, SalouType.Xml);
                    default:
                        throw new SalouException($"SendToServerConverter: DbType not supported {dt}");
                }
                throw new SalouException($"SendToServerConverter: DbType not supported {dt}");
            }
            else if (dt == null)
            {
                //Switch saloutype and return
                switch (st.Value)
                {
                    case SalouType.DBNull:
                        return (o, DbType.Object, st.Value);
                    case SalouType.String:
                        return (o, DbType.String, st.Value);
                    case SalouType.Binary:
                        return (o, DbType.Binary, st.Value);
                    case SalouType.Byte:
                        return (o, DbType.Byte, st.Value);
                    case SalouType.Boolean:
                        return (o, DbType.Boolean, st.Value);
                    case SalouType.Decimal:
                        return (o, DbType.Decimal, st.Value);
                    case SalouType.Date:
                        return (o, DbType.Date, st.Value);
                    case SalouType.DateTime:
                        return (o, DbType.DateTime, st.Value);
                    case SalouType.Double:
                        return (o, DbType.Double, st.Value);
                    case SalouType.Guid:
                        return (o, DbType.Guid, st.Value);
                    case SalouType.Int16:
                        return (o, DbType.Int16, st.Value);
                    case SalouType.Int32:
                        return (o, DbType.Int32, st.Value);
                    case SalouType.Int64:
                        return (o, DbType.Int64, st.Value);
                    //case SalouType.Unknown:
                    //    return (o, DbType.VarNumeric, st.Value);
                    case SalouType.Single:
                        return (o, DbType.Single, st.Value);
                    case SalouType.Time:
                        return (o, DbType.Time, st.Value);
                    case SalouType.DateTimeOffset:
                        return (o, DbType.DateTimeOffset, st.Value);
                    case SalouType.Xml:
                        return (o, DbType.Xml, st.Value);
                    default:
                        throw new SalouException($"SendToServerConverter: SalouType not supported {st}");
                }

            }

            //if all are given, we return the given values
            return (o, dt.Value, st.Value);
        }
        /// <summary>
        /// see <see cref="RecivedFromClientConverterDelegate"/>
        /// </summary>
        /// <param name="o"></param>
        /// <param name="d"></param>
        /// <param name="st"></param>
        /// <returns></returns>
        /// <exception cref="SalouException"></exception>
        private static (object? value, DbType dbType, SalouType salouType) RecivedFromClientConverterFkt(object? o, DbType d, SalouType st)
        {
            //Should all be there
            switch (st)
            {
                case SalouType.NULL:
                    return (null, DbType.Object, SalouType.NULL);
                case SalouType.DBNull:
                    return (o, d, st);
                case SalouType.String:
                    return (o, d, st);
                case SalouType.Binary:
                    return (o, d, st);
                case SalouType.Byte:
                    return (o, DbType.Byte, st);
                case SalouType.Boolean:
                    return (o, DbType.Boolean, st);
                case SalouType.Decimal:
                    return (o, d, st);
                case SalouType.Date:
                    return (o, DbType.Date, st);
                case SalouType.DateTime:
                    return (o, d, st);
                case SalouType.Double:
                    return (o, DbType.Double, st);
                case SalouType.Guid:
                    return (o, DbType.Guid, st);
                case SalouType.Int16:
                    if (d == DbType.UInt16)
                        return ((uint?)o, DbType.UInt16, st);
                    else
                        return (o, DbType.Int16, st);
                case SalouType.Int32:
                    if (d == DbType.UInt32)
                        return ((uint?)o, DbType.UInt32, st);
                    else
                        return (o, DbType.Int32, st);
                case SalouType.Int64:
                    if (d == DbType.UInt64)
                        return ((ulong?)o, DbType.UInt64, st);
                    else
                        return (o, DbType.Int64, st);
                //case SalouType.Unknown:
                //    return (o, DbType.VarNumeric, st);
                case SalouType.Single:
                    return (o, DbType.Single, st);
                case SalouType.Time:
                    return (o, DbType.Time, st);
                case SalouType.DateTimeOffset:
                    return (o, DbType.DateTimeOffset, st);
                case SalouType.Xml:
                    return (o, DbType.Xml, st);
                default:
                    throw new SalouException($"SendToServerConverter: SalouType not supported {st}");
            }
        }

        /// <summary>
        /// Helper to convert .NET Type to DbType
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Type</returns>
        private static DbType ConvertNetToDbType(Type type)
        {
            if (type == typeof(Int16)) return DbType.Int16;
            else if (type == typeof(Int32)) return DbType.Int32;
            else if (type == typeof(Int64)) return DbType.Int64;
            else if (type == typeof(string)) return DbType.String;
            else if (type == typeof(byte[])) return DbType.Binary;
            else if (type == typeof(byte)) return DbType.Byte;
            else if (type == typeof(bool)) return DbType.Boolean;
            else if (type == typeof(decimal)) return DbType.Decimal;
            else if (type == typeof(Single)) return DbType.Single;
            else if (type == typeof(double)) return DbType.Double;
            else if (type == typeof(Guid)) return DbType.Guid;
            else if (type == typeof(DateTime)) return DbType.DateTime;
            else if (type == typeof(XmlDocument)) return DbType.Xml;
            else if (type == typeof(DateOnly)) return DbType.Date;
            else if (type == typeof(DateTimeOffset)) return DbType.DateTimeOffset;
            else if (type == typeof(TimeSpan)) return DbType.Time;
            else if (type == typeof(DBNull)) return DbType.Object;
            else if (type == typeof(UInt16)) return DbType.UInt16;
            else if (type == typeof(UInt32)) return DbType.UInt32;
            else if (type == typeof(UInt64)) return DbType.UInt64;
            else return DbType.Object;
        }
        /// <summary>
        /// helper to convert DbType to .NET Type
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Type</returns>
        private static Type ConvertDbTypeToNetType(DbType type)
        {
            return type switch
            {
                DbType.Int16 => typeof(Int16),
                DbType.Int32 => typeof(Int32),
                DbType.Int64 => typeof(Int64),
                DbType.String => typeof(string),
                DbType.AnsiString => typeof(string),
                DbType.AnsiStringFixedLength => typeof(string),
                DbType.StringFixedLength => typeof(string),
                DbType.Binary => typeof(byte[]),
                DbType.Byte => typeof(byte),
                DbType.Boolean => typeof(bool),
                DbType.Decimal => typeof(decimal),
                DbType.Single => typeof(Single),
                DbType.Double => typeof(double),
                DbType.Guid => typeof(Guid),
                DbType.DateTime2 => typeof(DateTime),
                DbType.DateTime => typeof(DateTime),
                DbType.Xml => typeof(XmlDocument),
                DbType.Date => typeof(DateOnly),
                DbType.DateTimeOffset => typeof(DateTimeOffset),
                DbType.Time => typeof(TimeSpan),
                DbType.Object => typeof(DBNull),
                DbType.UInt16 => typeof(UInt16),
                DbType.UInt32 => typeof(UInt32),
                DbType.UInt64 => typeof(UInt64),
                _ => typeof(object),
            };
        }
    }
}
