// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Naos.WinRM.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Security;

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
        ICollection<dynamic> RunScript(string scriptBlock, ICollection<object> scriptBlockParameters);
    }

    /// <inheritdoc />
    public class MachineManager : IManageMachines
    {
        private const long FileChunkSizeThresholdByteCount = 150000;

        private const long FileChunkSizePerSend = 100000;

        private readonly string privateIpAddress;

        private readonly string username;

        private readonly SecureString password;

        /// <summary>
        /// Initializes a new instance of the <see cref="MachineManager"/> class.
        /// </summary>
        /// <param name="privateIpAddress">Private IP address of machine to interact with.</param>
        /// <param name="username">Username to use to connect.</param>
        /// <param name="password">Password to use to connect.</param>
        public MachineManager(string privateIpAddress, string username, SecureString password)
        {
            this.privateIpAddress = privateIpAddress;
            this.username = username;
            this.password = password;
        }

        /// <summary>
        /// Locally updates the trusted hosts to have the ipAddress provided.
        /// </summary>
        /// <param name="ipAddress">IP Address to add to local trusted hosts.</param>
        public static void AddIpAddressToLocalTrusedHosts(string ipAddress)
        {
            var command = "winrm s winrm/config/client \"@{TrustedHosts=`\"$IpAddress`\"}\"";
            var info = new ProcessStartInfo("powershell.exe", "-command \"" + command + "\"") { CreateNoWindow = false };
            var process = Process.Start(info);

            while (!process.HasExited)
            {
                process.Refresh();
            }

            if (process.ExitCode != 0)
            {
                throw new ApplicationException(
                    "Exit code should have been 0 running command (" + command + ") and was: " + process.ExitCode);
            }
        }

        /// <summary>
        /// Converts a basic string to a secure string.
        /// </summary>
        /// <param name="inputAsString">String to convert.</param>
        /// <returns>SecureString version of string.</returns>
        public static SecureString ConvertStringToSecureString(string inputAsString)
        {
            var ret = new SecureString();
            foreach (char c in inputAsString)
            {
                ret.AppendChar(c);
            }

            return ret;
        }

        /// <inheritdoc />
        public void Reboot(bool force = true)
        {
            var forceAddIn = force ? " -Force" : string.Empty;
            var restartScriptBlock = "{ Restart-Computer" + forceAddIn + " }";
            this.RunScript(restartScriptBlock);
        }

        /// <inheritdoc />
        public void SendFile(string filePathOnTargetMachine, byte[] fileContents, bool appended = false)
        {
            using (var runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                var sessionObject = this.BeginSession(runspace);

                if (fileContents.Length <= FileChunkSizeThresholdByteCount)
                {
                    this.SendFileSessioned(filePathOnTargetMachine, fileContents, appended, runspace, sessionObject);
                }
                else
                {
                    // deconstruct and send pieces as appended...
                    var nibble = new List<byte>();
                    foreach (byte currentByte in fileContents)
                    {
                        if (nibble.Count < FileChunkSizePerSend)
                        {
                            nibble.Add(currentByte);
                        }
                        else
                        {
                            this.SendFileSessioned(filePathOnTargetMachine, nibble.ToArray(), true, runspace, sessionObject);
                            nibble.Clear();
                        }
                    }

                    // flush the "buffer"...
                    if (nibble.Any())
                    {
                        this.SendFileSessioned(filePathOnTargetMachine, nibble.ToArray(), true, runspace, sessionObject);
                    }
                }

                this.EndSession(sessionObject, runspace);

                runspace.Close();
            }
        }

        private void SendFileSessioned(
            string filePathOnTargetMachine,
            byte[] fileContents,
            bool appended,
            Runspace runspace,
            object sessionObject)
        {
            var commandName = appended ? "Add-Content" : "Set-Content";
            var sendFileScriptBlock = @"
	                { 
		                param($filePath, $fileContents)

		                $parentDir = Split-Path $filePath
		                if (-not (Test-Path $parentDir))
		                {
			                md $parentDir | Out-Null
		                }

		                " + commandName + @" -Path $filePath -Encoding Byte -Value $fileContents
	                }";

            var arguments = new object[] { filePathOnTargetMachine, fileContents };

            var notUsedResults = this.RunScriptSessioned(sendFileScriptBlock, arguments, runspace, sessionObject);
        }

        /// <inheritdoc />
        public ICollection<dynamic> RunScript(string scriptBlock, ICollection<object> scriptBlockParameters = null)
        {
            List<object> ret = null;

            using (var runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                var sessionObject = this.BeginSession(runspace);

                ret = this.RunScriptSessioned(scriptBlock, scriptBlockParameters, runspace, sessionObject);

                this.EndSession(sessionObject, runspace);

                runspace.Close();
            }

            return ret;
        }

        private void EndSession(object sessionObject, Runspace runspace)
        {
            var removeSessionCommand = new Command("Remove-PSSession");
            removeSessionCommand.Parameters.Add("Session", sessionObject);
            var unneededOutput = this.RunCommand(runspace, removeSessionCommand);
        }

        private object BeginSession(Runspace runspace)
        {
            var powershellCredentials = new PSCredential(this.username, this.password);

            var sessionOptionsCommand = new Command("New-PSSessionOption");
            sessionOptionsCommand.Parameters.Add("OperationTimeout", 0);
            sessionOptionsCommand.Parameters.Add("IdleTimeout", TimeSpan.FromMinutes(20).TotalMilliseconds);
            var sessionOptionsObject = this.RunCommand(runspace, sessionOptionsCommand).Single().BaseObject;

            var sessionCommand = new Command("New-PSSession");
            sessionCommand.Parameters.Add("ComputerName", this.privateIpAddress);
            sessionCommand.Parameters.Add("Credential", powershellCredentials);
            sessionCommand.Parameters.Add("SessionOption", sessionOptionsObject);
            var sessionObject = this.RunCommand(runspace, sessionCommand).Single().BaseObject;
            return sessionObject;
        }

        private List<dynamic> RunScriptSessioned(
            string scriptBlock,
            ICollection<object> scriptBlockParameters,
            Runspace runspace,
            object sessionObject)
        {
            using (var powershell = PowerShell.Create())
            {
                powershell.Runspace = runspace;
                var variableNameArgs = "scriptBlockArgs";
                var variableNameSession = "invokeCommandSession";

                powershell.Runspace.SessionStateProxy.SetVariable(variableNameSession, sessionObject);

                var argsAddIn = string.Empty;
                if (scriptBlockParameters != null && scriptBlockParameters.Count > 0)
                {
                    powershell.Runspace.SessionStateProxy.SetVariable(variableNameArgs, scriptBlockParameters.ToArray());
                    argsAddIn = " -ArgumentList $" + variableNameArgs;
                }

                var fullScript = "$sc = " + scriptBlock + Environment.NewLine + "Invoke-Command -Session $"
                                 + variableNameSession + argsAddIn + " -ScriptBlock $sc";
                powershell.AddScript(fullScript);

                var output = powershell.Invoke();

                this.ThrowOnError(powershell, scriptBlock);

                var ret = output.Cast<dynamic>().ToList();
                return ret;
            }
        }

        private List<PSObject> RunCommand(Runspace runspace, Command arbitraryCommand)
        {
            using (var powershell = PowerShell.Create())
            {
                powershell.Runspace = runspace;

                powershell.Commands.AddCommand(arbitraryCommand);

                var output = powershell.Invoke();

                this.ThrowOnError(powershell, arbitraryCommand.CommandText);

                var ret = output.ToList();
                return ret;
            }
        }

        private void ThrowOnError(PowerShell powershell, string attemptedScriptBlock)
        {
            if (powershell.Streams.Error.Count > 0)
            {
                var errorString = powershell.Streams.Error.Select(_ => _.ErrorDetails.Message + Environment.NewLine);
                throw new RemoteExecutionException(
                    "Failed to run script (" + attemptedScriptBlock + ") on " + this.privateIpAddress + " got errors: "
                    + errorString);
            }
        }
    }
}