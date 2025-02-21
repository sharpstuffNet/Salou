﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalouWS4Sql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// implements a DbConnection over the Salou Websocket Service
    /// Starting Point for all Salou operations
    /// </summary>
    public class SalouConnection : DbConnection
    {
        /// <summary>
        /// Uri static store
        /// </summary>
        static Uri? __uri;
        /// <summary>
        /// Timeout static store
        /// </summary>
        static int __timeout;
        /// <summary>
        /// Logger static store
        /// </summary>
        static ILogger? __logger;
        /// <summary>
        /// ConnectionString static store
        /// </summary>
        static string? __constr;
        /// <summary>
        /// Static Initialize the Salou Client from Config to be used if no parameters are passed
        /// </summary>
        /// <param name="logger">Logger</param>
        /// <param name="configuration">Configuration</param>
        /// <exception cref="SalouException"></exception>
        public static void Salou_ClientInit(ILogger? logger, IConfiguration? configuration)
        {
            __logger = logger;

            if (configuration == null)
                return;

            IConfigurationSection cfg = configuration.GetSection("Salou:Client");
            var url = cfg.GetValue<string>("Url");
            if (string.IsNullOrEmpty(url))
                throw new SalouException("Url not found in configuration");

            __uri = new Uri(url);
            __timeout = cfg.GetValue<int>("Timeout");
            __constr = cfg.GetValue<string>("Connstr");

            Salou.ReadConfiguration(configuration);
        }

        /// <summary>
        /// Uri of the Websocket Service
        /// </summary>
        /// <remarks>ws://localhost:5249/ws</remarks>
        Uri? _uri;
        /// <summary>
        /// Timeout in seconds
        /// </summary>
        int _timeout;
        /// <summary>
        /// Logger
        /// </summary>
        ILogger? _logger;
        /// <summary>
        /// Connection Service ID
        /// </summary>
        int _conSrvrId;
        /// <summary>
        /// Server WSRID
        /// </summary>
        int _WSRID;

        /// <summary>
        /// Create a SalouConnection
        /// </summary>
        /// <param name="log">Logger</param>
        /// <exception cref="SalouException"></exception>
        public SalouConnection(ILogger? log = null)
        {
            _logger = log ?? __logger;
            if (__uri == null)
                throw new SalouException("Client not initialized");
            _uri = __uri;
            _timeout = __timeout;
            ConnectionString = __constr;
        }

        /// <summary>
        /// Create a SalouConnection
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="log">Logger</param>
        /// <exception cref="SalouException"></exception>
        public SalouConnection(IConfiguration configuration, ILogger? log = null)
        {
            _logger = log ?? __logger;

            IConfigurationSection cfg = configuration.GetSection("Salou:Client");
            var url = cfg.GetValue<string>("Url");
            if (string.IsNullOrEmpty(url) && __uri == null)
                throw new SalouException("Url not found in configuration");

            _uri = string.IsNullOrEmpty(url) ? __uri! : new Uri(url);
            _timeout = cfg.GetValue<int>("Timeout");
            ConnectionString = cfg.GetValue<string>("Connstr");
            Salou.ReadConfiguration(configuration);
        }

        /// <summary>
        /// Create a SalouConnection
        /// </summary>
        /// <param name="uri">URI: ws://localhost:5249/ws</param>
        /// <param name="connstr">Connection String</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="log">Logger</param>
        public SalouConnection(Uri uri, string connstr, int timeout, ILogger? log = null)
        {
            _logger = log ?? __logger;
            _uri = uri;
            _timeout = timeout;
            ConnectionString = connstr;
        }

        /// <summary>
        /// Web Service Client
        /// </summary>
        WSClient? _wsClient;

        /// <summary>
        /// Web Service Client
        /// </summary>
        static WSClient? __wsClient;

        /// <summary>
        /// Store the ConnectionState
        /// </summary>
        ConnectionState _stateInternal = ConnectionState.Closed;

        /// <summary>
        /// Store the Database name
        /// </summary>
        string _database = string.Empty;

        /// <summary>
        /// Our WebService Client
        /// </summary>
        internal WSClient? WsClient => _wsClient;

        /// <inheritdoc />
        public override event StateChangeEventHandler? StateChange;

        /// <inheritdoc />
        [AllowNull]
        public override string ConnectionString { get; set; }


        /// <summary>
        /// Set the ConnectionState and fire the StateChange event
        /// </summary>
        ConnectionState StateInternal
        {
            get => _stateInternal;
            set
            {
                var oldState = _stateInternal;
                _stateInternal = value;
                StateChange?.Invoke(this, new StateChangeEventArgs(oldState, value));
            }
        }

        /// <inheritdoc />
        public override string Database => _database;

        /// <inheritdoc />
        public override string DataSource => _uri?.Host ?? string.Empty;

        /// <inheritdoc />
        public override string ServerVersion
        {
            get
            {
                if (_wsClient == null)
                    throw new SalouException("Connection not open");

                return _wsClient.Send<string>(SalouRequestType.ServerVersion, null, _conSrvrId) ?? string.Empty;
            }
        }

        /// <inheritdoc />
        public override ConnectionState State => StateInternal;

        /// <summary>
        /// Srever Side CallID
        /// </summary>
        internal int ConSrvrId { get => _conSrvrId; }

        /// <inheritdoc />
        protected override DbCommand CreateDbCommand() => new SalouCommand(this);

        /// <inheritdoc />
        protected override DbTransaction BeginDbTransaction(IsolationLevel il)
        {
            if (_wsClient == null)
                throw new SalouException("Connection not open");

            _wsClient?.Send(SalouRequestType.BeginTransaction, null, _conSrvrId, il);
            return new SalouTransaction(this, il);
        }

        /// <inheritdoc />
        public override void ChangeDatabase(string databaseName)
        {
            if (_wsClient == null)
                throw new SalouException("Connection not open");

            _database = databaseName;
            _wsClient?.Send(SalouRequestType.ChangeDatabase, null, _conSrvrId, Database);
        }

        /// <inheritdoc />
        public override void Close()
        {
            if (!Salou.LeaveClientOpen && __wsClient != null && _wsClient == null)
            {
                __wsClient.Close();
                __wsClient = null;
                return;
            }
            if (_wsClient == null)
                throw new SalouException("Connection not open");

            _wsClient?.Send(SalouRequestType.ConnectionClose, null, _conSrvrId);

            if (!Salou.LeaveClientOpen || _wsClient != __wsClient)
            {
                _wsClient?.Close();
            }
            _wsClient = null;
            StateInternal = ConnectionState.Closed;
        }

        /// <inheritdoc />
        override protected void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _wsClient?.Send(SalouRequestType.ConnectionClose, null, _conSrvrId);
            _wsClient?.Dispose();
            _wsClient = null;
            StateInternal = ConnectionState.Closed;
        }

        /// <inheritdoc />
        public override void Open()
        {
            if (_wsClient != null)
                throw new SalouException("Connection already open");

            if (_uri == null)
                throw new SalouException("Connection not initialized");

            StateInternal = ConnectionState.Connecting;
            if (!Salou.LeaveClientOpen || __wsClient == null)
            {
                _wsClient = new WSClient(_logger, _uri, _timeout);
                _wsClient.Open();
                if (Salou.LeaveClientOpen)
                    __wsClient = _wsClient;
            }
            else
                _wsClient = __wsClient;

            var res = (_wsClient?.Send<(int, int)>(SalouRequestType.ConnectionOpen, null, ConnectionString, Database)).GetValueOrDefault((-1, -1));
            if (res.Item1 > -1)
            {
                StateInternal = ConnectionState.Open;
                _WSRID = res.Item1;
                _conSrvrId = res.Item2;
                _logger?.LogInformation($"Connection Opened: WSRID:{_WSRID}");
            }
        }
    }
}
