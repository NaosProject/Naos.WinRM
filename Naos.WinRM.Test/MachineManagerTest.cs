// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MachineManagerTest.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Test
{
    using Spritely.Recipes;

    using Xunit;

    public class MachineManagerTest
    {
        [Fact(Skip = "Debug test designed to illustrate usage.")]
        public static void Examples()
        {
            var machineManager = new MachineManager(
                "10.0.0.1",
                "Administrator",
                "password".ToSecureString(),
                true);

            var powershellFileObjects = machineManager.RunScript("{ param($path) ls $path }", new[] { @"D:\Temp" });
        }
    }
}
