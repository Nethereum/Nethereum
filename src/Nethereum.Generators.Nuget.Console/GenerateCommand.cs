using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generators.Net;
using Nethereum.Generators.Nuget.Console.Configuration;
using System;
using System.IO;

namespace Nethereum.Generators.Nuget.Console
{
    public class GenerateCommand : CommandLineApplication
    {
        private readonly CommandOption _projectPath;

        public GenerateCommand()
        {
            Name = "generate";
            Description = "Generates a Nethereum (c#) code based based on the abi";
            _projectPath = Option("-p | --projectPath", "The project file name and path", CommandOptionType.SingleValue);
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

            if (!File.Exists(projectPath))
            {
                System.Console.WriteLine("The project file does not exist");
                return 1;
            }

            GenerateCode(projectPath);

            return 0;
        }

        private static void GenerateCode(string projectPath)
        {
            var codeGenConfigurationFactory = new GeneratorConfigurationFactory();
            var config = codeGenConfigurationFactory.ReadFromProjectPath(projectPath);

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
