// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MachineManagerTest.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Test
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using Xunit;

    public class MachineManagerTest
    {
        // [Fact]
        public static void Examples()
        {
            // this was used for debugging but provides an example case.
            var machineManager = new MachineManager(
                "10.0.0.1",
                "Administrator",
                MachineManager.ConvertStringToSecureString("password"));

            var fileObjects = machineManager.RunScript("{ param($path) ls $path }", new[] { @"D:\Temp" });
        }
    }
}
