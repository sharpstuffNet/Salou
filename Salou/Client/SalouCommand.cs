using SalouWS4Sql;
using SalouWS4Sql.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// implements a DbCommand over the Salou Websocket Service
    /// </summary>
    public class SalouCommand : DbCommand
    {
        /// <summary>
        /// Store the Parameters
        /// </summary>
        SalouParameterCollection _dbParameterCollection = new SalouParameterCollection();
        /// <summary>
        /// Store the Connection
        /// </summary>
        SalouConnection? _salouConnection;
        /// <summary>
        /// Store the Transaction
        /// </summary>
        SalouTransaction? _salouTransaction;
        /// <summary>
        /// Store the ClientCallerID
        /// </summary>
        int? _clientCallID;

        /// <inheritdoc /> 
        public SalouCommand()
        {

        }
        /// <inheritdoc /> 
        public SalouCommand(SalouConnection salouConnection)
        {
            _salouConnection = salouConnection;
        }
        /// <inheritdoc /> 
        public SalouCommand(string commandText, SalouConnection salouConnection)
        {
            _salouConnection = salouConnection;
            CommandText = commandText;
        }
        /// <summary>
        /// Page Size for the DataReader
        /// </summary>
        public int Salou_ReaderPageSize { get; set; } = Salou.DefaultPageSize;
        /// <summary>
        /// Page Size for the DataReader used in the initial call where also the schema has to be send
        /// </summary>
        public int Salou_PageSizeInitalCall { get; set; } = Salou.DefaultPageSizeInitalCall;
        /// <summary>
        /// Use Schema for the DataReader
        /// </summary>
        public UseSchema ReaderUseSchema { get; set; } = UseSchema.Full;
        /// <inheritdoc /> 
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override string CommandText { get; set; } = string.Empty;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        /// <inheritdoc /> 
        public override int CommandTimeout { get; set; }
        /// <inheritdoc /> 
        public override CommandType CommandType { get; set; }
        /// <inheritdoc /> 
        public override bool DesignTimeVisible { get; set; }
        /// <inheritdoc /> 
        public override UpdateRowSource UpdatedRowSource { get; set; }
        /// <inheritdoc /> 
        protected override DbParameter CreateDbParameter() => new SalouParameter();
        /// <inheritdoc /> 
        protected override DbConnection? DbConnection
        {
            get => _salouConnection;
            set
            {
                _salouConnection = value is SalouConnection salouConnection
                    ? salouConnection
                    : throw new SalouException("You can only use a SalouConnection");
            }
        }
        /// <inheritdoc /> 
        protected override DbTransaction? DbTransaction
        {
            get => _salouTransaction;
            set
            {
                _salouTransaction = value is SalouTransaction salouTransaction
                ? salouTransaction
                : throw new SalouException("You can only use a SalouTransaction");
            }
        }

        /// <inheritdoc /> 
        protected override DbParameterCollection DbParameterCollection => _dbParameterCollection;

        /// <inheritdoc /> 
        public override void Cancel()
        {
            if (_salouConnection?.WsClient == null || _clientCallID == null)
            {
                throw new SalouException("No or No Open connection / exevuted Command");
            }
            _salouConnection.WsClient.Send(SalouRequestType.CommandCancel, null, (int)_clientCallID);
        }

        /// <inheritdoc /> 
        public override int ExecuteNonQuery()
        {
            if (_salouConnection?.WsClient == null)
                throw new SalouException("No or No Open connection");

            if (UpdatedRowSource != UpdateRowSource.None)
                throw new SalouException("UpdatedRowSource not supported");

            _clientCallID = Interlocked.Increment(ref WSClient.ClientCallID);
            var cd = new CommandData(CommandText, CommandTimeout, CommandType, Parameters);
            return _salouConnection.WsClient.Send<int>(SalouRequestType.ExecuteNonQuery, _clientCallID, _salouConnection.ConSrvrId, cd);
        }
        /// <inheritdoc /> 
        public override object? ExecuteScalar()
        {
            if (_salouConnection?.WsClient == null)
                throw new SalouException("No or No Open connection");

            if (UpdatedRowSource != UpdateRowSource.None)
                throw new SalouException("UpdatedRowSource not supported");

            _clientCallID = Interlocked.Increment(ref WSClient.ClientCallID);
            var cd = new CommandData(CommandText, CommandTimeout, CommandType, Parameters);
            return _salouConnection.WsClient.Send<object>(SalouRequestType.ExecuteScalar, _clientCallID, _salouConnection.ConSrvrId, cd);
        }
        /// <summary>
        /// ExecuteScalar with a generic return type
        /// </summary>
        /// <typeparam name="T">Return Type</typeparam>
        /// <returns>Value</returns>
        /// <exception cref="SalouException"></exception>
        public T? ExecuteScalar<T>()
        {
            if (_salouConnection?.WsClient == null)
                throw new SalouException("No or No Open connection");

            if (UpdatedRowSource != UpdateRowSource.None)
                throw new SalouException("UpdatedRowSource not supported");

            _clientCallID = Interlocked.Increment(ref WSClient.ClientCallID);
            var cd = new CommandData(CommandText, CommandTimeout, CommandType, Parameters);
            return _salouConnection.WsClient.Send<T>(SalouRequestType.ExecuteScalar, _clientCallID, _salouConnection.ConSrvrId, cd);
        }
        /// <inheritdoc /> 
        public override void Prepare()
        {
            //NOP
        }

        /// <inheritdoc /> 
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (_salouConnection?.WsClient == null)
                throw new SalouException("No or No Open connection");

            if (UpdatedRowSource != UpdateRowSource.None)
                throw new SalouException("UpdatedRowSource not supported");

            var cd = new CommandData(CommandText, CommandTimeout, CommandType, Parameters);
            var x = _salouConnection.WsClient.Send<byte[]>(SalouRequestType.ExecuteReaderStart, _clientCallID, _salouConnection.ConSrvrId, cd, behavior, Math.Min(Salou_ReaderPageSize, Salou_PageSizeInitalCall), ReaderUseSchema);
            if (x == null || x.Length == 0)
                throw new SalouException("No Data returned");

            return new SalouDataReader(_salouConnection, Salou_ReaderPageSize, x);
        }
        /// <inheritdoc /> 
        override protected void Dispose(bool disposing)
        {
            //NOP
        }
    }
}
