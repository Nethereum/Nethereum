using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Console
{
    public class ConsoleModel:TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public ConsoleModel(ContractABI contractABI, string contractName, 
                            string byteCode, string @namespace, 
                            string cqsNamespace, string functionOutputNamespace):
            base(@namespace, contractName, "Console")
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            ContractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel(contractABI.Constructor, cqsNamespace, byteCode, contractName);
            InitialiseNamespaceDependencies();

            if(!string.IsNullOrEmpty(cqsNamespace))
                NamespaceDependencies.Add(cqsNamespace);

            if(!string.IsNullOrEmpty(functionOutputNamespace))
                NamespaceDependencies.Add(functionOutputNamespace);
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System",
                "System.Threading",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "System.Numerics",
                "Nethereum.Hex.HexTypes",
                "Nethereum.ABI.FunctionEncoding.Attributes",
                "Nethereum.Web3",
                "Nethereum.RPC.Eth.DTOs",
                "Nethereum.Contracts.CQS",
                "Nethereum.Contracts.ContractHandlers",
                "Nethereum.Contracts.Extensions",
                "Nethereum.Web3.Accounts",
                "Nethereum.Contracts",
                "System.Threading" });
        }
    }
}
 