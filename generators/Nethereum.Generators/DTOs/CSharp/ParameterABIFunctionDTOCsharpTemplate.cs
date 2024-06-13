using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ParameterABIFunctionDTOCSharpTemplate
    {
        private ParameterABIModel parameterModel;
        private ParameterABIModelTypeMap parameterAbiModelTypeMap;

        public ParameterABIFunctionDTOCSharpTemplate()
        {
            var typeMapper = new ABITypeToCSharpType();
            parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeMapper, CodeGenLanguage.CSharp);
        }

        public string GenerateAllProperties(ParameterABI[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(ParameterABI parameter)
        {
            return GenerateProperty(parameter, SpaceUtils.Two___Tabs);
        }

        public string GenerateProperty(ParameterABI parameter, string spacing)
        {
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage.CSharp);
            return
                $@"{spacing}[Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order})]
{spacing}public virtual {parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)} {parameterModel.GetPropertyName()} {{ get; set; }}";
        }

        public string GenerateAllProperties(ParameterABI[] parameters, string spacing)
        {
            return string.Join(Environment.NewLine, parameters.Select(x => GenerateProperty(x, spacing)));
        }

        public string GenerateAllFunctionParameters(ParameterABI[] parameters)
        {
            return string.Join(", ", parameters.Select(GenerateFunctionParameter));
        }

        public string GenerateFunctionParameter(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage.CSharp);
            return $@"{parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)} {parameterModel.GetVariableName()}";
        }

        public string GenerateAssigmentFunctionParametersToProperties(ParameterABI[] parameters, string objectName, string spacing)
        {
            return string.Join(Environment.NewLine, parameters.Select(x => GenerateAssigmentFunctionParameterToProperty(x, objectName, spacing)));
        }

        public string GenerateAssigmentFunctionParameterToProperty(ParameterABI parameter, string objectName, string spacing)
        {
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage.CSharp);
            return $@"{spacing}{objectName}.{parameterModel.GetPropertyName()} = {parameterModel.GetVariableName()};";
        }
    }
}