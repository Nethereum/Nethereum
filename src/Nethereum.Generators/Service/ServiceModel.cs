using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class ServiceModel:TypeMessageModel
    {
        public string CLASSNAME_SUFFIX = "Service";
        public ContractABI ContractABI { get; }
        public string ContractName { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public ServiceModel(ContractABI contractABI, string contractName, 
                            string byteCode, string @namespace, 
                            string cqsNamespace, string functionOutputNamespace):base(@namespace)
        {
            ContractABI = contractABI;
            ContractName = contractName;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            ContractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel(contractABI.Constructor, cqsNamespace, byteCode, contractName);
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
 