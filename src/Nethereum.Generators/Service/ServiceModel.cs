using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class ServiceModel:TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public ServiceModel(ContractABI contractABI, string contractName, 
                            string byteCode, string @namespace, 
                            string cqsNamespace, string functionOutputNamespace):
            base(@namespace, contractName, "Service")
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            ContractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel(contractABI.Constructor, cqsNamespace, byteCode, contractName);
        }
    }
}
 