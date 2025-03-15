using BrotliSharpLib;
using SalouWS4Sql;
using SalouWS4Sql.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouTest
{
    public class TestBase: IDisposable
    {
        
        SalouConnection? _con;
        public TestBase()
        {
            Salou.Compress = BrotliCompress;
            Salou.Decompress = BrotliDecompress;
        }
        
        private static byte[] BrotliDecompress(byte[] data) => Brotli.DecompressBuffer(data, 0, data.Length);
        private static byte[] BrotliCompress(byte[] data) => Brotli.CompressBuffer(data, 0, data.Length);

        public void Dispose()
        {
            if(_con?.State == System.Data.ConnectionState.Open)
                _con.Close();
            _con?.Dispose();
        }

        internal SalouConnection Init(bool noOpen = false)
        {
            _con = new SalouConnection(new Uri("ws://localhost:5000/ws"), "Test", 120, null);
            if(!noOpen)
                _con.Open();

            return _con;
        }
    }
}
