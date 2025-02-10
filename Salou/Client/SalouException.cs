
using System;
using System.Data.Common;

namespace SalouWS4Sql.Client
{
    /// <summary>
    /// Exception thrown from Salou
    /// </summary>
    [Serializable]
    public class SalouException : DbException
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SalouException() { }
        /// <summary>
        /// Constructor with message
        /// </summary>
        /// <param name="message">message</param>
        public SalouException(string message) : base(message) { }
        /// <summary>
        /// Constructor with message and inner exception
        /// </summary>
        /// <param name="message">message</param>
        /// <param name="innerException">inner exception</param>
        public SalouException(string message, Exception innerException) : base(message, innerException) { }
    }
}