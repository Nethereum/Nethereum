using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Tests.CSharp
{
    public class ContractDeploymentCQSMessageGeneratorTests: GeneratorTestBase<ContractDeploymentCQSMessageGenerator>
    {
        static ContractDeploymentCQSMessageGenerator CreateGenerator()
        {
            var constructorAbi = new ConstructorABI { InputParameters = new[] { new ParameterABI("uint256", "totalSupply") } };

            return new ContractDeploymentCQSMessageGenerator(
                constructorAbi, namespaceName: "DefaultNamespace", byteCode: "0x123456789", contractName: "StandardContract");
        }

        public ContractDeploymentCQSMessageGeneratorTests():
            base(CreateGenerator(), "CSharp", "ContractDeploymentCqsMessage01.csharp.txt", "StandardContractDeployment.cs")
        {
        }
    }


}