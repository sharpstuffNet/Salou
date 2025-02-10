using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql
{
    /// <summary>
    /// Use Schema enumeration for the DataReader
    /// </summary>
    public enum UseSchema
    {
        /// <summary>
        /// get names only
        /// </summary>
        NamesOnly,
        /// <summary>
        /// get full schema
        /// </summary>
        Full,
        /// <summary>
        /// none .... mor performance but only index access for columns
        /// </summary>
        None
    }
}
