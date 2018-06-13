using System;
using Nethereum.Generator.Console.Configuration;
using Nethereum.Generators;
using Nethereum.Generators.Net;
using System.IO;

namespace Nethereum.Generator.Console.Generation
{
    public class CodeGenerationWrapper : ICodeGenerationWrapper
    {
        private readonly IGeneratorConfigurationFactory _codeGenConfigurationFactory;
        private readonly IGeneratedFileWriter _generatedFileWriter;

        public CodeGenerationWrapper()
        {
            _codeGenConfigurationFactory = new GeneratorConfigurationFactory();
            _generatedFileWriter = new GeneratedFileWriter();
        }

        public CodeGenerationWrapper(IGeneratorConfigurationFactory generatorConfigurationFactory, IGeneratedFileWriter generatedFileWriter)
        {
            _codeGenConfigurationFactory = generatorConfigurationFactory;
            _generatedFileWriter = generatedFileWriter;
        }

        public void FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder)
        {
            var config = _codeGenConfigurationFactory.FromAbi(contractName, abiFilePath, binFilePath, baseNamespace, outputFolder);
            Generate(config);
        }

        public void FromProject(string projectPath, string assemblyName)
        {
            var config = _codeGenConfigurationFactory.FromProject(projectPath, assemblyName);
            Generate(config);
        }

        private void Generate(GeneratorConfiguration config)
        {
            if (config?.ABIConfigurations == null)
                return;

            foreach (var item in config.ABIConfigurations)
            {
                GenerateFilesForItem(item);
            }
        }

        private void GenerateFilesForItem(ABIConfiguration item)
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
            _generatedFileWriter.WriteFiles(generatedFiles);
        }
    }
}
