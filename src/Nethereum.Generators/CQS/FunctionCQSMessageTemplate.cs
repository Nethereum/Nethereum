using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageTemplate
    {
        private ParameterABIFunctionDTOTemplate _parameterABIFunctionDTOTemplate;
        private FunctionOutputDTOModel _functionOutputDTOModel;
        private FunctionCQSMessageModel _functionCQSMessageModel;
        private FunctionABIModel _functionABIModel;
        public FunctionCQSMessageTemplate()
        {
            _parameterABIFunctionDTOTemplate = new ParameterABIFunctionDTOTemplate();
            _functionOutputDTOModel = new FunctionOutputDTOModel();
            _functionCQSMessageModel = new FunctionCQSMessageModel();
            _functionABIModel = new FunctionABIModel();
        }

        public string GenerateFullClass(FunctionABI functionABI, string namespaceName, string namespaceFunctionOutput)
        {
            return
                $@"using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using {namespaceFunctionOutput};
namespace {namespaceName}
{{
{GenerateClass(functionABI)}
}}
";
        }

        public string GenerateClass(FunctionABI functionABI)
        {
            var header = "";
            if (_functionABIModel.IsMultipleOutput(functionABI))
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}"", typeof({_functionOutputDTOModel.GetFunctionOutputTypeName(functionABI)}))]";
            }

            if (_functionABIModel.IsSingleOutput(functionABI))
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType(functionABI)}""))]";
            }

            if (_functionABIModel.HasNoReturn(functionABI))
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}""]";
            }

            return $@"{header}
{SpaceUtils.OneTab}public class {_functionCQSMessageModel.GetFunctionMessageTypeName(functionABI)}:ContractMessage
{SpaceUtils.OneTab}{{
{_parameterABIFunctionDTOTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }
            
    }
}