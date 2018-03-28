using System.Runtime.CompilerServices;
using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageGenerator : ClassGeneratorBase<FunctionCQSMessageTemplate, FunctionCQSMessageModel>
    {
        public FunctionABI FunctionABI { get; }

        public FunctionCQSMessageGenerator(FunctionABI functionABI, string @namespace, string namespaceFunctionOutput)
        {
            FunctionABI = functionABI;
            ClassModel = new FunctionCQSMessageModel(FunctionABI, @namespace);
            var typeMapper = new ABITypeToCSharpType();
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, namespaceFunctionOutput);
            var functionABIModel = new FunctionABIModel(ClassModel.FunctionABI, typeMapper);
            ClassTemplate = new FunctionCQSMessageTemplate(ClassModel, functionOutputDTOModel, functionABIModel);
        }
    }
}