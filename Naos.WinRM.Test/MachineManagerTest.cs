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
        [Fact]
        public static void TestLargeFileCopy()
        {
            // this was used for debugging but provides an example case.
            var machineManager = new MachineManager(
                "10.23.1.227",
                "Administrator",
                MachineManager.ConvertStringToSecureString("(8knsKYWFf"));

            var fileObjects = machineManager.RunScript("{ param($path) ls $path }", new[] { @"D:\Temp" });
        }
    }
}
