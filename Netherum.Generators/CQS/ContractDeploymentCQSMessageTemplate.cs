using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class ContractDeploymentCQSMessageTemplate
    {
        private ParameterABIFunctionDTOTemplate _parameterABIFunctionDTOTemplate;
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        public ContractDeploymentCQSMessageTemplate()
        {
            _parameterABIFunctionDTOTemplate = new ParameterABIFunctionDTOTemplate();
            _contractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel();
        }

        public string GenerateFullClass(ConstructorABI constructorABI, string namespaceName, string byteCode, string contractName)
        {
            return
$@"using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace {namespaceName}
{{
{GenerateClass(constructorABI, byteCode, contractName)}
}}
";
        }

        public string GenerateClass(ConstructorABI constructorABI, string byteCode, string contractName)
        {
            var typeName = _contractDeploymentCQSMessageModel.GetContractDeploymentMessageTypeName(contractName);
            return
$@"{SpaceUtils.OneTab}public class {typeName}:ContractDeploymentMessage
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public static string BYTECODE = ""{byteCode}"";
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {typeName}():base(BYTECODE)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {typeName}(string byteCode):base(byteCode)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}
{SpaceUtils.TwoTabs}
{_parameterABIFunctionDTOTemplate.GenerateAllProperties(constructorABI.InputParameters)}
{SpaceUtils.OneTab}}}";
        }

    }
}