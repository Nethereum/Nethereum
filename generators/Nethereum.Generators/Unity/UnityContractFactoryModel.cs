using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Unity
{
    public class UnityContractFactoryModel : TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public UnityContractFactoryModel(ContractABI contractABI, string contractName,
                            string byteCode, string @namespace,
                            string cqsNamespace, string functionOutputNamespace) :
            base(@namespace, contractName, "ContractRequestFactory")
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            InitialiseNamespaceDependencies();

            if (!string.IsNullOrEmpty(cqsNamespace))
                NamespaceDependencies.Add(cqsNamespace);

            if (!string.IsNullOrEmpty(functionOutputNamespace))
                NamespaceDependencies.Add(functionOutputNamespace);
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System",
                "System.Threading.Tasks",
                "System.Collections",
                "System.Collections.Generic",
                "System.Numerics",
                "System.Threading",
                "Nethereum.RPC.Eth.DTOs",
                "Nethereum.Unity.Contracts",
                "Newtonsoft.Json" });
        }
    }
}
 