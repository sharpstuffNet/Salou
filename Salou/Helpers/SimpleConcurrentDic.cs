using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SalouWS4Sql.Helpers
{
#nullable enable
    /// <summary>
    /// A simple Concurrent Dictionary for performance
    /// </summary>
    /// <typeparam name="K">key</typeparam>
    /// <typeparam name="V">value</typeparam>
    public class SimpleConcurrentDic<K, V> : IEnumerable<V> where V : class where K : notnull
    {
        /// <summary>
        /// Data
        /// </summary>
        private readonly Dictionary<K, V> _dict = new();
        /// <summary>
        /// Get the enumerator
        /// </summary>
        /// <returns>enumerator</returns>
        public IEnumerator<V> GetEnumerator()
        {
            V[] arr;
            lock (_dict)
                arr = _dict.Values.ToArray();
            return ((IEnumerable<V>)arr).GetEnumerator();
        }
        /// <summary>
        /// remove an element using the key
        /// </summary>
        /// <param name="key">key</param>
        internal void Remove(K key)
        {
            lock (_dict)
                _dict.Remove(key);
        }
        /// <summary>
        /// Add an element to the dictionary
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="val">value</param>
        internal void Add(K key, V val)
        {
            lock (_dict)
                _dict.Add(key, val);
        }
        /// <summary>
        /// Get an element from the dictionary
        /// </summary>
        /// <param name="key">key</param>
        /// <returns>Value</returns>
        internal V? Get(K key)
        {
            lock (_dict)
            {
                if (_dict.TryGetValue(key, out V? val))
                    return val;
                return null;
            }
        }
        /// <summary>
        /// Get the enumerator
        /// </summary>
        /// <returns>IEnumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
