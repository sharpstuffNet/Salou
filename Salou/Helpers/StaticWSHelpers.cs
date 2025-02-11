using Salou48.Helpers;
using SalouWS4Sql.Client;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


namespace SalouWS4Sql.Helpers
{
#nullable enable
    /// <summary>
    /// Static Helper Functions for the Websocket Service Client and Server
    /// </summary>
    internal static class StaticWSHelpers
    {
        /// <summary>
        /// Size of an Int
        /// </summary>
        internal static readonly int SizeOfInt = sizeof(int);
        /// <summary>
        /// Size of a Long
        /// </summary>
        internal static readonly int SizeOfLong = sizeof(long);
        /// <summary>
        /// Size of the Head
        /// </summary>
        internal static readonly int SizeOfHead = sizeof(int) * 2 + sizeof(byte);
        /// <summary>
        /// Head Space
        /// </summary>
        /// <remarks>Not Realy used only copyed so static works fine</remarks>
        internal static readonly byte[] StartBaEmpty = new byte[SizeOfHead];

        /// <summary>
        /// Read a full message from a WebSocket in multiple steps till long enogh and drop the rest
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="ba"></param>
        /// <param name="readRest"></param>
        /// <returns></returns>
        internal static async Task<(bool, WebSocketCloseStatus?, string?)> WSReciveFull(WebSocket ws, byte[] ba, bool readRest = true)
        {
            WebSocketReceiveResult? wssr = null;
            int recBytes = 0;
            while (recBytes < ba.Length)
            {
                wssr = await ws.ReceiveAsync(new ArraySegment<byte>(ba, recBytes, ba.Length - recBytes), System.Threading.CancellationToken.None);

                if (wssr == null || wssr.EndOfMessage)
                    return (recBytes == ba.Length, null, null);
#pragma warning disable CS8629 // Nullable value type may be null.
                if (wssr.MessageType == WebSocketMessageType.Close)
                    return (true, wssr.CloseStatus.Value, wssr.CloseStatusDescription);
#pragma warning restore CS8629 // Nullable value type may be null.
                recBytes += wssr.Count;
            }
            //drop the Rest
            while (readRest)
            {
                byte[] ba2 = new byte[1024];
                wssr = await ws.ReceiveAsync(ba2, System.Threading.CancellationToken.None);
                if (wssr == null || wssr.EndOfMessage)
                    return (recBytes == ba.Length, null, null);
#pragma warning disable CS8629 // Nullable value type may be null.
                if (wssr.MessageType == WebSocketMessageType.Close)
                    return (true, wssr.CloseStatus.Value, wssr.CloseStatusDescription);
#pragma warning restore CS8629 // Nullable value type may be null.
            }
            return (recBytes != ba.Length, null, null);
        }
        /// <summary>
        /// Read a string from a Span and move span
        /// </summary>
        /// <param name="span">span</param>
        /// <returns>string</returns>
        internal static string? ReadString(ref Span<byte> span)
        {
            var len2 = ReadInt(ref span);
            if (len2 == -1)
                return null;
            if (len2 == 0)
                return string.Empty;
#if NETFX48
            var s = Encoding.UTF8.GetString(span.Slice(0, len2).ToArray());
#else
            var s = Encoding.UTF8.GetString(span.Slice(0, len2));
#endif
            span = span.Slice(len2);
            return s;
        }
        /// <summary>
        /// Write a string to a MemoryStream
        /// incl length
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="s">s</param>
        internal static void WriteString(MemoryStream ms, string? s)
        {
            if (s == null)
            {
                WriteInt(ms, -1);
                return;
            }
            var bad = Encoding.UTF8.GetBytes(s);
            WriteInt(ms, bad.Length);
            ms.Write(bad);
        }
        /// <summary>
        /// Write a Int to a MemoryStream
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="i">int</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt(MemoryStream ms, int i)
        {
            var ba = new byte[SizeOfInt];
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(ba), i);
            ms.Write(ba);
        }
        /// <summary>
        /// Read a Int from a Span and move span
        /// </summary>
        /// <param name="span">span</param>
        /// <returns>int</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt(ref Span<byte> span)
        {
            var ret = BinaryPrimitives.ReadInt32LittleEndian(span);
            span = span.Slice(SizeOfInt);
            return ret;
        }
        /// <summary>
        /// Write a Long to a MemoryStream
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="i">long</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteLong(MemoryStream ms, long i)
        {
            var ba = new byte[SizeOfLong];
            BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(ba), i);
            ms.Write(ba);
        }
        /// <summary>
        /// Read a Long from a Span and move span
        /// </summary>
        /// <param name="span">Span</param>
        /// <returns>long</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadLong(ref Span<byte> span)
        {
            var ret = BinaryPrimitives.ReadInt64LittleEndian(span);
            span = span.Slice(SizeOfLong);
            return ret;
        }
        /// <summary>
        /// Read a Byte from a Span and move span
        /// </summary>
        /// <param name="span">Span</param>
        /// <returns>byte</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte ReadByte(ref Span<byte> span)
        {
            var ret = span[0];
            span = span.Slice(1);
            return ret;
        }

        /// <summary>
        /// Read a Int[4] from a Span and move span
        /// </summary>
        /// <param name="span">Span</param>
        /// <returns>Int[4]</returns>
        internal static int[] ReadArray4(ref Span<byte> span)
        {
            int[] bits = new int[4];
            bits[0] = ReadInt(ref span);
            bits[1] = ReadInt(ref span);
            bits[2] = ReadInt(ref span);
            bits[3] = ReadInt(ref span);

            return bits;
        }
        /// <summary>
        /// Write a Int[4] to a MemoryStream
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="bits">Int[4]</param>
        /// <exception cref="ArgumentException"></exception>
        private static void WriteArray4(MemoryStream ms, int[] bits)
        {
            if (bits.Length != 4)
                throw new ArgumentException("Array must be 4 long");

            WriteInt(ms, bits[0]);
            WriteInt(ms, bits[1]);
            WriteInt(ms, bits[2]);
            WriteInt(ms, bits[3]);
        }
        /// <summary>
        /// Read a NullableDBType from Span and move span
        /// </summary>
        /// <param name="span">data</param>
        /// <param name="Null">null or DBNull.value for null</param>
        /// <returns>object, type</returns>
        internal static (object?, Type) ReadNullableDbType(ref Span<byte> span, object? Null)
        {
            var ty = new NullableDBType(ref span);
            return ReadNullableDbTypeData(ty, ref span,Null);
        }
        /// <summary>
        /// Read a NullableDBType's Data from Span and move span
        /// </summary>
        /// <param name="ty">NullableDBType</param>
        /// <param name="span">data</param>
        /// <param name="recursiveProtection">so the object can't create a infinit loop</param>
        /// <param name="Null">null or DBNull.value for null</param>
        /// <returns>object, type</returns>
        internal static (object?, Type) ReadNullableDbTypeData(NullableDBType ty, ref Span<byte> span2,object? Null, bool recursiveProtection = false)
        {
            var span3 = span2;
            DateTimeKind kind;

            switch (ty.Type)
            {
                case DbType.AnsiString:
                    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(String));
                case DbType.Binary:
                    {
                        if (ty.IsNull)
                            return (Null, typeof(byte[]));
                        var len2 = BinaryPrimitives.ReadInt32LittleEndian(span2);
                        span2 = span2.Slice(SizeOfInt + len2);
                        return (span3.Slice(StaticWSHelpers.SizeOfInt, len2).ToArray(), typeof(byte[]));
                    }
                case DbType.Byte:
                    return (ty.IsNull) ? (Null, typeof(Byte)) : (ReadByte(ref span2), typeof(Byte));
                case DbType.Boolean:
                    return (ty.IsNull) ? (Null, typeof(Boolean)) : ((bool)(ReadByte(ref span2) == 'T'), typeof(Boolean));
                case DbType.Currency:
                    return (ty.IsNull ? Null : new decimal(StaticWSHelpers.ReadArray4(ref span2)), typeof(decimal));
                case DbType.Date:
                    return (ty.IsNull) ? (Null, typeof(DateOnly)) : (DateOnly.FromDayNumber(ReadInt(ref span2)), typeof(DateOnly));
                case DbType.DateTime:
                    if (ty.IsNull) return (Null, typeof(DateTime));
                    kind = ReadByte(ref span2) == 'U' ? DateTimeKind.Utc : DateTimeKind.Local;
                    return (new DateTime(ReadLong(ref span2), kind), typeof(DateTime));
                case DbType.Decimal:
                    return (ty.IsNull ? Null : new decimal(StaticWSHelpers.ReadArray4(ref span2)), typeof(decimal));
                case DbType.Double:
                    if (ty.IsNull) return (Null, typeof(double));
                    span2 = span2.Slice(sizeof(double));
#if NETFX48
                    return (Net48Extensions.ReadDoubleLittleEndian(span3), typeof(double));
#else
                    return (BinaryPrimitives.ReadDoubleLittleEndian(span3), typeof(double));
#endif
                case DbType.Guid:
                    if (ty.IsNull) return (Null, typeof(Guid));
                    span2 = span2.Slice(16);
#if NETFX48
                    return (new Guid(span3.ToArray()), typeof(Guid));
#else
                    return (new Guid(span3), typeof(Guid));
#endif
                case DbType.Int16:
                    if (ty.IsNull) return (Null, typeof(Int16));
                    span2 = span2.Slice(sizeof(Int16));
                    return (BinaryPrimitives.ReadInt16LittleEndian(span3), typeof(Int16));
                case DbType.Int32:
                    return (ty.IsNull) ? (Null, typeof(Int32)) : (ReadInt(ref span2), typeof(Int32));
                case DbType.Int64:
                    return (ty.IsNull) ? (Null, typeof(Int64)) : (ReadLong(ref span2), typeof(Int64));
                case DbType.Object:
                    if (ty.IsNull)
                        return (Null, typeof(Object));
                    if (!recursiveProtection)
                    {
                        //if not null try to do inner type right
                        var ty2 = new NullableDBType(ref span2);
                        var dbty = ReadNullableDbTypeData(ty2, ref span2, Null,true);
                        return (dbty.Item1, typeof(Object));
                    }
                    else
                        throw new SalouException("Recursive type protection");
                case DbType.SByte:
                    return (ty.IsNull) ? (Null, typeof(SByte)) : ((sbyte)ReadByte(ref span2), typeof(SByte));
                case DbType.Single:
                    if (ty.IsNull) return (Null, typeof(Single));
                    span2 = span2.Slice(sizeof(Single));
#if NETFX48
                    return (Net48Extensions.ReadSingleLittleEndian(span3), typeof(Single));
#else
                    return (BinaryPrimitives.ReadSingleLittleEndian(span3), typeof(Single));
#endif

                case DbType.String:
                    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(String));
                case DbType.Time:
                    return (ty.IsNull) ? (Null, typeof(TimeOnly)) : (new TimeOnly(ReadLong(ref span2)), typeof(TimeOnly));
                case DbType.UInt16:
                    if (ty.IsNull) return (Null, typeof(UInt16));
                    span2 = span2.Slice(sizeof(UInt16));
                    return (BinaryPrimitives.ReadUInt16LittleEndian(span3), typeof(UInt16));
                case DbType.UInt32:
                    return (ty.IsNull) ? (Null, typeof(UInt32)) : ((UInt32)ReadInt(ref span2), typeof(UInt32));
                case DbType.UInt64:
                    return (ty.IsNull) ? (Null, typeof(UInt64)) : ((UInt64)ReadLong(ref span2), typeof(Int64));
                case DbType.VarNumeric:
                    throw new SalouException("Unsupported Type VarNumeric");
                case DbType.AnsiStringFixedLength:
                    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(string));//Could optimize with trim and 2nd length
                case DbType.StringFixedLength:
                    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(string));//Could optimize with trim and 2nd length
                case DbType.Xml:
                    if (ty.IsNull)
                        return (Null, typeof(System.Xml.XmlDocument));
                    else
                    {
                        var st = StaticWSHelpers.ReadString(ref span2);
                        if (st == null)
                            throw new SalouException("XML is null");
                        var xd = new System.Xml.XmlDocument();
                        xd.LoadXml(st);
                        return (xd, typeof(System.Xml.XmlDocument));
                    }
                case DbType.DateTime2:
                    if (ty.IsNull) return (Null, typeof(DateTime));
                    kind = ReadByte(ref span2) == 'U' ? DateTimeKind.Utc : DateTimeKind.Local;
                    return (new DateTime(ReadLong(ref span2)), typeof(DateTime));
                case DbType.DateTimeOffset:
                    if (ty.IsNull) return (Null, typeof(DateTimeOffset));
                    var ticks = ReadLong(ref span2);
                    var offSetTicks = ReadLong(ref span2);
                    return (new DateTimeOffset(ticks, new TimeSpan(offSetTicks)), typeof(DateTimeOffset));
                default:
                    throw new SalouException("Unknown DbType");
            }
        }
        /// <summary>
        /// Write a object to a MemoryStream as a NullableDBType
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="obj">object</param>
        /// <param name="unsupportedTypeAsString">add unsupportedTypeAsString instead of throw</param>
        /// <exception cref="SalouException"></exception>
        internal static void WriteObjectAsDBType(MemoryStream ms, object? obj, bool unsupportedTypeAsString = false)
        {
            if (obj == null)
            {
                WriteNullableDbType(ms, new NullableDBType(DbType.Object, true), null);
                return;
            }
            switch (obj)
            {
                case Int16:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Int16), obj);
                    break;
                case Int32:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Int32), obj);
                    break;
                case Int64:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Int64), obj);
                    break;
                case string:
                    WriteNullableDbType(ms, new NullableDBType(DbType.String), obj);
                    break;
                case byte[]:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Binary), obj);
                    break;
                case byte:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Byte), obj);
                    break;
                case bool:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Boolean), obj);
                    break;
                case decimal:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Currency), obj);
                    break;
                case Single:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Single), obj);
                    break;
                case sbyte:
                    WriteNullableDbType(ms, new NullableDBType(DbType.SByte), obj);
                    break;
                case UInt16:
                    WriteNullableDbType(ms, new NullableDBType(DbType.UInt16), obj);
                    break;
                case UInt32:
                    WriteNullableDbType(ms, new NullableDBType(DbType.UInt32), obj);
                    break;
                case UInt64:
                    WriteNullableDbType(ms, new NullableDBType(DbType.UInt64), obj);
                    break;
                case double:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Double), obj);
                    break;
                case Guid:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Guid), obj);
                    break;
                case DateTime:
                    WriteNullableDbType(ms, new NullableDBType(DbType.DateTime), obj);
                    break;
                case XmlDocument:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Xml), obj);
                    break;
                case DateOnly:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Date), obj);
                    break;
                case DateTimeOffset:
                    WriteNullableDbType(ms, new NullableDBType(DbType.DateTimeOffset), obj);
                    break;
                case TimeOnly:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Time), obj);
                    break;
                case DBNull:
                    WriteNullableDbType(ms, new NullableDBType(DbType.Object), null);
                    break;
                default:
                    if (unsupportedTypeAsString)
                        WriteNullableDbType(ms, new NullableDBType(DbType.String), obj.ToString());
                    else
                        throw new SalouException($"Unsupported Type {obj.GetType()}");
                    break;
            }
        }
        /// <summary>
        /// Write a NullableDBType to a MemoryStream
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="ty">NullableDBType</param>
        /// <param name="obj">object</param>
        /// <exception cref="SalouException"></exception>
        internal static void WriteNullableDbType(MemoryStream ms, NullableDBType ty, object? obj)
        {
            NullableDBType ty2 = new NullableDBType(ty.Type, obj == null || obj is DBNull);
            ty2.ToMs(ms);

            if (obj == null || obj is DBNull)
                return;

            byte[] ba2;

            switch (ty2.Type)
            {                
                case DbType.Binary:
                    WriteInt(ms, ((byte[])obj).Length);
                    ms.Write((byte[])obj);
                    break;
                case DbType.Byte:
                    ms.WriteByte((byte)obj);
                    break;
                case DbType.Boolean:
                    ms.WriteByte((byte)((bool)obj ? 'T' : 'F'));
                    break;
                case DbType.Currency:
                    StaticWSHelpers.WriteArray4(ms, decimal.GetBits((decimal)obj));
                    break;
                case DbType.Date:
                    WriteInt(ms, ((DateOnly)obj).DayNumber);
                    break;
                case DbType.DateTime:
                    var dt = (DateTime)obj;
                    ms.WriteByte((byte)(dt.Kind == DateTimeKind.Utc ? 'U' : 'L'));
                    WriteLong(ms, dt.Ticks);
                    break;
                case DbType.Decimal:
                    StaticWSHelpers.WriteArray4(ms, decimal.GetBits((decimal)obj));
                    break;
                case DbType.Double:
                    ba2 = new byte[sizeof(double)];
#if NETFX48
                    Net48Extensions.WriteDoubleLittleEndian(new Span<byte>(ba2), (double)obj);
#else
                    BinaryPrimitives.WriteDoubleLittleEndian(new Span<byte>(ba2), (double)obj);
#endif
                    ms.Write(ba2);
                    break;
                case DbType.Guid:
                    ms.Write(((Guid)obj).ToByteArray());
                    break;
                case DbType.Int16:
                    ba2 = new byte[sizeof(Int16)];
                    BinaryPrimitives.WriteInt16LittleEndian(new Span<byte>(ba2), (Int16)obj);
                    ms.Write(ba2);
                    break;
                case DbType.Int32:
                    WriteInt(ms, (Int32)obj);
                    break;
                case DbType.Int64:
                    WriteLong(ms, (Int64)obj);
                    break;
                case DbType.Object:
                    WriteObjectAsDBType(ms, obj);
                    break;
                case DbType.SByte:
                    ms.WriteByte((byte)(sbyte)obj);
                    break;
                case DbType.Single:
                    ba2 = new byte[sizeof(Single)];
#if NETFX48
                    Net48Extensions.WriteSingleLittleEndian(new Span<byte>(ba2), (Single)obj);
#else
                    BinaryPrimitives.WriteSingleLittleEndian(new Span<byte>(ba2), (Single)obj);
#endif

                    ms.Write(ba2);
                    break;               
                case DbType.Time:
                    WriteLong(ms, ((TimeOnly)obj).Ticks);
                    break;
                case DbType.UInt16:
                    ba2 = new byte[sizeof(UInt16)];
                    BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(ba2), (UInt16)obj);
                    ms.Write(ba2);
                    break;
                case DbType.UInt32:
                    WriteInt(ms, (int)(UInt32)obj);
                    break;
                case DbType.UInt64:
                    WriteLong(ms, (Int64)(UInt64)obj);
                    break;
                case DbType.VarNumeric:
                    throw new SalouException("Unsupported Type VarNumeric");
                case DbType.String:                    
                case DbType.AnsiString:                   
                case DbType.AnsiStringFixedLength:                    
                case DbType.StringFixedLength:
                    StaticWSHelpers.WriteString(ms, (string)obj);
                    break;
                case DbType.Xml:
                    StaticWSHelpers.WriteString(ms, ((XmlDocument)obj).InnerXml);
                    break;
                case DbType.DateTime2:
                    var dt2 = (DateTime)obj;
                    ms.WriteByte((byte)(dt2.Kind == DateTimeKind.Utc ? 'U' : 'L'));
                    WriteLong(ms, dt2.Ticks);
                    break;
                case DbType.DateTimeOffset:
                    var dto = (DateTimeOffset)obj;
                    WriteLong(ms, dto.Ticks);
                    WriteLong(ms, dto.Offset.Ticks);
                    break;
                default:
                    throw new SalouException($"Unsupported Type {obj.GetType()}");
            }
        }


        internal static DbType DotNetTypeToDbType(Type type,bool unsupportedTypeAsString = false)
        {
            if (type == typeof(Int16)) return DbType.Int16;
            if (type == typeof(Int32)) return DbType.Int32;
            if (type == typeof(Int64)) return DbType.Int64;
            if (type == typeof(string)) return DbType.String;
            if (type == typeof(byte[])) return DbType.Binary;
            if (type == typeof(byte)) return DbType.Byte;
            if (type == typeof(bool)) return DbType.Boolean;
            if (type == typeof(decimal)) return DbType.Currency;
            if (type == typeof(Single)) return DbType.Single;
            if (type == typeof(sbyte)) return DbType.SByte;
            if (type == typeof(UInt16)) return DbType.UInt16;
            if (type == typeof(UInt32)) return DbType.UInt32;
            if (type == typeof(UInt64)) return DbType.UInt64;
            if (type == typeof(double)) return DbType.Double;
            if (type == typeof(Guid)) return DbType.Guid;
            if (type == typeof(DateTime)) return DbType.DateTime;
            if (type == typeof(XmlDocument)) return DbType.Xml;
            if (type == typeof(DateOnly)) return DbType.Date;
            if (type == typeof(DateTimeOffset)) return DbType.DateTimeOffset;
            if (type == typeof(TimeOnly)) return DbType.Time;
            //"System.Data.SqlTypes.SqlString"
            if (unsupportedTypeAsString)
                return DbType.String;
            throw new SalouException($"Unsupported Type {type}");
        }

        /// <summary>
        /// Convert a DbType to a .NET Type
        /// </summary>
        /// <param name="ty">DbType</param>
        /// <returns>Type</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal static Type DbTypeToNetType(DbType ty)
        {
            switch (ty)
            {
                case DbType.AnsiString:
                case DbType.String:
                case DbType.AnsiStringFixedLength:
                case DbType.StringFixedLength:
                    return typeof(string);
                case DbType.Binary:
                    return typeof(byte[]);
                case DbType.Byte:
                    return typeof(byte);
                case DbType.Boolean:
                    return typeof(bool);
                case DbType.Currency:
                case DbType.Decimal:
                    return typeof(decimal);
                case DbType.Date:
                    return typeof(DateOnly);
                case DbType.DateTime:
                case DbType.DateTime2:
                    return typeof(DateTime);
                case DbType.Double:
                    return typeof(double);
                case DbType.Guid:
                    return typeof(Guid);
                case DbType.Int16:
                    return typeof(short);
                case DbType.Int32:
                    return typeof(int);
                case DbType.Int64:
                    return typeof(long);
                case DbType.Object:
                    return typeof(object);
                case DbType.SByte:
                    return typeof(sbyte);
                case DbType.Single:
                    return typeof(float);
                case DbType.Time:
                    return typeof(TimeOnly);
                case DbType.UInt16:
                    return typeof(ushort);
                case DbType.UInt32:
                    return typeof(uint);
                case DbType.UInt64:
                    return typeof(ulong);
                case DbType.Xml:
                    return typeof(XmlDocument);
                case DbType.DateTimeOffset:
                    return typeof(DateTimeOffset);
                default:
                    throw new ArgumentOutOfRangeException(nameof(ty), $"Unsupported DbType: {ty}");
            }
        }
    }
}
