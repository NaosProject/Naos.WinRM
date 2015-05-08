// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Credentials.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Contract
{
    using System.Security;

    /// <summary>
    /// Credentials to be used for execution.
    /// </summary>
    public class Credentials
    {
        /// <summary>
        /// Gets or sets the user name to login with.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password to login with.
        /// </summary>
        public SecureString Password { get; set; }
    }
}
