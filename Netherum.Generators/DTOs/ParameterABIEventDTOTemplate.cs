using System;
using System.Linq;
using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class ParameterABIEventDTOTemplate
    {
        private ParameterABIModel parameterModel;
        private Utils utils;

        public ParameterABIEventDTOTemplate()
        {
            parameterModel = new ParameterABIModel();
            utils = new Utils();
        }

        public string GenerateAllProperties(Parameter[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(Parameter parameter)
        {
            return
                $@"{SpaceUtils.TwoTabs}[Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order}, {utils.GetBooleanAsString(parameter.Indexed)} )]
{SpaceUtils.TwoTabs}public {parameterModel.GetParameterCSharpOutputMapType(parameter)} {parameterModel.GetParameterPropertyName(parameter)} {{get; set;}}";
        }
    }
}