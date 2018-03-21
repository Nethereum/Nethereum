using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class FunctionOutputDTOTemplate: IClassTemplate
    {
        private ParameterABIFunctionDTOTemplate _parameterABIFunctionDTOTemplate;
        private FunctionOutputDTOModel _model;
        public FunctionOutputDTOTemplate(FunctionOutputDTOModel model)
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
{SpaceUtils.NoTabs}using Nethereum.ABI.FunctionEncoding.Attributes;
{SpaceUtils.NoTabs}namespace {_model.Namespace}
{SpaceUtils.NoTabs}{{
{GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }

        public string GenerateClass()
        {
            if (_model.CanGenerateOutputDTO())
            {
                return
                    $@"{SpaceUtils.OneTab}[FunctionOutput]
{SpaceUtils.OneTab}public class {_model.GetTypeName()}
{SpaceUtils.OneTab}{{
{_parameterABIFunctionDTOTemplate.GenerateAllProperties(_model.FunctionABI.OutputParameters)}
{SpaceUtils.OneTab}}}";
            }
            return null;
        }
    }
}