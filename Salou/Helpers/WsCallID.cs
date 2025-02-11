using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalouWS4Sql.Helpers
{
    /// <summary>
    /// WsCallID is a unique ID for a Websocket Call
    /// </summary>
    internal struct WsCallID
    {
        /// <summary>
        /// Next ID
        /// </summary>
        private static int _nextID = 0;

        /// <summary>
        /// this id
        /// </summary>
        private int _value;

        /// <summary>
        /// Create a new WsCallID
        /// </summary>
        public WsCallID()
        {
            _value = Interlocked.Increment(ref _nextID);
        }

        /// <summary>
        /// from int to WsCallID
        /// </summary>
        /// <param name="value">int</param>
        public static implicit operator WsCallID(int value)
        {
            return new WsCallID { _value = value };
        }
        /// <summary>
        /// to int from WsCallID
        /// </summary>
        /// <param name="value">int</param>
        public static implicit operator int(WsCallID value)
        {
            return value._value;
        }
        /// <summary>
        /// to string from WsCallID
        /// </summary>
        /// <returns>string</returns>
        public override string ToString()
        {
            return _value.ToString();
        }
    }
}
