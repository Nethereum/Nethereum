using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nethereum.Generators.Core;
using Newtonsoft.Json;

namespace Nethereum.Generator.Console.Configuration
{
    public class GeneratorConfigurationFactory
    {
        public static string ConfigFileName = GeneratorConfigurationConstants.ConfigFileName;

        public GeneratorConfiguration FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder)
        {
            var config = new GeneratorConfiguration();

            var abi = GetFileContent(outputFolder, abiFilePath);

            if (string.IsNullOrEmpty(abi))
                throw new ArgumentException("Could not find abi file or abi content is empty");

            if (string.IsNullOrEmpty(binFilePath))
                binFilePath = abiFilePath.Replace(".abi", ".bin");

            var byteCode = GetFileContent(outputFolder, binFilePath);

            if (string.IsNullOrEmpty(contractName))
                contractName = Path.GetFileNameWithoutExtension(abiFilePath);

            var abiConfig = new ABIConfiguration
            {
                ABI = abi,
                ByteCode = byteCode,
                ContractName = contractName
            };

            SetDefaults(abiConfig, baseNamespace, outputFolder);

            config.ABIConfigurations = new List<ABIConfiguration>();
            config.ABIConfigurations.Add(abiConfig);
            return config;
        }

        string FindFirstMatchingProjectFile(string folder)
        {
            foreach (var extension in CodeGenLanguageExt.ProjectFileExtensions.Values)
            {
                var files = Directory.GetFiles(folder, $"*{extension}");
                if (files.Length > 0)
                    return files[0];
            }

            return null;
        }

        private (string folder, string file) ResolveProjectFileAndFolder(string destinationProjectFolderOrFileName)
        {
            FileAttributes attr = File.GetAttributes(destinationProjectFolderOrFileName);

            if (attr.HasFlag(FileAttributes.Directory))
            {
                var file = FindFirstMatchingProjectFile(destinationProjectFolderOrFileName);
                return (destinationProjectFolderOrFileName, file);
            }

            var folder = Path.GetDirectoryName(destinationProjectFolderOrFileName);
            return (folder, destinationProjectFolderOrFileName);
        }

        public GeneratorConfiguration FromProject(string destinationProjectFolderOrFileName, string assemblyName)
        {
            (string projectFolder, string projectFilePath) =
                ResolveProjectFileAndFolder(destinationProjectFolderOrFileName);

            if (string.IsNullOrEmpty(projectFolder) ||
                !Directory.Exists(projectFolder))
                return null;

            var codeGenConfig = FromConfigFile(projectFolder, assemblyName);
            if (codeGenConfig != null)
                return codeGenConfig;

            if (string.IsNullOrEmpty(projectFilePath) ||
                !File.Exists(projectFilePath))
                return null;

            var language = DeriveCodeGenLanguage(projectFilePath);

            return FromAbiFilesInProject(projectFilePath, assemblyName, language);
        }


        private CodeGenLanguage DeriveCodeGenLanguage(string destinationProjectFileName)
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

        public GeneratorConfiguration FromAbiFilesInProject(string projectFileName, string assemblyName, CodeGenLanguage language)
        {
            var config = new GeneratorConfiguration();
            var projectFolder = Path.GetDirectoryName(projectFileName);
            var projectName = Path.GetFileNameWithoutExtension(projectFileName);
            var abiFiles = Directory.GetFiles(projectFolder, "*.abi", SearchOption.AllDirectories);
            var abiConfigurations = new List<ABIConfiguration>(abiFiles.Length);
            config.ABIConfigurations = abiConfigurations;

            foreach (var abiFile in abiFiles)
            {
                var abiConfig = CreateAbiConfiguration(abiFile, projectFolder, projectName, assemblyName, language);
                abiConfigurations.Add(abiConfig);
            }

            return config;
        }

        private static ABIConfiguration CreateAbiConfiguration(string abiFile, string projectFolder, string projectName, string assemblyName, CodeGenLanguage language)
        {
            var contractName = Path.GetFileNameWithoutExtension(abiFile);

            var binFileName = Path.Combine(Path.GetDirectoryName(abiFile), contractName + ".bin");
            var byteCode = File.Exists(binFileName) ? File.ReadAllText(binFileName) : string.Empty;

            var abiConfig = new ABIConfiguration
            {
                CodeGenLanguage = language,
                ABI = File.ReadAllText(abiFile),
                ByteCode = byteCode,
                ContractName = contractName
            };

            var defaultNamespace = CreateNamespaceFromAssemblyName(assemblyName);

            SetDefaults(abiConfig, defaultNamespace, projectFolder);
            return abiConfig;
        }

        public string DeriveConfigFilePath(string projectFolder)
        {
            return Path.Combine(projectFolder, ConfigFileName);
        }

        public GeneratorConfiguration FromConfigFile(string destinationProjectFolder, string assemblyName)
        {
            var configFilePath = DeriveConfigFilePath(destinationProjectFolder);

            if (!File.Exists(configFilePath))
                return null;

            var content = File.ReadAllText(configFilePath, Encoding.UTF8);
            var configuration = JsonConvert.DeserializeObject<GeneratorConfiguration>(content);

            if (configuration == null)
                return null;

            var defaultNamespace = CreateNamespaceFromAssemblyName(assemblyName);

            foreach (var abiConfiguration in configuration.ABIConfigurations)
            {
                SetDefaults(abiConfiguration, defaultNamespace, destinationProjectFolder);
            }

            return configuration;
        }

        private static string CreateNamespaceFromAssemblyName(string assemblyName)
        {
            return Path.GetFileNameWithoutExtension(assemblyName);
        }

        private static void SetDefaults(ABIConfiguration abiConfiguration, string defaultNamespace, string destinationProjectFolder)
        {
            if (string.IsNullOrEmpty(abiConfiguration.ContractName) && !string.IsNullOrEmpty(abiConfiguration.ABIFile))
                abiConfiguration.ContractName = Path.GetFileNameWithoutExtension(abiConfiguration.ABIFile);

            if (string.IsNullOrEmpty(abiConfiguration.ABI))
                abiConfiguration.ABI = GetFileContent(destinationProjectFolder, abiConfiguration.ABIFile);

            //by convention - look for bin folder in the same place as the abi
            if (string.IsNullOrEmpty(abiConfiguration.BinFile) && !string.IsNullOrEmpty(abiConfiguration.ABIFile))
            {
                abiConfiguration.BinFile = abiConfiguration.ABIFile.Replace(".abi", ".bin");
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
                abiConfiguration.CQSNamespace = abiConfiguration.ContractName + ".CQS";

            if (string.IsNullOrEmpty(abiConfiguration.DTONamespace))
                abiConfiguration.DTONamespace = abiConfiguration.ContractName + ".DTO";

            if (string.IsNullOrEmpty(abiConfiguration.ServiceNamespace))
                abiConfiguration.ServiceNamespace = abiConfiguration.ContractName + ".Service";
        }

        private static string GetFileContent(string destinationProjectFolder, string pathToFile)
        {
            if(string.IsNullOrEmpty(pathToFile))
                return null;

            if(Path.IsPathRooted(pathToFile))
                return File.Exists(pathToFile) ? File.ReadAllText(pathToFile) : null;

            if (pathToFile.Contains(".."))
            {
                var absolutePath = Path.GetFullPath(destinationProjectFolder + pathToFile);
                if (File.Exists(absolutePath))
                    return absolutePath;
            }

            var projectPath = Path.Combine(destinationProjectFolder, pathToFile);
            if(File.Exists(projectPath))
                return File.ReadAllText(projectPath);

            var matchingFiles = Directory.GetFiles(destinationProjectFolder, Path.GetFileName(pathToFile), SearchOption.AllDirectories);
            if(matchingFiles.Length > 0)
                return File.ReadAllText(matchingFiles.First());

            var fullPath = Path.GetFullPath(pathToFile);

            if(File.Exists(fullPath))
                return File.ReadAllText(fullPath);

            return null;
        }

    }
}