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
    using System.Security;

    using Naos.WinRM.Contract;

    /// <inheritdoc />
    public class MachineManager : IManageMachines
    {
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
            this.RunRemoteScriptBlock("{ Restart-Computer" + forceAddIn + " }");
        }

        /// <inheritdoc />
        public void SendFile(string filePathOnTargetMachine, byte[] fileContents)
        {
            var sendFileScriptBlock = @"
	                { 
		                param($filePath, $fileContents)

		                $parentDir = Split-Path $filePath
		                if (-not (Test-Path $parentDir))
		                {
			                md $parentDir | Out-Null
		                }

		                Set-Content -Path $filePath -Encoding Byte -Value $fileContents
	                }";

            var arguments = new object[] { filePathOnTargetMachine, fileContents };
            var results = this.RunRemoteScriptBlock(sendFileScriptBlock, arguments);

            var hasresults = results == string.Empty;
        }

        private string RunRemoteScriptBlock(string scriptBlockText, ICollection<object> arguments = null)
        {
            var remoteCommand = new ScriptBlock() { ScriptText = scriptBlockText };
            var credentials = new Credentials() { Username = this.username, Password = this.password };
            var results = remoteCommand.Execute(this.privateIpAddress, credentials, arguments);
            return results;
        }
    }
}