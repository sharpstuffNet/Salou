using SalouWS4Sql.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Threading;

#if NETFX48

#else
using System.Reflection.PortableExecutable;
#endif
namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// Implements a DbDataReader over the Salou Websocket Service
    /// </summary>
    internal class SalouDataReader : DbDataReader, IDisposable
    {
        /// <summary>
        /// Page Size
        /// </summary>
        public int PageSize { get; set; }
        /// <summary>
        /// Connection
        /// </summary>
        SalouConnection _con;
        /// <summary>
        /// Reader Data (Scema etc)
        /// </summary>
        ReaderData? _data;
        /// <summary>
        /// Is Closed
        /// </summary>
        bool _isClosed;
        /// <summary>
        /// All Rows
        /// </summary>
        SimpleConcurrentList<object?[]>? _rows;
        /// <summary>
        /// Current Row Index
        /// </summary>
        int _curRowIdx = -1;
        /// <summary>
        /// Current Row
        /// </summary>
        object?[]? _curRow;
        /// <summary>
        /// Schema Columns
        /// </summary>
        Dictionary<string, int>? _schemaColumns;
        /// <summary>
        /// Column Names
        /// </summary>
        Dictionary<string, int>? _colNames;
        /// <summary>
        /// No More Data to get
        /// </summary>
        bool _nomoreData = false;
        /// <summary>
        /// Last Page Size used for server call
        /// </summary>
        int _lastPageSize = 0;
        /// <summary>
        /// Has Rows
        /// </summary>
        bool _hasRows;
        /// <summary>
        /// Tryed Next Results
        /// </summary>
        bool _tryedNextResults = false;
        /// <summary>
        /// Read Multi Threaded
        /// </summary>
        bool _readMultiThreaded = false;
        /// <summary>
        /// Thread for Reading
        /// </summary>
        Thread? _thread;
        /// <summary>
        /// Manual Reset Event for sync Reding between Threads
        /// </summary>
        ManualResetEventSlim? _mres1;
        /// <summary>
        /// Signal to Stop the Thread
        /// </summary>
        bool _threadStop=false;

        /// <summary>
        /// Create a SalouDataReader
        /// </summary>
        /// <param name="con">Connection</param>
        /// <param name="pageSize">Page Size</param>
        /// <param name="ba">Initial Data</param>
        public SalouDataReader(SalouConnection con, int pageSize, byte[] ba)
        {
            _readMultiThreaded = Salou.RedaerReadMultiThreaded;
            if (_readMultiThreaded)
            {
                _mres1 = new ManualResetEventSlim(false);
                _thread = new Thread(() =>
                {
                    try
                    {
                        while (!_threadStop && LoadMoreData())
                        {
                            //Signal the Main Thread so if it is waiting for data
                            lock (_mres1!)
                                _mres1.Set();
                            Thread.Sleep(1);
                        }
                        lock (_mres1!)
                            _mres1.Set();
                    }
                    catch(ThreadAbortException)
                    {
                        //Ignore
                    }
                });
            }

            _con = con;
            PageSize = pageSize;
            _lastPageSize = PageSize;

            InitializeCurrentResultSet(ba);
        }

        /// <summary>
        /// initialize the current result set
        /// </summary>
        /// <param name="ba">Data</param>
        private void InitializeCurrentResultSet(byte[] ba)
        {
            _schemaColumns = null;
            _colNames = null;
            _rows = new SimpleConcurrentList<object?[]>();
            _nomoreData = false;
            _lastPageSize = 0;

            var span = new Span<byte>(ba);
            _data = new ReaderData(ref span);

            //Whole Schema?
            if (_data.UseSchema == UseSchema.Full)
            {
                _schemaColumns = new Dictionary<string, int>();

                for (int i = 0; i < _data.SchemaTable!.Columns.Count; i++)
                    _schemaColumns.Add(_data.SchemaTable.Columns[i].ColumnName, i);

                var nCol = _schemaColumns["ColumnName"];
                var iCol = _schemaColumns["ColumnOrdinal"];
                _colNames = new Dictionary<string, int>();
                foreach (DataRow r in _data.SchemaTable.Rows)
                    _colNames.Add((string)r[nCol], (int)r[iCol]);
            }
            else if (_data.UseSchema == UseSchema.NamesOnly)
            {
                _colNames = new Dictionary<string, int>();
                for (int i = 0; i < _data.ColNames!.Length; i++)
                    _colNames.Add(_data.ColNames[i], i);
            }

            //Need Data?
            _hasRows = _data.HasRows;
            if (_hasRows)
                LoadData(span);

            _curRowIdx = -1;
            _curRow = Array.Empty<object?[]>();

            _thread?.Start();
        }

        /// <inheritdoc />
        public override bool NextResult()
        {
            if (_tryedNextResults || _data == null || _con == null)
                return false;

            _lastPageSize = PageSize;
            var ba = _con.WsClient!.Send<byte[]>(SalouRequestType.ReaderNextResult, null, _data.ID, _lastPageSize);
            if (ba == null || ba.Length == 0)
            {
                _tryedNextResults = true;
                _hasRows = false;
                return false;
            }

            InitializeCurrentResultSet(ba);
            return true;
        }
        /// <summary>
        /// Start to Load more data
        /// </summary>
        /// <returns>success</returns>
        bool LoadMoreData()
        {
            if (_nomoreData || _data == null || _con == null)
                return false;

            var rows = _rows?.Count();

            _lastPageSize = PageSize;
            var ba = _con.WsClient!.Send<byte[]>(SalouRequestType.ContinueReader, null, _data.ID, _lastPageSize);
            LoadData(new Span<byte>(ba));

            if (rows == _rows?.Count())
                _nomoreData = true;

            return rows != _rows?.Count();
        }

        /// <summary>
        /// Load the Data from byte[]
        /// </summary>
        /// <param name="ba">Data</param>
        /// <exception cref="SalouException"></exception>
        void LoadData(Span<byte> ba)
        {
            if (_data == null)
                throw new SalouException("No Data");

            int i = 0;

            while (ba.Length > 0)
            {
                var row = new object?[_data.FieldCount];
                for (int c = 0; c < _data.FieldCount; c++)
                    row[c] = StaticWSHelpers.ClientRecievedSalouType(ref ba).value;

                i++;
                _rows?.Add(row);
            }
            if (i < _lastPageSize)
                _nomoreData = true;
        }

#pragma warning disable CS8603 // Possible null reference return. 
#pragma warning disable CS8602 // Possible null reference return. 
        /// <inheritdoc />
        public override int Depth => _data.Depth;
        /// <inheritdoc />
        public override int FieldCount => _data.FieldCount;
        /// <inheritdoc />
        public override bool HasRows => _hasRows;
        /// <inheritdoc />
        public override bool IsClosed => _isClosed;
        /// <inheritdoc />
        public override int RecordsAffected => _data.RecordsAffected;
        /// <inheritdoc />
        public override object this[int ordinal] => _curRow[ordinal];
        /// <inheritdoc />
        public override object this[string name] => _colNames == null ? throw new SalouException("No Column Names or Schema Loaded") : _curRow[_colNames[name]];
        /// <inheritdoc />
        public override DataTable? GetSchemaTable() => _data.SchemaTable == null ? throw new SalouException("No Schema Loaded") : _data.SchemaTable;
#pragma warning restore CS8602 // Possible null reference return.
#pragma warning restore CS8603 // Possible null reference return.
        /// <inheritdoc />
        public override bool Read()
        {
            if (_curRow == null || _rows == null)
                throw new SalouException("curRow is null");

            if (_curRowIdx + 1 < _rows.Count())
            {
                _curRow = _rows[++_curRowIdx];
                return true;
            }
            else if(!_nomoreData)
            {
                if(_readMultiThreaded)
                {
                    //Wait for the Reading Thread
                    lock (_mres1!)
                    {
                        if(!_nomoreData)//safe Side
                            _mres1.Reset();
                    }
                    _mres1.Wait();

                    if (_curRowIdx + 1 < _rows.Count())
                    {
                        _curRow = _rows[++_curRowIdx];
                        return true;
                    }
                }
                else if (LoadMoreData())
                {
                        _curRow = _rows[++_curRowIdx];
                        return true;
                }
            }
            return false;
        }
        /// <inheritdoc />
        public override IEnumerator GetEnumerator()
        {
            while (Read())
            {
                yield return _curRow;
            }
        }
#if NETFX48

#else
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        /// <inheritdoc />
        public override Type GetFieldType(int ordinal)
        {
            if (_curRow == null)
                throw new SalouException("curRow is null");

            Type? ty = null;
            if (_data?.SchemaTable != null && _schemaColumns != null)
            {
                var t = (string)_data.SchemaTable.Rows[ordinal][_schemaColumns["DataType"]];
                ty = Type.GetType(t);
            }
            if (ty == null)
                ty = _curRow[ordinal]?.GetType();

            return ty ?? throw new SalouException("No Schema Loaded");
        }
        /// <inheritdoc />
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        {
            if (buffer == null)
                throw new SalouException("Buffer is null");
            if (bufferOffset + length > buffer.Length)
                throw new SalouException("Buffer too small");
            if (_curRow == null)
                throw new SalouException("curRow is null");

            var ch = _curRow[ordinal] as char[];
            if (ch == null)
            {
                var str = _curRow[ordinal] as string;
                if (str == null)
                    throw new SalouException("Not a string or char array");
                ch = str.ToCharArray();
            }

            for (long i = 0; i < length; i++)
            {
                if (i + dataOffset >= ch.Length)
                    return i;
                buffer[bufferOffset + i] = ch[dataOffset + i];
            }
            return length;
        }
        /// <inheritdoc />
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        {
            if (_curRow == null)
                throw new SalouException("No Row");
            if (buffer == null)
                throw new SalouException("Buffer is null");
            if (bufferOffset + length > buffer.Length)
                throw new SalouException("Buffer too small");

            var ch = _curRow[ordinal] as byte[];
            if (ch == null)
                throw new SalouException("Not a byte array");

            for (long i = 0; i < length; i++)
            {
                if (i + dataOffset >= ch.Length)
                    return i;
                buffer[bufferOffset + i] = ch[dataOffset + i];
            }
            return length;
        }
        /// <inheritdoc />
        public override int GetValues(object[] values)
        {
            if (_curRow == null)
                throw new SalouException("No Row");
            _curRow.CopyTo(values, 0);
            return _curRow.Length;
        }
        /// <inheritdoc />
        public override void Close()
        {
            _threadStop = true;

            if (_mres1 != null)
            {
                lock(_mres1)
                    _mres1.Set();
            }

            if (_data == null)
                throw new SalouException("No Data");

            if (!_isClosed)
                _con.WsClient?.Send<byte[]>(SalouRequestType.EndReader, null, _data.ID);
            _isClosed = true;
        }
#if NETFX48
        /// <inheritdoc />
        public new void Dispose()
        {
            Close();
            _mres1?.Dispose();

            base.Dispose();
        }
#else
        /// <inheritdoc />
        public async override ValueTask DisposeAsync()
        {
            Close();
            _mres1?.Dispose();

            await base.DisposeAsync(); ;
        }
#endif

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8605 // Unboxing a possibly null value.

#if NETFX48
        /// <inheritdoc />
        public override bool IsDBNull(int ordinal) => _curRow[ordinal] == null || _curRow[ordinal] is DBNull;
#else
        /// <inheritdoc />
        public override bool IsDBNull(int ordinal) => _curRow[ordinal] == null || _curRow[ordinal] is DBNull;
#endif
        /// <inheritdoc />
        public override bool GetBoolean(int ordinal) => (bool)_curRow[ordinal];
        /// <inheritdoc />
        public override byte GetByte(int ordinal) => (byte)_curRow[ordinal];
        /// <inheritdoc />
        public override char GetChar(int ordinal) => (char)_curRow[ordinal];
        /// <inheritdoc />
        public override DateTime GetDateTime(int ordinal) => (DateTime)_curRow[ordinal];
        /// <inheritdoc />
        public override decimal GetDecimal(int ordinal) => (decimal)_curRow[ordinal];
        /// <inheritdoc />
        public override double GetDouble(int ordinal) => (double)_curRow[ordinal];
        /// <inheritdoc />
        public override float GetFloat(int ordinal) => (float)_curRow[ordinal];
        /// <inheritdoc />
        public override Guid GetGuid(int ordinal) => (Guid)_curRow[ordinal];
        /// <inheritdoc />
        public override short GetInt16(int ordinal) => (short)_curRow[ordinal];
        /// <inheritdoc />
        public override int GetInt32(int ordinal) => (int)_curRow[ordinal];
        /// <inheritdoc />
        public override long GetInt64(int ordinal) => (long)_curRow[ordinal];
        /// <inheritdoc />
        public override string GetDataTypeName(int ordinal) => _data.SchemaTable == null ? throw new SalouException("No Schema Loaded") : (string)_data.SchemaTable.Rows[ordinal][_schemaColumns["DataType"]];
        /// <inheritdoc />
        public override string GetName(int ordinal) => _colNames == null ? throw new SalouException("No Column Names or Schema Loaded") : _colNames.FirstOrDefault(x => x.Value == ordinal).Key;
        /// <inheritdoc />
        public override int GetOrdinal(string name) => _colNames == null ? throw new SalouException("No Column Names or Schema Loaded") : _colNames[name];
        /// <inheritdoc />
        public override string GetString(int ordinal) => (string)_curRow[ordinal];
        /// <inheritdoc />
        public override object GetValue(int ordinal) => _curRow[ordinal];
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8605 // Unboxing a possibly null value.

    }
}
