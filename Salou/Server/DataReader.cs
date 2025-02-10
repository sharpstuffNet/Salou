using Microsoft.Extensions.Logging;
using SalouWS4Sql.Client;
using SalouWS4Sql.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Server
{
    /// <summary>
    /// internal serverside DataReader
    /// </summary>
    internal class DataReader
    {
        /// <summary>
        /// real reader
        /// </summary>
        DbDataReader? _reader;
        /// <summary>
        /// db command
        /// </summary>
        DbCommand _cmd;
        /// <summary>
        /// reader data to communicate with the client
        /// </summary>
        ReaderData? _data;
        /// <summary>
        /// CommandBehavior
        /// </summary>
        CommandBehavior _behave;
        /// <summary>
        /// UseSchema
        /// </summary>
        UseSchema _useSchema;
        /// <summary>
        /// reader id to sync with the client
        /// </summary>
        public int ID { get; private set; }

        /// <summary>
        /// Create a new DataReader
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <param name="behave">CommandBehavior</param>
        /// <param name="sid">WsCallID</param>
        /// <param name="useSchema">UseSchema</param>
        public DataReader(DbCommand cmd, CommandBehavior behave, WsCallID sid, UseSchema useSchema)
        {
            _useSchema = useSchema;
            _cmd = cmd;
            _behave = behave;
            ID = sid;
        }
        /// <summary>
        /// Start the reader
        /// </summary>
        /// <param name="msOut">MemoryStream to write the Data</param>
        /// <param name="pageSize">pageSize</param>
        /// <returns>Task</returns>
        internal async Task Start(MemoryStream msOut, int pageSize)
        {
            _reader = await _cmd.ExecuteReaderAsync(_behave);
            await InitDataSet(msOut, pageSize);
        }
        /// <summary>
        /// Get the next result
        /// </summary>
        /// <param name="msOut">MemoryStream to write the Data</param>
        /// <param name="pageSize">pageSize</param>
        /// <returns>Task success</returns>
        internal async Task<bool> NextResult(MemoryStream msOut, int pageSize)
        {
            SalouLog.LoggerFkt(LogLevel.Trace, () => $"Reader NextResult");

            if (_reader == null)
                return false;
            if (_reader.NextResult())
            {
                await InitDataSet(msOut, pageSize);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Init the DataSet
        /// </summary>
        /// <param name="msOut">MemoryStream to write the Data</param>
        /// <param name="pageSize">pageSize</param>
        /// <returns>Task</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task InitDataSet(MemoryStream msOut, int pageSize)
        {
            if (_reader == null)
                throw new InvalidOperationException("Reader is null");

            _data = new ReaderData(
                            ID,
                            _reader.Depth,
                            _reader.FieldCount,
                            _reader.HasRows,
                            _reader.RecordsAffected,
                            _reader.VisibleFieldCount,
                            _useSchema == UseSchema.None ? null : _reader.GetSchemaTable(),
                            _useSchema
                            );

            SalouLog.LoggerFkt(LogLevel.Debug, () => $"InitDataSet {ID} {_reader.FieldCount} {_useSchema}");

            _data.Write(msOut);
            using (var ms2 = new MemoryStream())
            {
                await Continue(ms2, pageSize);
                var ba = ms2.ToArray();
                msOut.Write(ba, 0, ba.Length);
            }
        }
        /// <summary>
        /// Continue the reader
        /// </summary>
        /// <param name="msOut">MemoryStream to write the Data</param>
        /// <param name="pageSize">pageSize</param>
        /// <returns>Task</returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task Continue(MemoryStream msOut, int pageSize)
        {
            if (_reader == null || _data==null)
                throw new InvalidOperationException("Reader is null");

            int r = 0;
            while (r++ < pageSize && await _reader.ReadAsync())
            {
                for (int c = 0; c < _data.FieldCount; c++)
                    StaticWSHelpers.WriteObjectAsDBType(msOut, _reader[c]);
            }

            SalouLog.LoggerFkt(LogLevel.Trace, () => $"Reader Continue {r} Rows");
        }
        /// <summary>
        /// End the reader usage (Close)
        /// </summary>
        /// <returns>Task</returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task End()
        {
            SalouLog.LoggerFkt(LogLevel.Trace, () => $"Reader End");

            if (_reader == null || _data == null)
                throw new InvalidOperationException("Reader is null");

            await _reader.CloseAsync();
        }

        
    }
}
