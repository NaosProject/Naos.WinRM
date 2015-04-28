// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteCommandExtensionMethods.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Core
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Text;

    using Naos.WinRM.Contract;

    /// <summary>
    /// Methods added onto the RemoteCommand object.
    /// </summary>
    public static class RemoteCommandExtensionMethods
    {
        /// <summary>
        /// Executes the specified command on the remote machine using provided credentials.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="credentials">Credentials to execute the call using.</param>
        /// <returns>String of new line delimited lines of output from remote command.</returns>
        public static string Execute(this RemoteCommand command, Credentials credentials)
        {
            var ret = new StringBuilder();

            var connectionInfo = new WSManConnectionInfo { ComputerName = command.ComputerName };

            using (var runspace = RunspaceFactory.CreateRunspace(connectionInfo))
            {
                runspace.Open();

                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript(command.CommandText);
                    var results = ps.Invoke();

                    foreach (var line in results)
                    {
                        ret.AppendLine(line.ToString());
                    }
                }

                runspace.Close();
            }

            return ret.ToString();
        }
    }
}
