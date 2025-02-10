using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// DbParameterCollection extension implements AddWithValue
    /// </summary>
    public static class DbParameterCollectionExtensions
    {
        /// <summary>
        /// Adds a parameter with the specified name and value to the collection.
        /// </summary>
        /// <param name="collection">DbParameterCollection</param>
        /// <param name="name">param name</param>
        /// <param name="value">param value</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AddWithValue(this DbParameterCollection collection, string name, object value)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            collection.Add(new SalouParameter { ParameterName = name, Value = value });
        }
    }
    /// <summary>
    /// Implements a DbParameterCollection over the Salou Websocket Service
    /// </summary>
    internal class SalouParameterCollection : DbParameterCollection
    {
        /// <summary>
        /// Parameters
        /// </summary>
        private readonly List<SalouParameter> _parameters = new List<SalouParameter>();

        /// <inheritdoc />
        public override int Count => _parameters.Count;
        /// <inheritdoc />
        public override object SyncRoot => ((ICollection)_parameters).SyncRoot;
        /// <inheritdoc />
        public override int Add(object value)
        {
            if (value is SalouParameter parameter)
            {
                _parameters.Add(parameter);
                return _parameters.Count - 1;
            }
            throw new ArgumentException("Value must be a SalouParameter.");
        }
        /// <inheritdoc />
        public override void AddRange(Array values)
        {
            var lst = values.OfType<SalouParameter>().ToArray();

            if (lst.Length != values.Length)
                throw new ArgumentException("Value must be a SalouParameter.");

            foreach (var value in lst)
            {
                Add(value);
            }
        }
        /// <inheritdoc />
        public override void Clear() => _parameters.Clear();
        /// <inheritdoc />
        public override bool Contains(object value)
        {
            if (value is SalouParameter parameter)
            {
                return _parameters.Contains(parameter);
            }
            else
                throw new ArgumentException("Value must be a SalouParameter.");
        }

        /// <inheritdoc />
        public override bool Contains(string value) => _parameters.Exists(p => p.ParameterName == value);
        /// <inheritdoc />
        public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);
        /// <inheritdoc />
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        /// <inheritdoc />
        protected override DbParameter GetParameter(int index) => _parameters[index];
        /// <inheritdoc />
        protected override DbParameter GetParameter(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                return _parameters[index];
            }
            throw new IndexOutOfRangeException("Parameter not found.");
        }
        /// <inheritdoc />
        public override int IndexOf(object value)
        {
            if (value is SalouParameter parameter)
            {
                return _parameters.IndexOf(parameter);
            }
            else
                throw new ArgumentException("Value must be a SalouParameter.");
        }
        /// <inheritdoc />
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);
        /// <inheritdoc />
        public override void Insert(int index, object value)
        {
            if (value is SalouParameter parameter)
            {
                _parameters.Insert(index, parameter);
            }
            else
                throw new ArgumentException("Value must be a SalouParameter.");
        }
        /// <inheritdoc />
        public override void Remove(object value)
        {
            if (value is SalouParameter parameter)
            {
                _parameters.Remove(parameter);
            }
            else
                throw new ArgumentException("Value must be a SalouParameter.");
        }
        /// <inheritdoc />
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        /// <inheritdoc />
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }
        /// <inheritdoc />
        protected override void SetParameter(int index, DbParameter value)
        {
            if (value is SalouParameter parameter)
            {
                _parameters[index] = parameter;
            }
            else
                throw new ArgumentException("Value must be a SalouParameter.");
        }
        /// <inheritdoc />
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            if (value is SalouParameter parameter)
            {
                var index = IndexOf(parameterName);
                if (index >= 0)
                {
                    _parameters[index] = parameter;
                }
                else
                    throw new IndexOutOfRangeException("Parameter not found.");
            }
            else
                throw new ArgumentException("Value must be a SalouParameter.");
        }
    }
}