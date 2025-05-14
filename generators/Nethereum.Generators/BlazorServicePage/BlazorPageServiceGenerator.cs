
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using System;

namespace Nethereum.Generators.BlazorServicePage
{
    public class BlazorPageServiceGenerator : ClassGeneratorBase<ClassTemplateBase<BlazorPageServiceModel>, BlazorPageServiceModel>
    {
        public  ContractABI ContractABI { get; }

        public BlazorPageServiceGenerator(ContractABI contractABI, string contractName, string @namespace, string cqsNamespace, string functionOutputNamespace, string sharedNamespace, CodeGenLanguage codeGenLanguage)
        {
            ContractABI = contractABI;
            ClassModel = new BlazorPageServiceModel(contractABI, contractName, @namespace, cqsNamespace, functionOutputNamespace, sharedNamespace);
            ClassModel.CodeGenLanguage = CodeGenLanguage.Razor;
            InitialiseTemplate(CodeGenLanguage.Razor);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.Razor:
                    ClassTemplate = new BlazorPageServiceCSharpRazorTemplate(ClassModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }
    }
}