using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageModel:TypeMessageModel
    {
        private const string CLASSNAME_SUFFIX = "Deployment";
        public ConstructorABI ConstructorABI { get; }
        public string ByteCode { get; }
        public string ContractName { get; }

        public ContractDeploymentCQSMessageModel(ConstructorABI constructorABI, string @namespace, string byteCode, string contractName):base(@namespace)
        {
            ConstructorABI = constructorABI;
            ByteCode = byteCode;
            ContractName = contractName;
        }
        
        protected override string GetClassNameSuffix()
        {
            return CLASSNAME_SUFFIX;
        }

        protected override string GetBaseName()
        {
            return ContractName;
        }
    }
}