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
        public string MudNamespace { get; set; }

        public FunctionCQSMessageGenerator(FunctionABI functionABI, string @namespace, string namespaceFunctionOutput, string sharedTypesNamespace, CodeGenLanguage codeGenLanguage, string mudNamespace = null)
        {
            FunctionABI = functionABI;
            MudNamespace = mudNamespace;
            ClassModel = new FunctionCQSMessageModel(FunctionABI, @namespace, sharedTypesNamespace);
            ClassModel.NamespaceDependencies.Add(namespaceFunctionOutput);
            ClassModel.CodeGenLanguage = codeGenLanguage;
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, namespaceFunctionOutput, sharedTypesNamespace);
            InitialiseTemplate(codeGenLanguage, functionOutputDTOModel);
        }

        private void InitialiseTemplate(CodeGenLanguage codeGenLanguage, FunctionOutputDTOModel functionOutputDTOModel)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    var csharpMapper = new ABITypeToCSharpType();
                    var functionCsharpABIModel = new FunctionABIModel(ClassModel.FunctionABI, csharpMapper, CodeGenLanguage.CSharp);
                    functionCsharpABIModel.MudNamespacePrefix = MudNamespace;
                    ClassTemplate = new FunctionCQSMessageCSharpTemplate(ClassModel, functionOutputDTOModel, functionCsharpABIModel);
                    break;
                case CodeGenLanguage.Vb:
                    var vbMapper = new ABITypeToVBType();
                    var functionVBABIModel = new FunctionABIModel(ClassModel.FunctionABI, vbMapper, CodeGenLanguage.Vb);
                    functionVBABIModel.MudNamespacePrefix = MudNamespace;
                    ClassTemplate = new FunctionCQSMessageVbTemplate(ClassModel, functionOutputDTOModel, functionVBABIModel);
                    break;
                case CodeGenLanguage.FSharp:
                    var fsMapper = new ABITypeToFSharpType();;
                    var functionfsABIModel = new FunctionABIModel(ClassModel.FunctionABI, fsMapper, CodeGenLanguage.FSharp);
                    functionfsABIModel.MudNamespacePrefix = MudNamespace;
                    ClassTemplate = new FunctionCQSMessageFSharpTemplate(ClassModel, functionOutputDTOModel, functionfsABIModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, "Code generation not implemented for this language");
            }

        }


    }
}