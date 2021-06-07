using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class ParameterABIModelTypeMap
    {
        public CodeGenLanguage CodeGenLanguage { get; }
        private readonly ITypeConvertor _typeConvertor;

        public ParameterABIModelTypeMap(ITypeConvertor typeConvertor, CodeGenLanguage codeGenLanguage)
        {
            CodeGenLanguage = codeGenLanguage;
            _typeConvertor = typeConvertor;
        }

        public string GetParameterDotNetOutputMapType(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage);
            return _typeConvertor.Convert(parameter.Type, parameterModel.GetStructTypeClassName(), true);
        }

        public string GetParameterDotNetInputMapType(ParameterABI parameter)
        {
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage);
            return _typeConvertor.Convert(parameter.Type, parameterModel.GetStructTypeClassName(), false);
        }

    }
}