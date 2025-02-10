using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalouWS4Sql.Helpers;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalouWS4Sql.Server
{
    /// <summary>
    /// Implemanetation of the Salou Server side 
    /// </summary>
    public class WebSocketServer
    {
        /// <summary>
        /// delegate to Create a new Open Connection
        /// check secutity and open the connection úsing the real DB driver
        /// </summary>
        /// <param name="constr">constr</param>
        /// <param name="dbName">dbName</param>
        /// <param name="ctx">HttpContext for sec checks</param>
        /// <returns>connection</returns>
        public delegate Task<DbConnection> CreateOpenConDelegate(string? constr, string? dbName, HttpContext ctx);

        /// <summary>
        /// all Websocket Requests
        /// </summary>
        SimpleConcurrentList<WebSocketRequest> _allWSS = new ();
        /// <summary>
        /// cleanUp Timer
        /// </summary>
        readonly Timer? _ti;
        /// <summary>
        /// Logger
        /// </summary>
        public ILogger? Logger { get; private set; }
        /// <summary>
        /// CreateOpenCon callback
        /// </summary>
        public CreateOpenConDelegate CreateOpenCon { get; private set; }
        /// <summary>
        /// constructor for WebSocketServer
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="createOpenCon">CreateOpenConDelegate</param>
        public WebSocketServer(ILogger? logger, IConfiguration? configuration, CreateOpenConDelegate createOpenCon)
        {
            CreateOpenCon = createOpenCon;
            Logger = logger;
            SalouLog.Logger = logger;

            //start the cleanup Timer
            _ti = new Timer(TiCb, null, 30000, 30000);

            SalouLog.LoggerFkt(LogLevel.Information, () => "Salou Server started");
        }

        /// <summary>
        /// Timer Callback to clean up the Websocket Requests
        /// </summary>
        /// <param name="state">unused</param>
        private void TiCb(object? state)
        {
            SalouLog.LoggerFkt(LogLevel.Trace, () => "Timer Callback");

            WebSocketRequest[] lst;
            lock (_allWSS)
                lst = _allWSS.ToArray();

            foreach (var ws in lst)
            {
                if (!ws.Check())
                {
                    _allWSS.Remove(ws);
                    ws.Dispose();
                }
            }
        }

        /// <summary>
        /// Accept a Websocket Request
        /// </summary>
        /// <param name="ws">WebSocket</param>
        /// <param name="ctx">HttpContext</param>
        /// <returns>Task</returns>
        public async Task HandleWebSocketRequest(WebSocket ws, HttpContext ctx)
        {
            SalouLog.LoggerFkt(LogLevel.Information, () => "AcceptWebSocketRequest");

            var wsr = new WebSocketRequest(this, ws,ctx);
            _allWSS.Add(wsr);
            await wsr.Recive();
        }
    }
}
