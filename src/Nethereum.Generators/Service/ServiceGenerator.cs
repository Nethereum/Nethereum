using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class ServiceGenerator : ClassGeneratorBase<ClassTemplateBase<ServiceModel>, ServiceModel>
    {
        public  ContractABI ContractABI { get; }

        public ServiceGenerator(ContractABI contractABI, string contractName, string byteCode, string @namespace, string cqsNamespace, string functionOutputNamespace, CodeGenLanguage codeGenLanguage)
        {
            ContractABI = contractABI;
            ClassModel = new ServiceModel(contractABI, contractName, byteCode, @namespace, cqsNamespace, functionOutputNamespace);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new ServiceCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    ClassTemplate = new ServiceVbTemplate(ClassModel);
                    break;
                case CodeGenLanguage.FSharp:
                    ClassTemplate = new ServiceFSharpTemplate(ClassModel); 
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }
    }
}