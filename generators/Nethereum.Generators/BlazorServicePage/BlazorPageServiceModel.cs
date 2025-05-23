using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class BlazorPageServiceModel:TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public string ContractName { get; }

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
                            string cqsNamespace, string functionOutputNamespace, string sharedTypesFullNamespace) :
            base(@namespace, contractName, "Page") // we need to duplicate the name due to typescript
        {
            System.Console.WriteLine($"Initialising constructor BlazorPageServiceModel ContractName: {ContractName}");
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            ContractName = contractName;
            FunctionOutputNamespace = functionOutputNamespace;
            InitialiseNamespaceDependencies();


            if (!string.IsNullOrEmpty(cqsNamespace))
                NamespaceDependencies.Add(cqsNamespace);

            if (!string.IsNullOrEmpty(functionOutputNamespace))
                NamespaceDependencies.Add(functionOutputNamespace);

            if (!string.IsNullOrEmpty(sharedTypesFullNamespace))
                NamespaceDependencies.Add(sharedTypesFullNamespace);
            System.Console.WriteLine($"Finished Initialising constructor BlazorPageServiceModel ContractName: {ContractName}");
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System.Numerics",
                "Nethereum.UI" });
        }
    }
}
 