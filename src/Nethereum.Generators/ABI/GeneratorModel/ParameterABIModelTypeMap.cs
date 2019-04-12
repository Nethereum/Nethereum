using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class ParameterABIModelTypeMap
    {
        private readonly ITypeConvertor _typeConvertor;

        public ParameterABIModelTypeMap(ITypeConvertor typeConvertor)
        {
            _typeConvertor = typeConvertor;
        }

        public string GetParameterDotNetOutputMapType(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter);
            return _typeConvertor.Convert(parameter.Type, parameterModel.GetStructTypeClassName(), true);
        }

        public string GetParameterDotNetInputMapType(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter);
            return _typeConvertor.Convert(parameter.Type, parameterModel.GetStructTypeClassName(), false);
        }

    }
}