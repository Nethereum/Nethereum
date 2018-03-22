using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageModel:TypeMessageModel
    {
        public ConstructorABI ConstructorABI { get; }
        public string ByteCode { get; }

        public ContractDeploymentCQSMessageModel(ConstructorABI constructorABI, string @namespace, string byteCode, string contractName)
            :base(@namespace, contractName, "Deployment")
        {
            ConstructorABI = constructorABI;
            ByteCode = byteCode;
        }
    }
}