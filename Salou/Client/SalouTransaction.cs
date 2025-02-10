using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// Implements a DbTransaction over the Salou Websocket Service
    /// </summary>
    internal class SalouTransaction : DbTransaction
    {
        /// <summary>
        /// Our Connection
        /// </summary>
        readonly SalouConnection? _salouConnection;
        /// <summary>
        /// Isolation Level
        /// </summary>
        readonly IsolationLevel _isolationLevel;
        /// <summary>
        /// Server Id
        /// </summary>
        readonly int _serverId;
        /// <summary>
        /// constructor for SalouTransaction
        /// </summary>
        /// <param name="connection">connection</param>
        /// <param name="isolationLevel">IsolationLevel</param>
        public SalouTransaction(SalouConnection connection, IsolationLevel isolationLevel = IsolationLevel.Serializable)
        {
            _salouConnection = connection;
            _isolationLevel = isolationLevel;
            _serverId = connection.ConSrvrId;
        }
        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => _isolationLevel;
        /// <inheritdoc />
        protected override DbConnection? DbConnection => _salouConnection;
        /// <inheritdoc />
        public override void Commit()
        {
            if (_salouConnection?.WsClient == null)
            {
                throw new SalouException("No or No Open connection");
            }
            _salouConnection.WsClient.Send(SalouWS4Sql.SalouRequestType.TransactionCommit,null,_serverId);
        }
        /// <inheritdoc />
        public override void Rollback()
        {
            if (_salouConnection?.WsClient == null)
            {
                throw new SalouException("No or No Open connection");
            }
            _salouConnection.WsClient.Send(SalouWS4Sql.SalouRequestType.TransactionRollback,null,_serverId);
        }
    }
}
