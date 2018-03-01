using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generator.Console.Generators.Services;

namespace Nethereum.Generator.Console.Generators.CQS
{
    public class GenerateCQSMessagesCommand : CommandLineApplication
    {
        private readonly CommandOption _abiFile;
        private readonly CommandOption _binFile;
        private readonly CommandOption _namespaceName;
        private readonly CommandOption _functionName;


        public GenerateCQSMessagesCommand()
        {
            Name = "gen-cqsFunctionMessage";
            Description = "Generates a Nethereum (c#) contract service based on the abi";
            _abiFile = Option("-af | --abiFile", "The file containing the abi", CommandOptionType.SingleValue);
            _binFile = Option("-bf | --binFile", "The file containing the bin", CommandOptionType.SingleValue);
            _namespaceName = Option("-n | --namespace", "Optional: The namespace name for the Service", CommandOptionType.SingleValue);
            _functionName = Option("-f | --functionName", "Optional: The single function / message name to generate", CommandOptionType.SingleValue);
            HelpOption("-? | -h | --help");
            OnExecute((Func<int>)RunCommand);
        }

        private int RunCommand()
        {
            var abiFile = _abiFile.Value();
            if (string.IsNullOrWhiteSpace(abiFile))
            {
                System.Console.WriteLine("A abi file needs was not specified");
                return 1;
            }

            var binFile = _binFile.Value();
            if (string.IsNullOrWhiteSpace(binFile))
            {
                System.Console.WriteLine("A bin file needs was not specified");
                return 1;
            }

            var functionName = _functionName.Value();
           
            var namespaceName = _namespaceName.Value();
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                namespaceName = ServiceModel.DEFAULT_NAMESPACE;
            }

            var abi = "";
            var byteCode = "";
            if (!File.Exists(abiFile))
            {
                System.Console.WriteLine(("Abi file not found"));
                return 1;
            }

            if (!File.Exists(binFile))
            {
                System.Console.WriteLine(("Bin file not found"));
                return 1;
            }

            using (var file = File.OpenText(abiFile))
            {
                abi = file.ReadToEnd();
            }

            using (var file = File.OpenText(binFile))
            {
                byteCode = file.ReadToEnd();
            }

            string directoryPath = null;
            if (!Directory.Exists("FunctionMessages"))
            {
                directoryPath = Directory.CreateDirectory("FunctionMessages").FullName;
            }
            else
            {
                directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "FunctionMessages");
            }

            if (!String.IsNullOrEmpty(functionName))
            {
                var model = new CQSMessageModel(abi, byteCode, functionName, namespaceName);
                CodeGeneratorService.GenerateFileAsync("Generators.CQS.CQSMessage.cshtml",
                    model, Path.Combine(directoryPath, model.GetFunctionMessageName() + ".cs")).Wait();
            }
            else
            {
                var model = new CQSMessageModel(abi, byteCode, namespaceName);
                foreach (var function in model.Contract.Functions)
                {
                    model.FunctionName = function.Name;
                    CodeGeneratorService.GenerateFileAsync("Generators.CQS.CQSMessage.cshtml",
                        model, Path.Combine(directoryPath, model.GetFunctionMessageName() + ".cs")).Wait();
                }
            }
            return 0;
        }
    }
}