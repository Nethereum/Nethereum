using System;
using System.IO;
using Nethereum.Generators;
using Nethereum.Generators.Core;
using Nethereum.Generators.Net;

namespace Nethereum.Generator.Console.Configuration
{
    public class ABIConfiguration
    {
        public string ContractName { get; set; }

        public string ABI { get; set; }

        public string ABIFile { get; set; }

        public string ByteCode { get; set; }

        public string BinFile { get; set; }

        public string BaseNamespace { get; set; }

        public string CQSNamespace { get; set; }

        public string DTONamespace { get; set; }

        public string ServiceNamespace { get; set; }

        public string BaseOutputPath { get; set; }

        public CodeGenLanguage CodeGenLanguage { get; set; }

        public ContractProjectGenerator GetContractGenerator(string defaultNamespace, string projectFolder)
        {
            //by convention - look for bin folder in the same place as the abi
            if (string.IsNullOrEmpty(BinFile) && !string.IsNullOrEmpty(ABIFile))
            {
                if (ABIFile.EndsWith(".abi", StringComparison.InvariantCultureIgnoreCase))
                {
                    BinFile = $"{ABIFile.Substring(0, ABIFile.Length - 4)}.bin";
                }
            }

            var abiString = ABI ?? GeneratorConfigurationUtils.GetFileContent(projectFolder, ABIFile);
            var byteCode = ByteCode ?? GeneratorConfigurationUtils.GetFileContent(projectFolder, BinFile);
            var abi = new GeneratorModelABIDeserialiser().DeserialiseABI(abiString);

            return new ContractProjectGenerator(
                abi,
                ContractName,
                byteCode,
                BaseNamespace ?? defaultNamespace,
                ServiceNamespace ?? $"{ContractName}.Service",
                CQSNamespace ?? $"{ContractName}.CQS",
                DTONamespace ?? $"{ContractName}.DTO",
                BaseOutputPath ?? projectFolder,
                Path.DirectorySeparatorChar.ToString(),
                CodeGenLanguage
            );
        }
    }
}