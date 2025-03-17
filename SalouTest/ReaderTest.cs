using SalouWS4Sql;
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
    /// Test Reader
    /// </summary>
    [TestClass]
    public sealed class ReaderTest
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
        /// Test Reader
        /// </summary>
        [TestMethod]
        public void ReaderIOP()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "[dbo].SeaCostumers";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@sea", "Fun");
            cmd.Parameters.Add(new SalouParameter() { DbType = System.Data.DbType.Int32, Direction = System.Data.ParameterDirection.Output, ParameterName = "@product_count" });
            cmd.Parameters.Add(new SalouParameter() { DbType = System.Data.DbType.Int32, Direction = System.Data.ParameterDirection.ReturnValue, ParameterName = "@return_value" });
            var rdr = cmd.ExecuteReader();
            Assert.IsNotNull(rdr);
            Assert.IsTrue(rdr.HasRows);
            int i = 0;
            while(rdr.Read())
            {
                i++;
                Assert.AreEqual(9, rdr.FieldCount);
                //Assert.AreEqual(2, rdr.GetInt32(1));
                //Assert.AreEqual(111, rdr.GetInt32(2));
            }
            Assert.AreEqual(2, i);
            //Assert.AreEqual(2, cmd.Parameters[1].Value);
            //Assert.AreEqual(111, cmd.Parameters[2].Value);
        }

        /// <summary>
        /// Test Reader incl Pageing
        /// </summary>
        [TestMethod]
        public void EasyReader()
        {
            Assert.IsNotNull(_con);
            var cmd = (SalouCommand)_con.CreateCommand();
            cmd.Salou_ReaderPageSize = 2;
            cmd.CommandText = "select * from dbo.Products";
           
            var rdr = cmd.ExecuteReader();
            Assert.IsNotNull(rdr);
            Assert.IsNotNull(rdr.GetSchemaTable());
            Assert.IsTrue(rdr.GetName(1) == "vend_id");
            Assert.IsTrue(rdr.HasRows);
            int i = 0;
            string s = "";
            
            while (rdr.Read())
            {
                i++;
                Assert.AreEqual(5, rdr.FieldCount);
                s = rdr.GetString(0);
            }
            Assert.AreEqual(9, i);
        }

        /// <summary>
        /// Test Reader incl Pageing
        /// </summary>
        [TestMethod]
        public void DoubleColumns()
        {
            Assert.IsNotNull(_con);
            var cmd = (SalouCommand)_con.CreateCommand();
            cmd.Salou_ReaderPageSize = 2;
            cmd.CommandText = "select *,prod_name from dbo.Products";

            var rdr = cmd.ExecuteReader();
            Assert.IsNotNull(rdr);
            Assert.IsNotNull(rdr.GetSchemaTable());
            Assert.IsTrue(rdr.GetName(1) == "vend_id");
            Assert.IsTrue(rdr.HasRows);
            int i = 0;
            string? s = "";

            while (rdr.Read())
            {
                i++;
                Assert.AreEqual(6, rdr.FieldCount);
                s = rdr["prod_name"].ToString();
                Assert.IsNotNull(s);
            }
            Assert.AreEqual(9, i);
        }

        /// <summary>
        /// Test Reader multi resut sets
        /// </summary>
        [TestMethod]
        public void ReaderMultiple()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "select * from Products;\r\nselect * from Vendors;";
           
            var rdr = cmd.ExecuteReader();
            Assert.IsNotNull(rdr);
            Assert.IsTrue(rdr.HasRows);
            int i = 0;
            string s = "";
            while (rdr.Read())
            {
                i++;
                Assert.AreEqual(5, rdr.FieldCount);
                s = rdr.GetString(0);
            }
            Assert.AreEqual(s, "RYL02     ");
            Assert.AreEqual(9, i);

            rdr.NextResult();

            i = 0;
            s = "";
            while (rdr.Read())
            {
                i++;
                Assert.AreEqual(7, rdr.FieldCount);
                s = rdr.GetString(0);
            }
            Assert.AreEqual(s.Trim(), "JTS01");
            Assert.AreEqual(6, i);
        }

        /// <summary>
        /// Test Reader no schema
        /// </summary>
        [TestMethod]
        public void ReaderNoSchema()
        {
            Assert.IsNotNull(_con);
            var cmd = (SalouCommand)_con.CreateCommand();
            cmd.ReaderUseSchema = UseSchema.None;
            cmd.CommandText = "select * from dbo.Products";

            var rdr = cmd.ExecuteReader();
            try
            {
                rdr.GetName(1);
                Assert.Fail();
            }
            catch (Exception)
            {
            }
            Assert.IsNotNull(rdr);
            Assert.IsTrue(rdr.HasRows);
            int i = 0;
            string s = "";
            while (rdr.Read())
            {
                i++;
                Assert.AreEqual(5, rdr.FieldCount);
                s = rdr.GetString(0);
            }
            Assert.AreEqual(9, i);
        }
        /// <summary>
        /// Test Reader schema names only
        /// </summary>
        [TestMethod]
        public void ReaderSchemaNamesOnly()
        {
            Assert.IsNotNull(_con);
            var cmd = (SalouCommand)_con.CreateCommand();
            cmd.ReaderUseSchema = UseSchema.NamesOnly;
            cmd.CommandText = "select * from dbo.Products";

            var rdr = cmd.ExecuteReader();
            Assert.IsTrue(rdr.GetName(1) == "vend_id");
            Assert.IsNotNull(rdr);
            Assert.IsTrue(rdr.HasRows);
            int i = 0;
            string s = "";
            while (rdr.Read())
            {
                i++;
                Assert.AreEqual(5, rdr.FieldCount);
                s = rdr.GetString(0);
            }
            Assert.AreEqual(9, i);
        }

        /// <summary>
        /// Test Reader no result
        /// </summary>
        [TestMethod]
        public void ReaderNoResult()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();

            cmd.CommandText = "select * from dbo.Products where 1=0";

            var rdr = cmd.ExecuteReader();
            try
            {
                rdr.GetName(1);
            }
            catch (Exception)
            {
            }
            Assert.IsNotNull(rdr);
            Assert.IsFalse(rdr.HasRows);
            
        }

        /// <summary>
        /// Test Reader no result
        /// </summary>
        [TestMethod]
        public void ReaderNoResultParam()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();

            cmd.CommandText = "select * from dbo.Products where 1=0";
            cmd.Parameters.AddWithValue("@sea", "Fun");
            var rdr = cmd.ExecuteReader();
            try
            {
                rdr.GetName(1);
            }
            catch (Exception)
            {
            }
            Assert.IsNotNull(rdr);
            Assert.IsFalse(rdr.HasRows);

        }
    }
}

