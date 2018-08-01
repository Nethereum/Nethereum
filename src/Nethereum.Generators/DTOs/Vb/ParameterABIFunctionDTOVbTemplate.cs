using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.DTOs
{
    public class ParameterABIFunctionDTOVbTemplate
    {
        private ParameterABIModel parameterModel;
        private ParameterABIModelTypeMap parameterAbiModelTypeMap;

        public ParameterABIFunctionDTOVbTemplate()
        {
            var typeMapper = new ABITypeToVBType();
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
                $@"{SpaceUtils.TwoTabs}<[Parameter](""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order})>
{SpaceUtils.TwoTabs}Public Overridable Property [{parameterModel.GetPropertyName()}] As {parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)}";
        }

        public string GenerateAllFunctionParameters(ParameterABI[] parameters)
        {
            return string.Join(", ", parameters.Select(GenerateFunctionParameter));
        }

        public string GenerateFunctionParameter(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter);
            return $@"ByVal {parameterModel.GetVariableName()} As {parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)}";
        }

        public string GenerateAssigmentFunctionParametersToProperties(ParameterABI[] parameters, string objectName, string spacing)
        {
            return string.Join(Environment.NewLine, parameters.Select(x => GenerateAssigmentFunctionParameterToProperty(x, objectName, spacing)));
        }

        public string GenerateAssigmentFunctionParameterToProperty(ParameterABI parameter, string objectName, string spacing)
        {
            var parameterModel = new ParameterABIModel(parameter);
            return $@"{spacing}{objectName}.{parameterModel.GetPropertyName()} = {parameterModel.GetVariableName()}";
        }
    }
}