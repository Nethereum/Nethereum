using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.MudService
{
    public class MudServiceModel:TypeMessageModel
    {
        public ContractABI ContractABI { get; }
        public string CQSNamespace { get; }
        public string FunctionOutputNamespace { get; }
        public string MudNamespace { get; }
        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public MudServiceModel(ContractABI contractABI, string contractName, 
                            string byteCode, string @namespace, 
                            string cqsNamespace, string functionOutputNamespace, string mudNamespace = ""):
            base(@namespace, contractName, "Service")
        {
            ContractABI = contractABI;
            CQSNamespace = cqsNamespace;
            FunctionOutputNamespace = functionOutputNamespace;
            MudNamespace = mudNamespace;
            ContractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel(contractABI.Constructor, cqsNamespace, byteCode, contractName);
            InitialiseNamespaceDependencies();

            if(!string.IsNullOrEmpty(cqsNamespace))
                NamespaceDependencies.Add(cqsNamespace);

            if(!string.IsNullOrEmpty(functionOutputNamespace))
                NamespaceDependencies.Add(functionOutputNamespace);
        }

        public override string GetFileName(string name)
        {
            return GetTypeName(name) + "MudExt.gen." + CodeGenLanguage.GetCodeOutputFileExtension();
        }

        public string GetResourceClassName()
        {
            return this.GetTypeName(this.Name) + "Resource";
        }

        public string GetSystemName()
        {
            var contractClassName = CommonGenerators.GenerateClassName(this.Name);
            //if (contractClassName.EndsWith("System"))
            //{
            //    return contractClassName.Substring(0, contractClassName.Length - "System".Length);
            //}
            return contractClassName;
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[] {
                "System",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "System.Numerics",
                "Nethereum.Mud.Contracts.Core.Systems",
                "Nethereum.Hex.HexTypes",
                "Nethereum.ABI.FunctionEncoding.Attributes",
                "Nethereum.ABI.Model",
                "Nethereum.ABI.FunctionEncoding",
                "Nethereum.Contracts.Create2Deployment",
                "Nethereum.Mud",
                "Nethereum.Web3",
                "Nethereum.RPC.Eth.DTOs",
                "Nethereum.Contracts.CQS",
                "Nethereum.Contracts.ContractHandlers",
                "Nethereum.Contracts",
                "System.Threading" });
        }
    }
}
 