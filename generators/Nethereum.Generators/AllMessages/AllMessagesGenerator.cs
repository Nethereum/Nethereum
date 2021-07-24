using System;
using System.Collections.Generic;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class AllMessagesGenerator : MultipleClassGeneratorBase<MultipleClassFileTemplate, AllMessagesModel>
    {
        private readonly IEnumerable<IClassGenerator> _classGenerators;

        public AllMessagesGenerator(IEnumerable<IClassGenerator> classGenerators, string contractName, string @namespace, CodeGenLanguage codeGenLanguage)
        {
            _classGenerators = classGenerators;
            Model = new AllMessagesModel(contractName, @namespace);
            Model.CodeGenLanguage = codeGenLanguage;
            InitialiseTemplate(codeGenLanguage);
        }

        public void InitialiseTemplate(CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    Template = new CSharpMultipleClassFileTemplate(_classGenerators, Model);
                    break;
                case CodeGenLanguage.Vb:
                     Template = new VbMultipleClassFileTemplate(_classGenerators, Model);
                    break;
                 case CodeGenLanguage.FSharp:
                   Template = new FSharpMultipleClassFileTemplate(_classGenerators, Model);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }
    }
}
 