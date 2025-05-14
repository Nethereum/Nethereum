using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using System;

namespace Nethereum.Generators.DTOs
{
    public class ErrorDTOGenerator : ClassGeneratorBase<ClassTemplateBase<ErrorDTOModel>, ErrorDTOModel>
    {
        public ErrorDTOGenerator(ErrorABI abi, string @namespace, string sharedTypesNamespace, CodeGenLanguage codeGenLanguage)
        {
            ClassModel = new ErrorDTOModel(abi, @namespace, sharedTypesNamespace);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new ErrorDTOCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    ClassTemplate = new ErrorDTOVbTemplate(ClassModel);
                    break;
                case CodeGenLanguage.FSharp:
                    ClassTemplate = new ErrorDTOFSharpTemplate(ClassModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }
    }
}