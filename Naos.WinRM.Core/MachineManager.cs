// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MachineManager.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Security;

    using Naos.WinRM.Contract;

    using ScriptBlock = Naos.WinRM.Contract.ScriptBlock;

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
            var restartScriptBlock = new ScriptBlock() { ScriptText = "{ Restart-Computer" + forceAddIn + " }" };
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
                    SendFileSessioned(filePathOnTargetMachine, fileContents, appended, runspace, sessionObject);
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
                            SendFileSessioned(filePathOnTargetMachine, nibble.ToArray(), true, runspace, sessionObject);
                            nibble.Clear();
                        }
                    }

                    // flush the "buffer"...
                    if (nibble.Any())
                    {
                        SendFileSessioned(filePathOnTargetMachine, nibble.ToArray(), true, runspace, sessionObject);
                    }
                }

                EndSession(sessionObject, runspace);

                runspace.Close();
            }
        }

        private static void SendFileSessioned(
            string filePathOnTargetMachine,
            byte[] fileContents,
            bool appended,
            Runspace runspace,
            object sessionObject)
        {
            var commandName = appended ? "Add-Content" : "Set-Content";
            var sendFileScriptBlock = new ScriptBlock() 
            { 
                ScriptText = @"
	                { 
		                param($filePath, $fileContents)

		                $parentDir = Split-Path $filePath
		                if (-not (Test-Path $parentDir))
		                {
			                md $parentDir | Out-Null
		                }

		                " + commandName + @" -Path $filePath -Encoding Byte -Value $fileContents
	                }" 
            };

            var arguments = new object[] { filePathOnTargetMachine, fileContents };

            var notUsedResults = RunScriptSessioned(sendFileScriptBlock, arguments, runspace, sessionObject);
        }

        /// <inheritdoc />
        public ICollection<object> RunScript(ScriptBlock scriptBlock, ICollection<object> scriptBlockParameters = null)
        {
            List<object> ret = null;

            using (var runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                var sessionObject = this.BeginSession(runspace);

                ret = RunScriptSessioned(scriptBlock, scriptBlockParameters, runspace, sessionObject);

                EndSession(sessionObject, runspace);

                runspace.Close();
            }

            return ret;
        }

        private static void EndSession(object sessionObject, Runspace runspace)
        {
            var removeSessionCommand = new Command("Remove-PSSession");
            removeSessionCommand.Parameters.Add("Session", sessionObject);
            var unneededOutput = RunCommand(runspace, removeSessionCommand);
        }

        private object BeginSession(Runspace runspace)
        {
            var credentials = new Credentials() { Username = this.username, Password = this.password };
            var powershellCredentials = new PSCredential(credentials.Username, credentials.Password);

            var sessionOptionsCommand = new Command("New-PSSessionOption");
            sessionOptionsCommand.Parameters.Add("OperationTimeout", 0);
            sessionOptionsCommand.Parameters.Add("IdleTimeout", TimeSpan.FromMinutes(20).TotalMilliseconds);
            var sessionOptionsObject = RunCommand(runspace, sessionOptionsCommand).Single().BaseObject;

            var sessionCommand = new Command("New-PSSession");
            sessionCommand.Parameters.Add("ComputerName", this.privateIpAddress);
            sessionCommand.Parameters.Add("Credential", powershellCredentials);
            sessionCommand.Parameters.Add("SessionOption", sessionOptionsObject);
            var sessionObject = RunCommand(runspace, sessionCommand).Single().BaseObject;
            return sessionObject;
        }

        private static List<object> RunScriptSessioned(
            ScriptBlock scriptBlock,
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

                var fullScript = "$sc = " + scriptBlock.ScriptText + Environment.NewLine + "Invoke-Command -Session $"
                                 + variableNameSession + argsAddIn + " -ScriptBlock $sc";
                powershell.AddScript(fullScript);

                var output = powershell.Invoke();

                if (powershell.Streams.Error.Count > 0)
                {
                    var errorString = powershell.Streams.Error.Select(_ => _ + Environment.NewLine);
                    throw new RemoteExecutionException(
                        "Failed to run script (" + scriptBlock.ScriptText + ") got back: " + errorString);
                }

                var ret = output.Select(_ => _.BaseObject).ToList();
                return ret;
            }
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