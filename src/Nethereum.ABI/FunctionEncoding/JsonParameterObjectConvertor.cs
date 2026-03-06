using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Nethereum.ABI.FunctionEncoding
{
    public static class JsonParameterObjectConvertor
    {
        public static object[] ConvertToFunctionInputParameterValues(this JToken jObject, FunctionABI function)
        {
            return ConvertToFunctionInputParameterValues(jObject, function.InputParameters);
        }

        public static object[] ConvertToFunctionInputParameterValues(this JToken jObject, Parameter[] parameters)
        {
            var output = new List<object>();
            var parametersInOrder = parameters.OrderBy(x => x.Order);
            foreach (var parameter in parametersInOrder)
            {
                var abiType = parameter.ABIType;
                var jToken = jObject[parameter.GetParameterNameUsingDefaultIfNotSet()];

                AddJTokenValueInputParameters(output, abiType, jToken);
            }

            return output.ToArray();
        }

        private static void AddJTokenValueInputParameters(List<object> inputParameters, ABIType abiType, JToken jToken)
        {
            if (abiType is TupleType tupleAbi)
            {
                inputParameters.Add(ConvertToFunctionInputParameterValues(jToken ?? new JObject(), tupleAbi.Components));
                return;
            }

            if (abiType is ArrayType arrayAbi)
            {
                var array = jToken as JArray ?? new JArray();
                var elementType = arrayAbi.ElementType;
                var arrayOutput = new List<object>();
                foreach (var element in array)
                {
                    AddJTokenValueInputParameters(arrayOutput, elementType, element);
                }
                inputParameters.Add(arrayOutput);
                return;
            }

            if (abiType is Bytes32Type || abiType is BytesType)
            {
                var hex = jToken?.ToString();
                inputParameters.Add(string.IsNullOrEmpty(hex) ? new byte[0] : hex.HexToByteArray());
                return;
            }

            if (abiType is StringType || abiType is AddressType)
            {
                inputParameters.Add(jToken?.ToString() ?? "");
                return;
            }

            if (abiType is IntType)
            {
                var str = jToken?.ToString();
                inputParameters.Add(string.IsNullOrEmpty(str) ? BigInteger.Zero : BigInteger.Parse(str));
                return;
            }

            if (abiType is BoolType)
            {
                inputParameters.Add(jToken != null && jToken.Type == JTokenType.Boolean ? jToken.Value<bool>() : false);
                return;
            }
        }
    }
}
