
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using System;

namespace Nethereum.Generators.Service
{
    
    public class BlazorPageServiceGenerator : ClassGeneratorBase<BlazorPageServiceModel>
    {
        public  ContractABI ContractABI { get; }

        public BlazorPageServiceGenerator(ContractABI contractABI, string contractName, string @namespace, string cqsNamespace, string functionOutputNamespace, string sharedNamespace, CodeGenLanguage codeGenLanguage)
        {
            System.Console.WriteLine($"Initialising constructor BlazorPageServiceGenerator ContractName: {contractName}");
            ContractABI = contractABI;
            ClassModel = new BlazorPageServiceModel(contractABI, contractName, @namespace, cqsNamespace, functionOutputNamespace, sharedNamespace);
            System.Console.WriteLine("Setting Code Language");
            ClassModel.CodeGenLanguage = CodeGenLanguage.Razor;
            System.Console.WriteLine("Initialising template");
            InitialiseTemplate();
            System.Console.WriteLine($"Finished Initialising constructor BlazorPageServiceGenerator ContractName: {contractName}");
        }

        public void InitialiseTemplate()
        {
          
            System.Console.WriteLine("Razor creating template");
            System.Console.WriteLine($"ClassModel: {ClassModel.ContractName}");
            System.Console.WriteLine($"ClassModel: {ClassModel.ClassNameSuffix}");
            System.Console.WriteLine($"ClassModel: {ClassModel.Namespace}");
            System.Console.WriteLine($"ClassModel: {ClassModel.GetServiceTypeName()}");
            System.Console.WriteLine($"ClassModel: {ClassModel.GetContractDeploymentTypeName()}");
            System.Console.WriteLine($"ClassModel: {ClassModel.FunctionOutputNamespace}");
            System.Console.WriteLine($"ClassModel: {ClassModel.CQSNamespace}");
            System.Console.WriteLine($"ClassModel: {ClassModel.NamespaceDependencies.Count}");

            ClassTemplate = new BlazorPageServiceCSharpRazorTemplate(ClassModel);
        }
    }
}