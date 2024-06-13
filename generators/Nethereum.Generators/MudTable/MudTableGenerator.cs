using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.MudTable
{
    public class MudTableGenerator : ClassGeneratorBase<ClassTemplateBase<MudTableModel>, MudTableModel>
    {
        public MudTableGenerator(MudTable mudTable, string @namespace, CodeGenLanguage codeGenLanguage)
        {
            ClassModel = new MudTableModel(mudTable, @namespace) { CodeGenLanguage = codeGenLanguage };
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    ClassTemplate = new MudTableCSharpTemplate(ClassModel);
                    break;
                case CodeGenLanguage.Vb:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
                    break;
                case CodeGenLanguage.FSharp:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
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