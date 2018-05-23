using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Nethereum.Generators.Core;
using Nethereum.Generators.Tests.Common.TestData;

namespace Nethereum.Generators.Tests.Common
{
    public class ProjectTestContext
    {
        public string TargetProjectFolder { get; }
        public string OutputAssemblyName { get; }
        public string ProjectName { get; set; }
        public string ProjectFilePath { get; set; }


        public ProjectTestContext(string testClass, string testName)
        {
            TargetProjectFolder = Path.Combine(
                TestEnvironment.TempPath, 
                testClass, 
                testName);

            ProjectName = TargetProjectFolder.Split(Path.DirectorySeparatorChar).Last();
            OutputAssemblyName = $"{ProjectName}.dll";
            ProjectFilePath = Path.Combine(TargetProjectFolder, ProjectName) + ".csproj";
        }

        public string WriteFileToProject(string fileName, string fileContent)
        {
            return TestEnvironment.WriteFileToFolder(TargetProjectFolder, fileName, fileContent);
        }

        public void CreateProject(CodeGenLanguage language = CodeGenLanguage.CSharp, IEnumerable<Tuple<string, string>> nugetPackages = null)
        {
            EmptyTargetFolder();

            Directory.CreateDirectory(TargetProjectFolder);
            CreateProjectFile(language);
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

            DeleteDirectoryWithRetry(path);
        }

        private static void DeleteDirectoryWithRetry(string path, int attemptNumber = 0)
        {
            try
            {
                attemptNumber++;
                if(attemptNumber < 4)
                    Directory.Delete(path, true);
                else
                    Debug.WriteLine($"Failed to delete directory '{attemptNumber}'.  Attempt count exceeded.");
            }
            catch (Exception x)
            {
                Debug.WriteLine($"Failed to delete directory '{attemptNumber}', attemptNumber: {attemptNumber}. {x.Message}");
                Thread.Sleep(1000 * attemptNumber);
                DeleteDirectoryWithRetry(path, attemptNumber);
            }
        }

        public void AddNugetPackage(string packageName, string version)
        {
            var args = $"add package {packageName} -v {version}";
            DotNet(args);
        }

        private void CreateProjectFile(CodeGenLanguage language)
        {
            DotNet($"new classLib -lang {language.ToDotNetCli()}");
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
                process.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
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
