using System.Collections.Generic;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Core
{

    public class ParameterABIModel : ParameterModel<ParameterABI>
    {
        public const string AnonymousInputParameterPrefix = "ParamValue";
        public const string AnonymousOutputParameterPrefix = "ReturnValue";

        public ParameterABIModel(ParameterABI parameter, CodeGenLanguage codeGenLanguage) : base(parameter, codeGenLanguage)
        {
            
        }

        public ParameterABIModel(CodeGenLanguage codeGenLanguage) : base(codeGenLanguage)
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

        public string GetPropertyName(ParameterDirection parameterDirection)
        {
            return GetPropertyName(Parameter.Name, Parameter.Order, parameterDirection);
        }

        public string GetVariableName(string name, int order)
        {
            return CommonGenerators.GenerateVariableName(NameOrDefault(name, order), CodeGenLanguage);
        }

        public string GetPropertyName(string name, int order, ParameterDirection parameterDirection = ParameterDirection.Output)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = NameOrDefault(name, order, parameterDirection);
            }

            return CommonGenerators.GeneratePropertyName(name, CodeGenLanguage);
        }

        public virtual string GetStructTypeClassName()
        {
            if (string.IsNullOrEmpty(Parameter.StructType)) return null;
            return CommonGenerators.GenerateClassName(Parameter.StructType);
        }

        private string NameOrDefault(string name, int order, ParameterDirection parameterDirection = ParameterDirection.Output)
        {
            if (!string.IsNullOrEmpty(name))
                return name;

            var prefix = parameterDirection == ParameterDirection.Input
                ? AnonymousInputParameterPrefix
                : AnonymousOutputParameterPrefix;

            return $"{prefix}{order}";
        }
    }
}