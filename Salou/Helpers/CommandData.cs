using SalouWS4Sql.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Helpers
{
    /// <summary>
    /// Command Data for the Salou Websocket Service supports Data exchange
    /// </summary>
    [DebuggerDisplay("{Parameters.Count} {CommandText}")]
    internal record CommandData
    {
        /// <summary>
        /// Command Text
        /// </summary>
        internal string CommandText {get; set; }
        /// <summary>
        /// Command Timeout
        /// </summary>
        internal int CommandTimeout { get; set; }
        /// <summary>
        /// Command Type
        /// </summary>
        internal CommandType CommandType { get; set; }
        /// <summary>
        /// Parameters
        /// </summary>
        internal DbParameterCollection Parameters { get; set; }

        /// <summary>
        /// Create a CommandData
        /// </summary>
        /// <param name="commandText">commandText</param>
        /// <param name="commandTimeout">commandTimeout</param>
        /// <param name="commandType">commandType</param>
        /// <param name="parameters">parameters</param>
        public CommandData(string commandText, int commandTimeout, CommandType commandType, DbParameterCollection parameters)
        {
            CommandText = commandText;
            CommandTimeout = commandTimeout;
            CommandType = commandType;
            Parameters = parameters;
        }

        /// <summary>
        /// deserialize a CommandData
        /// </summary>
        /// <param name="span">Data</param>
        public CommandData(ref Span<byte> span)
        {
            Parameters = new SalouParameterCollection();
            CommandText = StaticWSHelpers.ReadString(ref span) ?? "";
            CommandTimeout = StaticWSHelpers.ReadInt(ref span);
            CommandType = (CommandType)StaticWSHelpers.ReadInt(ref span);
            int count = StaticWSHelpers.ReadInt(ref span);

            for (int i = 0; i < count; i++)
            {
                var p = new SalouParameter();
                p.ParameterName = StaticWSHelpers.ReadString(ref span) ?? "";
                p.Direction = (ParameterDirection)StaticWSHelpers.ReadByte(ref span);
                var data= StaticWSHelpers.ServerRecievedSalouType(ref span);
                p.DbType = data.dbType;
                p.Value = data.value;
                var x = StaticWSHelpers.ReadInt(ref span);
                if (x != int.MinValue)
                    p.Size = x;
                var y = StaticWSHelpers.ReadByte(ref span);
                if (y != byte.MinValue)
                    p.Scale = y;
                var z = StaticWSHelpers.ReadByte(ref span);
                if (z != byte.MinValue)
                    p.Precision = z;
                var q = StaticWSHelpers.ReadByte(ref span);
                if (q != 'N')
                    p.IsNullable = q == 'T';

                Parameters.Add(p);
            }
        }

        /// <summary>
        /// serialize a CommandData
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        internal void WriteToServer(MemoryStream ms)
        {
            StaticWSHelpers.WriteString(ms, CommandText);
            StaticWSHelpers.WriteInt(ms, CommandTimeout);
            StaticWSHelpers.WriteInt(ms, (int)CommandType);
            StaticWSHelpers.WriteInt(ms, Parameters.Count);

            foreach (DbParameter p in Parameters)
            {
                if (p is SalouParameter sp)
                {
                    StaticWSHelpers.WriteString(ms, sp.ParameterName);
                    ms.WriteByte((byte)sp.Direction);
                    StaticWSHelpers.ClientWriteSalouType(ms, sp.DbType, sp.Value);//always so fallback to dfault AnsiString
                    StaticWSHelpers.WriteInt(ms, sp.SizeSet ? sp.Size : int.MinValue);
                    ms.WriteByte(sp.ScaleSet ? sp.Scale : byte.MinValue);
                    ms.WriteByte(sp.PrecisionSet ? sp.Precision : byte.MinValue);
                    ms.WriteByte((byte)(sp.IsNullableSet ? (sp.IsNullable ? 'T' : 'F') : 'N'));
                }
                else
                {
                    StaticWSHelpers.WriteString(ms, p.ParameterName);
                    ms.WriteByte((byte)p.Direction);
                    StaticWSHelpers.ClientWriteSalouType(ms, p.DbType, p.Value);
                    StaticWSHelpers.WriteInt(ms, p.Size);
                    ms.WriteByte(p.Scale);
                    ms.WriteByte(p.Precision);
                    ms.WriteByte((byte)(p.IsNullable ? 'T' : 'F'));
                }
            }
        }

        /// <summary>
        /// process Command Data comming back from the server
        /// </summary>
        /// <param name="span">Data</param>
        /// <returns>object / type for the rest of the Data</returns>
        internal (object? value, Type netType) ReadReturnFromServer(ref Span<byte> span)
        {
            var cnt = StaticWSHelpers.ReadInt(ref span);
            for (int i = 0; i < cnt; i++)
            {
                string name = StaticWSHelpers.ReadString(ref span) ?? string.Empty;
                var par = Parameters.Cast<DbParameter>()
                    .Where(p => (p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput || p.Direction == ParameterDirection.ReturnValue) && p.ParameterName == name).FirstOrDefault();
                if (par == null)
                    StaticWSHelpers.DropSalouType(ref span);//Dump and Move On
                else
                {
                    par.Value = StaticWSHelpers.ClientRecievedSalouType(ref span).value; //Type already set
                    par.Size = StaticWSHelpers.ReadInt(ref span);
                    par.Scale= StaticWSHelpers.ReadByte(ref span);
                    par.Precision = StaticWSHelpers.ReadByte(ref span);
                    par.IsNullable = StaticWSHelpers.ReadByte(ref span)=='T';
                }

            }
            var rt = (SalouReturnType)StaticWSHelpers.ReadByte(ref span);
            if (rt == SalouReturnType.NullableSalouType)
                return StaticWSHelpers.ClientRecievedSalouType(ref span);
            if (rt == SalouReturnType.ReaderStart || rt== SalouReturnType.ReaderContinue)
                return (span.ToArray(), typeof(byte[]));
            if (rt == SalouReturnType.Integer)
                return (StaticWSHelpers.ReadInt(ref span),typeof(int));
            if (rt == SalouReturnType.Long)
                return (StaticWSHelpers.ReadLong(ref span), typeof(Int64));
            return (null,typeof(object));
        }
    }
}
