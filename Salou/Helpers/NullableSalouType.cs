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
    /// NullableSalouType store data for a DbType and if it is null
    /// </summary>
    internal struct NullableSalouType
    {
        /// <summary>
        /// DbType
        /// </summary>
        public SalouType Type { get; private set; }
        /// <summary>
        /// Is Null
        /// </summary>
        public bool IsNull {get; private set; }
        /// <summary>
        /// Create a NullableSalouType
        /// </summary>
        public NullableSalouType()
        {
                
        }
        /// <summary>
        /// Create a NullableSalouType with data from a Span
        /// </summary>
        /// <param name="ba">data</param>
        public NullableSalouType(ref Span<byte> ba)
        {
            byte value = StaticWSHelpers.ReadByte(ref ba);
            Type = value >= 128 ? (SalouType)(value-128) : (SalouType)value;
            IsNull = value >= 128;
        }
        /// <summary>
        /// Create a NullableSalouType
        /// </summary>
        /// <param name="ty">DbType</param>
        /// <param name="isNull">isNull</param>
        public NullableSalouType(SalouType ty, bool isNull=false)
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
        public static implicit operator NullableSalouType(byte value)
        {
            return new NullableSalouType { Type = value >= 128 ? (SalouType)(value - 128) : (SalouType)value, IsNull = value>=128 };
        }
        /// <summary>
        /// read from byte
        /// </summary>
        /// <param name="value">NullableSalouType</param>
        public static implicit operator byte(NullableSalouType value)
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
