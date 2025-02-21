using System;

namespace SalouWS4Sql.Client
{
    /// <summary>
    /// Exception thrown when a Salou operation times out
    /// </summary>
    [Serializable]
    public class SalouTimeoutException : Exception
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SalouTimeoutException()
        {
        }
        /// <summary>
        /// Constructor with message
        /// </summary>
        /// <param name="message">message</param>
        public SalouTimeoutException(string? message) : base(message)
        {
        }
        /// <summary>
        /// Constructor with message and inner exception
        /// </summary>
        /// <param name="message">message</param>
        /// <param name="innerException">innerException</param>
        public SalouTimeoutException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}