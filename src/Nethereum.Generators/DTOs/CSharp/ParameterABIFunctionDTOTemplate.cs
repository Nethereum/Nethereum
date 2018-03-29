using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ParameterABIFunctionDTOTemplate
    {
        private ParameterABIModel parameterModel;
        private ParameterABIModelTypeMap parameterAbiModelTypeMap;

        public ParameterABIFunctionDTOTemplate()
        {
            var typeMapper = new ABITypeToCSharpType();
            parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeMapper);
              
        }

        public string GenerateAllProperties(ParameterABI[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter);
            return 
                $@"{SpaceUtils.TwoTabs}[Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order})]
{SpaceUtils.TwoTabs}public {parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)} {parameterModel.GetPropertyName()} {{get; set;}}";
        }
    }
}