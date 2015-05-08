// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScriptBlockExtensionMethods.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Runtime.CompilerServices;
    using System.Text;

    using Naos.WinRM.Contract;

    using NaosCredentials = Naos.WinRM.Contract.Credentials;
    using NaosScriptBlock = Naos.WinRM.Contract.ScriptBlock;

    /// <summary>
    /// Methods added onto the RemoteCommand object.
    /// </summary>
    public static class ScriptBlockExtensionMethods
    {
        /// <summary>
        /// Executes the specified command on the remote machine using provided credentials.
        /// </summary>
        /// <param name="scriptBlock">Command to execute.</param>
        /// <param name="ipAddress">IP Address of the remote computer to execute the script block on.</param>
        /// <param name="credentials">Credentials to execute the call using.</param>
        /// <param name="arguments">Inputs to be provided to script (like parameters to a script block that is specified).</param>
        /// <returns>String of new line delimited lines of output from remote command.</returns>
        public static string Execute(this NaosScriptBlock scriptBlock, string ipAddress, NaosCredentials credentials, ICollection<object> arguments = null)
        {
            var ret = new StringBuilder();

            var powershellCredentials = new PSCredential(credentials.Username, credentials.Password);

            using (var runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                var sessionOptionsCommand = new Command("New-PSSessionOption");
                sessionOptionsCommand.Parameters.Add("OperationTimeout", 0);
                sessionOptionsCommand.Parameters.Add("IdleTimeout", TimeSpan.FromMinutes(20).TotalMilliseconds);
                var sessionOptionsObject = RunCommand(runspace, sessionOptionsCommand).Single().BaseObject;

                var sessionCommand = new Command("New-PSSession");
                sessionCommand.Parameters.Add("ComputerName", ipAddress);
                sessionCommand.Parameters.Add("Credential", powershellCredentials);
                sessionCommand.Parameters.Add("SessionOption", sessionOptionsObject);
                var sessionObject = RunCommand(runspace, sessionCommand).Single().BaseObject;

                using (var powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    var variableNameArgs = "scriptBlockArgs";
                    var variableNameSession = "invokeCommandSession";

                    powershell.Runspace.SessionStateProxy.SetVariable(variableNameSession, sessionObject);

                    var argsAddIn = string.Empty;
                    if (arguments != null && arguments.Count > 0)
                    {
                        powershell.Runspace.SessionStateProxy.SetVariable(variableNameArgs, arguments.ToArray());
                        argsAddIn = " -ArgumentList $" + variableNameArgs;
                    }

                    var fullScript = "$sc = " + scriptBlock.ScriptText + Environment.NewLine
                                     + "Invoke-Command -Session $" + variableNameSession + argsAddIn
                                     + " -ScriptBlock $sc";                    
                    powershell.AddScript(fullScript);

                    var output = powershell.Invoke();

                    if (powershell.Streams.Error.Count > 0)
                    {
                        var errorString = powershell.Streams.Error.Select(_ => _ + Environment.NewLine);
                        throw new RemoteExecutionException(
                            "Failed to run script (" + scriptBlock.ScriptText + ") got back: " + errorString);
                    }

                    foreach (var o in output)
                    {
                        ret.AppendLine(o.ToString());
                    }
                }

                var removeSessionCommand = new Command("Remove-PSSession");
                removeSessionCommand.Parameters.Add("Session", sessionObject);
                var unneededOutput = RunCommand(runspace, removeSessionCommand);

                runspace.Close();
            }

            return ret.ToString();
        }

        private static List<PSObject> RunCommand(Runspace runspace, Command arbitraryCommand)
        {
            using (var powershell = PowerShell.Create())
            {
                powershell.Runspace = runspace;

                powershell.Commands.AddCommand(arbitraryCommand);

                var output = powershell.Invoke();

                var ret = output.ToList();
                return ret;
            }
        }
    }
}
