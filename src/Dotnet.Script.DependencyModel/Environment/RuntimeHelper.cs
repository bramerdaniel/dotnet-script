﻿using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.DotNet.PlatformAbstractions;

namespace Dotnet.Script.DependencyModel.Environment
{
    public static class RuntimeHelper
    {
        private static readonly Regex RuntimeMatcher =
            new Regex($"{GetPlatformIdentifier()}.*-{GetProcessArchitecture()}");

        private static readonly Lazy<string> LazyTargetFramework = new Lazy<string>(GetNetCoreAppVersion); 

        public static string GetPlatformIdentifier()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Darwin) return "osx";
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Linux) return "linux";
            return "win";            
        }

        public static bool IsWindows()
        {
            return GetPlatformIdentifier() == "win";
        }

        public static string TargetFramework => LazyTargetFramework.Value;

        private static string GetNetCoreAppVersion()
        {
            // https://github.com/dotnet/BenchmarkDotNet/blob/94863ab4d024eca04d061423e5aad498feff386b/src/BenchmarkDotNet/Portability/RuntimeInformation.cs#L156 

            var codeBase = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.CodeBase;
            var pattern = @"^.*Microsoft\.NETCore\.App\/(\d\.\d)";
            var match = Regex.Match(codeBase, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new InvalidOperationException("Unable to determine netcoreapp version");
            }
            var version = match.Groups[1].Value;
            return $"netcoreapp{version}";
        }

        private static string GetDotnetBinaryPath()
        {
            string basePath;
            if (IsWindows())
            {
                basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
            }
            else
            {
                basePath = "usr/local/share";
            }
            return Path.Combine(basePath, "dotnet");
        }
       
        public static string GetPathToNuGetStoreFolder()
        {            
            var processArchitecture = GetProcessArchitecture();
            var storePath = Path.Combine(GetDotnetBinaryPath(), "store", processArchitecture, TargetFramework);
            return storePath;
        }


        private static string GetProcessArchitecture()
        {
            return RuntimeEnvironment.RuntimeArchitecture;            
        }

        public static string GetRuntimeIdentifier()
        {
            var platformIdentifier = GetPlatformIdentifier();
            if (platformIdentifier == "osx" || platformIdentifier == "linux")
            {
                return $"{platformIdentifier}-{GetProcessArchitecture()}";
            }
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            return runtimeIdentifier;
        }

        public static string CreateTempFolder(string targetDirectory)
        {
            if (!Path.IsPathRooted(targetDirectory))
            {
                throw new ArgumentOutOfRangeException(nameof(targetDirectory), "Must be a root path");
            }

            var tempDirectory = Path.GetTempPath();
            var pathRoot = Path.GetPathRoot(targetDirectory);
            var targetDirectoryWithoutRoot = targetDirectory.Substring(pathRoot.Length);
            if (pathRoot.Length > 0 && RuntimeHelper.IsWindows())
            {
                var driveLetter = pathRoot.Substring(0, 1);
                if (driveLetter == "\\")
                {
                    targetDirectoryWithoutRoot = targetDirectoryWithoutRoot.TrimStart(new char[] { '\\' });
                    driveLetter = "UNC";
                }

                targetDirectoryWithoutRoot = Path.Combine(driveLetter, targetDirectoryWithoutRoot);
            }
            var pathToProjectDirectory = Path.Combine(tempDirectory, "scripts", targetDirectoryWithoutRoot);

            if (!Directory.Exists(pathToProjectDirectory))
            {
                Directory.CreateDirectory(pathToProjectDirectory);
            }

            return pathToProjectDirectory;
        }

        internal static bool AppliesToCurrentRuntime(string runtime)
        {            
            return string.IsNullOrWhiteSpace(runtime) || RuntimeMatcher.IsMatch(runtime);
        }
    }
}