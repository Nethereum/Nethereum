using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.XUnit
{
    public class SimpleTestModel:TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }

        public SimpleTestModel(ContractABI contractABI, string contractName, 
                            string @namespace, 
                            string cqsNamespace, string functionOutputNamespace):
            base(@namespace, contractName, "Test")
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            InitisialiseNamespaceDependencies();
            NamespaceDependencies.Add(cqsNamespace);
            NamespaceDependencies.Add(functionOutputNamespace);
        }

        private void InitisialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "System.Numerics",
                "System.Threading",
                "Nethereum.Hex.HexTypes",
                "Nethereum.ABI.FunctionEncoding.Attributes",
                "Nethereum.Web3",
                "Nethereum.RPC.Eth.DTOs",
                "Nethereum.Contracts.CQS",
                "Nethereum.Contracts.IntegrationTester",
                "Xunit",
                "Xunit.Abstractions"
             });
        }
    }
}
 