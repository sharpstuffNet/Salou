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
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;


namespace Salou48.Helpers
{
    /// <summary>
    /// Extensions for .NET 4.8
    /// implements missing functions in .NET 4.8
    /// </summary>
    public static class Net48Extensions
    {
#nullable enable
#if NETFX48
        /// <summary>
        /// Recive a message from the Websocket
        /// </summary>
        /// <param name="ws">WebSocket</param>
        /// <param name="buffer">Data buffer</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>WebSocketReceiveResult</returns>
        public static async Task<WebSocketReceiveResult> ReceiveAsync(this System.Net.WebSockets.WebSocket ws, byte[] buffer, System.Threading.CancellationToken ct)
        {
            return await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        }
        /// <summary>
        /// Send a message to the Websocket
        /// </summary>
        /// <param name="ws">WebSocket</param>
        /// <param name="baOut">Data</param>
        /// <param name="mt">WebSocketMessageType</param>
        /// <param name="b">end of Message</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns>Task</returns>
        public static async Task SendAsync(this System.Net.WebSockets.WebSocket ws, byte[] baOut, WebSocketMessageType mt, bool b, CancellationToken ct)
        {
            await ws.SendAsync(new ArraySegment<byte>(baOut),mt, b, ct);
        }
        /// <summary>
        /// write a buffer to a MemoryStream the easy way
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="buffer">buffer</param>
        public static void Write(this MemoryStream ms, byte[] buffer)
        {
            ms.Write(buffer,0,buffer.Length);
        }
        /// <summary>
        /// RotateLeft int
        /// </summary>
        /// <param name="value">value</param>
        /// <param name="offset">offset</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint RotateLeft(uint value, int offset)
            => (value << offset) | (value >> (32 - offset));
        
        /// <summary>
        /// RotateLeft long
        /// </summary>
        /// <param name="value">value</param>
        /// <param name="offset">offset</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong RotateLeft(ulong value, int offset)
            => (value << offset) | (value >> (64 - offset));

        /// <summary>
        /// Reverse Endianness for long
        /// </summary>
        /// <param name="value">value</param>
        /// <returns>value reversed</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ReverseEndianness(long value)
        {
            // Operations on 32-bit values have higher throughput than
            // operations on 64-bit values, so decompose.

            return ((long)ReverseEndianness((uint)value) << 32)
                + ReverseEndianness((uint)(value >> 32));
        }
        /// <summary>
        /// RotateRight int
        /// </summary>
        /// <param name="value">value</param>
        /// <param name="offset">offset</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint RotateRight(uint value, int offset)
       => (value >> offset) | (value << (32 - offset));
        /// <summary>
        /// Reverse Endianness for uint
        /// </summary>
        /// <param name="value">value</param>
        /// <returns>value reversed</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseEndianness(uint value)
        {
            // This takes advantage of the fact that the JIT can detect
            // ROL32 / ROR32 patterns and output the correct intrinsic.
            //
            // Input: value = [ ww xx yy zz ]
            //
            // First line generates : [ ww xx yy zz ]
            //                      & [ 00 FF 00 FF ]
            //                      = [ 00 xx 00 zz ]
            //             ROR32(8) = [ zz 00 xx 00 ]
            //
            // Second line generates: [ ww xx yy zz ]
            //                      & [ FF 00 FF 00 ]
            //                      = [ ww 00 yy 00 ]
            //             ROL32(8) = [ 00 yy 00 ww ]
            //
            //                (sum) = [ zz yy xx ww ]
            //
            // Testing shows that throughput increases if the AND
            // is performed before the ROL / ROR.

            return RotateRight(value & 0x00FF00FFu, 8) // xx zz
                + RotateLeft(value & 0xFF00FF00u, 8); // ww yy
        }
        /// <summary>
        /// Read a double from a Span in Little Endian
        /// </summary>
        /// <param name="span">data</param>
        /// <returns>double</returns>
        public static double ReadDoubleLittleEndian(Span<byte> span)
        {
            return !BitConverter.IsLittleEndian ?
                BitConverter.Int64BitsToDouble(ReverseEndianness(MemoryMarshal.Read<long>(span))) :
                MemoryMarshal.Read<double>(span);
        }
        /// <summary>
        /// Read a Single from a Span in Little Endian
        /// </summary>
        /// <param name="span">span</param>
        /// <returns>Single</returns>
        public static Single ReadSingleLittleEndian(Span<byte> span)
        {
            return !BitConverter.IsLittleEndian ?
                ReverseEndianness(MemoryMarshal.Read<int>(span)) :
                MemoryMarshal.Read<float>(span);
        }
        /// <summary>
        /// Write a double to a Span in Little Endian
        /// </summary>
        /// <param name="span">buffer</param>
        /// <param name="d">double</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleLittleEndian(Span<byte> span, double d)
        {
            if (!BitConverter.IsLittleEndian)
            {
                long tmp = ReverseEndianness(BitConverter.DoubleToInt64Bits(d));
                MemoryMarshal.Write(span, ref tmp);
            }
            else
            {
                MemoryMarshal.Write(span, ref d);
            }
        }
        /// <summary>
        /// Write a Single to a Span in Little Endian
        /// </summary>
        /// <param name="span">buffer</param>
        /// <param name="s">single</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingleLittleEndian(Span<byte> span, Single s)
        {
            if (!BitConverter.IsLittleEndian)
            {
                uint tmp = ReverseEndianness((uint)s);
                MemoryMarshal.Write(span, ref tmp);
            }
            else
            {
                MemoryMarshal.Write(span, ref s);
            }
        }

#endif
    }

    //In this file because it should be replaced with NET9 code in the near Future
    /// <summary>
    /// Async Helper
    /// </summary>
    /// <remarks>Thenks to deleted User in https://www.reddit.com/r/dotnet/comments/yiacng/what_is_the_best_approach_to_call_asynchronous/?rdt=52024</remarks>
    public static class AsyncHelper
    {
#nullable enable
        private static readonly TaskFactory _taskFactory = new
            TaskFactory(CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default);

        public static TResult RunSync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default(CancellationToken))
            => _taskFactory
                .StartNew(func)
                .Unwrap()
                .GetAwaiter()
                .GetResult();

        public static T RunSync<T>(Func<object?,Task<T>> func,object state, CancellationToken cancellationToken = default(CancellationToken))
             => _taskFactory
            .StartNew(func,state, cancellationToken)
            .Unwrap()
            .GetAwaiter()
            .GetResult();

        public static void RunSync(Func<Task> func, CancellationToken cancellationToken = default(CancellationToken))
            => _taskFactory
                .StartNew(func, cancellationToken)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
    }

}
