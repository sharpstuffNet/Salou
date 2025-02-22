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

            SalouConClosedException? ex1=null;
            (object? value, Type netType)? rt=null;
            int i = Math.Max(Salou.AutoReconnectTry, 1);
            while (i > 0 && (ex1== null || ex1.HappendWhileSending))
            {
                try
                {
                    rt = SendIntern(reqType, clientCallID.Value, para);
                    break;
                }
                catch (SalouConClosedException ex)
                {
                    ex1 = ex;
                    if (--i > 0)
                        Reconnect();
                }
            }
            if(rt==null)
                throw ex1 ?? new SalouException("Invalid Return");

            Salou.LoggerFkt(LogLevel.Trace, () => $"WSClient Result {rt?.netType}");
            if (typeof(T) == typeof(object) || rt.Value.netType == typeof(T))
                return rt.Value.value == null ? default(T) : (T)rt.Value.value;
            else if (rt.Value.value == null && rt.Value.netType == null)
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

            SalouConClosedException? ex1 = null;
            (object? value, Type netType)? rt = null;
            int i = Math.Max(Salou.AutoReconnectTry, 1);
            while (i > 0 && (ex1 == null || ex1.HappendWhileSending))
            {
                try
                {
                    rt = SendIntern(reqType, clientCallID.Value, para);
                    break;
                }
                catch (SalouConClosedException ex)
                {
                    ex1 = ex;
                    if(--i > 0)
                        Reconnect();
                }
            }
            if (rt == null)
                throw ex1 ?? new SalouException("Invalid Return");
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

            bool compressed = false;
            if (Salou.Compress != null && baOut != null && baOut.Length > Salou.Compressionthreshold)
            {
                var baOut1 = Salou.Compress(baOut);
                if (baOut1.Length < baOut.Length)
                {
                    baOut = baOut1;
                    compressed = true;
                }
            }

            //Add Header
            var baHeadO = new byte[StaticWSHelpers.SizeOfHead];
            var span = new Span<byte>(baHeadO);
            BinaryPrimitives.WriteInt32LittleEndian(span, baOut?.Length ?? 0);//cant' ref in Async
            span = span.Slice(StaticWSHelpers.SizeOfInt);
            span[0] = (byte)reqToDo; span = span.Slice(1);
            BinaryPrimitives.WriteInt32LittleEndian(span, clientCallID);
            span = span.Slice(StaticWSHelpers.SizeOfInt);
            span[0] = (byte)(compressed ? 'B' : 'L'); span = span.Slice(1);

            Salou.LoggerFkt(LogLevel.Information, () => $"Send Header {reqToDo} {clientCallID} len: {baOut?.Length ?? 0}");

            //Only 1 request at a time
            CancellationToken cancellationToken = default(CancellationToken);// new CancellationTokenSource(StaticWSHelpers.WSRequestTimeout).Token;
            if (Salou.ClientSendReciveTimeout.HasValue)
            {
                cancellationToken = new CancellationTokenSource(Salou.ClientSendReciveTimeout.Value).Token;
                if(Salou.ClientSendReciveTimeoutThrowException)
                    cancellationToken.Register(() => throw new SalouTimeoutException("Client Send/Revieve Timeout"));
            }
            CallState stateO = DOASyncSerialized<CallState>((Func<object?,Task<CallState>>)SendInternAsync, cancellationToken,
                new CallState()
                {
                    clientCallID = clientCallID,
                    baOut = baOut ?? Array.Empty<byte>(),
                    baHeadO = baHeadO,
                    rty = SalouReturnType.Nothing,
                    baIn = Array.Empty<byte>(),
                    para = para,
                    reqToDo= reqToDo
                });

            if (stateO.compressed && Salou.Decompress != null && stateO.baIn!=null)
                stateO.baIn = Salou.Decompress(stateO.baIn);

            //Process Data
            return ProcessReturn(stateO.clientCallID, stateO.rty, stateO.para, stateO.baIn ?? Array.Empty<byte>());
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
            internal bool compressed;
        }
        private async Task<CallState> SendInternAsync(object? state)
        {
            var stateO = (CallState)state!;

            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(stateO.baHeadO, WebSocketMessageType.Binary, stateO.baOut.Length == 0, CancellationToken.None);
                if (stateO.baOut.Length > 0 && _webSocket.State == WebSocketState.Open)
                    await _webSocket.SendAsync(stateO.baOut, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            if(_webSocket.State != WebSocketState.Open)
                throw new SalouConClosedException("{_webSocket.State}",true);

            stateO.baOut = Array.Empty<byte>();//Free Memory

            //Recive Header
            int reclen = 0;
            StaticWSHelpers.WsState wsRecivedState= StaticWSHelpers.WsState.OK;
            var baHead = new byte[StaticWSHelpers.SizeOfHead];
            if(_webSocket.State == WebSocketState.Open)
                (reclen, wsRecivedState) = await StaticWSHelpers.WSReciveFull(_webSocket, baHead, false);
            else
                throw new SalouConClosedException("{_webSocket.State}",false);

            //Even if its closing we look what the other side wants if we can
            if (reclen == baHead.Length)
            {
                //Process Header
                var span = new Span<byte>(baHead);
                int len = StaticWSHelpers.ReadInt(ref span);
                stateO.rty = (SalouReturnType)StaticWSHelpers.ReadByte(ref span);
                var id = StaticWSHelpers.ReadInt(ref span);
                stateO.compressed = (StaticWSHelpers.ReadByte(ref span) == 'B');

                if (id != stateO.clientCallID)
                    throw new SalouException("Invalid ID");

                //Recive Data
                stateO.baIn = new byte[len];
                if (len > 0 && wsRecivedState == StaticWSHelpers.WsState.OK && _webSocket.State == WebSocketState.Open)
                    (reclen, wsRecivedState) = await StaticWSHelpers.WSReciveFull(_webSocket, stateO.baIn);
                else
                    reclen = 0;

                Salou.LoggerFkt(LogLevel.Debug, () => $"Recieved Data. Expexted: {len} Recived: {reclen} Call# {stateO.clientCallID}");
                if (reclen != len)
                {
                    if (_webSocket.State != WebSocketState.Open)
                        throw new SalouConClosedException("{_webSocket.State}",false);
                    throw new SalouException("Invalid Data Length");
                }
            }

            if (_webSocket.State == WebSocketState.CloseReceived)
                try { _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None).Wait(); } catch { }//ignore
            if (_webSocket.State != WebSocketState.Open)
                throw new SalouConClosedException("Connection Closed",false);

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
            Salou.LoggerFkt(LogLevel.Trace, () => $"WriteBytesToSend {tranToDO} Param Length: {para?.Length}");

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
            Salou.LoggerFkt(LogLevel.Trace, () => $"ProcessReturn Data {rty} Param Length: {para?.Length} Data Length: {baIn?.Length}");
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
