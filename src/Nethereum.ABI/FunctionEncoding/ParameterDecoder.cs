using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.FunctionEncoding
{
    public class ParameterDecoder
    {

        private readonly AttributesToABIExtractor attributesToABIExtractor;

        public ParameterDecoder()
        {
            attributesToABIExtractor = new AttributesToABIExtractor();
        }

        public object DecodeAttributes(byte[] output, Type objectType)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(objectType);
            var objectResult = Activator.CreateInstance(objectType);
            return DecodeAttributes(output, objectResult, properties.ToArray());
        }

        public object DecodeAttributes(string output, Type objectType)
        {
            return DecodeAttributes(output.HexToByteArray(), objectType);
        }

        public object DecodeAttributes(byte[] output, object result, params PropertyInfo[] properties)
        {
            if (output == null || output.Length == 0) return result;
            var parameterObjects = GetParameterOutputsFromAttributes(properties);
            var orderedParameters = parameterObjects.OrderBy(x => x.Parameter.Order).ToArray();
            var parameterResults = DecodeOutput(output, orderedParameters);

            foreach (var parameterResult in parameterResults)
            {
                var parameter = (ParameterOutputProperty)parameterResult;
                var propertyInfo = parameter.PropertyInfo;
                var decodedResult = parameter.Result;

                if (parameter.Parameter.ABIType is TupleType tupleType)
                {

                    decodedResult = Activator.CreateInstance(propertyInfo.PropertyType);
                    AssingValuesFromPropertyList(decodedResult, parameter);
                }
#if DOTNET35
                propertyInfo.SetValue(result, decodedResult, null);
#else
                propertyInfo.SetValue(result, decodedResult);
#endif
            }

            return result;
        }


        public object DecodeAttributes(string output, object result, params PropertyInfo[] properties)
        {
            if (output == "0x") return result;
            return DecodeAttributes(output.HexToByteArray(), result, properties);
        }

        public T DecodeAttributes<T>(string output, T result, params PropertyInfo[] properties)
        {
            return (T)DecodeAttributes(output, (object)result, properties);
        }

        public void AssingValuesFromPropertyList(object instance, ParameterOutputProperty result)
        {
            if (result.Parameter.ABIType is TupleType)
            {
                var childrenProperties = result.ChildrenProperties;
                if (result.Result != null)
                {
                    var outputResult = (List<ParameterOutput>) result.Result;

                    foreach (var parameterOutput in outputResult)
                    {
                        var childrenProperty =
                            childrenProperties.FirstOrDefault(x =>
                                x.Parameter.Order == parameterOutput.Parameter.Order);

                        if (childrenProperty != null)
                        {
                            var decodedResult = parameterOutput.Result;
                            if (childrenProperty.Parameter.ABIType is TupleType)
                            {
                                //Adding the result to the children property for assignment to the instance
                                childrenProperty.Result = parameterOutput.Result;
                                //creating a new instance of our object property
                                decodedResult = Activator.CreateInstance(childrenProperty.PropertyInfo.PropertyType);
                                AssingValuesFromPropertyList(decodedResult, childrenProperty);
                            }
#if DOTNET35
                            childrenProperty.PropertyInfo.SetValue(instance, decodedResult, null);
#else
                            childrenProperty.PropertyInfo.SetValue(instance, decodedResult);
#endif
                        }
                    }
                }

            }
        }

        public List<ParameterOutputProperty> GetParameterOutputsFromAttributes(PropertyInfo[] properties)
        {
            var parameterObjects = new List<ParameterOutputProperty>();

            foreach (var property in properties)
                if (property.IsDefined(typeof(ParameterAttribute), true))
                {
#if DOTNET35
                    var parameterAttribute =
                        (ParameterAttribute)property.GetCustomAttributes(typeof(ParameterAttribute), true)[0];
#else
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>(true);
#endif
                    var parameterOutputProperty = new ParameterOutputProperty
                    {
                        Parameter = parameterAttribute.Parameter,
                        PropertyInfo = property,
                    };

                    if (parameterAttribute.Parameter.ABIType is TupleType tupleType)
                    {
                        attributesToABIExtractor.InitTupleComponentsFromTypeAttributes(property.PropertyType,
                            tupleType);
                        parameterOutputProperty.ChildrenProperties =
                            GetParameterOutputsFromAttributes(property.PropertyType);
                    }
                    else if (parameterAttribute.Parameter.ABIType is ArrayType arrayType)
                    {
                        InitTupleElementFromArray(property, parameterAttribute, arrayType);

                        parameterAttribute.Parameter.DecodedType = property.PropertyType;
                    }
                    else
                    {
                        parameterAttribute.Parameter.DecodedType = property.PropertyType;
                        
                    }
                    parameterObjects.Add(parameterOutputProperty);

                }
            return parameterObjects;
        }

        private void InitTupleElementFromArray(PropertyInfo property, ParameterAttribute parameterAttribute, ArrayType arrayType, int depth = 1)
        {
            if (arrayType.ElementType is TupleType tupleTypeElement)
            {
                var type = GetListElementType(property.PropertyType, depth);
                if (type == null) throw new Exception("Tuple array has to decode to a IList<T>: " + parameterAttribute.Parameter.Name);

                attributesToABIExtractor.InitTupleComponentsFromTypeAttributes(type,
                    tupleTypeElement);
            }

            if(arrayType.ElementType is ArrayType arrayElementType)
            {
                InitTupleElementFromArray(property, parameterAttribute, arrayElementType, depth + 1);
            }
        }

        private Type GetListElementType(Type startType, int depthOfLists)
        {
#if NETSTANDARD1_1 || PCL && !NET35
            var type = startType.GenericTypeArguments.FirstOrDefault();
#else
            var type = startType.GetGenericArguments().FirstOrDefault();
#endif
            for(int i = 1; i < depthOfLists; i++)
            {
#if NETSTANDARD1_1 || PCL && !NET35
                type = type.GenericTypeArguments.FirstOrDefault();
#else
                type = type.GetGenericArguments().FirstOrDefault();
#endif
            }
            return type;

        }

        public List<ParameterOutputProperty> GetParameterOutputsFromAttributes(Type type)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(type);
            return GetParameterOutputsFromAttributes(properties.ToArray());
        }

        public List<ParameterOutput> DecodeDefaultData(byte[] data, params Parameter[] inputParameters)
        {
            var parameterOutputs = new List<ParameterOutput>();

            foreach (var inputParameter in inputParameters)
            {
                inputParameter.DecodedType = inputParameter.ABIType.GetDefaultDecodingType();
                parameterOutputs.Add(new ParameterOutput
                {
                    Parameter = inputParameter
                });
            }

            return DecodeOutput(data, parameterOutputs.ToArray());
        }

        public List<ParameterOutput> DecodeDefaultData(string data, params Parameter[] inputParameters)
        { 
           return DecodeDefaultData(data.HexToByteArray(), inputParameters);
        }

        public List<ParameterOutput> DecodeOutput(byte[] outputBytes, params ParameterOutput[] outputParameters)
        {
            var currentIndex = 0;

            Array.Sort(outputParameters, (x, y) => x.Parameter.Order.CompareTo(y.Parameter.Order));

            foreach (var outputParam in outputParameters)
            {
                var param = outputParam.Parameter;
                if (param.ABIType.IsDynamic())
                {
                    var dataIndexStart = EncoderDecoderHelpers.GetNumberOfBytes(HeadWord(outputBytes, currentIndex));
                    if (dataIndexStart < 0 || dataIndexStart > outputBytes.Length)
                        throw new Exception($@"Encoded data offset out of range. Offset: {dataIndexStart}, Data: {outputBytes.Length}");
                    outputParam.DataIndexStart = dataIndexStart;
                    currentIndex = currentIndex + 32;
                }
                else
                {
                    var bytes = Nethereum.ABI.Decoders.AbiOffsetTable.Slice(outputBytes, currentIndex, param.ABIType.FixedSize);
                    outputParam.Result = param.ABIType.Decode(bytes, outputParam.Parameter.DecodedType);

                    currentIndex = currentIndex + param.ABIType.FixedSize;
                }
            }

            var dynamicCount = 0;
            foreach (var outputParam in outputParameters)
                if (outputParam.Parameter.ABIType.IsDynamic()) dynamicCount++;

            if (dynamicCount > 0)
            {
                var starts = new int[dynamicCount];
                var dynamicParameters = new ParameterOutput[dynamicCount];
                var next = 0;
                foreach (var outputParam in outputParameters)
                {
                    if (!outputParam.Parameter.ABIType.IsDynamic()) continue;
                    starts[next] = outputParam.DataIndexStart;
                    dynamicParameters[next++] = outputParam;
                }

                var ends = Nethereum.ABI.Decoders.AbiOffsetTable.ComputeEnds(starts, outputBytes.Length);
                for (var i = 0; i < dynamicCount; i++)
                {
                    var item = dynamicParameters[i];
                    var bytes = Nethereum.ABI.Decoders.AbiOffsetTable.Slice(outputBytes, starts[i], ends[i] - starts[i]);
                    item.Result = item.Parameter.ABIType.Decode(bytes, item.Parameter.DecodedType);
                }
            }

            return outputParameters.ToList();
        }


        private static byte[] HeadWord(byte[] data, int index)
        {
            var count = Math.Max(0, Math.Min(32, data.Length - index));
            var word = new byte[count];
            Array.Copy(data, index, word, 0, count);
            return word;
        }

        public List<ParameterOutput> DecodeOutput(string output, params ParameterOutput[] outputParameters)
        {
            var outputBytes = output.HexToByteArray();
            return DecodeOutput(outputBytes, outputParameters);
        }

        protected void InitTupleElementComponents(Type type, TupleType tupleType)
        {
            if (tupleType.Components == null)
                attributesToABIExtractor.InitTupleComponentsFromTypeAttributes(type,
                    tupleType);
        }
    }
}