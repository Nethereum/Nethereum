using System;
using System.Runtime.CompilerServices;
using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageGenerator : ClassGeneratorBase<ClassTemplateBase<FunctionCQSMessageModel>, FunctionCQSMessageModel>
    {
        public FunctionABI FunctionABI { get; }

        public FunctionCQSMessageGenerator(FunctionABI functionABI, string @namespace, string namespaceFunctionOutput, CodeGenLanguage codeGenLanguage)
        {
            FunctionABI = functionABI;
            ClassModel = new FunctionCQSMessageModel(FunctionABI, @namespace);
            ClassModel.NamespaceDependencies.Add(namespaceFunctionOutput);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, namespaceFunctionOutput);
            InitialiseTemplate(codeGenLanguage, functionOutputDTOModel);
        }

        private void InitialiseTemplate(CodeGenLanguage codeGenLanguage, FunctionOutputDTOModel functionOutputDTOModel)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    var csharpMapper = new ABITypeToCSharpType();
                    var functionCsharpABIModel = new FunctionABIModel(ClassModel.FunctionABI, csharpMapper);
                    ClassTemplate = new FunctionCQSMessageCSharpTemplate(ClassModel, functionOutputDTOModel, functionCsharpABIModel);
                    break;
                case CodeGenLanguage.Vb:
                    var vbMapper = new ABITypeToVBType();
                    var functionVBABIModel = new FunctionABIModel(ClassModel.FunctionABI, vbMapper);
                    ClassTemplate = new FunctionCQSMessageVbTemplate(ClassModel, functionOutputDTOModel, functionVBABIModel);
                    break;
                case CodeGenLanguage.FSharp:
                    var fsMapper = new ABITypeToFSharpType();;
                    var functionfsABIModel = new FunctionABIModel(ClassModel.FunctionABI, fsMapper);
                    ClassTemplate = new FunctionCQSMessageFSharpTemplate(ClassModel, functionOutputDTOModel, functionfsABIModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }


    }
}