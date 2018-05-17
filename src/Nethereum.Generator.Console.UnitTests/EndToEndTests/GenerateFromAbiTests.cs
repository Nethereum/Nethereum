using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.EndToEndTests
{
    public class GenerateFromAbiTests
    {
        [Fact]
        public void GeneratesExpectedFilesAndCompiles()
        {
            //given
            var targetProjectFolder = Path.Combine(
                Path.GetTempPath(), "Nethereum.Generator.Console.UnitTests", "GenerateFromAbiTests", "Sample.CodeGenProject");

            EmptyTargetFolder(targetProjectFolder);

            const string relativeAbiPath = "StandardContract.abi";
            const string baseNamespace = "Sample.CodeGenProject";
            const string generateCommand = "gen-fromabi";
            var fullAbiFilePath = Path.Combine(targetProjectFolder, relativeAbiPath);
            var pathToConsole =
                Path.GetFullPath("../../../../Nethereum.Generator.Console/bin/debug/netcoreapp2.0/Nethereum.Generator.Console.dll");

            Directory.CreateDirectory(targetProjectFolder);
            File.WriteAllText(fullAbiFilePath, TestData.StandardContract.ABI, Encoding.UTF8);
            CreateProjectFile(targetProjectFolder);
            AddNugetPackages(targetProjectFolder);

            //when
            GenerateCode(pathToConsole, targetProjectFolder, baseNamespace, generateCommand, fullAbiFilePath);

            //then
            Assert.True(Directory.Exists(Path.Combine(targetProjectFolder, "StandardContract")));
            Assert.True(Directory.Exists(Path.Combine(targetProjectFolder, "StandardContract", "CQS")));
            Assert.True(Directory.Exists(Path.Combine(targetProjectFolder, "StandardContract", "DTO")));
            Assert.True(Directory.Exists(Path.Combine(targetProjectFolder, "StandardContract", "Service")));

            BuildProject(targetProjectFolder);

            Assert.True(File.Exists(Path.Combine(targetProjectFolder, @"bin\Debug\netstandard2.0\Sample.CodeGenProject.dll")));

            //clean up
            Directory.Delete(targetProjectFolder, true);
        }

        private static void EmptyTargetFolder(string targetProjectFolder)
        {
            if (Directory.Exists(targetProjectFolder))
                Directory.Delete(targetProjectFolder, true);
        }

        private static void GenerateCode(string pathToConsole, string targetProjectFolder, string baseNamespace, string generateCommand, string fullAbiFilePath)
        {
            var args =
                $"{pathToConsole} {generateCommand} -abi {fullAbiFilePath} -o {targetProjectFolder} -ns {baseNamespace}";

            var startInfo = new ProcessStartInfo("dotnet", args);
            Process.Start(startInfo).WaitForExit();
        }

        private static void AddNugetPackages(string targetProjectFolder)
        {
            var args = "add package Nethereum.Web3 -v 2.4.0";
            var startInfo = new ProcessStartInfo("dotnet", args){WorkingDirectory = targetProjectFolder};
            Process.Start(startInfo).WaitForExit();
        }

        private static void CreateProjectFile(string targetProjectFolder)
        {
            var startInfo = new ProcessStartInfo("dotnet", "new classLib"){WorkingDirectory = targetProjectFolder};
            Process.Start(startInfo).WaitForExit();
        }

        private static void BuildProject(string targetProjectFolder)
        {
            var startInfo = new ProcessStartInfo("dotnet", "build"){WorkingDirectory = targetProjectFolder};
            Process.Start(startInfo).WaitForExit();
        }
    }
}
