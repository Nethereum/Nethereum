using System;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.FunctionEncoding
{
    public class FunctionCallEncoder: ParametersEncoder
    {
        
        public string EncodeRequest<T>(T functionInput)
        {
            var type = typeof(T);

            var function = type.GetTypeInfo().GetCustomAttribute<FunctionAttribute>();
            if (function == null)
                throw new ArgumentException("Function Attribute is required", nameof(functionInput));

            var encodedParameters = EncodeParametersFromTypeAttributes(type, functionInput);
        
            return EncodeRequest(function.Sha3Signature, encodedParameters.ToHex());
        }


        public string EncodeRequest(string sha3Signature, Parameter[] parameters, params object[] values)
        {
            var parametersEncoded = EncodeParameters(parameters, values).ToHex();

            return EncodeRequest(sha3Signature, parametersEncoded);
        }

        public string EncodeRequest(string sha3Signature, string encodedParameters)
        {
            var prefix = "0x";

            if (sha3Signature.StartsWith(prefix))
            {
                prefix = "";
            }

            return prefix + sha3Signature + encodedParameters;
        }

        
    }
}