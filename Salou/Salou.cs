using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SalouWS4Sql.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace SalouWS4Sql
{
#nullable enable
    /// <summary>
    /// Log Delegate to make a perf logging in client and server
    /// </summary>
    /// <param name="lvl">LogLevel</param>
    /// <param name="msgBuilder">Log Msg Builder</param>
    /// <param name="ex">Exception</param>
    public delegate void LogDelegate(LogLevel lvl, Func<string> msgBuilder, Exception? ex = null);

    /// <summary>
    /// Converter Delegate to convert the Data from the Server to the Client
    /// Converting NET Data to DBData to Salou Data used for Serialization incl object if neccessary
    /// </summary>
    /// <param name="o">object</param>
    /// <param name="dt">DbType</param>
    /// <param name="st">SalouType</param>
    /// <returns>value and types</returns>
    public delegate (object? value, DbType dbType, SalouType salouType) SendToServerConverterDelegate(object? o, DbType? dt, SalouType? st);
    /// <summary>
    /// Converter Delegate to convert the Data from the Server to the Client
    /// Converting NET Data to DBData to Salou Data used for Serialization incl object if neccessary
    /// </summary>
    /// <param name="o">object</param>
    /// <param name="dt">DbType</param>
    /// <param name="st">SalouType</param>
    /// <returns>value and types</returns>
    public delegate (object? value, DbType? dbType, SalouType salouType, Type ty) RecivedFromServerConverterDelegate(object? o, DbType dt, SalouType st);
    /// <summary>
    /// Converter Delegate to convert the Data from the Server to the Client
    /// Converting NET Data to DBData to Salou Data used for Serialization incl object if neccessary
    /// </summary>
    /// <param name="o">object</param>
    /// <param name="dt">DbType</param>
    /// <param name="type">Type</param>
    /// <param name="st">SalouType</param>
    /// <returns>value and types</returns>
    public delegate (object? value, DbType dbType, SalouType salouType) ServerSendToClientConverterDelegate(object? o, DbType? dt, Type? type, SalouType? st);
    /// <summary>
    /// Converter Delegate to convert the Data from the Server to the Client
    /// Converting NET Data to DBData to Salou Data used for Serialization incl object if neccessary
    /// </summary>
    /// <param name="o">object</param>
    /// <param name="d">dbType</param>
    /// <param name="st">salouType</param>
    /// <returns>value and types</returns>
    public delegate (object? value, DbType dbType, SalouType salouType) RecivedFromClientConverterDelegate(object? o, DbType d, SalouType st);

    public delegate byte[] CompressDecompressDelegate(byte[] data);

    /// <summary>
    /// Salou Configuration
    /// </summary>
    public static partial class Salou
    {
        /// <summary>
        /// actual Logger
        /// </summary>
        public static ILogger? Logger { get; set; }
        /// <summary>
        /// Logger Function
        /// </summary>
        public static LogDelegate LoggerFkt { get; set; }

        /// <summary>
        /// The Used SendToServerConverter 
        /// </summary>
        public static SendToServerConverterDelegate SendToServerConverter { get; set; } = SendToServerConverterFkt;
        /// <summary>
        /// The Used RecivedFromServerConverter
        /// </summary>
        public static RecivedFromServerConverterDelegate RecivedFromServerConverter { get; set; } = RecivedFromServerConverterFkt;
        /// <summary>
        /// The Used ServerSendToClientConverter
        /// </summary>
        public static ServerSendToClientConverterDelegate ServerSendToClientConverter { get; set; } = ServerSendToClientConverterFkt;
        /// <summary>
        /// The Used RecivedFromClientConverter
        /// </summary>
        public static RecivedFromClientConverterDelegate RecivedFromClientConverter { get; set; } = RecivedFromClientConverterFkt;

        public static CompressDecompressDelegate? Compress { get; set; } = null;

        public static CompressDecompressDelegate? Decompress { get; set; } = null;

        /// <summary>
        /// Default Page Size for the DataReader
        /// </summary>
        public static int DefaultPageSize { get; set; } = 100;
        /// <summary>
        /// Default Page Size for the DataReader used in the initial call where also the schema has to be send
        /// </summary>
        public static int DefaultPageSizeInitalCall { get; set; } = 25;

        //public static bool ReturnDBNull { get; set; } = true;

        /// <summary>
        /// Add a Header to the Websocket Request for example authentication
        /// </summary>
        public static Dictionary<string, string?> RequestHeaders = new Dictionary<string, string?>();
        /// <summary>
        /// Try too Leave the Client open over multiple connections
        /// </summary>
        /// <remarks>force close using Close on an not opend connection after it is open</remarks>
        public static bool LeaveClientOpen { get; set; } = true;

        /// <summary>
        /// Sets The Default Logger Function
        /// </summary>
        static Salou()
        {
            LoggerFkt = LoggerFunction;
        }

        /// <summary>
        /// the Configuration information
        /// </summary>
        static IConfiguration? _cfg;

        /// <summary>
        /// Read the Configuration
        /// </summary>
        /// <param name="cfg">IConfiguration</param>
        public static void ReadConfiguration(IConfiguration cfg)
        {
            _cfg = cfg;

            //TODO Read Config
            IConfigurationSection cfgS = cfg.GetSection("Salou");
            DefaultPageSize = cfgS.GetValue<int>("ReaderPageSize");
            DefaultPageSizeInitalCall = cfgS.GetValue<int>("ReaderPageSizeInitalCall");
        }

        /// <summary>
        /// Log a message depending on the LogLevel so that the string is not built if the LogLevel is not enabled
        /// </summary>
        /// <param name="lvl">LogLevel</param>
        /// <param name="msgBuilder">msgBuilder</param>
        /// <param name="ex">Exception</param>
        private static void LoggerFunction(LogLevel lvl, Func<string> msgBuilder, Exception? ex = null)
        {
            if (Logger == null || !Logger.IsEnabled(lvl))
                return;

            if (ex != null)
                Logger.Log(lvl, ex, msgBuilder());
            else
                Logger.Log(lvl, msgBuilder());
        }


    }
}
