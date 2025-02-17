
using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using SalouWS4Sql.Server;
using System.Data.SqlClient;
using SalouWS4Sql;
using BrotliSharpLib;

namespace SalouServer
{
    /// <summary>
    /// Test Server for Salou
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Salou Server
        /// </summary>
        static WebSocketServer? __salouServer;

        /// <summary>
        /// Web Application
        /// </summary>
        static WebApplication? __app;

        /// <summary>
        /// Almost norma ASP.NET Core Main
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Task</returns>
        public static async Task Main(string[] args)
        {
            //Setup the WebApplication
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.json",
                                              optional: true,
                                              reloadOnChange: true)
                                 .AddEnvironmentVariables();

            // Add NReco File Logger
            builder.Logging.AddFile(builder.Configuration.GetSection("Logging"));

            __app = builder.Build();

            Salou.Compress = BrotliCompress;
            Salou.Decompress = BrotliDecompress;
            //!!Create the Salou Server -- next few Lines are Important
            __salouServer = new WebSocketServer(__app.Logger, __app.Configuration, CreateOpenCon);
            
            //Map The Routing and Add The Server
            __app.MapGet("/", () => "Salou Running");
            __app.UseWebSockets();
            __app.Map("/ws", StartWS);

            //All Specific Salou Stuff done
            __app.Logger.LogInformation("Salou Server Started");

            await __app.RunAsync();
        }

        private static byte[] BrotliDecompress(byte[] data)=> Brotli.DecompressBuffer(data, 0, data.Length);
        private static byte[] BrotliCompress(byte[] data)=> Brotli.CompressBuffer(data,0, data.Length);

        /// <summary>
        /// Create a new Open Connection
        /// </summary>
        /// <param name="constr">constr from the Client</param>
        /// <param name="dbName">dbName name of the Database from the Client</param>
        /// <param name="ctx">ctx comming from the Call - for security checks ...</param>
        /// <returns>Connection</returns>
        /// <remarks>in theory could be any relational DB</remarks>
        private static async Task<DbConnection> CreateOpenCon(string? constr, string? dbName, HttpContext ctx)
        {
            ////Here you can do some security checks
            //Read Real Constr from config
            constr = __app?.Configuration.GetConnectionString("DefaultConnection");
            
            //Create the Connection
            var ret = new SqlConnection(constr);
            
            //Open the Connection and do initalization
            await ret.OpenAsync();
            if (!string.IsNullOrWhiteSpace(dbName)&&ret.Database!=dbName)
                await ret.ChangeDatabaseAsync(dbName);
            
            //Hand it back
            return ret;
        }

        /// <summary>
        /// Start the WebSocket, init and call to Salou
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <returns>Task</returns>
        private static async Task StartWS(HttpContext context)
        {
            if (__salouServer!=null & context.WebSockets.IsWebSocketRequest)
            {
                var ws = await context.WebSockets.AcceptWebSocketAsync();
#pragma warning disable CS8602 // Possible null reference argument. (Checked in if)
                await __salouServer.HandleWebSocketRequest(ws,context);
#pragma warning restore CS8602 // Possible null reference argument.
            }
            else
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }
}
