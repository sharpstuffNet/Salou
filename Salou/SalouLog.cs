using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql
{
#nullable enable
    /// <summary>
    /// Log Delegate to make a perf logging in client and server
    /// </summary>
    /// <param name="lvl">LogLevel</param>
    /// <param name="msgBuilder">Log Msg Builder</param>
    /// <param name="ex">Exception</param>
    public delegate void LogDelegate(LogLevel lvl, Func<string> msgBuilder, Exception? ex=null);
    /// <summary>
    /// Log Class for logging in Salou
    /// </summary>
    public static class SalouLog
    {
        /// <summary>
        /// actual Logger
        /// </summary>
        public static ILogger? Logger { get; set; }
        /// <summary>
        /// Logger Function
        /// </summary>
        public static LogDelegate LoggerFkt { get; private set; }
        /// <summary>
        /// Set the Logger Function
        /// </summary>
        static SalouLog()
        {
            LoggerFkt = LoggerFunction;
        }
        /// <summary>
        /// Log a message depending on the LogLevel so that the string is not built if the LogLevel is not enabled
        /// </summary>
        /// <param name="lvl">LogLevel</param>
        /// <param name="msgBuilder">msgBuilder</param>
        /// <param name="ex">Exception</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoggerFunction(LogLevel lvl, Func<string> msgBuilder, Exception? ex=null)
        {
            if (Logger == null || !Logger.IsEnabled(lvl))
                return;

            if(ex != null)
                Logger.Log(lvl, ex, msgBuilder());
            else
                Logger.Log(lvl, msgBuilder());
        }
    }
}
