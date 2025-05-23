using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using System;

namespace Nethereum.Generators.DTOs
{
    public class EventDTOGenerator: ClassGeneratorBase<EventDTOModel>
    { 
        public EventDTOGenerator(EventABI abi, string @namespace, string sharedTypesNamespace, CodeGenLanguage codeGenLanguage)
        {
            ClassModel = new EventDTOModel(abi, @namespace, sharedTypesNamespace);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new EventDTOCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    ClassTemplate = new EventDTOVbTemplate(ClassModel);
                    break;
                case CodeGenLanguage.FSharp:
                    ClassTemplate = new EventDTOFSharpTemplate(ClassModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }
    }
}