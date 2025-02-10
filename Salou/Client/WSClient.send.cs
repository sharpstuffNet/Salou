using Salou48.Helpers;
using SalouWS4Sql.Helpers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// Implements a Websocket Client for use with the Salou DB Provider
    /// </summary>
    internal partial class WSClient : IDisposable
    {
        /// <summary>
        /// Send to the Service
        /// </summary>
        /// <typeparam name="T">return type</typeparam>
        /// <param name="reqType">RequestType</param>
        /// <param name="sid">call id</param>
        /// <param name="para">parameters</param>
        /// <returns>result depending on type</returns>
        /// <exception cref="SalouException"></exception>
        internal T? Send<T>(SalouRequestType reqType, WsCallID? sid = null, params object[] para)
        {
            var rt = SendIntern(reqType, sid ?? new WsCallID(), para);
            SalouLog.LoggerFkt(LogLevel.Trace, () => $"WSClient Result {rt}");
            if (typeof(T) == typeof(object) || rt.Item2 == typeof(T))
                return rt.Item1 == null ? default(T) : (T)rt.Item1;
            else if (rt.Item1 == null && rt.Item2 == null)
                return default(T);
            throw new SalouException("Invalid Type");
        }
        /// <summary>
        /// Send to the Service
        /// </summary>
        /// <param name="reqType">reqType</param>
        /// <param name="sid">call id</param>
        /// <param name="para">paramters</param>
        internal void Send(SalouRequestType reqType, WsCallID? sid = null, params object[] para)
        {
            SendIntern(reqType, sid ?? new WsCallID(), para);
        }

        /// <summary>
        /// Internal Send
        /// </summary>
        /// <param name="reqToDo">what to do</param>
        /// <param name="sid">id</param>
        /// <param name="para">parameters</param>
        /// <returns>return value / type</returns>
        /// <exception cref="SalouException"></exception>
        /// <exception cref="SalouConClosedException"></exception>
        private (object?, Type) SendIntern(SalouRequestType reqToDo, WsCallID sid, params object[] para)
        {
            byte[] baOut;
            var baIn= Array.Empty<byte>();
            bool isError = false;
            string? closedDesc = string.Empty;
            WebSocketCloseStatus? closedStatus;

            //Add Data to be send
            using (var ms = new MemoryStream())
            {
                //Space for Header
                ms.Write(StaticWSHelpers.StartBaEmpty);
                //Write Data
                WriteBytesToSend(ms, reqToDo, para);
                baOut = ms.ToArray();
            }

            //Add Header
            var span = new Span<byte>(baOut);
            BinaryPrimitives.WriteInt32LittleEndian(span, Math.Max(baOut.Length - StaticWSHelpers.SizeOfHead, 0));//cant' ref in Async
            span = span.Slice(StaticWSHelpers.SizeOfInt);
            span[0] = (byte)reqToDo; span = span.Slice(1);
            BinaryPrimitives.WriteInt32LittleEndian(span, (int)sid);

            SalouLog.LoggerFkt(LogLevel.Information, () => $"Send Header {reqToDo} {sid} len: {baOut.Length - StaticWSHelpers.SizeOfHead}");

            SalouReturnType rty = SalouReturnType.Nothing;

            //Only 1 request at a time
            DOASyncSerialized(async () =>
            {
                //Send
                await _webSocket.SendAsync(baOut, WebSocketMessageType.Binary, true, CancellationToken.None);

                baOut=Array.Empty<byte>();//Free Memory

                //Recive Header
                var baHead = new byte[StaticWSHelpers.SizeOfHead];
                (isError, closedStatus, closedDesc) = await StaticWSHelpers.WSReciveFull(_webSocket, baHead, false);

                if (isError)
                {
                    if (closedStatus == null)
                        throw new SalouException(closedDesc ?? "Error Unknown");
                    else
                        throw new SalouConClosedException(closedDesc ?? "Error Unknown");
                }

                //Process Header
                var span = new Span<byte>(baHead);
                int len = StaticWSHelpers.ReadInt(ref span);
                rty = (SalouReturnType)StaticWSHelpers.ReadByte(ref span);
                var id = (WsCallID)StaticWSHelpers.ReadInt(ref span);

                if (id != sid)
                    throw new SalouException("Invalid ID");

                SalouLog.LoggerFkt(LogLevel.Debug, () => $"Recieved Data {len}=={baIn.Length} {sid}");

                //Recive Data
                baIn = new byte[len];
                if (len > 0)
                {
                    (isError, closedStatus, closedDesc) = await StaticWSHelpers.WSReciveFull(_webSocket, baIn);
                    if (isError)
                    {
                        if (closedStatus == null)
                            throw new SalouException(closedDesc ?? "Error Unknown");
                        else
                            throw new SalouConClosedException(closedDesc ?? "Error Unknown");
                    }
                }

            });

            //Process Data
            return ProcessReturn(sid, rty, para, baIn);
        }

        /// <summary>
        /// prepare the data to be send
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        /// <param name="tranToDO">SalouRequestType</param>
        /// <param name="para">parameters depending on type</param>
        /// <exception cref="SalouException"></exception>
        private void WriteBytesToSend(MemoryStream ms, SalouRequestType tranToDO, object[] para)
        {
            SalouLog.LoggerFkt(LogLevel.Trace, () => $"WriteBytesToSend Data {tranToDO} {para.Length}");

            switch (tranToDO)
            {
                case SalouRequestType.ConnectionOpen:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");

                        StaticWSHelpers.WriteInt(ms,1);//Version
                        StaticWSHelpers.WriteString(ms, (string)para[0]);//ConStr
                        StaticWSHelpers.WriteString(ms, (string)para[1]);//DataBase
                    }
                    break;
                case SalouRequestType.ChangeDatabase:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");

                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//connID
                        StaticWSHelpers.WriteString(ms, (string)para[1]);//DataBase
                    }
                    break;

                case SalouRequestType.BeginTransaction:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");

                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//connID
                        StaticWSHelpers.WriteInt(ms, (int)(IsolationLevel)para[1]);
                    }
                    break;

                case SalouRequestType.ExecuteReaderStart:
                    {
                        if (para.Length != 5)
                            throw new SalouException("Invalid Parameter");

                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//connID
                        ((CommandData)para[1]).Write(ms); 
                        StaticWSHelpers.WriteInt(ms, (int)(CommandBehavior)para[2]);
                        StaticWSHelpers.WriteInt(ms, (int)para[3]);//PageSize
                        ms.WriteByte((byte)((UseSchema)para[4]));//UseSchema                                            
                    }
                    break;
                case SalouRequestType.ContinueReader:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");
                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//ID
                        StaticWSHelpers.WriteInt(ms, (int)para[1]);//PageSize
                    }
                    break;
                case SalouRequestType.ReaderNextResult:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");
                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//ID
                        StaticWSHelpers.WriteInt(ms, (int)para[1]);//PageSize
                    }
                    break;
                case SalouRequestType.EndReader:
                    {
                        if (para.Length != 1)
                            throw new SalouException("Invalid Parameter");
                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//ID
                    }
                    break;
                //Only CommandData
                case SalouRequestType.ExecuteScalar:
                case SalouRequestType.ExecuteNonQuery:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");
                        StaticWSHelpers.WriteInt(ms, (int)para[0]);//connID
                        ((CommandData)para[1]).Write(ms);
                    }
                    break;

                //Without Parameter
                case SalouRequestType.TransactionRollback:
                case SalouRequestType.TransactionCommit:
                case SalouRequestType.CommandCancel:
                case SalouRequestType.ConnectionClose:
                case SalouRequestType.ServerVersion:
                    if (para.Length != 1)
                        throw new SalouException("Invalid Parameter");
                    StaticWSHelpers.WriteInt(ms, (int)para[0]);//ID
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Process the return data
        /// </summary>
        /// <param name="sid">sid to sort</param>
        /// <param name="rty">SalouReturnType</param>
        /// <param name="para">parameters</param>
        /// <param name="baIn">incomming data</param>
        /// <returns></returns>
        /// <exception cref="SalouServerException"></exception>
        /// <exception cref="SalouException"></exception>
        private (object?, Type) ProcessReturn(WsCallID sid, SalouReturnType rty, object[] para, byte[] baIn)
        {
            SalouLog.LoggerFkt(LogLevel.Trace, () => $"ProcessReturn Data {rty} {para.Length} {baIn.Length}");
            var span = new Span<byte>(baIn);
            switch (rty)
            {
                case SalouReturnType.Bool:
                    return (span[0] == 0, typeof(Boolean));
                case SalouReturnType.Integer:
                    return (StaticWSHelpers.ReadInt(ref (span)), typeof(Int32));
                case SalouReturnType.String:
                    return (StaticWSHelpers.ReadString(ref span), typeof(string));
                case SalouReturnType.DBNull:
                    return (DBNull.Value, typeof(DBNull));
                case SalouReturnType.NullableDBType:
                    var ty = new NullableDBType(ref span);
                    return StaticWSHelpers.ReadNullableDbTypeDAta(ty, ref span);
                case SalouReturnType.Nothing:
                    return default;
                case SalouReturnType.Exception:
                    throw new SalouServerException(StaticWSHelpers.ReadString(ref span) ?? string.Empty);
                case SalouReturnType.CommandParameters:
                    return (((CommandData)para[1]).Read(ref span));
                case SalouReturnType.ReaderStart:
                case SalouReturnType.ReaderContinue:
                    return (baIn, typeof(byte[]));
                default:
                    throw new SalouException("Unknown SalouReturnType");
            }
        }


    }
}
