using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
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
        }

        public string WriteFileToProject(string fileName, string fileContent)
        {
            return TestEnvironment.WriteFileToFolder(TargetProjectFolder, fileName, fileContent);
        }

        private void ReferenceLocalNugetPackages()
        {
            if (!Directory.Exists(LocalNugetPackageFolder))
                return;

            string nugetConfig = 
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""Local"" value=""{LocalNugetPackageFolder}"" />
    <add key=""NuGet"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>
";
            WriteFileToProject("NuGet.config", nugetConfig);
        }

        public string LocalNugetPackageFolder { get; set; } = @"C:\dev\test\nuget\packages";

        public void CreateProject(CodeGenLanguage language = CodeGenLanguage.CSharp, IEnumerable<Tuple<string, string>> nugetPackages = null)
        {
            EmptyTargetFolder();

            Directory.CreateDirectory(TargetProjectFolder);
            ReferenceLocalNugetPackages();
            CreateProjectFile(language);
            if (nugetPackages != null)
            {
                foreach (var nuget in nugetPackages)
                {
                    AddNugetPackage(nuget.Item1, nuget.Item2);
                }
            }
        }

        public void CreateEmptyProject()
        {
            Directory.CreateDirectory(TargetProjectFolder);
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
            var outputDir = Path.Combine(TargetProjectFolder, "bin", "Debug", TargetFramework);
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
            if (!Directory.Exists(path))
                return;

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
                if (!Directory.Exists(path))
                    return;

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

        public string TargetFramework { get; set; } = "netcoreapp2.1";

        private void CreateProjectFile(CodeGenLanguage language)
        {
            DotNet($"new classLib -f {TargetFramework} -lang {language.ToDotNetCli()}");
            ProjectFilePath = Path.Combine(TargetProjectFolder, ProjectName) + CodeGenLanguageExt.ProjectFileExtensions[language];
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

        public void AddAssemblyReferences(IEnumerable<string> assemblyPaths)
        {
                /*
    <ItemGroup>
    <Reference Include="Cryptlet.Messages">
    <HintPath>..\..\..\Epiphyte\dotnet\Epiphyte\CryptletMessages\bin\Debug\netcoreapp2.0\Cryptlet.Messages.dll</HintPath>
    </Reference>
    </ItemGroup>
     */

                var projectDoc = new XmlDocument();
                projectDoc.Load(ProjectFilePath);
                var projectElement = projectDoc.DocumentElement;
                var itemGroupElement = projectDoc.CreateElement("ItemGroup");
                projectElement.AppendChild(itemGroupElement);
                foreach (var dll in assemblyPaths)
                {
                    var referenceElement = projectDoc.CreateElement("Reference");
                    itemGroupElement.AppendChild(referenceElement);
                    var includeAttribute = projectDoc.CreateAttribute("Include");
                    includeAttribute.Value = Path.GetFileNameWithoutExtension(dll);
                    referenceElement.Attributes.Append(includeAttribute);
                    var hintPathElement = projectDoc.CreateElement("HintPath");
                    hintPathElement.InnerText = dll;
                    referenceElement.AppendChild(hintPathElement);
                }

                projectDoc.Save(ProjectFilePath);
        }

        public void SetRootNamespaceInProject(string rootNamespace)
        {
            var projectDoc = new XmlDocument();
            projectDoc.Load(ProjectFilePath);
            var propertyGroupElement = projectDoc.DocumentElement.SelectSingleNode("PropertyGroup");
            var rootNsElement = propertyGroupElement.SelectSingleNode("RootNamespace");
            if (rootNsElement == null)
            {
                rootNsElement = projectDoc.CreateElement("RootNamespace");
                propertyGroupElement.AppendChild(rootNsElement);
            }

            rootNsElement.InnerText = rootNamespace;
            projectDoc.Save(ProjectFilePath);
        }

        public void SetAssemblyNameInProject(string rootNamespace)
        {
            var projectDoc = new XmlDocument();
            projectDoc.Load(ProjectFilePath);
            var propertyGroupElement = projectDoc.DocumentElement.SelectSingleNode("PropertyGroup");
            var assemblyNameElement = propertyGroupElement.SelectSingleNode("AssemblyName");
            if (assemblyNameElement == null)
            {
                assemblyNameElement = projectDoc.CreateElement("AssemblyName");
                propertyGroupElement.AppendChild(assemblyNameElement);
            }

            assemblyNameElement.InnerText = rootNamespace;
            projectDoc.Save(ProjectFilePath);
        }

    }
}
