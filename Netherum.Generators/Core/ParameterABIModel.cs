using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{
    public class ParameterABIModel
    {
        private CommonGenerators commonGenerators;
        private ABITypeToCSharpType abiTypeToCSharpType;

        public ParameterABIModel()
        {
            commonGenerators = new CommonGenerators();
            abiTypeToCSharpType = new ABITypeToCSharpType();
        }

        public string GetParameterVariableName(string name, int order)
        {
            return commonGenerators.GenerateVariableName(GetParameterName(name, order));
        }

        public string GetParameterPropertyName(Parameter parameter)
        {
            return GetParameterPropertyName(parameter.Name, parameter.Order);
        }

        public string GetParameterVariableName(Parameter parameter)
        {
            return GetParameterVariableName(parameter.Name, parameter.Order);
        }

        public string GetParameterPropertyName(string name, int order)
        {
            return commonGenerators.GeneratePropertyName(GetParameterName(name, order));
        }

        public string GetParameterCSharpOutputMapType(Parameter parameter)
        {
            return abiTypeToCSharpType.GetTypeMap(parameter.Type, true);
        }

        public string GetParameterCSharpInputMapType(Parameter parameter)
        {
            return abiTypeToCSharpType.GetTypeMap(parameter.Type, false);
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