using System.Text;
using Nethereum.ABI.Model;
using Nethereum.Generator.Console;

namespace Nethereum.Generator.Console
{
    public class FunctionOutputDTOTemplate
    {
        private ParameterABIFunctionDTOTemplate _parameterABIFunctionDTOTemplate;
        private FunctionOutputDTOModel _functionOutputDTOModel;
        public FunctionOutputDTOTemplate()
        {
            _parameterABIFunctionDTOTemplate = new ParameterABIFunctionDTOTemplate();
            _functionOutputDTOModel = new FunctionOutputDTOModel();
        }

        public string GenerateFullClass(FunctionABI functionABI, string namespaceName)
        {
            return
                $@"using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace {namespaceName}
{{
{GenerateClass(functionABI)}
}}
";
        }

        public string GenerateClass(FunctionABI functionABI)
        {
            if (_functionOutputDTOModel.CanGenerateOutputDTO(functionABI))
            {
                return
                    $@"{SpaceUtils.OneTab}[FunctionOutput]
{SpaceUtils.OneTab}public class {_functionOutputDTOModel.GetFunctionOutputTypeName(functionABI)}
{SpaceUtils.OneTab}{{
{_parameterABIFunctionDTOTemplate.GenerateAllProperties(functionABI.OutputParameters)}
{SpaceUtils.OneTab}}}";
            }
            return null;
        }
    }
}