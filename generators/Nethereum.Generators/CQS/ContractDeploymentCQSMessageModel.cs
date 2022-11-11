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
            InitisialiseNamespaceDependencies();
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new []{"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.Contracts", "Nethereum.ABI.FunctionEncoding.Attributes"});
        }
    }
}