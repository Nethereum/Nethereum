using Nethereum.Generators.Model;
using Nethereum.Generators.Net;
using Newtonsoft.Json.Linq;

namespace Nethereum.Generator.Console.Configuration
{
    public class CompiledContract
    {
        public string ContractName { get; set; }

        public ContractABI SingleAbi
        {
            get
            {
                if (!string.IsNullOrEmpty(AbiString))
                {
                    return new GeneratorModelABIDeserialiser().DeserialiseABI(AbiString);
                }

                return new GeneratorModelABIDeserialiser().DeserialiseABI(Abi.ToString());
            }
        }

        public string Bytecode { get; set; }

        public string AbiString { get; set; }

        public JToken Abi { get; set; }
    }
}
