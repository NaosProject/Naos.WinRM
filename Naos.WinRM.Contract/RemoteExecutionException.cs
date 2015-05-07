// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteExecutionException.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Contract
{
    using System;

    /// <summary>
    /// Custom exception for when things go wrong running remote commands.
    /// </summary>
    public class RemoteExecutionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteExecutionException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public RemoteExecutionException(string message)
            : base(message)
        {
        }
    }
}
