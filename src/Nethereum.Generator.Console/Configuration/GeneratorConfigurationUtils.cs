using System;
using Nethereum.Generators.Core;
using System.IO;
using System.Linq;

namespace Nethereum.Generator.Console.Configuration
{
    public static class GeneratorConfigurationUtils
    {
        public static string ConfigFileName = GeneratorConfigurationConstants.ConfigFileName;

        public static void ResolveEmptyValuesWithDefaults(this ABIConfiguration abiConfiguration, string defaultNamespace, string destinationProjectFolder)
        {
            if (string.IsNullOrEmpty(abiConfiguration.ContractName) && !string.IsNullOrEmpty(abiConfiguration.ABIFile))
                abiConfiguration.ContractName = Path.GetFileNameWithoutExtension(abiConfiguration.ABIFile);

            if (string.IsNullOrEmpty(abiConfiguration.ABI))
                abiConfiguration.ABI = GetFileContent(destinationProjectFolder, abiConfiguration.ABIFile);

            //by convention - look for bin folder in the same place as the abi
            if (string.IsNullOrEmpty(abiConfiguration.BinFile) && !string.IsNullOrEmpty(abiConfiguration.ABIFile))
            {
                if (abiConfiguration.ABIFile.EndsWith(".abi", StringComparison.InvariantCultureIgnoreCase))
                {
                    abiConfiguration.BinFile = $"{abiConfiguration.ABIFile.Substring(0, abiConfiguration.ABIFile.Length - 4)}.bin";
                }
            }

            if (string.IsNullOrEmpty(abiConfiguration.ByteCode)  && !string.IsNullOrEmpty(abiConfiguration.BinFile))
                abiConfiguration.ByteCode = GetFileContent(destinationProjectFolder, abiConfiguration.BinFile);

            //no bytecode so clear bin file
            if (string.IsNullOrEmpty(abiConfiguration.ByteCode))
                abiConfiguration.BinFile = null;

            if (string.IsNullOrEmpty(abiConfiguration.BaseOutputPath))
                abiConfiguration.BaseOutputPath = destinationProjectFolder;

            if (string.IsNullOrEmpty(abiConfiguration.BaseNamespace))
                abiConfiguration.BaseNamespace = defaultNamespace;

            if (string.IsNullOrEmpty(abiConfiguration.CQSNamespace))
                abiConfiguration.CQSNamespace = $"{abiConfiguration.ContractName}.CQS";

            if (string.IsNullOrEmpty(abiConfiguration.DTONamespace))
                abiConfiguration.DTONamespace = $"{abiConfiguration.ContractName}.DTO";

            if (string.IsNullOrEmpty(abiConfiguration.ServiceNamespace))
                abiConfiguration.ServiceNamespace = $"{abiConfiguration.ContractName}.Service";
        }

        public static string GetFileContent(string projectFolder, string relativeOrAbsolutePath)
        {
            if(string.IsNullOrEmpty(relativeOrAbsolutePath))
                return null;

            if(Path.IsPathRooted(relativeOrAbsolutePath))
                return File.Exists(relativeOrAbsolutePath) ? File.ReadAllText(relativeOrAbsolutePath) : null;

            if (relativeOrAbsolutePath.Contains(".."))
            {
                var absolutePath = Path.GetFullPath(projectFolder + relativeOrAbsolutePath);
                if (File.Exists(absolutePath))
                    return absolutePath;
            }

            var projectPath = Path.Combine(projectFolder, relativeOrAbsolutePath);
            if(File.Exists(projectPath))
                return File.ReadAllText(projectPath);

            var matchingFiles = Directory.GetFiles(projectFolder, Path.GetFileName(relativeOrAbsolutePath), SearchOption.AllDirectories);
            if(matchingFiles.Length > 0)
                return File.ReadAllText(matchingFiles.First());

            var fullPath = Path.GetFullPath(relativeOrAbsolutePath);

            if(File.Exists(fullPath))
                return File.ReadAllText(fullPath);

            return null;
        }

        public static (string folder, string file) GetFullFileAndFolderPaths(string destinationProjectFolderOrFileName)
        {
            FileAttributes attr = File.GetAttributes(destinationProjectFolderOrFileName);

            if (attr.HasFlag(FileAttributes.Directory))
            {
                var file = FindFirstProjectFile(destinationProjectFolderOrFileName);
                return (destinationProjectFolderOrFileName, file);
            }

            var folder = Path.GetDirectoryName(destinationProjectFolderOrFileName);
            return (folder, destinationProjectFolderOrFileName);
        }

        public static string FindFirstProjectFile(string folder)
        {
            foreach (var extension in CodeGenLanguageExt.ProjectFileExtensions.Values)
            {
                var files = Directory.GetFiles(folder, $"*{extension}");
                if (files.Length > 0)
                    return files[0];
            }

            return null;
        }

        public static CodeGenLanguage DeriveCodeGenLanguage(string destinationProjectFileName)
        {
            var extension = Path.GetExtension(destinationProjectFileName).ToLower();

            foreach (var codeGenLanguage in CodeGenLanguageExt.ProjectFileExtensions.Keys)
            {
                var projectExtension = CodeGenLanguageExt.ProjectFileExtensions[codeGenLanguage];
                if (projectExtension == extension)
                    return codeGenLanguage;
            }

            throw new ArgumentException($"Could not derive code gen language. Unrecognised project file type ({extension}).");
        }

        public static string DeriveConfigFilePath(string projectFolder)
        {
            return Path.Combine(projectFolder, ConfigFileName);
        }

        public static string CreateNamespaceFromAssemblyName(string assemblyName)
        {
            return Path.GetFileNameWithoutExtension(assemblyName);
        }
    }
}
