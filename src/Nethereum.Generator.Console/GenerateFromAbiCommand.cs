using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generators.Net;
using System;
using System.IO;
using Nethereum.Generators;
using Nethereum.Generator.Console.Configuration;

namespace Nethereum.Generator.Console
{
    public class GenerateFromAbiCommand : CommandLineApplication
    {
        private readonly CommandOption _contractName;
        private readonly CommandOption _abiFilePath;
        private readonly CommandOption _binCodeFilePath;
        private readonly CommandOption _outputFolder;
        private readonly CommandOption _baseNamespace;

        public GenerateFromAbiCommand()
        {
            Name = "gen-fromabi";
            Description = "Generates a Nethereum (c#) code based based on the abi";
            _contractName = Option("-cn | --contractName", "The contract name", CommandOptionType.SingleValue);
            _abiFilePath = Option("-abi | --abiPath", "The abi file and path", CommandOptionType.SingleValue);
            _binCodeFilePath = Option("-bin | --binPath", "The bin file and path", CommandOptionType.SingleValue);
            _outputFolder = Option("-o | --outputPath", "Theoutput path for the generated code", CommandOptionType.SingleValue);
            _baseNamespace = Option("-ns | --namespace", "The base namespace for the generated code", CommandOptionType.SingleValue);
            HelpOption("-? | -h | --help");
            OnExecute((Func<int>)RunCommand);
        }

        private int RunCommand()
        {
            var abiFilePath = _abiFilePath.Value();
            if (string.IsNullOrWhiteSpace(abiFilePath))
            {
                System.Console.WriteLine("An abi file must be specified");
                return 1;
            }

            var outputFolder = (_outputFolder).Value();
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                System.Console.WriteLine("An output folder must be specified");
                return 1;
            }

            var baseNamespace = _baseNamespace.Value();
            if (string.IsNullOrWhiteSpace(baseNamespace))
            {
                System.Console.WriteLine("A base namespace must be specified");
                return 1;
            }

            var contractName = _contractName.Value();

            GenerateCode(contractName, abiFilePath, _binCodeFilePath.Value(), baseNamespace, outputFolder);

            return 0;
        }

        private static void GenerateCode(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder)
        {
            var codeGenConfigurationFactory = new GeneratorConfigurationFactory();
            var config = codeGenConfigurationFactory.FromAbi(contractName, abiFilePath, binFilePath, baseNamespace, outputFolder);

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
