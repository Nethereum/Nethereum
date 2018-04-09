using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{

    public class ParameterABIModel : ParameterModel<ParameterABI>
    {
        public ParameterABIModel(ParameterABI parameter) : base(parameter)
        {
        }

        public override string GetVariableName()
        {
            return GetVariableName(Parameter.Name, Parameter.Order);
        }

        public override string GetPropertyName()
        {
            return GetPropertyName(Parameter.Name, Parameter.Order);
        }

        public string GetVariableName(string name, int order)
        {
            return CommonGenerators.GenerateVariableName(GetParameterName(name, order));
        }

        public string GetPropertyName(string name, int order)
        {
            return CommonGenerators.GeneratePropertyName(GetParameterName(name, order));
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