using System;
using System.Linq;
using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class ParameterABIFunctionDTOTemplate
    {
        private ParameterABIModel parameterModel;

        public ParameterABIFunctionDTOTemplate()
        {
            parameterModel = new ParameterABIModel();    
        }

        public string GenerateAllProperties(Parameter[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(Parameter parameter)
        {
            return 
                $@"{SpaceUtils.TwoTabs}[Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order})]
{SpaceUtils.TwoTabs}public {parameterModel.GetParameterCSharpOutputMapType(parameter)} {parameterModel.GetParameterPropertyName(parameter)} {{get; set;}}";
        }
    }
}