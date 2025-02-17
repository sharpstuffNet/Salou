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
        List<T> _list = new List<T>();

        /// <summary>
        /// Get the enumerator
        /// </summary>
        /// <returns>enumerator</returns>
        public IEnumerator<T> GetEnumerator()
        {
            T[] arr;
            lock (_list)
                arr = _list.ToArray();
            return ((IEnumerable<T>)arr).GetEnumerator();
        }

        /// <summary>
        /// Get an element by index
        /// </summary>
        /// <param name="index">index</param>
        /// <returns>element</returns>
        public T this[int index]
        {
            get
            {
                lock (_list)
                    return _list[index];
            }
        }

        /// <summary>
        /// remove an element
        /// </summary>
        /// <param name="ws">element</param>
        internal void Remove(T ws)
        {
            lock (_list)
                _list.Remove(ws);
        }
        /// <summary>
        /// Add an element to the list
        /// </summary>
        /// <param name="ws">element</param>
        internal void Add(T ws)
        {
            lock (_list)
                _list.Add(ws);
        }

        /// <summary>
        /// Count the elements in the list
        /// </summary>
        /// <returns>Count</returns>
        internal int Count()
        {
            lock (_list)
                return _list.Count();
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
