using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class StructTypeGenerator : ClassGeneratorBase<ClassTemplateBase<StructTypeModel>, StructTypeModel>
    {

        public StructTypeGenerator(StructABI structTypeABI, string @namespace, CodeGenLanguage codeGenLanguage)
        {
            ClassModel = new StructTypeModel(structTypeABI, @namespace) { CodeGenLanguage = codeGenLanguage };
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new StructTypeCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    throw new NotImplementedException();
                   // ClassTemplate = new FunctionOutputDTOVbTemplate(ClassModel);
                    break;
                case CodeGenLanguage.FSharp:
                    throw new NotImplementedException();
                    // ClassTemplate = new FunctionOutputDTOFSharpTemplate(ClassModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }
        }

        public override string GenerateClass()
        {
            return ClassTemplate.GenerateClass();
        }

        public override string GenerateFileContent()
        {
            return ClassTemplate.GenerateFullClass();
        }

    }
}