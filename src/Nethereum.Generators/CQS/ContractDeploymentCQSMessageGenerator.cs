using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageGenerator: ClassGeneratorBase<ContractDeploymentCQSMessageTemplate, ContractDeploymentCQSMessageModel>
    {
        public ContractDeploymentCQSMessageGenerator(ConstructorABI abi, string namespaceName, string byteCode, string contractName)
        {
            ClassModel = new ContractDeploymentCQSMessageModel(abi, namespaceName, byteCode, contractName);
            ClassTemplate = new ContractDeploymentCQSMessageTemplate(ClassModel);
        }

    }
}