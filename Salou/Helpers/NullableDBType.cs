using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Helpers
{
    /// <summary>
    /// NullableDBType store data for a DbType and if it is null
    /// </summary>
    internal struct NullableDBType
    {
        /// <summary>
        /// DbType
        /// </summary>
        public DbType Type { get; private set; }
        /// <summary>
        /// Is Null
        /// </summary>
        public bool IsNull {get; private set; }
        /// <summary>
        /// Create a NullableDBType
        /// </summary>
        public NullableDBType()
        {
                
        }
        /// <summary>
        /// Create a NullableDBType with data from a Span
        /// </summary>
        /// <param name="ba">data</param>
        public NullableDBType(ref Span<byte> ba)
        {
            byte value = StaticWSHelpers.ReadByte(ref ba);
            Type = value >= 128 ? (DbType)(value-128) : (DbType)value;
            IsNull = value >= 128;
        }
        /// <summary>
        /// Create a NullableDBType
        /// </summary>
        /// <param name="ty">DbType</param>
        /// <param name="isNull">isNull</param>
        public NullableDBType(DbType ty, bool isNull=false)
        {
            Type = ty;
            IsNull = isNull;
        }
        /// <summary>
        /// Write the data to a memory stream
        /// </summary>
        /// <param name="ms">MemoryStream</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToMs(MemoryStream ms)
        {
            ms.WriteByte((byte)(IsNull ? ((byte)Type)+128 : (byte)Type));
        }
        /// <summary>
        /// Convert to byte
        /// </summary>
        /// <param name="value">sbyte</param>
        public static implicit operator NullableDBType(byte value)
        {
            return new NullableDBType { Type = value >= 128 ? (DbType)(value - 128) : (DbType)value, IsNull = value>=128 };
        }
        /// <summary>
        /// read from byte
        /// </summary>
        /// <param name="value">NullableDBType</param>
        public static implicit operator byte(NullableDBType value)
        {
            return (byte)(value.IsNull ? ((byte)value.Type) + 128 : (byte)value.Type);
        }

        /// <summary>
        /// Convert to string
        /// </summary>
        /// <returns>string</returns>
        public override string ToString()
        {
            return $"{Type} {(IsNull ? "NULL" : "")}";
        }
    }
}
