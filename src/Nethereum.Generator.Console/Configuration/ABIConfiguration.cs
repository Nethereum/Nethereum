using System;
using Nethereum.Generator.Console.Models;

namespace Nethereum.Generator.Console.Configuration
{
    public class ABIConfiguration
    {
        public string ContractName { get; set; }

        public string ABIFile { get; set; }

        public string BinFile { get; set; }

        public ContractDefinition GetContractDefinition(string projectFolder)
        {
            //by convention - look for bin folder in the same place as the abi
            if (string.IsNullOrEmpty(BinFile) && !string.IsNullOrEmpty(ABIFile))
            {
                if (ABIFile.EndsWith(".abi", StringComparison.InvariantCultureIgnoreCase))
                {
                    BinFile = $"{ABIFile.Substring(0, ABIFile.Length - 4)}.bin";
                }
            }

            var abi = GeneratorConfigurationUtils.GetFileContent(projectFolder, ABIFile);
            var byteCode = GeneratorConfigurationUtils.GetFileContent(projectFolder, BinFile);

            return new ContractDefinition(abi)
            {
                ContractName = ContractName,
                Bytecode = byteCode
            };
        }
    }
}