using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generators.Net;
using System;
using System.IO;
using Nethereum.Generators;

namespace Nethereum.Generator.Console
{
    public class GenerateFromProjectCommand : CommandLineApplication
    {
        private readonly CommandOption _projectPath;
        private readonly CommandOption _assemblyName;

        public GenerateFromProjectCommand()
        {
            Name = "gen-fromproject";
            Description = "Generates a Nethereum (c#) code based based on the abi";
            _projectPath = Option("-p | --projectPath", "The project file name and path", CommandOptionType.SingleValue);
            _assemblyName = Option("-a | --assemblyName", "The output assembly name for the project", CommandOptionType.SingleValue);
            HelpOption("-? | -h | --help");
            OnExecute((Func<int>)RunCommand);
        }

        private int RunCommand()
        {
            var projectPath = _projectPath.Value();
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                System.Console.WriteLine("A project file needs was not specified");
                return 1;
            }

            var assemblyName = _assemblyName.Value();
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                System.Console.WriteLine("An assembly name was not specified");
                return 1;
            }

            if (!File.Exists(projectPath))
            {
                System.Console.WriteLine("The project file does not exist");
                return 1;
            }

            GenerateCode(projectPath, assemblyName);

            return 0;
        }

        private static void GenerateCode(string projectPath, string assemblyName)
        {
            var codeGenConfigurationFactory = new GeneratorConfigurationFactory();
            var config = codeGenConfigurationFactory.FromProject(projectPath, assemblyName);

            if (config?.ABIConfigurations == null)
                return;

            foreach (var item in config.ABIConfigurations)
            {
                GenerateFilesForItem(item);
            }
        }

        private static void GenerateFilesForItem(ABIConfiguration item)
        {
            var generator = new ContractProjectGenerator(
                item.CreateContractABI(),
                item.ContractName,
                item.ByteCode,
                item.BaseNamespace,
                item.ServiceNamespace,
                item.CQSNamespace,
                item.DTONamespace,
                item.BaseOutputPath,
                Path.DirectorySeparatorChar.ToString(),
                item.CodeGenLanguage
            );

            var generatedFiles = generator.GenerateAll();
            GeneratedFileWriter.WriteFilesToDisk(generatedFiles);
        }
    }
}
