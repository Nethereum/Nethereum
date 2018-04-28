using System.Collections.Generic;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{

    public class ParameterABIModel : ParameterModel<ParameterABI>
    {
        public ParameterABIModel(ParameterABI parameter) : base(parameter)
        {
        }

        public ParameterABIModel() : base()
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
            return string.IsNullOrEmpty(name) ? "ReturnValue" + order : name;
        }
    }
}