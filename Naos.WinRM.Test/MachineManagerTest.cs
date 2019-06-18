// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MachineManagerTest.cs" company="Naos Project">
//    Copyright (c) Naos Project 2019. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Test
{
    using Xunit;

    public static class MachineManagerTest
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "powershellFileObjects", Justification = "Prefer to see that output is generated and not used...")]
        [Fact(Skip = "Debug test designed to illustrate usage.")]
        public static void Examples()
        {
            var machineManager = new MachineManager(
                "10.0.0.1",
                "Administrator",
                "password".ToSecureString(),
                autoManageTrustedHosts: true);

            var powershellFileObjects = machineManager.RunScript("{ param($path) ls $path }", new[] { @"D:\Temp" });
        }
    }
}
