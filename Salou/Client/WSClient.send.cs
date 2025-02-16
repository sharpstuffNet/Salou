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
        /// <param name="clientCallID">call id</param>
        /// <param name="para">parameters</param>
        /// <returns>result depending on type</returns>
        /// <exception cref="SalouException"></exception>
        internal T? Send<T>(SalouRequestType reqType, int? clientCallID = null, params object[] para)
        {
            if (clientCallID == null)
                clientCallID = Interlocked.Increment(ref ClientCallID);

            var rt = SendIntern(reqType, clientCallID.Value, para);
            Salou.LoggerFkt(LogLevel.Trace, () => $"WSClient Result {rt}");
            if (typeof(T) == typeof(object) || rt.netType == typeof(T))
                return rt.value == null ? default(T) : (T)rt.value;
            else if (rt.value == null && rt.netType == null)
                return default(T);
            throw new SalouException("Invalid Type");
        }
        /// <summary>
        /// Send to the Service
        /// </summary>
        /// <param name="reqType">reqType</param>
        /// <param name="clientCallID">call id</param>
        /// <param name="para">paramters</param>
        internal void Send(SalouRequestType reqType, int? clientCallID = null, params object[] para)
        {
            if (clientCallID == null)
                clientCallID = Interlocked.Increment(ref ClientCallID);
            SendIntern(reqType, clientCallID.Value, para);
        }

        /// <summary>
        /// Internal Send
        /// </summary>
        /// <param name="reqToDo">what to do</param>
        /// <param name="clientCallID">id</param>
        /// <param name="para">parameters</param>
        /// <returns>return value / type</returns>
        /// <exception cref="SalouException"></exception>
        /// <exception cref="SalouConClosedException"></exception>
        private (object? value, Type netType) SendIntern(SalouRequestType reqToDo, int clientCallID, params object[] para)
        {
            byte[] baOut;
            

            //Add Data to be send
            using (var ms = new MemoryStream())
            {
                //Write Data
                WriteBytesToSend(ms, reqToDo, para);
                baOut = ms.ToArray();
            }

            if (Salou.Compress != null)
                baOut = Salou.Compress(baOut);

            //Add Header
            var baHeadO = new byte[StaticWSHelpers.SizeOfHead];
            var span = new Span<byte>(baHeadO);
            BinaryPrimitives.WriteInt32LittleEndian(span, baOut.Length);//cant' ref in Async
            span = span.Slice(StaticWSHelpers.SizeOfInt);
            span[0] = (byte)reqToDo; span = span.Slice(1);
            BinaryPrimitives.WriteInt32LittleEndian(span, clientCallID);

            Salou.LoggerFkt(LogLevel.Information, () => $"Send Header {reqToDo} {clientCallID} len: {baOut.Length}");

            ////Only 1 request at a time
            //var (rty, baIn) = DOASyncSerialized<(SalouReturnType rty, byte[] baIn)>(async () =>
            //{
            //    //Send
            //    return await SendInternAsync(clientCallID, baOut, baHeadO);
            //    //});
            //});
            //Only 1 request at a time
            CallState stateO = DOASyncSerialized<CallState>((Func<object?,Task<CallState>>)SendInternAsync, 
                new CallState()
                {
                    clientCallID = clientCallID,
                    baOut = baOut,
                    baHeadO = baHeadO,
                    rty = SalouReturnType.Nothing,
                    baIn = Array.Empty<byte>(),
                    para = para,
                    reqToDo= reqToDo
                });

            if (Salou.DeCompress != null)
                stateO.baIn = Salou.DeCompress(stateO.baIn);

            //Process Data
            return ProcessReturn(stateO.clientCallID, stateO.rty, stateO.para, stateO.baIn);
        }
        internal struct CallState
        {
            internal int clientCallID;
            internal byte[] baOut;
            internal byte[] baHeadO;
            internal SalouReturnType rty;
            internal byte[] baIn;
            internal object[] para;
            internal SalouRequestType reqToDo;
        }
        private async Task<CallState> SendInternAsync(object? state)
        {
            var stateO = (CallState)state!;

            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(stateO.baHeadO, WebSocketMessageType.Binary, stateO.baOut.Length == 0, CancellationToken.None);
                if (stateO.baOut.Length > 0)
                    await _webSocket.SendAsync(stateO.baOut, WebSocketMessageType.Binary, true, CancellationToken.None);
            }

            stateO.baOut = Array.Empty<byte>();//Free Memory

            //Recive Header
            var baHead = new byte[StaticWSHelpers.SizeOfHead];
            var wsRecivedState = await StaticWSHelpers.WSReciveFull(_webSocket, baHead, false);
            if (wsRecivedState == StaticWSHelpers.WsState.OK)
            {

                //Process Header
                var span = new Span<byte>(baHead);
                int len = StaticWSHelpers.ReadInt(ref span);
                stateO.rty = (SalouReturnType)StaticWSHelpers.ReadByte(ref span);
                var id = StaticWSHelpers.ReadInt(ref span);

                if (id != stateO.clientCallID)
                    throw new SalouException("Invalid ID");

                //Recive Data
                stateO.baIn = new byte[len];
                if (len > 0 && _webSocket.State == WebSocketState.Open)
                    wsRecivedState = await StaticWSHelpers.WSReciveFull(_webSocket, stateO.baIn);

                Salou.LoggerFkt(LogLevel.Debug, () => $"Recieved Data. Expexted: {len} Recived: {stateO.baIn.Length} Call# {stateO.clientCallID}");
            }

            if (_webSocket.State == WebSocketState.CloseReceived)
                try { _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None).Wait(); } catch { }//ignore
            if (_webSocket.State != WebSocketState.Open)
                throw new SalouConClosedException("Connection Closed");

            return stateO;
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
            Salou.LoggerFkt(LogLevel.Trace, () => $"WriteBytesToSend {tranToDO} Param Length: {para.Length}");

            switch (tranToDO)
            {
                case SalouRequestType.ConnectionOpen:
                    {
                        if (para.Length != 2)
                            throw new SalouException("Invalid Parameter");

                        StaticWSHelpers.WriteInt(ms, Version.VERSION_NUMBER);//Version
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
                        ((CommandData)para[1]).WriteToServer(ms);
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
                        ((CommandData)para[1]).WriteToServer(ms);
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
        /// <param name="clientCallID">sid to sort</param>
        /// <param name="rty">SalouReturnType</param>
        /// <param name="para">parameters</param>
        /// <param name="baIn">incomming data</param>
        /// <returns></returns>
        /// <exception cref="SalouServerException"></exception>
        /// <exception cref="SalouException"></exception>
        private (object? value, Type netType) ProcessReturn(int clientCallID, SalouReturnType rty, object[] para, byte[] baIn)
        {
            Salou.LoggerFkt(LogLevel.Trace, () => $"ProcessReturn Data {rty} Param Length: {para.Length} Data Length: {baIn.Length}");
            var span = new Span<byte>(baIn);
            switch (rty)
            {
                case SalouReturnType.Bool:
                    return (span[0] == 0, typeof(Boolean));
                case SalouReturnType.Integer:
                    return (StaticWSHelpers.ReadInt(ref (span)), typeof(Int32));
                case SalouReturnType.Long:
                    return (StaticWSHelpers.ReadLong(ref (span)), typeof(Int64));
                case SalouReturnType.TwoInt32:
                    var i1 = StaticWSHelpers.ReadInt(ref (span));
                    var i2 = StaticWSHelpers.ReadInt(ref (span));
                    return ((i1, i2), typeof((int, int)));
                case SalouReturnType.String:
                    return (StaticWSHelpers.ReadString(ref span), typeof(string));
                case SalouReturnType.DBNull:
                    return (DBNull.Value, typeof(DBNull));
                case SalouReturnType.NullableSalouType:
                    return StaticWSHelpers.ClientRecievedSalouType(ref span);
                case SalouReturnType.Nothing:
                    return default;
                case SalouReturnType.Exception:
                    throw new SalouServerException(StaticWSHelpers.ReadString(ref span) ?? string.Empty);
                case SalouReturnType.CommandParameters:
                    return (((CommandData)para[1]).ReadReturnFromServer(ref span));
                case SalouReturnType.ReaderStart:
                case SalouReturnType.ReaderContinue:
                    return (baIn, typeof(byte[]));
                default:
                    throw new SalouException("Unknown SalouReturnType");
            }
        }


    }
}
