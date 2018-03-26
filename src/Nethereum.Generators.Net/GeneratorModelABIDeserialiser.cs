using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Generators.Model;
using Newtonsoft.Json;

namespace Nethereum.Generators.Net
{
    public class GeneratorModelABIDeserialiser
    {
        public ContractABI DeserialiseABI(string abi)
        {
            var abiDeserialiser = new ABIDeserialiser();

            var baseContractABI = abiDeserialiser.DeserialiseContract(abi);

            var contractABI = new ContractABI
            {
                Constructor = new ConstructorABI
                {
                    InputParameters = baseContractABI.Constructor.InputParameters
                        .Select(p => new Parameter(p.Type, p.Name, p.Order, p.SerpentSignature)).ToArray()
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
                        InputParameters =
                            e.InputParameters.Select(p => new Parameter(p.Type, p.Name, p.Order){Indexed = p.Indexed})
                                .ToArray()
                    };
                }).ToArray()
            };

            return contractABI;
        }
    }
}