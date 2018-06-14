using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto
{
    public static class ABIToProtoTemplateUtility
    {
        private static readonly ISolidityToProtoBufTypeConverter TypeConverter = new SolidityToProtoBufTypeConverter();
        private static readonly CommonGenerators CommonGenerators = new CommonGenerators();

        public static string GenerateMessageSchema(string name, string suffix, IEnumerable<ParameterABI> parameters, ParameterDirection parameterDirection)
        {
            if (!parameters?.Any() ?? false)
                return string.Empty;

            return
$@"{SpaceUtils.NoTabs}message {CommonGenerators.GenerateClassName(name)}{suffix} {{
{SpaceUtils.NoTabs}{GenerateFields(parameters, parameterDirection)}}}";
        }

        public static string GenerateFields(IEnumerable<ParameterABI> parameters, ParameterDirection parameterDirection)
        {
            int anonymousFieldCount = 0;
            var fieldText = new StringBuilder();
            foreach (var parameter in parameters)
            {
                fieldText.AppendLine(GenerateProtoField(parameter, parameterDirection, ref anonymousFieldCount));
            }
            return fieldText.ToString();
        }

        public static string GenerateProtoField(Parameter parameter, ParameterDirection parameterDirection, ref int anonymousFieldCount)
        {
            return $"{SpaceUtils.OneTab}{TypeConverter.Convert(parameter.Type)} {GenerateProtoFieldName(parameter, parameterDirection, ref anonymousFieldCount)} = {parameter.Order};";
        }

        public static string GenerateProtoFieldName(Parameter parameter, ParameterDirection parameterDirection, ref int anonymousFieldCount)
        {
            return GenerateProtoFieldName(parameter.Name, parameter.Order, parameterDirection, ref anonymousFieldCount);
        }

        public static Dictionary<ParameterDirection, string> AnonymousParameterPrefix = new Dictionary<ParameterDirection, string>
        {
            {ParameterDirection.Input, "param_value"},
            {ParameterDirection.Output, "return_value"}
        };

        public static string GenerateProtoFieldName(string parameterName, int order, ParameterDirection parameterDirection, ref int anonymousFieldCount)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                anonymousFieldCount++;
                parameterName = $"{AnonymousParameterPrefix[parameterDirection]}{anonymousFieldCount}";
            }
            else
            {
                parameterName = parameterName.Trim();
            }

            var fieldName = ApplyGoogleProtobufFieldFormattingRules(parameterName);
            return fieldName;
        }

        private static string ApplyGoogleProtobufFieldFormattingRules(string parameterName)
        {
            var fieldName = new StringBuilder();
            for (int i = 0; i < parameterName.Length; i++)
            {
                var c = parameterName[i];

                if (i > 0 && Char.IsUpper(c))
                    fieldName.Append("_");

                if (i > 0 && Char.IsNumber(c) && !Char.IsNumber(parameterName[i-1]))
                    fieldName.Append("_");

                fieldName.Append(Char.ToLower(c));
            }

            return fieldName.ToString();
        }

        public static IEnumerable<ParameterABI> Prepend
            (this IEnumerable<ParameterABI> parameters, params ParameterABI[] parametersToPrePend)
        {
            var allParameters = new List<ParameterABI>(parametersToPrePend);
            allParameters
                .AddRange(
                    parameters.Select((p) => new ParameterABI(p.Type, p.Name, p.Order + parametersToPrePend.Length)));
            return allParameters;
        }

        public static IEnumerable<ParameterABI> Ordered(this IEnumerable<ParameterABI> parameters)
        {
            if (parameters == null)
                return new ParameterABI[0];

            return parameters?.OrderBy(p => p.Order);
        }
    }
}
