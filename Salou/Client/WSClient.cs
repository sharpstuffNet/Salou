using Microsoft.Extensions.Logging;
using SalouWS4Sql;
using SalouWS4Sql.Helpers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalouWS4Sql.Client
{
    /// <summary>
    /// Implements a Websocket Client for use with the Salou DB Provider
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#nullable enable
    internal partial class WSClient : IDisposable
    {
        /// <summary>
        /// Uri of the Websocket Service
        /// </summary>
        Uri _uri;
        /// <summary>
        /// Timeout for the Websocket Service
        /// </summary>
        int _timeout;
        /// <summary>
        /// Websocket Client
        /// </summary>
        ClientWebSocket _webSocket;
        /// <summary>
        /// Semaphore for the Websocket Client so that only one async operation can be done at a time
        /// </summary>
        SemaphoreSlim _semaphore;
        /// <summary>
        /// CancellationToken for the Websocket Client
        /// </summary>
        CancellationToken _ct;
        /// <summary>
        /// Logger
        /// </summary>
        ILogger? _logger;
        /// <summary>
        /// Constructor for WSClient
        /// </summary>
        /// <param name="logger">logger</param>
        /// <param name="uri">Uri</param>
        /// <param name="timeout">timeout</param>
        internal WSClient(ILogger? logger, Uri uri, int timeout)
        {
            _logger = logger;
            SalouLog.Logger = logger;
            _uri = uri;
            _timeout = timeout;
            _semaphore = new SemaphoreSlim(1);
            _webSocket = new ClientWebSocket();
            foreach (var item in SalouConnection.Salou_RequestHeaders)
                _webSocket.Options.SetRequestHeader(item.Key,item.Value);
            _ct = new CancellationToken();

            SalouLog.LoggerFkt(LogLevel.Information, () => $"WSClient started {uri}");
        }

        /// <summary>
        /// Open the Websocket
        /// </summary>
        internal void Open()
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                //var ct=new CancellationTokenSource(120).Token;   
                DOASyncSerialized(async () =>
                {
                    await _webSocket.ConnectAsync(_uri, _ct);
                });//Timeout was not a good idea here - blocks inner semaphore even in finally 

                SalouLog.LoggerFkt(LogLevel.Information, () => $"WSClient opened");
            }
        }

        
        /// <summary>
        /// Close the Websocket
        /// </summary>
        internal void Close()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                DOASyncSerialized(async () =>
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _ct);
                });

                SalouLog.LoggerFkt(LogLevel.Information, () => $"WSClient closed");
            }
        }
        /// <summary>
        /// Do some async operation serial via Semaphore
        /// </summary>
        /// <param name="act"></param>
        /// <returns></returns>
        private void DOASyncSerialized(Func<Task> act)
        {
            _semaphore.Wait();
            try
            {
                Salou48.Helpers.AsyncHelper.RunSync(act);
                //act().Wait();
            }
            catch (Exception ex)
            {
                SalouLog.LoggerFkt(LogLevel.Error, () => $"{ex.Message} Error in ESClient", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        /// <summary>
        /// Dispose the Websocket
        /// </summary>
        public void Dispose()
        {
            if (_webSocket.State == WebSocketState.Open)
                DOASyncSerialized(async () =>
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _ct);
                });

            _webSocket.Dispose();

            SalouLog.LoggerFkt(LogLevel.Information, () => $"WSClient disposed");
        }
    }
}
