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
        /// Initialize the connection
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            _con = new SalouConnection(new Uri("ws://localhost:5249/ws"), "Test", 120, null);
        }

        /// <summary>
        /// Cleanup the connection
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            _con?.Dispose();
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
