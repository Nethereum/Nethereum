using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.CQS
{
    public class ContractDeploymentCQSMessageTemplate: IClassTemplate
    {
        private ParameterABIFunctionDTOTemplate _parameterABIFunctionDTOTemplate;
        private ContractDeploymentCQSMessageModel _model;
        public ContractDeploymentCQSMessageTemplate(ContractDeploymentCQSMessageModel model)
        {
            _parameterABIFunctionDTOTemplate = new ParameterABIFunctionDTOTemplate();
            _model = model;
        }

        public string GenerateFullClass()
        {
            return
$@"{SpaceUtils.NoTabs}using System;
{SpaceUtils.NoTabs}using System.Threading.Tasks;
{SpaceUtils.NoTabs}using System.Numerics;
{SpaceUtils.NoTabs}using Nethereum.Hex.HexTypes;
{SpaceUtils.NoTabs}using Nethereum.Contracts.CQS;
{SpaceUtils.NoTabs}using Nethereum.ABI.FunctionEncoding.Attributes;
{SpaceUtils.NoTabs}namespace {_model.Namespace}
{SpaceUtils.NoTabs}{{
{SpaceUtils.NoTabs}{GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }

        public string GenerateClass()
        {
            var typeName = _model.GetTypeName();
            return
$@"{SpaceUtils.OneTab}public class {typeName}:ContractDeploymentMessage
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public static string BYTECODE = ""{_model.ByteCode}"";
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {typeName}():base(BYTECODE)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {typeName}(string byteCode):base(byteCode)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}
{SpaceUtils.TwoTabs}
{_parameterABIFunctionDTOTemplate.GenerateAllProperties(_model.ConstructorABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }

    }
}