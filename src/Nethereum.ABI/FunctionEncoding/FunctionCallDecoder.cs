using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;

namespace Nethereum.ABI.FunctionEncoding
{
    public class FunctionCallDecoder : ParameterDecoder
    {
        public List<ParameterOutput> DecodeFunctionInput(string sha3Signature, string data,
            params Parameter[] parameters)
        {
            if (!sha3Signature.StartsWith("0x")) sha3Signature = "0x" + sha3Signature;
            if (!data.StartsWith("0x")) data = "0x" + data;

            if ((data == "0x") || (data == sha3Signature)) return null;
            if (data.StartsWith(sha3Signature))
                data = data.Substring(sha3Signature.Length); //4 bytes?
            return DecodeDefaultData(data, parameters);
        }

        public T DecodeFunctionInput<T>(string sha3Signature, string data) where T : new()
        {
            return DecodeFunctionInput(new T(), sha3Signature, data);
        }

        public T DecodeFunctionInput<T>(T functionInput, string sha3Signature, string data)
        {
            if (!sha3Signature.StartsWith("0x")) sha3Signature = "0x" + sha3Signature;
            if (!data.StartsWith("0x")) data = "0x" + data;

            if ((data == "0x") || (data == sha3Signature)) return default(T);
            if (data.StartsWith(sha3Signature))
                data = data.Substring(sha3Signature.Length);
            DecodeFunctionOutput(functionInput, data);
            return functionInput;
        }

        public T DecodeFunctionOutput<T>(string output) where T : new()
        {
            if (output == "0x") return default(T);
            var result = new T();
            DecodeFunctionOutput(result, output);
            return result;
        }

        public T DecodeFunctionOutput<T>(T functionOutputResult, string output)
        {
            if (output == "0x")
                return functionOutputResult;

            var type = typeof(T);

            var function = FunctionOutputAttribute.GetAttribute<T>();
            if (function == null)
                throw new ArgumentException("Generic Type should have a Function Ouput Attribute");

#if DOTNET35
            var properties = type.GetTypeInfo().DeclaredProperties();
#else
            var properties = type.GetTypeInfo().DeclaredProperties;
#endif
            DecodeAttributes(output, functionOutputResult, properties.ToArray());

            return functionOutputResult;
        }

        /// <summary>
        ///     Decodes the output of a function using either a FunctionOutputAttribute  (T)
        ///     or the parameter casted to the type T, only one outputParameter should be used in this scenario.
        /// </summary>
        public T DecodeOutput<T>(string output, params Parameter[] outputParameter) where T : new()
        {
            if (output == "0x") return default(T);
            var function = FunctionOutputAttribute.GetAttribute<T>();

            if (function == null)
            {
                if (outputParameter != null)
                {
                    if (outputParameter.Length > 1)
                        throw new Exception(
                            "Only one output parameter supported to be decoded this way, use a FunctionOutputAttribute or define each outputparameter");

                    return DecodeSimpleTypeOutput<T>(outputParameter[0], output);
                }

                return default(T);
            }
            return DecodeFunctionOutput<T>(output);
        }

        public T DecodeSimpleTypeOutput<T>(Parameter outputParameter, string output)
        {
            if (output == "0x") return default(T);

            if (outputParameter != null)
            {
                outputParameter.DecodedType = typeof(T);
                var parmeterOutput = new ParameterOutput
                {
                    
                    Parameter = outputParameter
                };

                var results = DecodeOutput(output, parmeterOutput);

                if (results.Any())
                    return (T) results[0].Result;
            }

            return default(T);
        }
    }
}