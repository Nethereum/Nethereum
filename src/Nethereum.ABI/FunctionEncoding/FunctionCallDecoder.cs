using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.FunctionEncoding
{
    public class ParameterDecoder
    {
        public PropertyInfo[] GetPropertiesWithParameterAttributes(params PropertyInfo[] properties)
        {
            var result = new List<PropertyInfo>();
            foreach (var property in properties)
                if (property.IsDefined(typeof(ParameterAttribute), false))
                    result.Add(property);
            return result.ToArray();
        }

        public T DecodeAttributes<T>(string output, T result, params PropertyInfo[] properties)
        {
            if (output == "0x") return result;
            var parameterObjects = new List<ParameterOutputProperty>();

            foreach (var property in properties)
                if (property.IsDefined(typeof(ParameterAttribute), false))
                {
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>();
                    parameterObjects.Add(new ParameterOutputProperty
                    {
                        Parameter = parameterAttribute.Parameter,
                        PropertyInfo = property,
                        DecodedType = property.PropertyType
                    });
                }
            var orderedParameters = parameterObjects.OrderBy(x => x.Parameter.Order).ToArray();
            var parameterResults = DecodeOutput(output, orderedParameters);

            foreach (var parameterResult in parameterResults)
            {
                var parameter = (ParameterOutputProperty) parameterResult;
                var propertyInfo = parameter.PropertyInfo;
                propertyInfo.SetValue(result, parameter.Result);
            }

            return result;
        }

        public List<ParameterOutput> DecodeDefaultData(string data, params Parameter[] inputParameters)
        {
            var parameterOutputs = new List<ParameterOutput>();

            foreach (var inputParameter in inputParameters)
                parameterOutputs.Add(new ParameterOutput
                {
                    Parameter = inputParameter,
                    DecodedType = inputParameter.ABIType.GetDefaultDecodingType()
                });

            return DecodeOutput(data, parameterOutputs.ToArray());
        }

        public List<ParameterOutput> DecodeOutput(string output, params ParameterOutput[] outputParameters)
        {
            var outputBytes = output.HexToByteArray();

            var currentIndex = 0;

            foreach (var outputParam in outputParameters)
            {
                var param = outputParam.Parameter;
                if (param.ABIType.IsDynamic())
                {
                    outputParam.DataIndexStart =
                        EncoderDecoderHelpers.GetNumberOfBytes(outputBytes.Skip(currentIndex).ToArray());
                    currentIndex = currentIndex + 32;
                }
                else
                {
                    var bytes = outputBytes.Skip(currentIndex).Take(param.ABIType.FixedSize).ToArray();
                    outputParam.Result = param.ABIType.Decode(bytes, outputParam.DecodedType);

                    currentIndex = currentIndex + param.ABIType.FixedSize;
                }
            }

            ParameterOutput currentDataItem = null;
            foreach (
                var nextDataItem in outputParameters.Where(outputParam => outputParam.Parameter.ABIType.IsDynamic()))
            {
                if (currentDataItem != null)
                {
                    var bytes =
                        outputBytes.Skip(currentDataItem.DataIndexStart).Take(nextDataItem.DataIndexStart).ToArray();
                    currentDataItem.Result = currentDataItem.Parameter.ABIType.Decode(bytes, currentDataItem.DecodedType);
                }
                currentDataItem = nextDataItem;
            }

            if (currentDataItem != null)
            {
                var bytes = outputBytes.Skip(currentDataItem.DataIndexStart).ToArray();
                currentDataItem.Result = currentDataItem.Parameter.ABIType.Decode(bytes, currentDataItem.DecodedType);
            }
            return outputParameters.ToList();
        }
    }


    public class FunctionCallDecoder : ParameterDecoder
    {
        public T DecodeSimpleTypeOutput<T>(Parameter outputParameter, string output)
        {
            if (output == "0x") return default(T);

            if (outputParameter != null)
            {
                var parmeterOutput = new ParameterOutput
                {
                    DecodedType = typeof(T),
                    Parameter = outputParameter
                };

                var results = DecodeOutput(output, parmeterOutput);

                if (results.Any())
                    return (T) results[0].Result;
            }

            return default(T);
        }

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

            var properties = type.GetTypeInfo().DeclaredProperties;

            DecodeAttributes(output, functionOutputResult, properties.ToArray());

            return functionOutputResult;
        }
    }
}