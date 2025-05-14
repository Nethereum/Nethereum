using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.Service;

namespace Nethereum.Generators.BlazorServicePage
{
    public class BlazorPageServiceModel:TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public string ContractName { get; }

        public const string NAME_SUFFIX = "Page";

        public string GetServiceTypeName()
        {
           return ServiceModel.GetDefaultTypeName(ContractName);
        }

        public string GetContractDeploymentTypeName()
        {
            return ContractDeploymentCQSMessageModel.GetDefaultTypeName(ContractName);
        }

        public BlazorPageServiceModel(ContractABI contractABI, string contractName, 
                            string @namespace, 
                            string cqsNamespace, string functionOutputNamespace, string shareNamespace):
            base(@namespace, contractName, NAME_SUFFIX)
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            ContractName = contractName;
            FunctionOutputNamespace = functionOutputNamespace;
            InitialiseNamespaceDependencies();

            if (!string.IsNullOrEmpty(cqsNamespace))
                NamespaceDependencies.Add(cqsNamespace);

            if (!string.IsNullOrEmpty(functionOutputNamespace))
                NamespaceDependencies.Add(functionOutputNamespace);

            if (!string.IsNullOrEmpty(shareNamespace))
                NamespaceDependencies.Add(shareNamespace);
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System.Numerics",
                "Nethereum.UI" });
        }
    }
}
 