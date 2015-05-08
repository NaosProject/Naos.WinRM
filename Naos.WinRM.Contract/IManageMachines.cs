// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IManageMachines.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Contract
{
    /// <summary>
    /// Manages various remote tasks on a machine using the WinRM protocol.
    /// </summary>
    public interface IManageMachines
    {
        /// <summary>
        /// Reboots the machine in question.
        /// </summary>
        /// <param name="force">Can override default behavior of a forceful reboot (kick users off).</param>
        void Reboot(bool force = true);

        /// <summary>
        /// Sends a file to the remote machine at the provided file path on that target computer.
        /// </summary>
        /// <param name="filePathOnTargetMachine">File path to write the contents to on the remote machine.</param>
        /// <param name="fileContents">Payload to write to the file.</param>
        void SendFile(string filePathOnTargetMachine, byte[] fileContents);
    }
}