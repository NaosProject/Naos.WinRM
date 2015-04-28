// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteCommand.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Contract
{
    /// <summary>
    /// Representation of a command to run on a remote machine.
    /// </summary>
    public class RemoteCommand
    {
        /// <summary>
        /// Gets or sets the name of the computer to run the command on.
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets or sets the command text to execute.
        /// </summary>
        public string CommandText { get; set; }
    }
}
