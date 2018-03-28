using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{
    public class Parameter
    {
        public Parameter(string name, string type, int order)
        {
            Name = name;
            Type = type;
            Order = order;
        }

        public string Name { get; protected set; }
        public string Type { get; protected set; }
        public int Order { get; protected set; }
    }

    public class ParameterABIModel
    {
        private readonly ITypeConvertor _typeConvertor;
        private CommonGenerators commonGenerators;

        public ParameterABIModel(ITypeConvertor typeConvertor)
        {
            _typeConvertor = typeConvertor;
            commonGenerators = new CommonGenerators();
        }

        public string GetParameterVariableName(string name, int order)
        {
            return commonGenerators.GenerateVariableName(GetParameterName(name, order));
        }

        public string GetParameterPropertyName(ParameterABI parameter)
        {
            return GetParameterPropertyName(parameter.Name, parameter.Order);
        }

        public string GetParameterVariableName(ParameterABI parameter)
        {
            return GetParameterVariableName(parameter.Name, parameter.Order);
        }

        public string GetParameterPropertyName(string name, int order)
        {
            return commonGenerators.GeneratePropertyName(GetParameterName(name, order));
        }

        public string GetParameterDotNetOutputMapType(ParameterABI parameter)
        {
            return _typeConvertor.ConvertToDotNetType(parameter.Type, true);
        }

        public string GetParameterDotNetInputMapType(ParameterABI parameter)
        {
            return _typeConvertor.ConvertToDotNetType(parameter.Type, false);
        }

        public string GetParameterName(string name, int order)
        {
            if (name != "") return name;
            switch (order)
            {
                case 0:
                    return "a";
                case 1:
                    return "b";
                case 2:
                    return "c";
                case 3:
                    return "d";
                case 4:
                    return "e";
                case 5:
                    return "f";
                case 6:
                    return "g";
            }
            return "h";
        }

    }
}