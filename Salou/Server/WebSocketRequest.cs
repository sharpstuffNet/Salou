using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SalouWS4Sql.Client;
using SalouWS4Sql.Helpers;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Net.WebSockets;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using static SalouWS4Sql.Helpers.StaticWSHelpers;

namespace SalouWS4Sql.Server
{
    /// <summary>
    /// WebSocket Request handler
    /// </summary>
    internal class WebSocketRequest : IDisposable
    {

        /// <summary>
        /// WebSocket Request ID
        /// </summary>
        public static int WSRID = 0;

        /// <summary>
        /// WebSocket Request ID
        /// </summary>
        int _WSRID;

        /// <summary>
        /// WebSocket
        /// </summary>
        WebSocket _ws;
        /// <summary>
        /// Logger
        /// </summary>
        ILogger? _logger;
        /// <summary>
        /// the WebSocket Server
        /// </summary>
        WebSocketServer _wss;
        /// <summary>
        /// HttpContext
        /// </summary>
        HttpContext _ctx;
        /// <summary>
        /// All DataReaders
        /// </summary>
        SimpleConcurrentDic<int, DataReader> _allRDR = new();
        /// <summary>
        /// All Connections
        /// </summary>
        SimpleConcurrentDic<int, DbConnection> _allCons = new();
        /// <summary>
        /// All Commands
        /// </summary>
        SimpleConcurrentDic<int, DbCommand> _allCmd = new();
        /// <summary>
        /// All Transactions
        /// </summary>
        SimpleConcurrentDic<int, DbTransaction> _allTrans = new();

        /// <summary>
        /// Create a new WebSocketRequest
        /// </summary>
        /// <param name="wss">WebSocketServer</param>
        /// <param name="ws">WebSocket</param>
        /// <param name="ctx">HttpContext</param>
        internal WebSocketRequest(WebSocketServer wss, WebSocket ws, HttpContext ctx)
        {
            _WSRID = Interlocked.Increment(ref WSRID);
            if (_WSRID > int.MaxValue - 10)
                _WSRID = Interlocked.Exchange(ref WSRID, _WSRID - int.MaxValue);

            _logger = wss.Logger;

            _ws = ws;
            _wss = wss;
            _ctx = ctx;
        }
        /// <summary>
        /// Check if the WebSocket is open from the timer
        /// </summary>
        /// <returns></returns>
        internal bool Check()
        {
            return _ws.State == WebSocketState.Open;
        }
        /// <summary>
        /// CleanUp
        /// </summary>
        public void Dispose()
        {
            //Close alconnections should close related stuff
            //lets relay on that for now
            foreach (var item in _allCons)
            {
                try
                {
                    if (item?.State == ConnectionState.Open)
                        item?.Close();
                    item?.Dispose();
                }
                catch { }
            }
        }
        /// <summary>
        /// Actual Recive and Send Loop
        /// </summary>
        /// <returns>Task</returns>
        internal async Task Recive()
        {
            try
            {
                //Init
                StaticWSHelpers.WsState recivedState = StaticWSHelpers.WsState.OK;
                var baHead = new byte[StaticWSHelpers.SizeOfHead];

                //Loop as long as no error
                while (recivedState == StaticWSHelpers.WsState.OK)
                {
                    //Data per request
                    var baIn = Array.Empty<byte>();
                    SalouReturnType rty = SalouReturnType.Nothing;
                    Span<byte> span;
                    int len = 0;
                    SalouRequestType? reqToDo = null;
                    int? sid = null;
                    int reclen = 0;

                    //Recive Header
                    (reclen, recivedState) = await StaticWSHelpers.WSReciveFull(_ws, baHead, false);

                    //Even if its closing we look what the other side wants if we can
                    if (reclen == baHead.Length)
                    {
                        span = new Span<byte>(baHead);
                        len = StaticWSHelpers.ReadInt(ref span);
                        reqToDo = (SalouRequestType)StaticWSHelpers.ReadByte(ref span);
                        sid = StaticWSHelpers.ReadInt(ref span);
                        bool compressed1 = (StaticWSHelpers.ReadByte(ref span) == 'B');

                        Salou.LoggerFkt(LogLevel.Information, () => $"WSR {_WSRID}: Recieved Header {reqToDo} Call# {sid} len: {reclen}");

                        if (recivedState == StaticWSHelpers.WsState.Closed || recivedState == StaticWSHelpers.WsState.Closing)
                            break;

                        //Recive Data
                        baIn = new byte[len];
                        if (len > 0 && recivedState == WsState.OK && _ws.State == WebSocketState.Open)
                            (reclen, recivedState) = await StaticWSHelpers.WSReciveFull(_ws, baIn);
                        else
                            reclen = 0;

                        if (reclen == len)
                        {
                            Salou.LoggerFkt(LogLevel.Debug, () => $"WSR {_WSRID}: Recieved Data Expected: {len} Recived: {reclen} Call# {sid}");

                            if (compressed1 && Salou.Decompress != null)
                                baIn = Salou.Decompress(baIn);
                        }
                        else
                            Salou.LoggerFkt(LogLevel.Warning, () => $"WSR {_WSRID}: Recieved Data Expected: {len} Recived: {reclen} Call# {sid}");
                    }

                    //***prepare send
                    byte[] baOut;

                    //Check if we have enough Data
                    if (reqToDo == null || sid == null || reclen != len)
                    {
                        using (var ms = new MemoryStream())
                        {
                            Salou.LoggerFkt(LogLevel.Warning, () => $"WSR {_WSRID}: Not Enough Data {reqToDo} Call# {sid}");

                            StaticWSHelpers.WriteString(ms, $"Not Enough Data {reqToDo}");
                            rty = SalouReturnType.Exception;
                            baOut = ms.ToArray();
                        }
                    }
                    else
                    {
                        //Prepare Data to be send
                        using (var ms = new MemoryStream())
                        {
                            //*Parse the Data and do work
                            rty = await ProcessRequest(reqToDo.Value, sid.Value, baIn ?? Array.Empty<byte>(), ms);

                            baIn = Array.Empty<byte>();//Free Memory
                            baOut = ms.ToArray();
                        }
                    }

                    if (recivedState == StaticWSHelpers.WsState.Closed || recivedState == StaticWSHelpers.WsState.Closing || _ws.State != WebSocketState.Open)
                        break;

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

                    // Add Header
                    var baHeadO = new byte[StaticWSHelpers.SizeOfHead];
                    span = new Span<byte>(baHeadO);
                    BinaryPrimitives.WriteInt32LittleEndian(span, baOut?.Length ?? 0);//cant' ref in Async
                    span = span.Slice(StaticWSHelpers.SizeOfInt);
                    span[0] = (byte)rty; span = span.Slice(1);
                    BinaryPrimitives.WriteInt32LittleEndian(span, sid == null ? int.MinValue : (int)sid);
                    span = span.Slice(StaticWSHelpers.SizeOfInt);
                    span[0] = (byte)(compressed ? 'B' : 'G'); span = span.Slice(1);

                    Salou.LoggerFkt(LogLevel.Information, () => $"WSR {_WSRID}: Answer {reqToDo} Call# {sid} Return:{rty} Len: {baOut?.Length ?? 0}");

                    //Send
                    if (_ws.State == WebSocketState.Open)
                    {
                        await _ws.SendAsync(baHeadO, WebSocketMessageType.Binary, baOut?.Length == 0, CancellationToken.None);
                        if (baOut?.Length > 0 && _ws.State == WebSocketState.Open)
                            await _ws.SendAsync(baOut, WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                    //Could Close after ... so
                    if (_ws.State != WebSocketState.Open)
                        break;
                }
                //try normal Close
                if (_ws.State == WebSocketState.CloseReceived)
                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                else if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);

                Salou.LoggerFkt(LogLevel.Information, () => $"WSR {_WSRID}: WS Closed");
            }
            catch (System.Net.WebSockets.WebSocketException wex)
            {
                Salou.LoggerFkt(LogLevel.Warning, () => $"WSR {_WSRID}: {wex.Message} Error in Recive. Code {wex.WebSocketErrorCode}", wex);
            }
            catch (Exception ex)
            {
                Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in Recive", ex);
                if (_ws.State == WebSocketState.Open)
                    try
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.InternalServerError, string.Empty, CancellationToken.None);
                    }
                    catch { }//Ignore
            }
        }

        /// <summary>
        /// Process the Request - do the Real Work
        /// </summary>
        /// <param name="reqToDo">SalouRequestType</param>
        /// <param name="sid">id</param>
        /// <param name="baIn">Data comming in</param>
        /// <param name="msOut">MemoryStream outgoing Data</param>
        /// <returns>TAsk SalouReturnType</returns>
        /// <exception cref="Exception"></exception>
        private async Task<SalouReturnType> ProcessRequest(SalouRequestType reqToDo, int sid, byte[] baIn, MemoryStream msOut)
        {
            DbCommand? cmd = null;
            var spanIn = new Span<byte>(baIn);
            switch (reqToDo)
            {
                case SalouRequestType.TransactionRollback:
                    try //Doing this in a Method doesnt make the code better ;)
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var tran = _allTrans.Get(id);
                        if (tran != null)
                        {
                            _allTrans.Remove(id);
                            await tran.RollbackAsync();
                            await tran.DisposeAsync();
                            return SalouReturnType.Nothing;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Transaction is null");
                        StaticWSHelpers.WriteString(msOut, "Transaction is null");
                        return SalouReturnType.Exception;

                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in TransactionCommit", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.TransactionCommit:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var tran = _allTrans.Get(id);
                        if (tran != null)
                        {
                            _allTrans.Remove(id);
                            await tran.CommitAsync();
                            await tran.DisposeAsync();
                            return SalouReturnType.Nothing;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Transaction is null");
                        StaticWSHelpers.WriteString(msOut, "Transaction is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in TransactionCommit", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.CommandCancel:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        cmd = _allCmd.Get(id);
                        if (cmd != null)
                        {
                            _allCmd.Remove(id);
                            cmd.Cancel();
                            cmd.Dispose();
                            return SalouReturnType.Nothing;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Command is null");
                        StaticWSHelpers.WriteString(msOut, "Command is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in CommandCancel", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ConnectionOpen:
                    try
                    {
                        var v = StaticWSHelpers.ReadInt(ref spanIn);
                        if (v != Version.VERSION_NUMBER)
                        {
                            Salou.LoggerFkt(LogLevel.Error, () => $"Version missmatch");
                            throw new Exception("Version missmatch");
                        }

                        var constr = StaticWSHelpers.ReadString(ref spanIn);
                        var db = StaticWSHelpers.ReadString(ref spanIn);
                        var con = await _wss.CreateOpenCon(constr, db, _ctx);
                        if (con != null && con.State == ConnectionState.Open)
                        {
                            if (_allCons.Get(sid) != null)
                            {
                                Salou.LoggerFkt(LogLevel.Warning, () => $"WSR {_WSRID}: Connection already open");
                                StaticWSHelpers.WriteString(msOut, "Connection already open");
                                return SalouReturnType.Exception;
                            }
                            _allCons.Add(sid, con);
                            StaticWSHelpers.WriteInt(msOut, _WSRID);
                            StaticWSHelpers.WriteInt(msOut, sid);
                        }
                        else
                        {
                            StaticWSHelpers.WriteInt(msOut, -1);
                            StaticWSHelpers.WriteInt(msOut, -1);
                        }
                        return SalouReturnType.TwoInt32;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ConnectionOpen", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ChangeDatabase:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var db = StaticWSHelpers.ReadString(ref spanIn);
                        var con = _allCons.Get(id);
                        if (con != null && db != null)
                        {
                            await con.ChangeDatabaseAsync(db);
                            return SalouReturnType.Nothing;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Connection is null");
                        StaticWSHelpers.WriteString(msOut, "Connection or DBName is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ChangeDatabase", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ConnectionClose:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var con = _allCons.Get(id);
                        if (con != null)
                        {
                            try
                            {
                                //Also close a transaction
                                var tran = _allTrans.Get(id);
                                if (tran != null)
                                {
                                    _allTrans.Remove(id);
                                    await tran.RollbackAsync();
                                    await tran.DisposeAsync();
                                }
                            }
                            catch { }

                            _allCons.Remove(id);
                            await con.CloseAsync();
                            await con.DisposeAsync();
                            return SalouReturnType.Nothing;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Connection is null");
                        StaticWSHelpers.WriteString(msOut, "Connection is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ConnectionClose", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.BeginTransaction:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var con = _allCons.Get(id);
                        if (con != null)
                        {
                            var isol = (IsolationLevel)StaticWSHelpers.ReadInt(ref spanIn);
                            var tran = await con.BeginTransactionAsync(isol);
                            _allTrans.Add(id, tran);//1 Tran per Connection
                            return SalouReturnType.Nothing;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR  {_WSRID} : Connection is null");
                        StaticWSHelpers.WriteString(msOut, "Connection is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in BeginTransaction", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ServerVersion:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var con = _allCons.Get(id);
                        if (con != null)
                        {
                            Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: con?.ServerVersion");
                            StaticWSHelpers.WriteString(msOut, con?.ServerVersion);
                            return SalouReturnType.String;
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR  {_WSRID} : Connection is null");
                        StaticWSHelpers.WriteString(msOut, "Connection is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ServerVersion", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ExecuteNonQuery:
                    {
                        try
                        {
                            var id = StaticWSHelpers.ReadInt(ref spanIn);
                            var con = _allCons.Get(id);
                            if (con != null)
                            {
                                cmd = PrepareCommand(id, con, ref spanIn);
                                _allCmd.Add(sid, cmd);

                                var ret = await cmd.ExecuteNonQueryAsync();
                                var baOut = new byte[StaticWSHelpers.SizeOfInt];
                                BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(baOut), ret);

                                return PostCommand(cmd, msOut, SalouReturnType.Integer, baOut);
                            }
                            Salou.LoggerFkt(LogLevel.Trace, () => $"WSR  {_WSRID} : Connection is null");
                            StaticWSHelpers.WriteString(msOut, "Connection is null");
                            return SalouReturnType.Exception;
                        }
                        catch (Exception ex)
                        {
                            Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ExecuteNonQuery", ex);
                            StaticWSHelpers.WriteString(msOut, ex.Message);
                            return SalouReturnType.Exception;
                        }
                        finally
                        {
                            if (cmd != null)
                            {
                                _allCmd.Remove(sid);
                                await cmd.DisposeAsync();
                            }
                        }
                    }
                case SalouRequestType.ExecuteScalar:
                    try
                    {
                        var spanIn2 = new Span<byte>(baIn);//Span Boundery Error
                        var id = StaticWSHelpers.ReadInt(ref spanIn2);
                        var con = _allCons.Get(id);
                        if (con != null)
                        {
                            cmd = PrepareCommand(id, con, ref spanIn2);
                            _allCmd.Add(sid, cmd);

                            object? obj = await cmd.ExecuteScalarAsync();

                            return PostCommand(cmd, msOut, SalouReturnType.NullableSalouType, obj);
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Connection is null");
                        StaticWSHelpers.WriteString(msOut, "Connection is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ExecuteNonQuery", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                    finally
                    {
                        if (cmd != null)
                        {
                            _allCmd.Remove(sid);
                            await cmd.DisposeAsync();
                        }
                    }
                case SalouRequestType.ExecuteReaderStart:
                    try
                    {
                        var spanIn2 = new Span<byte>(baIn);//Span Boundery Error
                        var conID = StaticWSHelpers.ReadInt(ref spanIn2);
                        var con = _allCons.Get(conID);
                        if (con != null)
                        {
                            cmd = PrepareCommand(conID, con, ref spanIn2);
                            _allCmd.Add(sid, cmd);

                            var behave = StaticWSHelpers.ReadInt(ref spanIn2);
                            var pageSize = StaticWSHelpers.ReadInt(ref spanIn2);
                            var useSchema = (UseSchema)StaticWSHelpers.ReadByte(ref spanIn2);

                            //Start the Reader
                            var rdr = new DataReader(cmd, (CommandBehavior)behave, sid, _WSRID, useSchema);
                            _allRDR.Add(sid, rdr);

                            byte[] ba3;
                            using (var ms = new MemoryStream())
                            {
                                await rdr.Start(ms, pageSize);
                                ba3 = ms.ToArray();
                            }

                            return PostCommand(cmd, msOut, SalouReturnType.ReaderStart, ba3);
                        }
                        Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Connection is null");
                        StaticWSHelpers.WriteString(msOut, "Connection is null");
                        return SalouReturnType.Exception;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ReaderStart", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.EndReader:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        DataReader? rdr = _allRDR.Get(id);
                        if (rdr == null)
                        {
                            Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Reader not found");
                            StaticWSHelpers.WriteString(msOut, "Reader not found");
                            return SalouReturnType.Exception;
                        }
                        //End the Reader
                        await rdr.End();

                        _allRDR.Remove(id);
                        cmd = _allCmd.Get(id);
                        if (cmd != null)
                        {
                            _allCmd.Remove(id);//Same sid
                            await cmd.DisposeAsync();
                        }
                        return SalouReturnType.Nothing;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in EndReader", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ContinueReader:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var pageSize = StaticWSHelpers.ReadInt(ref spanIn);
                        DataReader? rdr = _allRDR.Get(id);
                        if (rdr == null)
                        {
                            Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Reader not found");
                            StaticWSHelpers.WriteString(msOut, "Reader not found");
                            return SalouReturnType.Exception;
                        }
                        await rdr.Continue(msOut, pageSize);
                        return SalouReturnType.ReaderContinue;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}:{ex.Message} Error in ContinueReader", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                case SalouRequestType.ReaderNextResult:
                    try
                    {
                        var id = StaticWSHelpers.ReadInt(ref spanIn);
                        var pageSize = StaticWSHelpers.ReadInt(ref spanIn);
                        DataReader? rdr = _allRDR.Get(id);
                        if (rdr == null)
                        {
                            Salou.LoggerFkt(LogLevel.Trace, () => $"WSR {_WSRID}: Reader not found");
                            StaticWSHelpers.WriteString(msOut, "Reader not found");
                            return SalouReturnType.Exception;
                        }
                        if (await rdr.NextResult(msOut, pageSize))
                            return SalouReturnType.ReaderStart;
                        else
                            return SalouReturnType.Nothing;
                    }
                    catch (Exception ex)
                    {
                        Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {ex.Message} Error in ReaderNextResult", ex);
                        StaticWSHelpers.WriteString(msOut, ex.Message);
                        return SalouReturnType.Exception;
                    }
                default:
                    Salou.LoggerFkt(LogLevel.Error, () => $"WSR {_WSRID}: {reqToDo} unknown");
                    throw new Exception($"SalouReturnType unknown");
            }
        }

        /// <summary>
        /// Postprocessing Command Data
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <param name="msOut">MemoryStream</param>
        /// <param name="returnType">inner SalouReturnType</param>
        /// <param name="obj">inner object</param>
        /// <returns>SalouReturnType</returns>
        private SalouReturnType PostCommand(DbCommand cmd, MemoryStream msOut, SalouReturnType returnType, object? obj)
        {
            var outP = (from DbParameter p in cmd.Parameters
                        where p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput || p.Direction == ParameterDirection.ReturnValue
                        select p).ToArray();

            //Got Out Parameters
            if (outP.Length > 0)
            {
                Salou.LoggerFkt(LogLevel.Debug, () => $"WSR {_WSRID}: Out Parameters {outP.Length}");

                StaticWSHelpers.WriteInt(msOut, outP.Length);
                CommandData.SendOutParametersBackFromServer(msOut, outP);

                //inner return
                msOut.WriteByte((byte)returnType);

                if (obj == null || returnType == SalouReturnType.NullableSalouType)
                    StaticWSHelpers.ServerWriteSalouType(msOut, null, null, obj);
                else
                    msOut.Write((byte[])obj);
                return SalouReturnType.CommandParameters;
            }

            //No, so just the inner return
            if (obj == null || returnType == SalouReturnType.NullableSalouType)
            {
                StaticWSHelpers.ServerWriteSalouType(msOut, null, null, obj);
                return SalouReturnType.NullableSalouType;
            }
            else
            {
                msOut.Write((byte[])obj);
                return returnType;
            }
        }



        /// <summary>
        /// Prepare a Command
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="con">DbConnection</param>
        /// <param name="span">data</param>
        /// <returns>DbCommand</returns>
        private DbCommand PrepareCommand(int id, DbConnection con, ref Span<byte> span)
        {
            var cd = new CommandData(ref span);
            var cmd = con!.CreateCommand();
            cmd.CommandText = cd.CommandText;
            cmd.CommandTimeout = cd.CommandTimeout;
            cmd.CommandType = cd.CommandType;

            Salou.LoggerFkt(LogLevel.Information, () => $"WSR {_WSRID}: PrepareCommand {cd?.CommandText}");
            Salou.LoggerFkt(LogLevel.Debug, () => $"In Parameters {cd.Parameters.Count}");

            foreach (SalouParameter p in cd.Parameters)
            {
                var np = cmd.CreateParameter();
                np.ParameterName = p.ParameterName;
                np.Value = p.Value;

                np.Direction = p.Direction;

                if (p.DbYypeSet)
                    np.DbType = p.DbType;
                if (p.IsNullableSet)
                    np.IsNullable = p.IsNullable;
                if (p.SizeSet)
                    np.Size = p.Size;
                if (p.ScaleSet)
                    np.Scale = p.Scale;
                if (p.PrecisionSet)
                    np.Precision = p.Precision;

                //SourceColumn ignored
                //SourceColumnNullMapping ignored

                cmd.Parameters.Add(np);
            }

            var tran = _allTrans.Get(id);
            if (tran != null)
                cmd.Transaction = tran;

            return cmd;
        }


    }
}