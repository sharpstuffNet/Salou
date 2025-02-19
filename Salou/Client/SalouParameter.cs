using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SalouWS4Sql.Client
{

    /// <summary>
    /// Implements a DbParameter over the Salou Websocket Service
    /// </summary>

    [DebuggerDisplay("{ParameterName} {Value}")]
    public class SalouParameter : DbParameter
    {

        internal DbType? _dbType = null;

        /// <inheritdoc />  
        public override DbType DbType
        {
            get { return _dbType==null ? default(DbType) : _dbType.Value; }
            set { _dbType = value; }
        }
        internal bool DbYypeSet => _dbType.HasValue;
        /// <inheritdoc />        
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        internal bool? _isNullable = null;

        /// <inheritdoc />        
        public override bool IsNullable
        {
            get { return _isNullable==null ? default(bool) : IsNullable; }
            set { _isNullable = value; }
        }
        internal bool IsNullableSet => _isNullable.HasValue;
        /// <inheritdoc />        
        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        /// <inheritdoc />   
        /// <remarks>Ignored</remarks>
        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        /// <inheritdoc />        
        [AllowNull]
        public override object Value { get; set; }

        /// <inheritdoc />     
        /// <remarks>Ignored</remarks>
        public override bool SourceColumnNullMapping { get; set; }

        internal int? _size = null;

        /// <inheritdoc />        
        public override int Size
        {
            get { return _size==null ? default(int) : _size.Value; }
            set { _size = value; }
        }
        internal bool SizeSet => _size.HasValue;

        internal byte? _scale = null;

        /// <inheritdoc />        
        public override byte Scale
        {
            get { return _scale==null ? default(byte) : _scale.Value; }
            set { _scale = value; }
        }
        internal bool ScaleSet => _size.HasValue;

        internal byte? _precision = null;
        
        /// <inheritdoc />        
        public override byte Precision
        {
            get { return _precision==null ? default(byte) : _precision.Value; }
            set { _precision = value; }
        }
        internal bool PrecisionSet => _precision.HasValue;

        /// <inheritdoc />        
        public override void ResetDbType()
        {
            DbType = DbType.String;
        }
        
    }
}