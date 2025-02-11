using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Helpers
{
    /// <summary>
    /// Reader Data for the Salou Websocket Service supports Data exchange
    /// Initial Reader Data (ScemaTable ...) is sent to the client
    /// </summary>
    internal record ReaderData
    {
        /// <summary>
        /// ID
        /// </summary>
        public int ID { get; }
        /// <summary>
        /// Depth
        /// </summary>
        public int Depth { get; }
        /// <summary>
        /// Field Count
        /// </summary>
        public int FieldCount { get; }
        /// <summary>
        /// Has Rows
        /// </summary>
        public bool HasRows { get; }
        /// <summary>
        /// Records Affected
        /// </summary>
        public int RecordsAffected { get; }
        /// <summary>
        /// Visible Field Count
        /// </summary>
        public int VisibleFieldCount { get; }
        /// <summary>
        /// Schema Table
        /// </summary>
        public DataTable? SchemaTable { get; }
        /// <summary>
        /// Use Schema
        /// </summary>
        public UseSchema UseSchema { get; }
        /// <summary>
        /// Column Names
        /// </summary>
        public string[]? ColNames { get; }

        /// <summary>
        /// Create a ReaderData from span
        /// </summary>
        /// <param name="span">data</param>
        public ReaderData(ref Span<byte> span)
        {
            ID = StaticWSHelpers.ReadInt(ref span);
            Depth = StaticWSHelpers.ReadInt(ref span);
            FieldCount = StaticWSHelpers.ReadInt(ref span);
            HasRows = span[0] == 'T'; span = span.Slice(1);
            RecordsAffected = StaticWSHelpers.ReadInt(ref span);
            VisibleFieldCount = StaticWSHelpers.ReadInt(ref span);
            UseSchema = (UseSchema)StaticWSHelpers.ReadByte(ref span);

            switch (UseSchema)
            {
                case UseSchema.NamesOnly:
                    SchemaTable = null;
                    var els = StaticWSHelpers.ReadInt(ref span);
                    ColNames = new string[els];
                    for (int i = 0; i < els; i++)
                        ColNames[i] = StaticWSHelpers.ReadString(ref span) ?? string.Empty;
                    break;
                case UseSchema.Full:
                    ColNames = null;
                    var sctableCols = StaticWSHelpers.ReadInt(ref span);
                    var dt = new DataTable();
                    dt.BeginInit();

                    //Overview 'columns' of SchemaTable
                    int tColumn = -1;
                    int tsColumn = -1;
                    for (int i = 0; i < sctableCols; i++)
                    {
                        var na = StaticWSHelpers.ReadString(ref span);
                        if (na == "DataType")
                        {
                            tColumn = i;
                            dt.Columns.Add(na, typeof(System.Type));
                        }
                        else if (na == "ProviderSpecificDataType")
                        {
                            tsColumn = i;
                            dt.Columns.Add(na,typeof(System.Type));
                        }
                        else
                        {
                            var ty = (DbType)StaticWSHelpers.ReadByte(ref span);
                            dt.Columns.Add(na, StaticWSHelpers.DbTypeToNetType(ty));
                        }
                    }
                    dt.EndInit();
                    dt.BeginLoadData();

                    //Rows the real schema
                    var sctableRows = StaticWSHelpers.ReadInt(ref span);
                    for (int i = 0; i < sctableRows; i++)
                    {
                        var dr = dt.NewRow();
                        dr.BeginEdit();
                        for (int j = 0; j < sctableCols; j++)
                        {
                            if (j == tColumn)
                            {
                                var ty = (DbType)StaticWSHelpers.ReadByte(ref span);
                                dr[j] = StaticWSHelpers.DbTypeToNetType(ty);
                            }
                            else if (j == tsColumn)
                            {
                                ///ignored so that DataSet load etc works
                                //dr[j] = (DbType)StaticWSHelpers.ReadByte(ref span);
                            }
                            else
                                dr[j] = StaticWSHelpers.ReadNullableDbType(ref span, DBNull.Value).Item1;
                        }
                        dr.EndEdit();
                        dr.AcceptChanges();
                        dt.Rows.Add(dr);
                    }
                    dt.EndLoadData();
                    SchemaTable = dt;
                    break;
                case UseSchema.None:
                    SchemaTable = null;
                    ColNames = null;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Create a ReaderData on the server side
        /// </summary>
        /// <param name="iD">iD</param>
        /// <param name="depth">depth</param>
        /// <param name="fieldCount">fieldCount</param>
        /// <param name="hasRows">hasRows</param>
        /// <param name="recordsAffected">recordsAffected</param>
        /// <param name="visibleFieldCount">visibleFieldCount</param>
        /// <param name="dataTable">dataTable</param>
        /// <param name="useSchema">useSchema</param>
        public ReaderData(int iD, int depth, int fieldCount, bool hasRows, int recordsAffected, int visibleFieldCount, DataTable? dataTable, UseSchema useSchema)
        {
            ID = iD;
            Depth = depth;
            FieldCount = fieldCount;
            HasRows = hasRows;
            RecordsAffected = recordsAffected;
            VisibleFieldCount = visibleFieldCount;
            UseSchema = useSchema;
            switch (useSchema)
            {
                case UseSchema.NamesOnly:
                    if (dataTable != null)
                    {
                        var schemaColumns = new Dictionary<string, int>();

                        for (int i = 0; i < dataTable.Columns.Count; i++)
                            schemaColumns.Add(dataTable.Columns[i].ColumnName, i);

                        var nCol = schemaColumns["ColumnName"];
                        var iCol = schemaColumns["ColumnOrdinal"];
                        var colNames = new Dictionary<int, string>();
                        foreach (DataRow r in dataTable.Rows)
                            colNames.Add((int)r[iCol], (string)r[nCol]);

                        ColNames = (from a in colNames orderby a.Key select a.Value).ToArray();
                    }
                    SchemaTable = null;
                    break;
                case UseSchema.Full:
                    SchemaTable = dataTable;
                    break;
                case UseSchema.None:
                    SchemaTable = null;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Write the data to a memory stream
        /// </summary>
        /// <param name="msOut">MemoryStream</param>
        internal void Write(MemoryStream msOut)
        {
            StaticWSHelpers.WriteInt(msOut, ID);
            StaticWSHelpers.WriteInt(msOut, Depth);
            StaticWSHelpers.WriteInt(msOut, FieldCount);
            msOut.WriteByte((byte)(HasRows ? 'T' : 'F'));
            StaticWSHelpers.WriteInt(msOut, RecordsAffected);
            StaticWSHelpers.WriteInt(msOut, VisibleFieldCount);

            switch (UseSchema)
            {
                case UseSchema.NamesOnly:
                    if (ColNames == null || ColNames.Length == 0)
                        msOut.WriteByte((byte)UseSchema.None);
                    else
                    {
                        msOut.WriteByte((byte)UseSchema.NamesOnly);
                        StaticWSHelpers.WriteInt(msOut, ColNames.Length);
                        foreach (var item in ColNames)
                            StaticWSHelpers.WriteString(msOut, item);
                    }
                    break;
                case UseSchema.Full:
                    if (SchemaTable == null)
                        msOut.WriteByte((byte)UseSchema.None);
                    else
                    {
                        int tColumn = -1;
                        int tsColumn = -1;
                        msOut.WriteByte((byte)UseSchema.Full);
                        StaticWSHelpers.WriteInt(msOut, SchemaTable.Columns.Count);
                        foreach (DataColumn dc in SchemaTable.Columns)
                        {
                            StaticWSHelpers.WriteString(msOut, dc.ColumnName);
                            if (dc.ColumnName == "DataType")
                                tColumn = dc.Ordinal;
                            else if (dc.ColumnName == "ProviderSpecificDataType")
                                tsColumn = dc.Ordinal;
                            else
                            {
                                var ty = StaticWSHelpers.DotNetTypeToDbType(dc.DataType, true);
                                msOut.WriteByte((byte)ty);
                            }
                        }
                        StaticWSHelpers.WriteInt(msOut, SchemaTable.Rows.Count);
                        foreach (DataRow dr in SchemaTable.Rows)
                        {
                            for (int i = 0; i < SchemaTable.Columns.Count; i++)
                            {
                                if (i == tColumn)
                                {
                                    var ty = StaticWSHelpers.DotNetTypeToDbType((Type)dr[i], true);
                                    msOut.WriteByte((byte)ty);
                                }
                                else if (i == tsColumn)
                                {
                                    //msOut.WriteByte((byte)(DbType)dr[i]);
                                }
                                else
                                    StaticWSHelpers.WriteObjectAsDBType(msOut, dr[i], true);
                            }
                        }
                    }
                    break;
                case UseSchema.None:
                    msOut.WriteByte((byte)UseSchema.None);
                    break;
                default:
                    break;
            }
        }
    }
}
