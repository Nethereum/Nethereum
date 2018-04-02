using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using System;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageGenerator: ClassGeneratorBase<ClassTemplateBase<ContractDeploymentCQSMessageModel>, ContractDeploymentCQSMessageModel>
    {
        public ContractDeploymentCQSMessageGenerator(ConstructorABI abi, string namespaceName, string byteCode, string contractName, CodeGenLanguage codeGenLanguage)
        {
            ClassModel = new ContractDeploymentCQSMessageModel(abi, namespaceName, byteCode, contractName);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new ContractDeploymentCQSMessageCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    ClassTemplate = new ContractDeploymentCQSMessageVbTemplate(ClassModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }
          
        }

    }
}