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
    public sealed class ErrorTest
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
        public void CallNonExistingSP()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "[dbo].ImNotThere";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            try
            {
                var x = cmd.ExecuteScalar();
                Assert.Fail("Should have thrown an exception");
            }
            catch (SalouServerException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith("Could not find stored procedure"));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Wrong exception {ex}");
            }
        }

        [TestMethod]
        public void CallNonExistingTable()
        {
            Assert.IsNotNull(_con);
            var cmd = _con.CreateCommand();
            cmd.CommandText = "SELECT * FROM [dbo].ImNotThere";
            cmd.CommandType = System.Data.CommandType.Text;
            try
            {
                var rdr = cmd.ExecuteReader(System.Data.CommandBehavior.Default);
                if(rdr.Read())
                    Assert.Fail("Should not have read anything");
                Assert.Fail("Should have thrown an exception");
            }
            catch (SalouServerException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith("Invalid object name"));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Wrong exception {ex}");
            }
        }

        [TestMethod]
        public void ReadEmpty()
        {
            var cmd = (SalouCommand)_con.CreateCommand();
            cmd.Salou_ReaderPageSize = 2;
            cmd.CommandText = "select * from dbo.Products where 1=0";

            try
            {
                var rdr = cmd.ExecuteReader(System.Data.CommandBehavior.Default);
                if (rdr.Read())
                    Assert.Fail("Should not have read anything");
            }
            catch (SalouServerException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith("Invalid object name"));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Wrong exception {ex}");
            }
        }
    }
}
