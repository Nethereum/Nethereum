using System;
using Nethereum.Generators.Console.CSharp;
using Nethereum.Generators.Console.Vb;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.Service;

namespace Nethereum.Generators.Console
{
    public class ConsoleGenerator : ClassGeneratorBase<ClassTemplateBase<ConsoleModel>, ConsoleModel>
    {
        public  ContractABI ContractABI { get; }

        public ConsoleGenerator(ContractABI contractABI, string contractName, string byteCode, string @namespace, string cqsNamespace, string functionOutputNamespace, CodeGenLanguage codeGenLanguage)
        {
            ContractABI = contractABI;
            ClassModel = new ConsoleModel(contractABI, contractName, byteCode, @namespace, cqsNamespace, functionOutputNamespace);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }
        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new ConsoleCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    ClassTemplate = new ConsoleVbTemplate(ClassModel);
                    break;
                //case CodeGenLanguage.FSharp:

                //    //ClassTemplate = new ServiceFSharpTemplate(ClassModel); 
                //    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }

    }
}