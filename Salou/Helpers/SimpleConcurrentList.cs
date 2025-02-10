using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SalouWS4Sql.Helpers
{
    /// <summary>
    /// A simple Concurrent List for performance
    /// </summary>
    /// <typeparam name="T">Data to be stored</typeparam>
    public class SimpleConcurrentList<T> : IEnumerable<T> where T : class
    {
        /// <summary>
        /// Data in a List
        /// </summary>
        List<T> _dict = new List<T>();
        /// <summary>
        /// Get the enumerator
        /// </summary>
        /// <returns>enumerator</returns>
        public IEnumerator<T> GetEnumerator()
        {
            T[] arr;
            lock (_dict)
                arr = _dict.ToArray();
            return ((IEnumerable<T>)arr).GetEnumerator();
        }
        /// <summary>
        /// remove an element
        /// </summary>
        /// <param name="ws">element</param>
        internal void Remove(T ws)
        {
            lock (_dict)
                _dict.Remove(ws);
        }
        /// <summary>
        /// Add an element to the list
        /// </summary>
        /// <param name="ws">element</param>
        internal void Add(T ws)
        {
            lock (_dict)
                _dict.Add(ws);
        }
        /// <summary>
        /// Get an element from the list
        /// </summary>
        /// <returns>Enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
