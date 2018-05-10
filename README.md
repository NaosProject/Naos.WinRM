[![Build status](https://ci.appveyor.com/api/projects/status/v9swcwxn8df3tbym?svg=true)](https://ci.appveyor.com/project/Naos-Project/naos-winrm)

Naos.WinRM
================
A .NET wrapper for the WinRM protocol to remotely execute Powershell, optionally auto-manage TrustedHost list, and send/receive files!

Use - Referencing in your code
-----------
The entire implemenation is in a single file so it can be included without taking a dependency on the NuGet package if necessary preferred.
* Reference the NuGet package: <a target="_blank" href="http://www.nuget.org/packages/Naos.WinRM">http://www.nuget.org/packages/Naos.WinRM</a>.
  <br/><b>OR</b>
* Reference the NuGet recipe package which will only install the files and external dependencies: <a target="_blank" href="http://www.nuget.org/packages/Naos.Recipes.WinRM">http://www.nuget.org/packages/Naos.Recipes.WinRM</a>.
  <br/><b>OR</b>
* Include the single file (will still REQUIRE System.Management.Automation.dll reference or package reference to Naos.External.MS-WinRM) in your project: <a target="_blank" href="https://raw.githubusercontent.com/NaosProject/Naos.WinRM/master/Naos.WinRM/Naos.WinRM.cs">https://raw.githubusercontent.com/NaosProject/Naos.WinRM/master/Naos.WinRM/Naos.WinRM.cs</a>.


```C#
// this is the entrypoint to interact with the system (interfaced for testing).
var machineManager = new MachineManager(
    "10.0.0.1",
    "Administrator",
    "password".ToSecureString(),
    autoManageTrustedHosts: true);

// will perform a user initiated reboot.
machineManager.Reboot();

// can run random script blocks WITH parameters.
var fileObjects = machineManager.RunScript("{ param($path) ls $path }", new[] { @"C:\PathToList" });
var fileObjectsWithTwoTypedParameters = machineManager.RunScript("{ param([string] $path, [string] $filter) ls -Path $path -Filter $filter }", new[] { @"C:\Windows", "*.exe" });

// can run random cmd.exe commands WITH parameters.
var output = machineManager.RunCmd("xcopy", new[] { "D:\\File.txt", "D:\\Folder\\" });

// can run scripts and cmd commands locally.
machineManager.RunScriptOnLocalhost(...);
machineManager.RunCmdOnLocalhost(...);

// can transfer files to AND from the remote server (over WinRM's protocol!).
var localFilePath = @"D:\Temp\BigFileLocal.nupkg";
var fileBytes = File.ReadAllBytes(localFilePath);
var remoteFilePath = @"D:\Temp\BigFileRemote.nupkg";

machineManager.SendFile(remoteFilePath, fileBytes);
var downloadedBytes = machineManager.RetrieveFile(remoteFilePath);
```