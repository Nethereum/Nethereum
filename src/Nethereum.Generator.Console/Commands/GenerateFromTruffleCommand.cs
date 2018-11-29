using System;
using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generator.Console.Generation;

namespace Nethereum.Generator.Console.Commands
{
    public class GenerateFromTruffleCommand : CommandLineApplication
    {
        private readonly CommandOption _directory;
        private readonly CommandOption _outputFolder;
        private readonly CommandOption _baseNamespace;
        private readonly CommandOption _singleFile;
        public ICodeGenerationWrapper CodeGenerationWrapper { get; set; }

        public GenerateFromTruffleCommand()
        {
            Name = "from-truffle";
            Description = "Generates Nethereum code based based on a collection of compiled contracts.";
            _directory = Option("-d | --directory", "The directory containing the compiled contracts (Mandatory)", CommandOptionType.SingleValue);
            _outputFolder = Option("-o | --outputPath", "The output path for the generated code (Mandatory)", CommandOptionType.SingleValue);
            _baseNamespace = Option("-ns | --namespace", "The base namespace for the generated code (Mandatory)", CommandOptionType.SingleValue);
            _singleFile = Option("-sf | --SingleFile", "Generate the message definition in a single file (Optional - default is true)", CommandOptionType.SingleValue);
            OnExecute((Func<int>)RunCommand);
            CodeGenerationWrapper = new CodeGenerationWrapper();

            this.AddHelpOption();
        }

        private int RunCommand()
        {
            var directoryPath = _directory.Value();
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                System.Console.WriteLine("A directory path must be specified");
                return 1;
            }

            var outputFolder = _outputFolder.Value();
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

            var singleFile = true;

            if (_singleFile.HasValue())
            {
                bool.TryParse(_singleFile.Value(), out singleFile);
            }

            CodeGenerationWrapper.FromTruffle(directoryPath, baseNamespace, outputFolder, singleFile);

            return 0;
        }
    }
}
