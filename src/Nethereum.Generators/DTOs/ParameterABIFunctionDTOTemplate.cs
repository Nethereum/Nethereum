using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ParameterABIFunctionDTOTemplate
    {
        private ParameterABIModel parameterModel;

        public ParameterABIFunctionDTOTemplate()
        {
            var typeMapper = new ABITypeToCSharpType();
            parameterModel = new ParameterABIModel(typeMapper);    
        }

        public string GenerateAllProperties(ParameterABI[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(ParameterABI parameter)
        {
            return 
                $@"{SpaceUtils.TwoTabs}[Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order})]
{SpaceUtils.TwoTabs}public {parameterModel.GetParameterDotNetOutputMapType(parameter)} {parameterModel.GetParameterPropertyName(parameter)} {{get; set;}}";
        }
    }
}