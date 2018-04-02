using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOGenerator: ClassGeneratorBase<ClassTemplateBase<FunctionOutputDTOModel>,FunctionOutputDTOModel>
    {
     
        public FunctionOutputDTOGenerator(FunctionABI functionABI, string @namespace, CodeGenLanguage codeGenLanguage)
        {
            ClassModel = new FunctionOutputDTOModel(functionABI, @namespace) {CodeGenLanguage = codeGenLanguage};
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new FunctionOutputDTOCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    ClassTemplate = new FunctionOutputDTOVbTemplate(ClassModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }
        }

        public override string GenerateClass()
        {
            return ClassModel.CanGenerateOutputDTO() ? ClassTemplate.GenerateClass() : null;
        }

        public override string GenerateFileContent()
        {
            return ClassModel.CanGenerateOutputDTO() ? ClassTemplate.GenerateFullClass() : null;
        }

    }
}