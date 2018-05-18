using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Nethereum.Generator.Console.UnitTests.EndToEndTests
{
    public class EndToEndTestContext
    {
        public string TargetProjectFolder { get; }
        public string GeneratorConsolePath { get; }
        public string OutputAssemblyName { get; }
        public string ProjectName { get; set; }
        public string ProjectFilePath { get; set; }

        public const string GeneratorConsoleName = "Nethereum.Generator.Console.dll";

        public const string GeneratorConsoleRelativePath =
            "../../../../Nethereum.Generator.Console/bin/debug/netcoreapp2.0";

        public EndToEndTestContext(string testClass, string testName)
        {
            TargetProjectFolder = Path.Combine(
                Path.GetTempPath(), 
                GetType().Assembly.GetName(false).Name, 
                testClass, 
                testName);

            ProjectName = TargetProjectFolder.Split(Path.DirectorySeparatorChar).Last();
            OutputAssemblyName = $"{ProjectName}.dll";
            ProjectFilePath = Path.Combine(TargetProjectFolder, ProjectName) + ".csproj";
            GeneratorConsolePath = Path.GetFullPath(GeneratorConsoleRelativePath); 
        }

        public string WriteFileToProject(string fileName, string fileContent)
        {
            var fullPath = Path.Combine(TargetProjectFolder, fileName);
            var outputFolder = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            File.WriteAllText(fullPath, fileContent, Encoding.UTF8);
            return fullPath;
        }

        public void CreateProject(IEnumerable<Tuple<string, string>> nugetPackages = null)
        {
            EmptyTargetFolder();

            Directory.CreateDirectory(TargetProjectFolder);
            CreateProjectFile();
            if (nugetPackages != null)
            {
                foreach (var nuget in nugetPackages)
                {
                    AddNugetPackage(nuget.Item1, nuget.Item2);
                }
            }
        }

        public bool DirectoryExists(string subDirectory)
        {
            return Directory.Exists(Path.Combine(TargetProjectFolder, subDirectory));
        }

        public bool FileExists(string relativeFilePath)
        {
            return File.Exists(Path.Combine(TargetProjectFolder, relativeFilePath));
        }

        public void GenerateCode(string commandName, string commandArgs)
        {
            var args =
                $"{GeneratorConsoleName} {commandName} {commandArgs}";

            DotNet(args, workingFolderOverride: GeneratorConsolePath);
        }

        public void BuildProject()
        {
            DotNet("build");
        }

        public bool BuildHasSucceeded()
        {
            var outputDir = Path.Combine(TargetProjectFolder, @"bin\Debug\netstandard2.0\");
            return Directory.Exists(outputDir) && Directory.GetFiles(outputDir, "*.dll").Length > 0;
        }

        public void CleanUp()
        {
            DeleteDirectory(TargetProjectFolder);
        }

        private void EmptyTargetFolder()
        {
            if (Directory.Exists(TargetProjectFolder))
                DeleteDirectory(TargetProjectFolder);
        }

        private static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException) 
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        private void AddNugetPackage(string packageName, string version)
        {
            var args = $"add package {packageName} -v {version}";
            DotNet(args);
        }

        private void CreateProjectFile()
        {
            DotNet("new classLib");
        }

        private void DotNet(string args, string workingFolderOverride = null)
        {
            using (var process = new Process())
            {
                process.StartInfo.WorkingDirectory =
                    workingFolderOverride == null ? TargetProjectFolder : workingFolderOverride;

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.Arguments = args;
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();
                process.WaitForExit();
                var output = process.StandardOutput?.ReadToEnd();
                var error = process.StandardError?.ReadToEnd();

                if (!string.IsNullOrEmpty(output))
                {
                    Debug.Write(output);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.Write(error);
                    throw new Exception($"Error occurred calling dotnet with args '{args}'. {error}");
                }

                if (process.ExitCode != 0)
                {
                    throw new Exception(
                        $"Error occurred calling dotnet with args '{args}'. ExitCode was not 0. {output}");
                }
            }
        }


    }
}
