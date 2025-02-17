using SalouWS4Sql.Client;

namespace SalouTest
{
#nullable enable
    /// <summary>
    /// Test Open Close Transaction
    /// </summary>
    [TestClass]
    public sealed class OpenCloseTrans
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
            _con = _base.Init(true);
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
        /// Test Open Close
        /// </summary>
        [TestMethod]
        public void OpenClose()
        {
            Assert.IsNotNull(_con); 

            _con.Open();
            Assert.IsTrue(_con.State== System.Data.ConnectionState.Open);

            _con.Close();
            Assert.IsTrue(_con.State == System.Data.ConnectionState.Closed);
        }

       
    }
}
