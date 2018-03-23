using Nethereum.ABI.JsonDeserialisation;
using System.Linq;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.UnitTests
{
    public static class TestUtils
    {
        public static ContractABI DeserializeABI(string abi)
        {
            var abiDeserialiser = new ABIDeserialiser();

            var baseContractABI = abiDeserialiser.DeserialiseContract(abi);

            var contractABI = new ContractABI
            {
                Constructor = new ConstructorABI
                {
                    InputParameters = baseContractABI.Constructor.InputParameters.Select(p => new Parameter(p.Type, p.Name, p.Order, p.SerpentSignature)).ToArray()
                },
                Functions = baseContractABI.Functions.Select(f =>
                {
                    return new FunctionABI(f.Name, f.Constant, f.Serpent)
                    {
                        InputParameters =
                            f.InputParameters.Select(p => new Parameter(p.Type, p.Name, p.Order, p.SerpentSignature))
                                .ToArray(),
                        OutputParameters =
                            f.OutputParameters.Select(p => new Parameter(p.Type, p.Name, p.Order, p.SerpentSignature))
                                .ToArray()
                    };
                }).ToArray(),
                Events = baseContractABI.Events.Select(e =>
                {
                    return new EventABI(e.Name)
                    {
                        InputParameters = e.InputParameters.Select(p => new Parameter(p.Type, p.Name, p.Order, p.SerpentSignature)).ToArray()
                    };
                }).ToArray()
            };

            return contractABI;
        }
    }
}
