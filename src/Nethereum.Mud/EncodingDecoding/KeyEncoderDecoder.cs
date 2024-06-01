using Newtonsoft.Json.Linq;
using Nethereum.ABI;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Nethereum.Mud.EncodingDecoding
{

    public static class MudAbiExtensions
    {
        public static bool IsMudDynamic(this ABIType abiType)
        {
            return abiType.IsDynamic() || abiType is ArrayType;
        }
    }


    public class KeyEncoderDecoder
    {
        public static List<byte[]> EncodeKey<T>(T key)
        {
            if (key == null) return new List<byte[]>();
            var parametersEncoder = new ParametersEncoder();
            var keys = parametersEncoder.GetParameterAttributeValues(typeof(T), key);
            keys = keys.OrderBy(x => x.ParameterAttribute.Order).ToList();
            var fieldValues = new List<FieldValue>();
            foreach (var valueAttribute in keys)
            {
                fieldValues.Add(new FieldValue(valueAttribute.ParameterAttribute.Type, valueAttribute.Value, true, valueAttribute.ParameterAttribute.Name, valueAttribute.ParameterAttribute.Order));
            }
            return EncodeKey(fieldValues);
        }



        public static List<byte[]> EncodeKey(List<FieldValue> fieldValues)
        {
            var keyFields = fieldValues.Where(f => f.IsKey).OrderBy(f => f.Order).ToArray();
            var staticFields = keyFields.Where(f => f.ABIType.IsMudDynamic() == false).ToArray();
            var dynamicFields = keyFields.Where(f => f.ABIType.IsMudDynamic()).ToArray();
            if (dynamicFields.Length > 0)
            {
                throw new SchemaInvalidNumberOfFieldsException("Key cannot contain dynamic fields");
            }
            var staticFieldsBytes = staticFields.Select(f => f.ABIType.Encode(f.Value)).ToArray();
            return staticFieldsBytes.ToList();
        }

        public static List<FieldValue> DecodeKeyToFieldValues(byte[] outputBytes, List<FieldInfo> fields)
        {
            var keyFields = fields.Where(f => f.IsKey && f.ABIType.IsMudDynamic() == false).OrderBy(f => f.Order).ToArray();
            var fieldValues = new List<FieldValue>();
            var currentIndex = 0;
            foreach (var field in keyFields)
            {
                var fieldSize = field.ABIType.FixedSize;
                var bytes = outputBytes.Skip(currentIndex).Take(field.ABIType.FixedSize).ToArray();
                var value = field.ABIType.Decode(bytes, field.ABIType.GetDefaultDecodingType());
                fieldValues.Add(new FieldValue(field.Type, value, field.Name, field.Order));
                currentIndex += fieldSize;
            }
            return fieldValues;
        }

        public static T DecodeKey<T>(byte[] outputBytes) where T : new()
        {
            var parameterDecoder = new ParameterDecoder();
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(typeof(T));
            var result = new T();
            var values = parameterDecoder.GetParameterOutputsFromAttributes(properties.ToArray());
            var parameterResults = DecodeKey(outputBytes, values.ToArray());

            foreach (var parameterResult in parameterResults)
            {
                var parameter = (ParameterOutputProperty)parameterResult;
                var propertyInfo = parameter.PropertyInfo;
                var decodedResult = parameter.Result;
#if DOTNET35
                propertyInfo.SetValue(result, decodedResult, null);
#else
                propertyInfo.SetValue(result, decodedResult);
#endif
            }

            return result;
        }

        public static T DecodeKey<T>(List<byte[]> outputBytes) where T : new()
        {
            var parameterDecoder = new ParameterDecoder();
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(typeof(T));
            var result = new T();
            var values = parameterDecoder.GetParameterOutputsFromAttributes(properties.ToArray());
            var parameterResults = DecodeKey(outputBytes, values.ToArray());

            foreach (var parameterResult in parameterResults)
            {
                var parameter = (ParameterOutputProperty)parameterResult;
                var propertyInfo = parameter.PropertyInfo;
                var decodedResult = parameter.Result;
#if DOTNET35
                propertyInfo.SetValue(result, decodedResult, null);
#else
                propertyInfo.SetValue(result, decodedResult);
#endif
            }

            return result;
        }

        public static List<ParameterOutput> DecodeKey(List<byte[]> outputBytes, params ParameterOutput[] outputParameters)
        {
            Array.Sort(outputParameters, (x, y) => x.Parameter.Order.CompareTo(y.Parameter.Order));
            var staticFields = outputParameters.Where(f => f.Parameter.ABIType.IsMudDynamic() == false).ToArray();
            var currentIndex = 0;

            foreach (var field in staticFields)
            {
                var abiType = field.Parameter.ABIType;
                var fieldSize = abiType.FixedSize;
                var bytes = outputBytes[currentIndex];
                var value = abiType.Decode(bytes, field.Parameter.DecodedType);
                currentIndex++;
                field.Result = value;
            }
            return outputParameters.ToList();
        }

        public static List<ParameterOutput> DecodeKey(byte[] outputBytes, params ParameterOutput[] outputParameters)
        {
            Array.Sort(outputParameters, (x, y) => x.Parameter.Order.CompareTo(y.Parameter.Order));
            var staticFields = outputParameters.Where(f => f.Parameter.ABIType.IsMudDynamic() == false).ToArray();
            var currentIndex = 0;

            foreach (var field in staticFields)
            {
                var abiType = field.Parameter.ABIType;
                var fieldSize = abiType.FixedSize;
                var bytes = outputBytes.Skip(currentIndex).Take(abiType.FixedSize).ToArray();
                var value = abiType.Decode(bytes, field.Parameter.DecodedType);
                currentIndex += fieldSize;
                field.Result = value;
            }
            return outputParameters.ToList();
        }

        public static List<FieldValue> DecodeKeyToFieldValues(List<byte[]> outputBytes, List<FieldInfo> fields)
        {
            var keyFields = fields.Where(f => f.IsKey && f.ABIType.IsMudDynamic() == false).OrderBy(f => f.Order).ToArray();
            var fieldValues = new List<FieldValue>();
            var currentIndex = 0;
            foreach (var field in keyFields)
            {
                var fieldSize = field.ABIType.FixedSize;
                var value = field.ABIType.Decode(outputBytes[currentIndex], field.ABIType.GetDefaultDecodingType());
                fieldValues.Add(new FieldValue(field.Type, value, field.Name, field.Order));
                currentIndex++;
            }
            return fieldValues;
        }

        public static List<object> DecodeKey(byte[] outputBytes, List<FieldInfo> fields)
        {
            return DecodeKeyToFieldValues(outputBytes, fields).Select(f => f.Value).ToList();
        }

        public static List<object> DecodeKey(List<byte[]> outputBytes, List<FieldInfo> fields)
        {
            return DecodeKeyToFieldValues(outputBytes, fields).Select(f => f.Value).ToList();
        }
    }
}
