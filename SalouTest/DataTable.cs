using SalouWS4Sql.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouTest
{
#nullable enable
    /// <summary>
    /// Test DataTable functionality
    /// </summary>
    [TestClass]
    public sealed class DataTableTest
    {
        /// <summary>
        /// connection
        /// </summary>
        SalouConnection? _con;

        /// <summary>
        /// Initialize the connection
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            _con = new SalouConnection(new Uri("ws://localhost:5249/ws"), "Test", 120, null);
            _con.Open();
        }

        /// <summary>
        /// Cleanup the connection
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            _con?.Close();
            _con?.Dispose();
        }

        /// <summary>
        /// Test the creation of a DataSet
        /// </summary>
        [TestMethod]
        public void ReaderMultiple()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "select * from Products;\r\nselect * from Vendors;";

            var rdr = cmd.ExecuteReader();
            DataSet dataSet = new DataSet();
            do
            {
                DataTable dataTable = new DataTable();
                dataTable.Load(rdr);
                dataSet.Tables.Add(dataTable);
            } while (!rdr.IsClosed && rdr.HasRows);

            Assert.AreEqual(2, dataSet.Tables.Count);
        }
    }
}
