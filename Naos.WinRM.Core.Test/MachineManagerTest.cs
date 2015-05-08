// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MachineManagerTest.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Core.Test
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using Xunit;

    public class MachineManagerTest
    {
        // [Fact]
        public static void TestLargeFileCopy()
        {
            var machineManager = new MachineManager(
                "10.0.0.1",
                "Administrator",
                MachineManager.ConvertStringToSecureString("xxx"));

            var localFilePath = @"D:\Temp\BigFileLocal.nupkg";
            var fileBytes = File.ReadAllBytes(localFilePath);
            var remoteFilePath = @"D:\Temp\BigFileRemote.nupkg";

            var timer = new Stopwatch();
            timer.Start();
            machineManager.SendFile(remoteFilePath, fileBytes);
            timer.Stop();
            Console.WriteLine(timer.Elapsed);
        }
    }
}
