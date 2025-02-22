using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Client
{
    /// <summary>
    /// Exception thrown when the connection is closed
    /// </summary>
    [Serializable]
    public class SalouConClosedException : SalouException
    {
        /// <summary>
        /// Flag if the exception happend while sending
        /// </summary>
        public bool HappendWhileSending { get; set; }
        /// <summary>
        /// Default constructor
        /// </summary>
        public SalouConClosedException() { }
        /// <summary>
        /// Constructor with message
        /// </summary>
        /// <param name="message">message</param>
        public SalouConClosedException(string message, bool happendWhileSending) : base(message) { HappendWhileSending = happendWhileSending; }
        /// <summary>
        /// Constructor with message and inner exception
        /// </summary>
        /// <param name="message">message</param>
        /// <param name="innerException">inner exception</param>
        public SalouConClosedException(string message, Exception innerException, bool happendWhileSending) : base(message, innerException) { HappendWhileSending = happendWhileSending;}
    }
}