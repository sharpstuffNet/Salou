﻿using SalouWS4Sql.Helpers;
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
    internal class SalouDataReader : DbDataReader, ISalouDataReader, IDisposable
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
        Lookup<string, int>? _colNames;
        /// <summary>
        /// Infariant Column Names for Comparesion
        /// </summary>
        Lookup<string, int>? _colNamesLI;
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
        /// Signal to Stop the Thread
        /// </summary>
        bool _threadStop = false;

        /// <summary>
        /// Create a SalouDataReader
        /// </summary>
        /// <param name="con">Connection</param>
        /// <param name="pageSize">Page Size</param>
        /// <param name="ba">Initial Data</param>
        public SalouDataReader(SalouConnection con, int pageSize, byte[] ba)
        {
            _readMultiThreaded = Salou.ReaderReadMultiThreaded;
            if (_readMultiThreaded)
                _thread = new Thread(ThreadLoop);

            _con = con;
            PageSize = pageSize;
            _lastPageSize = PageSize;

            InitializeCurrentResultSet(ba);
        }

        private void ThreadLoop()
        {
            try
            {
                while (!_threadStop)
                {
                    bool success = LoadMoreData();
                    if (!success)
                        break;
                }
            }
            catch (ThreadInterruptedException)
            {
                //Ignore
            }
            catch (SalouServerException sex)
            {
                if (!(_threadStop && sex.Message == SalouConst.READER_NOT_FOUND))//.CompareTo("Reader not found", StringComparison.InvariantCultureIgnoreCase) == 0))
                    throw new SalouServerException(sex.Message, sex);
            }
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
                _colNames = (Lookup<string, int>)_data.SchemaTable.Rows
                    .Cast<DataRow>()
                    .Select(r => new { Name = (string)r[nCol], Ordinal = (int)r[iCol] })
                    .ToLookup(x => x.Name, x => x.Ordinal);

                if (_colNames != null && Salou.ReaderCompareLowerInvariant)
                    _colNamesLI = (Lookup<string, int>)_data.SchemaTable.Rows
                    .Cast<DataRow>()
                    .Select(r => new { Name = (string)r[nCol], Ordinal = (int)r[iCol] })
                    .ToLookup(x => x.Name.ToLowerInvariant(), x => x.Ordinal);
            }
            else if (_data.UseSchema == UseSchema.NamesOnly)
            {
                _colNames = (Lookup<string, int>)_data.ColNames!
                    .Select((name, index) => new { name, index })
                    .ToLookup(x => x.name, x => x.index);

                if (_colNames != null && Salou.ReaderCompareLowerInvariant)
                    _colNamesLI = (Lookup<string, int>)_data.ColNames!
                    .Select((name, index) => new { name = name.ToLowerInvariant(), index })
                    .ToLookup(x => x.name, x => x.index);
            }

            ////Whole Schema?
            //if (_data.UseSchema == UseSchema.Full)
            //{
            //    _schemaColumns = new Lookup<string, int>();

            //    for (int i = 0; i < _data.SchemaTable!.Columns.Count; i++)
            //        _schemaColumns.Add(_data.SchemaTable.Columns[i].ColumnName, i);

            //    var nCol = _schemaColumns["ColumnName"].First();
            //    var iCol = _schemaColumns["ColumnOrdinal"].First();
            //    _colNames = (Lookup<string, int>)(from DataRow r in _data.SchemaTable.Rows select ((string)r[nCol], (int)r[iCol])).ToLookup(k => k.Item1, v => v.Item2);
            //}
            //else if (_data.UseSchema == UseSchema.NamesOnly)
            //{
            //    _colNames = (Lookup<string, int>)(_data.ColNames!
            //.Select((name, index) => new { name, index })
            //.ToLookup(x => x.name, x => x.index);
            //    _colNames = new Lookup<string, int>();
            //    for (int i = 0; i < _data.ColNames!.Length; i++)
            //        _colNames.Add(_data.ColNames[i], i);
            //}

            //if (_colNames != null && Salou.ReaderCompareLowerInvariant)
            //    _colNamesLI = _colNames.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value);

            //Need Data?
            _hasRows = _data.HasRows;
            if (_hasRows)
                LoadData(span);

            _curRowIdx = -1;
            _curRow = Array.Empty<object?[]>();

            _threadStop = false;
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
            {
                _threadStop = true;
                _nomoreData = true;
            }

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
            {
                _nomoreData = true;
                _threadStop = true;
            }
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
        public Lookup<string, int>? ColNames { get => _colNames; }
        /// <inheritdoc />
        public Lookup<string, int>? ColNamesLowerInvariant { get => _colNamesLI; }

        /// <inheritdoc />
        public override object this[int ordinal] => _curRow[ordinal];
        /// <inheritdoc />
        public override object this[string name] => _colNames == null ? throw new SalouException("No Column Names or Schema Loaded")
            : Salou.ReaderCompareLowerInvariant ? _curRow[_colNamesLI[name.ToLowerInvariant()].First()] : _curRow[_colNames[name].First()];
        /// <inheritdoc />
        public override DataTable? GetSchemaTable() => _data.SchemaTable == null ? throw new SalouException("No Schema Loaded") : _data.SchemaTable;
#pragma warning restore CS8602 // Possible null reference return.
#pragma warning restore CS8603 // Possible null reference return.
        /// <inheritdoc />
        public override bool Read()
        {
            if (_curRow == null || _rows == null)
                throw new SalouException("curRow is null");

            var idxPlus1 = _curRowIdx + 1;
            if (idxPlus1 < _rows.Count())
            {
                _curRow = _rows[++_curRowIdx];
                return true;
            }
            else if (!_nomoreData)
            {
                if (_readMultiThreaded && _thread?.IsAlive == true)
                {
                    while (_thread?.IsAlive == true)
                    {
                        if (idxPlus1 < _rows.Count())
                        {
                            _curRow = _rows[++_curRowIdx];
                            return true;
                        }
                        Thread.Sleep(50);
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
            //_thread?.Interrupt();

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

            base.Dispose();
        }
#else
        /// <inheritdoc />
        public async override ValueTask DisposeAsync()
        {
            Close();

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
        public override string GetName(int ordinal)
        {
            if (_colNames == null)
                throw new SalouException("No Column Names or Schema Loaded");

            foreach (var item in _colNames)
            {
                foreach (var item1 in item)
                {
                    if (ordinal == item1)
                        return item.Key;
                }
            }
            return null;
        }
        /// <inheritdoc />
        public override int GetOrdinal(string name) => _colNames == null ? throw new SalouException("No Column Names or Schema Loaded")
            : Salou.ReaderCompareLowerInvariant ? _colNamesLI[name.ToLowerInvariant()].First() : _colNames[name].First();
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
