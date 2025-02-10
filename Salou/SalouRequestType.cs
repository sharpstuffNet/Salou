namespace SalouWS4Sql
{
    /// <summary>
    /// SalouRequestType
    /// </summary>
    internal enum SalouRequestType : byte
    {
        /// <summary>
        /// TransactionCommit
        /// </summary>
        TransactionCommit,
        /// <summary>
        /// CommandCancel
        /// </summary>
        CommandCancel,
        /// <summary>
        /// ConnectionOpen
        /// </summary>
        ConnectionOpen,
        /// <summary>
        /// ChangeDatabase
        /// </summary>
        ChangeDatabase,
        /// <summary>
        /// ConnectionClose
        /// </summary>
        ConnectionClose,
        /// <summary>
        /// BeginTransaction
        /// </summary>
        BeginTransaction,
        /// <summary>
        /// ExecuteNonQuery
        /// </summary>
        ExecuteNonQuery,
        /// <summary>
        /// ExecuteReaderStart
        /// </summary>
        ExecuteReaderStart,
        /// <summary>
        /// execute scalar
        /// </summary>
        ExecuteScalar,
        /// <summary>
        /// server Version
        /// </summary>
        ServerVersion,
        /// <summary>
        /// TransactionRollback
        /// </summary>
        TransactionRollback,
        /// <summary>
        /// EndReader
        /// </summary>
        EndReader,
        /// <summary>
        /// ContinueReader
        /// </summary>
        ContinueReader,
        /// <summary>
        /// ReaderNextResult
        /// </summary>
        ReaderNextResult
    }
}
