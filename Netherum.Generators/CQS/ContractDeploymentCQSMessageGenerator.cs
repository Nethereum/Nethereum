using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class ContractDeploymentCQSMessageGenerator : ABIServiceBase
    {
        public ContractDeploymentCQSMessageTemplate template;

        public ContractDeploymentCQSMessageGenerator()
        {
            template = new ContractDeploymentCQSMessageTemplate();
        }

        public string GenerateFullClass(ConstructorABI abi, string namespaceName, string byteCode, string contractName)
        {
            return template.GenerateFullClass(abi, namespaceName, byteCode, contractName);
        }

        public string GenerateFullClass(string abi, string namespaceName, string byteCode, string contractName)
        {
            return template.GenerateFullClass(GetConstructorABI(abi), namespaceName, byteCode, contractName);
        }

        public string GenerateClass(ConstructorABI abi, string byteCode, string contractName)
        {
            return template.GenerateClass(abi, byteCode, contractName);
        }

        public string GenerateClass(string abi, string byteCode, string contractName)
        {
            return GenerateClass(GetConstructorABI(abi), byteCode, contractName);
        }
    }
}