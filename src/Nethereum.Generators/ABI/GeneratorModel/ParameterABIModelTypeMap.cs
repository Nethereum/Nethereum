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
            return _typeConvertor.Convert(parameter.Type, true);
        }

        public string GetParameterDotNetInputMapType(ParameterABI parameter)
        {
            return _typeConvertor.Convert(parameter.Type, false);
        }

    }
}