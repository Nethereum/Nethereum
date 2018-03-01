using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOGenerator: ABIServiceBase
    {
        public FunctionOutputDTOTemplate template;

        public FunctionOutputDTOGenerator()
        {
            template = new FunctionOutputDTOTemplate();
        }

        public string GenerateFullClass(FunctionABI abi, string namespaceName)
        {
            return template.GenerateFullClass(abi, namespaceName);
        }

        public string GenerateFullClass(string abi, string namespaceName)
        {
            return template.GenerateFullClass(GetFirstFunction(abi), namespaceName);
        }

        public string GenerateClass(FunctionABI abi)
        {
            return template.GenerateClass(abi);
        }

        public string GenerateClass(string abi)
        {
            return GenerateClass(GetFirstFunction(abi));
        }
    }
}