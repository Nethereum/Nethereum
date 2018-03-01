using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class FunctionCQSMessageGenerator : ABIServiceBase
    {
        public FunctionCQSMessageTemplate template;

        public FunctionCQSMessageGenerator()
        {
            template = new FunctionCQSMessageTemplate();
        }

        public string GenerateFullClass(FunctionABI abi, string namespaceName, string namespaceFunctionOutput)
        {
            return template.GenerateFullClass(abi, namespaceName, namespaceFunctionOutput);
        }

        public string GenerateFullClass(string abi, string namespaceName, string namespaceFunctionOutput)
        {
            return template.GenerateFullClass(GetFirstFunction(abi), namespaceName, namespaceFunctionOutput);
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