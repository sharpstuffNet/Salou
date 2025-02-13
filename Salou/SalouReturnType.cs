using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalouWS4Sql
{
    /// <summary>
    /// internal serverside SalouReturnType
    /// </summary>
    internal enum SalouReturnType : byte
    {
        /// <summary>
        /// Nothing
        /// </summary>
        Nothing,
        /// <summary>
        /// Integer
        /// </summary>
        Integer,
        /// <summary>
        /// String
        /// </summary>
        String,
        /// <summary>
        /// DBNull
        /// </summary>
        DBNull,
        /// <summary>
        /// NullableDBType
        /// </summary>
        NullableDBType,
        /// <summary>
        /// Bool
        /// </summary>
        Bool,
        /// <summary>
        /// Exception incl string
        /// </summary>
        Exception,
        /// <summary>
        /// CommandParameters incl another SalouReturnType
        /// </summary>
        CommandParameters,
        /// <summary>
        /// ReaderStart
        /// </summary>
        ReaderStart,
        /// <summary>
        /// ReaderContinue
        /// </summary>
        ReaderContinue,
        /// <summary>
        /// Long
        /// </summary>
        Long,
        /// <summary>
        /// TwoInt32
        /// </summary>
        TwoInt32,
    }
}
