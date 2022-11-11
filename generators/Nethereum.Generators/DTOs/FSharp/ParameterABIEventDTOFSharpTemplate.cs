using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ParameterABIEventDTOFSharpTemplate
    {
        private ParameterABIModelTypeMap parameterAbiModelTypeMap;
        private Utils utils;

        public ParameterABIEventDTOFSharpTemplate()
        {
            var typeMapper = new ABITypeToFSharpType();
            parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeMapper, CodeGenLanguage.FSharp);
            utils = new Utils();
        }

        public string GenerateAllProperties(ParameterABI[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage.FSharp);
            return
                $@"{SpaceUtils.ThreeTabs}[<Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order}, {utils.GetBooleanAsString(parameter.Indexed)} )>]
{SpaceUtils.ThreeTabs}member val {parameterModel.GetPropertyName()} = Unchecked.defaultof<{parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)}> with get, set";
        }
    }
}