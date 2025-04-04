﻿using Microsoft.AspNetCore.Http;
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
        /// Defines a delegate for closing a database connection asynchronously.
        /// </summary>
        /// <param name="con">Represents the database connection that needs to be closed.</param>
        /// <param name="tran">Represents an optional transaction associated with the connection.</param>
        /// <returns>Returns a Task that represents the asynchronous operation.</returns>
        public delegate Task CloseConDelegate(DbConnection con, DbTransaction? tran);

        /// <summary>
        /// all Websocket Requests
        /// </summary>
        SimpleConcurrentList<WebSocketRequest> _allWSS = new();
        /// <summary>
        /// cleanUp Timer
        /// </summary>
        readonly Timer? _ti;
        /// <summary>
        /// Logger
        /// </summary>
        public ILogger? Logger { get; private set; }
        /// <summary>
        /// CreateOpenCon callback. It can be set privately and accessed publicly.
        /// </summary>
        public CreateOpenConDelegate CreateOpenCon { get; private set; }
        /// <summary>
        /// Represents an optional delegate for closing a connection. It can be set privately and accessed publicly.
        /// </summary>
        public CloseConDelegate? CloseCon { get; private set; }

        /// <summary>
        /// constructor for WebSocketServer
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="createOpenCon">CreateOpenCon Function</param>
        /// <param name="closeCon">CloseCon Function</param>
        public WebSocketServer(ILogger? logger, IConfiguration? configuration, CreateOpenConDelegate createOpenCon, CloseConDelegate? closeCon = null)
        {
            CreateOpenCon = createOpenCon;
            CloseCon = closeCon;
            Logger = logger;
            Salou.Logger = logger;

            //start the cleanup Timer
            _ti = new Timer(TiCb, null, 30000, 30000);

            Salou.LoggerFkt(LogLevel.Information, () => "Salou Server started");
        }

        /// <summary>
        /// Timer Callback to clean up the Websocket Requests
        /// </summary>
        /// <param name="state">unused</param>
        private void TiCb(object? state)
        {

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

            Salou.LoggerFkt(LogLevel.Trace, () => $"Timer Callback {lst?.Length}->{_allWSS?.Count()}");
        }

        /// <summary>
        /// Accept a Websocket Request
        /// </summary>
        /// <param name="ws">WebSocket</param>
        /// <param name="ctx">HttpContext</param>
        /// <returns>Task</returns>
        public async Task HandleWebSocketRequest(WebSocket ws, HttpContext ctx)
        {
            Salou.LoggerFkt(LogLevel.Information, () => "AcceptWebSocketRequest");

            var wsr = new WebSocketRequest(this, ws, ctx);
            _allWSS.Add(wsr);
            await wsr.Recive();
        }


    }
}
