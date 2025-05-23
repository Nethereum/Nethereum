
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
     
            ContractABI = contractABI;
            ClassModel = new BlazorPageServiceModel(contractABI, contractName, @namespace, cqsNamespace, functionOutputNamespace, sharedNamespace);
     
            ClassModel.CodeGenLanguage = CodeGenLanguage.Razor;
     
            InitialiseTemplate();
     
        }

        public void InitialiseTemplate()
        {
            ClassTemplate = new BlazorPageServiceCSharpRazorTemplate(ClassModel);
        }
    }
}