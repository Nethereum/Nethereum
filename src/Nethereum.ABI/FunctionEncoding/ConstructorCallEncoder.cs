using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.FunctionEncoding
{
    public class ConstructorCallEncoder : ParametersEncoder
    {
        public string EncodeRequest<T>(T constructorInput, string contractByteCode)
        {
            var type = typeof(T);

            //var function = type.GetTypeInfo().GetCustomAttribute<FunctionAttribute>();
            //if (function == null)
            //    throw new ArgumentException("Function Attribute is required", nameof(functionInput));

            var encodedParameters = EncodeParametersFromTypeAttributes(type, constructorInput);
            return EncodeRequest(contractByteCode, encodedParameters.ToHex());
        }

        public string EncodeRequest(string contractByteCode, Parameter[] parameters, params object[] values)
        {
            var parametersEncoded = EncodeParameters(parameters, values).ToHex();

            return EncodeRequest(contractByteCode, parametersEncoded);
        }

        public string EncodeRequest(string contractByteCode, string encodedParameters)
        {
            var prefix = "0x";

            if (contractByteCode.StartsWith(prefix))
            {
                prefix = "";
            }

            return prefix + contractByteCode + encodedParameters;
        }
    }
}