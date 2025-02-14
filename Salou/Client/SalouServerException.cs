using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Client
{
    /// <summary>
    /// Server Side Exception thrown from Salou
    /// </summary>
    [Serializable]
    public class SalouServerException : SalouException
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SalouServerException() { }
        /// <summary>
        /// Constructor with message
        /// </summary>
        /// <param name="message">message</param>
        public SalouServerException(string message) : base(message) {
            Salou.LoggerFkt(LogLevel.Debug, () => $"SalouServerException {message}");
        }
        /// <summary>
        /// Constructor with message and inner exception
        /// </summary>
        /// <param name="message">message</param>
        /// <param name="innerException">inner exception</param>
        public SalouServerException(string message, Exception innerException) : base(message, innerException) {
            Salou.LoggerFkt(LogLevel.Debug, () => $"SalouServerException {message}", innerException);
        }
    }
}