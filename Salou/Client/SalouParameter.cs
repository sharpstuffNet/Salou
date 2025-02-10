using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SalouWS4Sql.Client
{

    /// <summary>
    /// Implements a DbParameter over the Salou Websocket Service
    /// </summary>

    public class SalouParameter : DbParameter
    {
        
        /// <inheritdoc />        
        public override DbType DbType { get; set; }
        
        /// <inheritdoc />        
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        
        /// <inheritdoc />        
        public override bool IsNullable { get; set; }
        
        /// <inheritdoc />        
        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        
        /// <inheritdoc />        
        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        
        /// <inheritdoc />        
        [AllowNull] 
        public override object Value { get; set; }
        
        /// <inheritdoc />        
        public override bool SourceColumnNullMapping { get; set; }
        
        /// <inheritdoc />        
        public override int Size { get; set; }
        
        /// <inheritdoc />        
        public override void ResetDbType()
        {
            DbType = DbType.String;
        }

    }
}