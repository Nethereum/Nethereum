using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class FunctionCQSMessageTemplate : IClassTemplate
    {
        private ParameterABIFunctionDTOTemplate _parameterABIFunctionDTOTemplate;
        private FunctionOutputDTOModel _functionOutputDTOModel;
        private FunctionCQSMessageModel _functionCQSMessageModel;
        private FunctionABIModel _functionABIModel;
        
        public FunctionCQSMessageTemplate(FunctionCQSMessageModel model, FunctionOutputDTOModel functionOutputDTOModel, FunctionABIModel functionABIModel)
        {
            _parameterABIFunctionDTOTemplate = new ParameterABIFunctionDTOTemplate();
            _functionOutputDTOModel = functionOutputDTOModel;
            _functionCQSMessageModel = model;
            _functionABIModel = functionABIModel;
        }

        public string GenerateFullClass()
        {
            return
$@"{SpaceUtils.NoTabs}using System;
{SpaceUtils.NoTabs}using System.Threading.Tasks;
{SpaceUtils.NoTabs}using System.Numerics;
{SpaceUtils.NoTabs}using Nethereum.Hex.HexTypes;
{SpaceUtils.NoTabs}using Nethereum.ABI.FunctionEncoding.Attributes;
{SpaceUtils.NoTabs}using Nethereum.Contracts.CQS;
{SpaceUtils.NoTabs}using {_functionOutputDTOModel.Namespace};
{SpaceUtils.NoTabs}namespace {_functionCQSMessageModel.Namespace}
{SpaceUtils.NoTabs}{{
{SpaceUtils.NoTabs}{GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }

        public string GenerateClass()
        {
            var functionABI = _functionCQSMessageModel.FunctionABI;
            var header = "";
            if (_functionABIModel.IsMultipleOutput())
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}"", typeof({_functionOutputDTOModel.GetTypeName()}))]";
            }

            if (_functionABIModel.IsSingleOutput())
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}"", ""{_functionABIModel.GetSingleAbiReturnType()}"")]";
            }

            if (_functionABIModel.HasNoReturn())
            {
                header = $@"{SpaceUtils.OneTab}[Function(""{functionABI.Name}""]";
            }

            return $@"{header}
{SpaceUtils.OneTab}public class {_functionCQSMessageModel.GetTypeName()}:ContractMessage
{SpaceUtils.OneTab}{{
{_parameterABIFunctionDTOTemplate.GenerateAllProperties(functionABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }
            
    }
}