using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class AllMessagesModel:FileModel
    {
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public AllMessagesModel(string contractName, 
                             string @namespace, string dtoNamespace, string sharedNamespace):
            base(@namespace, contractName + "Definition")
        {
            InitialiseNamespaceDependencies(dtoNamespace, sharedNamespace);
        }

        private void InitialiseNamespaceDependencies(string dtoNamespace, string sharedNamespace)
        {
            NamespaceDependencies.AddRange(new[] {
                "System",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "System.Numerics",
                "Nethereum.Hex.HexTypes",
                "Nethereum.ABI.FunctionEncoding.Attributes",
                "Nethereum.RPC.Eth.DTOs",
                "Nethereum.Contracts.CQS",
                "Nethereum.Contracts",
                "System.Threading",
                dtoNamespace,
                sharedNamespace});
        }
    }
}
 