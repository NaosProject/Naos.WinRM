// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IManageMachines.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Contract
{
    using System.Collections.Generic;

    /// <summary>
    /// Manages various remote tasks on a machine using the WinRM protocol.
    /// </summary>
    public interface IManageMachines
    {
        /// <summary>
        /// Executes a user initiated reboot.
        /// </summary>
        /// <param name="force">Can override default behavior of a forceful reboot (kick users off).</param>
        void Reboot(bool force = true);

        /// <summary>
        /// Sends a file to the remote machine at the provided file path on that target computer.
        /// </summary>
        /// <param name="filePathOnTargetMachine">File path to write the contents to on the remote machine.</param>
        /// <param name="fileContents">Payload to write to the file.</param>
        /// <param name="appended">Optionally writes the bytes in appended mode or not (default is NOT).</param>
        void SendFile(string filePathOnTargetMachine, byte[] fileContents, bool appended = false);

        /// <summary>
        /// Runs an arbitrary script block.
        /// </summary>
        /// <param name="scriptBlock">Script block.</param>
        /// <param name="scriptBlockParameters">Parameters to be passed to the script block.</param>
        /// <returns>Collection of objects that were the output from the script block.</returns>
        ICollection<object> RunScript(ScriptBlock scriptBlock, ICollection<object> scriptBlockParameters);
    }
}