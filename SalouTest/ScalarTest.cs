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
        /// Test Base Class
        /// </summary>
        TestBase _base = new TestBase();

        /// <summary>
        /// Initialize the connection
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            _con = _base.Init();
        }

        /// <summary>
        /// Cleanup the connection
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            _base.Dispose();
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
        public void StringScalarDBNull()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "[dbo].SeaCostumers";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            //cmd.Parameters.AddWithValue("@sea", DBNull.Value);
            cmd.Parameters.Add(new SalouParameter() { DbType = System.Data.DbType.String, ParameterName = "@sea", Value=DBNull.Value });
            cmd.Parameters.Add(new SalouParameter() { DbType = System.Data.DbType.Int32, Direction = System.Data.ParameterDirection.Output, ParameterName = "@product_count" });
            cmd.Parameters.Add(new SalouParameter() { DbType = System.Data.DbType.Int32, Direction = System.Data.ParameterDirection.ReturnValue, ParameterName = "@return_value" });
            var x = cmd.ExecuteScalar();
            Assert.AreEqual(0, cmd.Parameters[1].Value);
            Assert.AreEqual(111, cmd.Parameters[2].Value);
            Assert.IsNull( x);
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

