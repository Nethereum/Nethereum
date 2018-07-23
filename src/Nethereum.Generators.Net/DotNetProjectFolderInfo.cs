using System.IO;
using System.Linq;
using System.Xml;

namespace Nethereum.Generators.Core
{
    public class DotNetProjectFolderInfo
    {
        public static DotNetProjectFolderInfo CreateFromPath(string pathToProjectFileOrProjectFolder)
        {
            return new DotNetProjectFolderInfo(pathToProjectFileOrProjectFolder);
        }

        public string FullPathToProjectFolder { get; set;}
        public string FullPathToProjectFile {get; set;}
        public string ProjectFileName { get;set; }
        public string ProjectName { get; set; }
        public string RootNamespace { get; set; }
        public string AssemblyName { get; set; }

        public CodeGenLanguage Language { get; set; } = CodeGenLanguage.CSharp;

        public DotNetProjectFolderInfo(string pathToProjectFileOrFolder)
        {
            if (File.GetAttributes(pathToProjectFileOrFolder).HasFlag(FileAttributes.Directory))
            {
                FullPathToProjectFolder = pathToProjectFileOrFolder;
                FullPathToProjectFile = FindProjectFile(pathToProjectFileOrFolder);
                if (FullPathToProjectFile != null)
                {
                    SetProjectInfo();
                }
            }
            else //it's a project file
            {
                FullPathToProjectFile = pathToProjectFileOrFolder;
                FullPathToProjectFolder = Path.GetDirectoryName(pathToProjectFileOrFolder);
                SetProjectInfo();
            }
        }

        private void SetProjectInfo()
        {
            ProjectFileName = Path.GetFileName(FullPathToProjectFile);
            ProjectName = Path.GetFileNameWithoutExtension(FullPathToProjectFile);
            Language = CodeGenLanguageExt.GetCodeGenLanguageFromProjectFile(ProjectFileName);
            RootNamespace = GetOrInferRootNamespace(FullPathToProjectFile, ProjectName);
            AssemblyName = GetOrInferAssemblyName(FullPathToProjectFile, ProjectName);
        }

        private string GetOrInferRootNamespace(string fullProjectFilePath, string projectName)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(fullProjectFilePath);
            var rootNamespaceElement = xmlDoc.DocumentElement.SelectSingleNode("PropertyGroup/RootNamespace");
            if (rootNamespaceElement != null)
                return rootNamespaceElement.InnerText;

            var assemblyNameElement = xmlDoc.DocumentElement.SelectSingleNode("PropertyGroup/AssemblyName");
            if (assemblyNameElement != null)
                return assemblyNameElement.InnerText;

            return projectName;
        }

        private string GetOrInferAssemblyName(string fullProjectFilePath, string projectName)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(fullProjectFilePath);
            var rootNamespaceElement = xmlDoc.DocumentElement.SelectSingleNode("PropertyGroup/AssemblyName");
            if (rootNamespaceElement != null)
                return rootNamespaceElement.InnerText;

            return projectName;
        }

        private string FindProjectFile(string path)
        {
            foreach (var fileExtension in CodeGenLanguageExt.GetValidProjectFileExtensions())
            {
                var searchPattern = $"*{fileExtension}";
                var firstMatch = Directory.GetFiles(path, searchPattern).FirstOrDefault();
                if (firstMatch != null)
                    return firstMatch;
            }

            return null;
        }
    }
}