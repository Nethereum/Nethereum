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
        public string SharedTypesFullNamespace { get; }
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }


        public ServiceModel(ContractABI contractABI, string contractName, 
                            string byteCode, string @namespace, 
                            string cqsNamespace, string functionOutputNamespace, string sharedTypesFullNamespace) :
            base(@namespace, contractName, "Service") // we need to duplicate the name due typescript
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            SharedTypesFullNamespace = sharedTypesFullNamespace;
            ContractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel(contractABI.Constructor, cqsNamespace, byteCode, contractName);
            InitialiseNamespaceDependencies();

            if(!string.IsNullOrEmpty(cqsNamespace))
                NamespaceDependencies.Add(cqsNamespace);

            if(!string.IsNullOrEmpty(functionOutputNamespace))
                NamespaceDependencies.Add(functionOutputNamespace);

            if(!string.IsNullOrEmpty(sharedTypesFullNamespace))
                NamespaceDependencies.Add(sharedTypesFullNamespace);
        }

        public static string GetDefaultTypeName(string name)
        {
           return GetDefaultTypeName(name, "Service");
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "System.Numerics",
                "Nethereum.Hex.HexTypes",
                "Nethereum.ABI.FunctionEncoding.Attributes",
                "Nethereum.Web3",
                "Nethereum.RPC.Eth.DTOs",
                "Nethereum.Contracts.CQS",
                "Nethereum.Contracts.ContractHandlers",
                "Nethereum.Contracts",
                "System.Threading" });
        }
    }
}
 