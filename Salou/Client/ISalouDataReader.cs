using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql.Client
{
#nullable enable
    /// <summary>
    /// Extendsions to the IDataReader interface to provide additional features
    /// </summary>
    public interface ISalouDataReader: IDataReader
    {
        /// <summary>
        /// Gets the dictionary of column names, mapping each name to the Index. The
        /// dictionary can be null.
        /// </summary>
        public Dictionary<string, int>? ColNames { get; }
        /// <summary>
        /// Gets the dictionary of column names in lower invariant case, mapping each name to the Index. The
        /// dictionary can be null.
        /// </summary>
        public Dictionary<string, int>? ColNamesLowerInvariant { get; }
        /// <summary>
        /// Represents the number of items to be displayed on a single page. It is commonly used for pagination in data
        /// display.
        /// </summary>
        public int PageSize { get; set; }
    }
}
