using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.Net;
using Newtonsoft.Json.Converters;

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

        public CodeGenLanguage CodeGenLanguage { get; set; }
        public string BaseOutputPath { get; set; }

        public ContractABI CreateContractABI()
        {
            return new GeneratorModelABIDeserialiser().DeserialiseABI(ABI);
        }
    }
}