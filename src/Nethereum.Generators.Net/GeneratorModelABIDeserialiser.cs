using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Generators.Model;
using System.Linq;

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
                Constructor = GetConstructor(baseContractABI),
                Functions = baseContractABI.Functions.Select(f =>
                {
                    return new FunctionABI(f.Name, f.Constant, f.Serpent)
                    {
                        InputParameters =
                            f.InputParameters.Select(p => new ParameterABI(p.Type, p.Name, p.Order))
                                .ToArray(),
                        OutputParameters =
                            f.OutputParameters.Select(p => new ParameterABI(p.Type, p.Name, p.Order))
                                .ToArray()
                    };
                }).ToArray(),
                Events = baseContractABI.Events.Select(e =>
                {
                    return new EventABI(e.Name)
                    {
                        InputParameters =
                            e.InputParameters.Select(p => new ParameterABI(p.Type, p.Name, p.Order){Indexed = p.Indexed})
                                .ToArray()
                    };
                }).ToArray()
            };

            var structDeserialiser = new StructABIDeserialiser();
            var structs = structDeserialiser.GetStructsFromAbi(abi);
            contractABI.Structs = structs;
            structDeserialiser.SetTupleTypeSameAsName(contractABI);

            return contractABI;
        }

        private static ConstructorABI GetConstructor(ABI.Model.ContractABI baseContractABI)
        {
            if (baseContractABI.Constructor == null) return new ConstructorABI();
            return new ConstructorABI
            {
                InputParameters = baseContractABI.Constructor.InputParameters
                    .Select(p => new ParameterABI(p.Type, p.Name, p.Order)).ToArray()
            };
        }
    }
}