using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Salou48.Helpers;
using SalouWS4Sql.Client;
using System;
using System.Buffers.Binary;

using System.Data;

using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

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
        internal static readonly int SizeOfHead = sizeof(int) * 2 + 2* sizeof(byte);
        /// <summary>
        /// Head Space
        /// </summary>
        /// <remarks>Not Realy used only copyed so static works fine</remarks>
        internal static readonly byte[] StartBaEmpty = new byte[SizeOfHead];

        /// <summary>
        /// Socket State
        /// </summary>
        internal enum WsState
        {
            OK,
            Error,
            Closed,
            Closing
        }
        /// <summary>
        /// Read a full message from a WebSocket in multiple steps till long enogh and drop the rest
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="ba"></param>
        /// <param name="readRest"></param>
        /// <returns></returns>
        internal static async Task<WsState> WSReciveFull(WebSocket ws, byte[] ba, bool readRest = true)
        {
            try
            {
                WebSocketReceiveResult? wssr = null;
                int recBytes = 0;
                while (recBytes < ba.Length)
                {
                    if (ws.State != WebSocketState.Open)
                        return (ws.State == WebSocketState.CloseReceived ? WsState.Closing : WsState.Closed);

                    wssr = await ws.ReceiveAsync(new ArraySegment<byte>(ba, recBytes, ba.Length - recBytes), System.Threading.CancellationToken.None);
                    recBytes += wssr.Count;

                    if (wssr.EndOfMessage)
                        return (recBytes == ba.Length ? WsState.OK : WsState.Error);

                    else if (wssr.MessageType == WebSocketMessageType.Close)
                        return (WsState.Closing);
                }
                //drop the Rest
                while (readRest)
                {
                    if (ws.State != WebSocketState.Open)
                        return (ws.State == WebSocketState.CloseReceived ? WsState.Closing : WsState.Closed);

                    byte[] ba2 = new byte[1024];
                    wssr = await ws.ReceiveAsync(ba2, System.Threading.CancellationToken.None);
                    if (wssr.EndOfMessage)
                        break;
                }
                return (recBytes == ba.Length ? WsState.OK : WsState.Error);
            }
            catch(System.Net.WebSockets.WebSocketException)
            {
                Salou.LoggerFkt(LogLevel.Trace, () => $"ConnectionResetException");
                return WsState.Error;
            }
            catch (ConnectionResetException)
            {
                Salou.LoggerFkt(LogLevel.Trace , () => $"ConnectionResetException");
                return WsState.Error;
            }
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
        /// Read a Value from a Span and move span
        /// </summary>
        /// <param name="span">Data</param>
        /// <returns>Value and Type</returns>
        internal static (object? value, Type netType) ClientRecievedSalouType(ref Span<byte> span)
        {
            var ( v,  d,  t) = Read(ref span,true);
            (object? value, DbType? dbType, SalouType salouType, Type ty) = Salou.RecivedFromServerConverter(v, d, t);
            return (value,ty);
        }
        /// <summary>
        /// Read a Value from a Span and move span
        /// </summary>
        /// <param name="span">Data</param>
        /// <returns>Value and DbType</returns>
        internal static (object? value, DbType? dbType) ClientRecievedDBType(ref Span<byte> span)
        {
            var (v, d, t) = Read(ref span, true);
            (object? value, DbType? dbType, SalouType salouType, Type ty) = Salou.RecivedFromServerConverter(v, d, t);
            return (value, dbType);
        }
        /// <summary>
        /// Read a Value from a Span and move span, then Drop Value
        /// </summary>
        /// <param name="span">Data</param>
        internal static void DropSalouType(ref Span<byte> span)
        {
            Read(ref span);
        }

        /// <summary>
        /// Write a Value to a MemoryStream Client toServer
        /// using Salou.SendToServerConverter
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="dbType">dbType</param>
        /// <param name="value">value</param>
        internal static void ClientWriteSalouType(MemoryStream ms, DbType? dbType, object? value)
        {
            var (v,d,t) = Salou.SendToServerConverter(value, dbType, null);
            Write(ms,v,d,t);         
        }

        /// <summary>
        /// Write a Value to a MemoryStream Server To Client
        /// using Salou.ServerSendToClientConverter
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="dbType">dbType</param>
        /// <param name="type">type</param>
        /// <param name="value">value</param>
        internal static void ServerWriteSalouType(MemoryStream ms, DbType? dbType,Type? type, object? value)
        {
            var (v, d, t) = Salou.ServerSendToClientConverter(value, dbType,type, null);
            Write(ms, v, d, t);
        }

        /// <summary>
        /// Read a Value from a Span and move span Server from Client
        /// using Salou.RecivedFromClientConverter
        /// </summary>
        /// <param name="span">Data</param>
        /// <returns>value and Type</returns>
        internal static (object? value, DbType dbType, SalouType salouType) ServerRecievedSalouType(ref Span<byte> span)
        {
            (object? v, DbType d, SalouType t) = Read(ref span);
            (object? v1, DbType d1, SalouType t1) = Salou.RecivedFromClientConverter(v, d, t);
            return (v1, d1, t1);
        }

        /// <summary>
        /// Read a Value from a Span and move span
        /// </summary>
        /// <param name="span2">span</param>
        /// <param name="DBNull4null">replace null w. DBNull</param>
        /// <returns>value and Type</returns>
        /// <exception cref="SalouException"></exception>
        private static (object? obj, DbType dbType, SalouType salouType) Read(ref Span<byte> span2, bool DBNull4null = false)
        {
            var Null = DBNull4null ? DBNull.Value : null;

            var dbType = (DbType)ReadByte(ref span2);
            SalouType salouType;
            var ty = ReadByte(ref span2);
            if (ty > 127) {
                salouType = (SalouType)(ty - 128);
                return (Null, dbType, salouType);
            }
            salouType = (SalouType)ty;
            if (salouType == SalouType.DBNull)
                return (DBNull.Value, dbType, salouType);
                        
            var span3 = span2;
            DateTimeKind kind;

            switch (salouType)
            {
                //case SalouType.AnsiString:
                //    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(String));
                case SalouType.Binary:
                    {
                        var len2 = BinaryPrimitives.ReadInt32LittleEndian(span2);
                        span2 = span2.Slice(SizeOfInt + len2);
                        return (span3.Slice(StaticWSHelpers.SizeOfInt, len2).ToArray(), dbType, salouType);
                    }
                case SalouType.Byte:
                    return (ReadByte(ref span2), dbType, salouType);
                case SalouType.Boolean:
                    return ((bool)(ReadByte(ref span2) == 'T'), dbType, salouType);
                //case SalouType.Currency:
                //    return (ty.IsNull ? Null : new decimal(StaticWSHelpers.ReadArray4(ref span2)), typeof(decimal));
                case SalouType.Date:
                    return (DateOnly.FromDayNumber(ReadInt(ref span2)), dbType, salouType);
                case SalouType.DateTime:
                    kind = ReadByte(ref span2) == 'U' ? DateTimeKind.Utc : DateTimeKind.Local;
                    return (new DateTime(ReadLong(ref span2), kind), dbType, salouType);
                case SalouType.Decimal:
                    return (new decimal(StaticWSHelpers.ReadArray4(ref span2)), dbType, salouType);
                case SalouType.Double:                    
                    span2 = span2.Slice(sizeof(double));
#if NETFX48
                    return (Net48Extensions.ReadDoubleLittleEndian(span3), dbType, salouType);
#else
                    return (BinaryPrimitives.ReadDoubleLittleEndian(span3), dbType, salouType);
#endif
                case SalouType.Guid:
                    span2 = span2.Slice(16);
#if NETFX48
                    return (new Guid(span3.ToArray()), dbType, salouType);
#else
                    return (new Guid(span3), dbType, salouType);
#endif
                case SalouType.Int16:                    
                    span2 = span2.Slice(sizeof(Int16));
                    return (BinaryPrimitives.ReadInt16LittleEndian(span3), dbType, salouType);
                case SalouType.Int32:
                    return (ReadInt(ref span2), dbType, salouType);
                case SalouType.Int64:
                    return (ReadLong(ref span2), dbType, salouType);
                //case SalouType.Object:
                //    if (ty.IsNull)
                //        return (Null, typeof(Object));
                //    if (!recursiveProtection)
                //    {
                //        //if not null try to do inner type right
                //        var ty2 = new NullableSalouType(ref span2);
                //        var dbty = ReadNullableSalouTypeData(ty2, ref span2, Null, true);
                //        return (dbty.Item1, typeof(Object));
                //    }
                //    else
                //        throw new SalouException("Recursive type protection");
                //case SalouType.SByte:
                //    return (ty.IsNull) ? (Null, typeof(SByte)) : ((sbyte)ReadByte(ref span2), typeof(SByte));
                case SalouType.Single:                    
                    span2 = span2.Slice(sizeof(Single));
#if NETFX48
                    return (Net48Extensions.ReadSingleLittleEndian(span3), dbType, salouType);
#else
                    return (BinaryPrimitives.ReadSingleLittleEndian(span3), dbType, salouType);
#endif
                case SalouType.String:
                    return (StaticWSHelpers.ReadString(ref span2), dbType, salouType);
                case SalouType.Time:
                    return (new TimeOnly(ReadLong(ref span2)), dbType, salouType);
                case SalouType.TimeSpan:
                    return (new TimeSpan(ReadLong(ref span2)), dbType, salouType);
                //case SalouType.UInt16:
                //    if (ty.IsNull) return (Null, typeof(UInt16));
                //    span2 = span2.Slice(sizeof(UInt16));
                //    return (BinaryPrimitives.ReadUInt16LittleEndian(span3), typeof(UInt16));
                //case SalouType.UInt32:
                //    return (ty.IsNull) ? (Null, typeof(UInt32)) : ((UInt32)ReadInt(ref span2), typeof(UInt32));
                //case SalouType.UInt64:
                //    return (ty.IsNull) ? (Null, typeof(UInt64)) : ((UInt64)ReadLong(ref span2), typeof(Int64));
                //case SalouType.VarNumeric:
                //    throw new SalouException("Unsupported Type VarNumeric");
                //case SalouType.AnsiStringFixedLength:
                //    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(string));//Could optimize with trim and 2nd length
                //case SalouType.StringFixedLength:
                //    return (ty.IsNull ? Null : StaticWSHelpers.ReadString(ref span2), typeof(string));//Could optimize with trim and 2nd length
                case SalouType.Xml:                   
                    {
                        var st = StaticWSHelpers.ReadString(ref span2);
                        if (st == null)
                            throw new SalouException("XML is null");
                        var xd = new System.Xml.XmlDocument();
                        xd.LoadXml(st);
                        return (xd, dbType, salouType);
                    }
                //case SalouType.DateTime2:
                //    if (ty.IsNull) return (Null, typeof(DateTime));
                //    kind = ReadByte(ref span2) == 'U' ? DateTimeKind.Utc : DateTimeKind.Local;
                //    return (new DateTime(ReadLong(ref span2)), typeof(DateTime));
                case SalouType.DateTimeOffset:                 
                    var ticks = ReadLong(ref span2);
                    var offSetTicks = ReadLong(ref span2);
                    return (new DateTimeOffset(ticks, new TimeSpan(offSetTicks)), dbType, salouType);
                default:
                    throw new SalouException("Unknown SalouType");
            }
            throw new SalouException("Unknown SalouType!");
        }

        /// <summary>
        /// Write a Value to a MemoryStream
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="obj">value</param>
        /// <param name="dbType">dbType</param>
        /// <param name="salouType">salouType</param>
        /// <exception cref="SalouException"></exception>
        private static void Write(MemoryStream ms, object? obj, DbType dbType, SalouType salouType)
        {
            ms.WriteByte((byte)dbType);

            if (obj == null)
            {
                ms.WriteByte((byte)(((byte)salouType) + 128));
                return;
            }
            ms.WriteByte((byte)salouType);

            byte[] ba2;

            switch (salouType)
            {
                case SalouType.DBNull:
                    break;
                case SalouType.Binary:
                    WriteInt(ms, ((byte[])obj).Length);
                    ms.Write((byte[])obj);
                    break;
                case SalouType.Byte:
                    ms.WriteByte((byte)obj);
                    break;
                case SalouType.Boolean:
                    ms.WriteByte((byte)((bool)obj ? 'T' : 'F'));
                    break;
                //case SalouType.Currency:
                //    StaticWSHelpers.WriteArray4(ms, decimal.GetBits((decimal)obj));
                //    break;
                case SalouType.Date:
                    WriteInt(ms, ((DateOnly)obj).DayNumber);
                    break;
                case SalouType.DateTime:
                    var dt = (DateTime)obj;
                    ms.WriteByte((byte)(dt.Kind == DateTimeKind.Utc ? 'U' : 'L'));
                    WriteLong(ms, dt.Ticks);
                    break;
                case SalouType.Decimal:
                    StaticWSHelpers.WriteArray4(ms, decimal.GetBits((decimal)obj));
                    break;
                case SalouType.Double:
                    ba2 = new byte[sizeof(double)];
#if NETFX48
                    Net48Extensions.WriteDoubleLittleEndian(new Span<byte>(ba2), (double)obj);
#else
                    BinaryPrimitives.WriteDoubleLittleEndian(new Span<byte>(ba2), (double)obj);
#endif
                    ms.Write(ba2);
                    break;
                case SalouType.Guid:
                    ms.Write(((Guid)obj).ToByteArray());
                    break;
                case SalouType.Int16:
                    ba2 = new byte[sizeof(Int16)];
                    BinaryPrimitives.WriteInt16LittleEndian(new Span<byte>(ba2), (Int16)obj);
                    ms.Write(ba2);
                    break;
                case SalouType.Int32:
                    WriteInt(ms, (Int32)obj);
                    break;
                case SalouType.Int64:
                    WriteLong(ms, (Int64)obj);
                    break;
                //case SalouType.Object:
                //    WriteObjectAsSalouType(ms, obj);
                //    break;
                //case SalouType.SByte:
                //    ms.WriteByte((byte)(sbyte)obj);
                //    break;
                case SalouType.Single:
                    ba2 = new byte[sizeof(Single)];
#if NETFX48
                    Net48Extensions.WriteSingleLittleEndian(new Span<byte>(ba2), (Single)obj);
#else
                    BinaryPrimitives.WriteSingleLittleEndian(new Span<byte>(ba2), (Single)obj);
#endif

                    ms.Write(ba2);
                    break;
                case SalouType.Time:
                    WriteLong(ms, ((TimeOnly)obj).Ticks);
                    break;
                case SalouType.TimeSpan:
                    WriteLong(ms, ((TimeSpan)obj).Ticks);
                    break;
                //case SalouType.UInt16:
                //    ba2 = new byte[sizeof(UInt16)];
                //    BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(ba2), (UInt16)obj);
                //    ms.Write(ba2);
                //    break;
                //case SalouType.UInt32:
                //    WriteInt(ms, (int)(UInt32)obj);
                //    break;
                //case SalouType.UInt64:
                //    WriteLong(ms, (Int64)(UInt64)obj);
                //    break;
                //case SalouType.VarNumeric:
                //    throw new SalouException("Unsupported Type VarNumeric");
                case SalouType.String:
                    //case SalouType.AnsiString:
                    //case SalouType.AnsiStringFixedLength:
                    //case SalouType.StringFixedLength:
                    StaticWSHelpers.WriteString(ms, (string)obj);
                    break;
                case SalouType.Xml:
                    StaticWSHelpers.WriteString(ms, ((XmlDocument)obj).InnerXml);
                    break;
                //case SalouType.DateTime2:
                //    var dt2 = (DateTime)obj;
                //    ms.WriteByte((byte)(dt2.Kind == DateTimeKind.Utc ? 'U' : 'L'));
                //    WriteLong(ms, dt2.Ticks);
                //    break;
                case SalouType.DateTimeOffset:
                    var dto = (DateTimeOffset)obj;
                    WriteLong(ms, dto.Ticks);
                    WriteLong(ms, dto.Offset.Ticks);
                    break;
                default:
                    throw new SalouException($"Unsupported Type {obj.GetType()}");
            }
        }
    }
}
