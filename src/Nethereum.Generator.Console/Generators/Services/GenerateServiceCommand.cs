using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.Generator.Console.Generators.Services
{
    public class GenerateServiceCommand : CommandLineApplication
    {
        private readonly CommandOption _abiFile;
        private readonly CommandOption _binFile;
        private readonly CommandOption _contractName;
        private readonly CommandOption _namespaceName;


        public GenerateServiceCommand()
        {
            Name = "gen-service";
            Description = "Generates a Nethereum (c#) contract service based on the abi";
            _abiFile = Option("-af | --abiFile", "The file containing the abi", CommandOptionType.SingleValue);
            _binFile = Option("-bf | --binFile", "The file containing the bin", CommandOptionType.SingleValue);
            _contractName = Option("-c | --contractName", "Optional: The contract name used as the name of the Service", CommandOptionType.SingleValue);
            _namespaceName = Option("-n | --namespace", "Optional: The namespace name for the Service", CommandOptionType.SingleValue);
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

            var contractName = _contractName.Value();
            if (string.IsNullOrWhiteSpace(contractName))
            {
                contractName = ServiceModel.DEFAULT_CONTRACTNAME;
            }
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

            CodeGeneratorService.GenerateFileAsync("Service.cshtml",
                new ServiceModel(abi, byteCode, contractName, namespaceName), contractName + "Service.cs").Wait();

            return 0;
        }
    }
}