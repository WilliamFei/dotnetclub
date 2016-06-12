﻿using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.PlatformAbstractions;
using System;
using System.IO;
using System.Linq;

namespace Discussion.Web.Tests {

    public static class TestEnv
    {
        public static string TestProjectPath()
        {
            // PlatformServices.Default.Application.ApplicationBasePath

            var args = Environment.GetCommandLineArgs();
            var appBaseIndex = Array.IndexOf(args, "--appbase");

            var path = appBaseIndex >= 0 ? args[appBaseIndex + 1] : Environment.CurrentDirectory;
            return path.NormalizeToAbsolutePath();
        }

        public static string DnxPath()
        {
            var dnxCommand = TestPlatformHelper.IsWindows ? "dnx.exe" : "dnx";
            var runtimeBin = PlatformServices.Default.Runtime.RuntimePath;

            if (string.IsNullOrWhiteSpace(runtimeBin))
            {
                throw new Exception("Runtime not detected on the machine.");
            }

            return Path.Combine(runtimeBin, dnxCommand).NormalizeToAbsolutePath();
        }

        public static string WebProjectPath()
        {
            return Path.Combine(TestProjectPath(), "../../src/Discussion.Web").NormalizeToAbsolutePath();
        }


        private static string NormalizeToAbsolutePath(this string relativePath)
        {
            return Path.GetFullPath(relativePath.NormalizeSeparatorChars());
        }

        public static string NormalizeSeparatorChars(this string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
