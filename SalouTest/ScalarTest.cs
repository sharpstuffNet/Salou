using SalouWS4Sql.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouTest
{
#nullable enable
    /// <summary>
    /// Test Scalar
    /// </summary>
    [TestClass]
    public sealed class ScalarTest
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
            _con = new SalouConnection(new Uri("ws://localhost:5249/ws"),"Test", 120, null);
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
        /// scalar test
        /// </summary>
        [TestMethod]
        public void StringScalarNoP()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "SELECT @@VERSION";
            var x=cmd.ExecuteScalar();
            Assert.IsTrue(x?.GetType() == typeof(string));
        }

        /// <summary>
        /// scalar test
        /// </summary>
        [TestMethod]
        public void StringScalarIOP()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "[dbo].SeaCostumers";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@sea","Fun");
            cmd.Parameters.Add(new SalouParameter(){ DbType= System.Data.DbType.Int32, Direction= System.Data.ParameterDirection.Output, ParameterName = "@product_count" });
            cmd.Parameters.Add(new SalouParameter() { DbType = System.Data.DbType.Int32, Direction = System.Data.ParameterDirection.ReturnValue, ParameterName = "@return_value" });
            var x =cmd.ExecuteScalar();
            Assert.AreEqual(2, cmd.Parameters[1].Value);
            Assert.AreEqual(111, cmd.Parameters[2].Value);
            Assert.AreEqual(3, x);
        }

        /// <summary>
        /// scalar test
        /// </summary>
        [TestMethod]
        public void StringScalarDec()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "SELECT top(1) item_price from OrderItems";
            var x = cmd.ExecuteScalar();
            Assert.IsTrue(x?.GetType() == typeof(decimal));
        }
    }
}

