using System.Collections.Generic;

namespace Nethereum.Generator.Console.sample
{
    public class StandardContractConfigCreator
    {
        public static void CreateTestGeneratorConfigFile(string outputFilePath)
        {
            var config = new GeneratorConfiguration
            {
                ABIConfigurations = new List<ABIConfiguration>
                {
                    new ABIConfiguration
                    {
                        ContractName = "StandardContract",
                        ABIFile = "StandardContract.abi",
                        BinFile = "StandardContract.bin"
                    }
                }
            };

            config.SaveToJson(outputFilePath);
        }
    }
}
