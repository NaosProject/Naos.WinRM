// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Naos.WinRM.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Security;
    using System.Security.Cryptography;

    /// <summary>
    /// Custom base exception to allow global catching of internally generated errors.
    /// </summary>
    public abstract class NaosWinRmExceptionBase : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NaosWinRmExceptionBase"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        protected NaosWinRmExceptionBase(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Custom exception for when trying to execute 
    /// </summary>
    public class TrustedHostMissingException : NaosWinRmExceptionBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TrustedHostMissingException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public TrustedHostMissingException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Custom exception for when things go wrong running remote commands.
    /// </summary>
    public class RemoteExecutionException : NaosWinRmExceptionBase
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
        /// Gets the IP address of the machine being managed.
        /// </summary>
        string IpAddress { get; }

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
        private readonly long fileChunkSizeThresholdByteCount;

        private readonly long fileChunkSizePerSend;

        private readonly string username;

        private readonly SecureString password;

        private readonly bool autoManageTrustedHosts;

        /// <summary>
        /// Initializes a new instance of the <see cref="MachineManager"/> class.
        /// </summary>
        /// <param name="ipAddress">IP address of machine to interact with.</param>
        /// <param name="username">Username to use to connect.</param>
        /// <param name="password">Password to use to connect.</param>
        /// <param name="autoManageTrustedHosts">Optionally specify whether to update the TrustedHost list prior to execution or assume it's handled elsewhere (default is FALSE).</param>
        /// <param name="fileChunkSizeThresholdByteCount">Optionally specify file size that will trigger chunking the file rather than sending as one file (150000 is default).</param>
        /// <param name="fileChunkSizePerSend">Optionally specify size of each chunk that is sent when a file is being chunked.</param>
        public MachineManager(
            string ipAddress, 
            string username, 
            SecureString password, 
            bool autoManageTrustedHosts = false,
            long fileChunkSizeThresholdByteCount = 150000,
            long fileChunkSizePerSend = 100000)
        {
            this.IpAddress = ipAddress;
            this.username = username;
            this.password = password;
            this.autoManageTrustedHosts = autoManageTrustedHosts;
            this.fileChunkSizeThresholdByteCount = fileChunkSizeThresholdByteCount;
            this.fileChunkSizePerSend = fileChunkSizePerSend;
        }

        /// <summary>
        /// Locally updates the trusted hosts to have the ipAddress provided.
        /// </summary>
        /// <param name="ipAddress">IP Address to add to local trusted hosts.</param>
        public static void AddIpAddressToLocalTrusedHosts(string ipAddress)
        {
            var currentTrustedHosts = GetListOfIpAddressesFromLocalTrustedHosts().ToList();

            if (!currentTrustedHosts.Contains(ipAddress))
            {
                currentTrustedHosts.Add(ipAddress);
                var newValue = currentTrustedHosts.Any() ? string.Join(",", currentTrustedHosts) : ipAddress;
                using (var runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();

                    var command = new Command("Set-Item");
                    command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");
                    command.Parameters.Add("Value", newValue);
                    command.Parameters.Add("Force", true);

                    var notUsedOutput = RunLocalCommand(runspace, command);
                }
            }
        }

        /// <summary>
        /// Locally updates the trusted hosts to remove the ipAddress provided (if applicable).
        /// </summary>
        /// <param name="ipAddress">IP Address to remove from local trusted hosts.</param>
        public static void RemoveIpAddressFromLocalTrusedHosts(string ipAddress)
        {
            var currentTrustedHosts = GetListOfIpAddressesFromLocalTrustedHosts().ToList();

            if (currentTrustedHosts.Contains(ipAddress))
            {
                currentTrustedHosts.Remove(ipAddress);
                var newValue = currentTrustedHosts.Any() ? string.Join(",", currentTrustedHosts) : string.Empty; // can't pass null must be an empty string...
                using (var runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();

                    var command = new Command("Set-Item");
                    command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");
                    command.Parameters.Add("Value", newValue);
                    command.Parameters.Add("Force", true);

                    var notUsedOutput = RunLocalCommand(runspace, command);
                }
            }
        }

        /// <summary>
        /// Locally updates the trusted hosts to have the ipAddress provided.
        /// </summary>
        /// <returns>List of the trusted hosts.</returns>
        public static ICollection<string> GetListOfIpAddressesFromLocalTrustedHosts()
        {
            try
            {
                using (var runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();

                    var command = new Command("Get-Item");
                    command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");

                    var response = RunLocalCommand(runspace, command);

                    var valueProperty = response.Single().Properties.Single(_ => _.Name == "Value");

                    var value = valueProperty.Value.ToString();

                    var ret = string.IsNullOrEmpty(value) ? new string[0] : value.Split(',');

                    return ret;
                }
            }
            catch (ItemNotFoundException itemNotFoundException)
            {
                // if we don't have any trusted hosts then just ignore...
                if (itemNotFoundException.Message
                    == "Cannot find path 'WSMan:\\localhost\\Client\\TrustedHosts' because it does not exist.")
                {
                    return new List<string>();
                }
                
                throw;
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
        public string IpAddress { get; private set; }

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

                var verifyFileDoesntExistScriptBlock = @"
	                { 
		                param($filePath)

			            if (Test-Path $filePath)
			            {
				            Write-Error ""File already exists at: $filePath""
			            }
	                }";

                if (!appended)
                {
                    this.RunScriptUsingSession(
                        verifyFileDoesntExistScriptBlock,
                        new[] { filePathOnTargetMachine },
                        runspace,
                        sessionObject);
                }

                if (fileContents.Length <= this.fileChunkSizeThresholdByteCount)
                {
                    this.SendFileUsingSession(filePathOnTargetMachine, fileContents, appended, runspace, sessionObject);
                }
                else
                {
                    // deconstruct and send pieces as appended...
                    var nibble = new List<byte>();
                    foreach (byte currentByte in fileContents)
                    {
                        if (nibble.Count < this.fileChunkSizePerSend)
                        {
                            nibble.Add(currentByte);
                        }
                        else
                        {
                            nibble.Add(currentByte);
                            this.SendFileUsingSession(filePathOnTargetMachine, nibble.ToArray(), true, runspace, sessionObject);
                            nibble.Clear();
                        }
                    }

                    // flush the "buffer"...
                    if (nibble.Any())
                    {
                        this.SendFileUsingSession(filePathOnTargetMachine, nibble.ToArray(), true, runspace, sessionObject);
                        nibble.Clear();
                    }
                }

                var expectedChecksum = ComputeSha256Hash(fileContents);
                var verifyChecksumScriptBlock = @"
	                { 
		                param($filePath, $expectedChecksum)

		                $fileToCheckFileInfo = New-Object System.IO.FileInfo($filePath)
		                if (-not $fileToCheckFileInfo.Exists)
		                {
			                # If the file can't be found, try looking for it in the current directory.
			                $fileToCheckFileInfo = New-Object System.IO.FileInfo($filePath)
			                if (-not $fileToCheckFileInfo.Exists)
			                {
				                throw ""Can't find the file specified to calculate a checksum on: $filePath""
			                }
		                }

		                $fileToCheckFileStream = $fileToCheckFileInfo.OpenRead()
                        $provider = New-Object System.Security.Cryptography.SHA256CryptoServiceProvider
                        $hashBytes = $provider.ComputeHash($fileToCheckFileStream)
		                $fileToCheckFileStream.Close()
		                $fileToCheckFileStream.Dispose()
		
		                $base64 = [System.Convert]::ToBase64String($hashBytes)
		
		                $calculatedChecksum = [System.String]::Empty
		                foreach ($byte in $hashBytes)
		                {
			                $calculatedChecksum = $calculatedChecksum + $byte.ToString(""X2"")
		                }

		                if($calculatedChecksum -ne $expectedChecksum)
		                {
			                Write-Error ""Checksums don't match on File: $filePath - Expected: $expectedChecksum - Actual: $calculatedChecksum""
		                }
	                }";

                this.RunScriptUsingSession(
                    verifyChecksumScriptBlock,
                    new[] { filePathOnTargetMachine, expectedChecksum },
                    runspace,
                    sessionObject);

                this.EndSession(sessionObject, runspace);

                runspace.Close();
            }
        }

        private void SendFileUsingSession(
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

            var notUsedResults = this.RunScriptUsingSession(sendFileScriptBlock, arguments, runspace, sessionObject);
        }

        /// <inheritdoc />
        public ICollection<dynamic> RunScript(string scriptBlock, ICollection<object> scriptBlockParameters = null)
        {
            List<object> ret;

            using (var runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                var sessionObject = this.BeginSession(runspace);

                ret = this.RunScriptUsingSession(scriptBlock, scriptBlockParameters, runspace, sessionObject);

                this.EndSession(sessionObject, runspace);

                runspace.Close();
            }

            return ret;
        }

        private void EndSession(object sessionObject, Runspace runspace)
        {
            if (this.autoManageTrustedHosts)
            {
                RemoveIpAddressFromLocalTrusedHosts(this.IpAddress);
            }

            var removeSessionCommand = new Command("Remove-PSSession");
            removeSessionCommand.Parameters.Add("Session", sessionObject);
            var unneededOutput = RunLocalCommand(runspace, removeSessionCommand);
        }

        private object BeginSession(Runspace runspace)
        {
            if (this.autoManageTrustedHosts)
            {
                AddIpAddressToLocalTrusedHosts(this.IpAddress);
            }

            var trustedHosts = GetListOfIpAddressesFromLocalTrustedHosts();
            if (!trustedHosts.Contains(this.IpAddress))
            {
                throw new TrustedHostMissingException(
                    "Cannot execute a remote command with out the IP address being added to the trusted hosts list.  Please set MachineManager to handle this automatically or add the address manually: "
                    + this.IpAddress);
            }

            var powershellCredentials = new PSCredential(this.username, this.password);

            var sessionOptionsCommand = new Command("New-PSSessionOption");
            sessionOptionsCommand.Parameters.Add("OperationTimeout", 0);
            sessionOptionsCommand.Parameters.Add("IdleTimeout", TimeSpan.FromMinutes(20).TotalMilliseconds);
            var sessionOptionsObject = RunLocalCommand(runspace, sessionOptionsCommand).Single().BaseObject;

            var sessionCommand = new Command("New-PSSession");
            sessionCommand.Parameters.Add("ComputerName", this.IpAddress);
            sessionCommand.Parameters.Add("Credential", powershellCredentials);
            sessionCommand.Parameters.Add("SessionOption", sessionOptionsObject);
            var sessionObject = RunLocalCommand(runspace, sessionCommand).Single().BaseObject;
            return sessionObject;
        }

        private List<dynamic> RunScriptUsingSession(
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

        private static List<PSObject> RunLocalCommand(Runspace runspace, Command arbitraryCommand)
        {
            using (var powershell = PowerShell.Create())
            {
                powershell.Runspace = runspace;

                powershell.Commands.AddCommand(arbitraryCommand);

                var output = powershell.Invoke();

                ThrowOnError(powershell, arbitraryCommand.CommandText, "localhost");

                var ret = output.ToList();
                return ret;
            }
        }

        private void ThrowOnError(PowerShell powershell, string attemptedScriptBlock)
        {
            ThrowOnError(powershell, attemptedScriptBlock, this.IpAddress);
        }

        private static void ThrowOnError(PowerShell powershell, string attemptedScriptBlock, string ipAddress)
        {
            if (powershell.Streams.Error.Count > 0)
            {
                var errorString = string.Join(
                    Environment.NewLine,
                    powershell.Streams.Error.Select(
                        _ =>
                        (_.ErrorDetails == null ? null : _.ErrorDetails.ToString())
                        ?? (_.Exception == null ? "Naos.WinRM: No error message available" : _.Exception.ToString())));
                throw new RemoteExecutionException(
                    "Failed to run script (" + attemptedScriptBlock + ") on " + ipAddress + " got errors: "
                    + errorString);
            }
        }

        private static string ComputeSha256Hash(byte[] bytes)
        {
            var provider = new SHA256Managed();
            var hashBytes = provider.ComputeHash(bytes);
            var calculatedChecksum = string.Empty;

            foreach (byte x in hashBytes)
            {
                calculatedChecksum += string.Format("{0:x2}", x);
            }

            return calculatedChecksum;
        }
    }
}