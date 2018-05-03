using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.Service;

namespace Nethereum.Generators.XUnit
{
    public class SimpleTestGenerator : ClassGeneratorBase<ClassTemplateBase<SimpleTestModel>, SimpleTestModel>
    {
        public  ContractABI ContractABI { get; }

        public SimpleTestGenerator(ContractABI contractABI, string contractName, string @namespace, string cqsNamespace, string functionOutputNamespace, CodeGenLanguage codeGenLanguage)
        {
            ContractABI = contractABI;
            ClassModel = new SimpleTestModel(contractABI, contractName, @namespace, cqsNamespace, functionOutputNamespace);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new SimpleTestCSharpTemplate(ClassModel);
                    break;
                //case CodeGenLanguage.Vb:
                //    ClassTemplate = new ServiceVbTemplate(ClassModel);
                //    break;
                //case CodeGenLanguage.FSharp:
                //    ClassTemplate = new ServiceFSharpTemplate(ClassModel); 
                //    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }
    }
}