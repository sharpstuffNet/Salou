# Salou
Tunnel a SqlServer Connection via WebSocket

## in Short:
Salou is a WebSocket Server that tunnels a SqlServer Connection

## History
I Created this sometime back in the NET 3.5 timeframe when i worked as a Freelancer
We needed to access a SqlServer from a WebApp but the Server was behind a Firewall and the only way to access it was via a WebService.
But to write specific WebServices for every Query was not an option.
So i created a WebSocket Server that tunnels the Connection to the SqlServer.

Even if i had the right to publish this as OpenSource unfortunately i could not find the time back then.

The Idea was later used in an project using DB2 a few years later.

No i needed something similiar but did not want to write it again and time is also an issue so i decided to publish it.

I updated everythung to NET 9 but kept the original Code structure. 
But updated it to SPAN and async with a little help from Copilot.
I also added a few things like a Logger and Configuration.

Then i needed to Backport the Client to 4.8 because i needed the Client it in a project that was still on 4.8

## How it works

- Usage: Create a Server (see SalouServer Example) init SalouServer with Logger, Configuration and a Func<SqlConnection> to create a new Connection
- Rout to SalouServer
```csharp
    public static async Task Main(string[] args)
    {
        ...
        SalouServer __salouServer = new SalouServer(__app.Logger, __app.Configuration, CreateOpenCon);

         //Map The Routing and Add The Server
        __app.MapGet("/", () => "Salou Running");
        __app.UseWebSockets();
        __app.Map("/ws", StartWS);


        __salouServer = new WebSocketServer(__app.Logger, __app.Configuration, CreateOpenCon);
        ...
    }

    private static async Task<DbConnection> CreateOpenCon(string? constr, string? dbName, HttpContext ctx)
    {
        var con = new SqlConnection(constr);
        await con.OpenAsync();
        return con;
    }

    private static async Task StartWS(HttpContext context)
    {
        if (__salouServer!=null & context.WebSockets.IsWebSocketRequest)
        {
            var ws = await context.WebSockets.AcceptWebSocketAsync();
            await __salouServer.AcceptWebSocketRequest(ws,context);
        }
        else
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
```
Use the ClientLib as DbConnection and set the URL to the Server
```csharp
    ...
    var con = new SalouConnection(new Uri("ws://localhost:5249/ws"), "Test", 120, null);
    con.Open();
    ...
```
if you cast for example SalouCommand to DbCommand you can set additional propertys like a PageSize for the Reader.
Some stuff you can also set via Configuration
      
## Settings and extendet Faetures
All Settings and extendet Faetures can be configured via the Static Salou Class
- Logger timeouts etc
- RedaerReadMultiThreaded
- Converters for DataTypes (examples in Salou.Converter.cs)
- Compression

```csharp
    ...
    Salou.Compress = BrotliCompress;
    Salou.Decompress = BrotliDecompress;
    ...
    private static byte[] BrotliDecompress(byte[] data)=> Brotli.DecompressBuffer(data, 0, data.Length);
    private static byte[] BrotliCompress(byte[] data)=> Brotli.CompressBuffer(data,0, data.Length);
    ...
```
           
# For Testing and a full example
please checkout mý Fork of ToyStore-Database-Example https://github.com/horstsstuff/ToyStore-Database-Example-Using-WinForms--ADO.NET-in-VB.git 
you also need the DB scripts fo the End To ENd Tests
