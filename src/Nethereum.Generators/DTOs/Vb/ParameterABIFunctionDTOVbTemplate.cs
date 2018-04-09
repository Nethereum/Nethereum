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
{SpaceUtils.TwoTabs}Public Property {parameterModel.GetPropertyName()} As {parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)}";
        }
    }
}