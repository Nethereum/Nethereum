using Nethereum.ABI;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Exceptions;

namespace Nethereum.Mud
{

    public class ValueEncoderDecoder
    {
        public static byte[] EncodeField(FieldInfo field, object value)
        {
            if (field.ABIType is ArrayType arrayType)
            {
                return arrayType.EncodePackedUsingElementPacked(value);
            }
            return field.ABIType.EncodePacked(value);
        }

        public static byte[] EncodeField(FieldValue fieldValue)
        {
            return EncodeField(fieldValue, fieldValue.Value);
        }

        public static byte[] EncodeValuesAsyByteArray<T>(T value)
        {
            var parametersEncoder = new ABI.FunctionEncoding.ParametersEncoder();
            var values = parametersEncoder.GetParameterAttributeValues(typeof(T), value);
            values = values.OrderBy(x => x.ParameterAttribute.Order).ToList();
            var fieldValues = new List<FieldValue>();
            foreach (var valueAttribute in values)
            {
                fieldValues.Add(new FieldValue(valueAttribute.ParameterAttribute.Type, valueAttribute.Value, valueAttribute.ParameterAttribute.Name, valueAttribute.ParameterAttribute.Order));
            }
            return EncodeValuesAsyByteArray(fieldValues);
        }

        public static byte[] EncodeValuesAsyByteArray(List<FieldValue> values)
        {
            var encodedValues = EncodeValues(values);
            return ByteUtil.Merge(encodedValues.StaticData).Concat(encodedValues.EncodedLengths).Concat(ByteUtil.Merge(encodedValues.DynamicData)).ToArray();
        }

        public static EncodedValues EncodedValues<T>(T value)
        {
            var parametersEncoder = new ABI.FunctionEncoding.ParametersEncoder();
            var values = parametersEncoder.GetParameterAttributeValues(typeof(T), value);
            values = values.OrderBy(x => x.ParameterAttribute.Order).ToList();
            var fieldValues = new List<FieldValue>();
            foreach (var valueAttribute in values)
            {
                fieldValues.Add(new FieldValue(valueAttribute.ParameterAttribute.Type, valueAttribute.Value, valueAttribute.ParameterAttribute.Name, valueAttribute.ParameterAttribute.Order));
            }
            return EncodeValues(fieldValues);
        }

        public static EncodedValues EncodeValues(List<FieldValue> fieldValues)
        {
            var valueFields = fieldValues.Where(f => f.IsKey == false).OrderBy(f => f.Order).ToArray();
            var staticFields = valueFields.Where(f => f.ABIType.IsDynamic() == false).ToArray();
            var dynamicFields = valueFields.Where(f => f.ABIType.IsDynamic()).ToArray();

            if (dynamicFields.Length > SchemaEncoder.MAX_NUMBER_OF_DYNAMIC_FIELDS)
            {
                throw new SchemaInvalidNumberOfFieldsException();
            }

            if ((valueFields.Length + dynamicFields.Length) > SchemaEncoder.MAX_NUMBER_OF_FIELDS)
            {
                throw new SchemaInvalidNumberOfFieldsException();
            }

            var staticFieldsBytes = staticFields.Select(f => EncodeField(f)).ToArray();
            var dynamicFieldsBytes = dynamicFields.Select(f => EncodeField(f)).ToArray();
            var encodedLengths = EncodedLengthsEncoderDecoder.Encode(dynamicFieldsBytes);
            return new EncodedValues
            {
                StaticData = ByteUtil.Merge(staticFieldsBytes),
                EncodedLengths = encodedLengths,
                DynamicData = ByteUtil.Merge(dynamicFieldsBytes)
            };
           
        }

        public static List<object> DecodeValues(byte[] outputBytes, List<FieldInfo> fields)
        {
            return DecodeValuesToFieldValues(outputBytes, fields).Select(f => f.Value).ToList();
        }

        public static T DecodeValues<T>(string outputBytes) where T : new()
        {
            return DecodeValues<T>(outputBytes.HexToByteArray());
        }

        public static T DecodeValues<T>(byte[] staticData, byte[] encodedLengths, byte[] dynamicData) where T : new()
        {
           return DecodeValues<T>(new EncodedValues { StaticData = staticData, EncodedLengths = encodedLengths, DynamicData = dynamicData });
        }

        public static T DecodeValues<T>(byte[] outputBytes) where T : new()
        {
            var parameterDecoder = new ParameterDecoder();
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(typeof(T));
            var result = new T();
            var values = parameterDecoder.GetParameterOutputsFromAttributes(properties.ToArray());
            var parameterResults = DecodeValues(outputBytes, values.ToArray());

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

        public static T DecodeValues<T>(EncodedValues encodedValues) where T : new()
        {
            var encodedBytes = ByteUtil.Merge(encodedValues.StaticData).Concat(encodedValues.EncodedLengths).Concat(ByteUtil.Merge(encodedValues.DynamicData)).ToArray();
            return DecodeValues<T>(encodedBytes);

        }

        public static List<ParameterOutput> DecodeValues(EncodedValues encodedValues, params ParameterOutput[] outputParameters)
        {
            var encodedBytes = ByteUtil.Merge(encodedValues.StaticData).Concat(encodedValues.EncodedLengths).Concat(ByteUtil.Merge(encodedValues.DynamicData)).ToArray();
            return DecodeValues(encodedBytes, outputParameters);
        }

        public static List<ParameterOutput> DecodeValues(byte[] outputBytes, params ParameterOutput[] outputParameters)
        {
            Array.Sort(outputParameters, (x, y) => x.Parameter.Order.CompareTo(y.Parameter.Order));
            var staticFields = outputParameters.Where(f => f.Parameter.ABIType.IsDynamic() == false).ToArray();
            var currentIndex = 0;

            foreach (var field in staticFields)
            { 
                var abiType = field.Parameter.ABIType;
                var fieldSize = abiType.StaticSize;
                var bytes = outputBytes.Skip(currentIndex).Take(abiType.StaticSize).ToArray();
                var value = abiType.DecodePacked(bytes, field.Parameter.DecodedType);
                currentIndex += fieldSize;
                field.Result = value;
            }

            var dynamicFields = outputParameters.Where(f => f.Parameter.ABIType.IsDynamic() == true).ToList();
            var encodedLengths = EncodedLengthsEncoderDecoder.Decode(outputBytes.Skip(currentIndex).ToArray());
            currentIndex += 32;

            for (int i = 0; i < dynamicFields.Count(); i++)
            {
                var fieldSize = encodedLengths.Lengths[i];
                var bytes = outputBytes.Skip(currentIndex).Take(fieldSize).ToArray();
                object value;
                if (bytes.Length == 0)
                {
                    //check what direction is the padding or just empty
                    bytes = bytes.PadBytes(fieldSize);
                }

                if (dynamicFields[i].Parameter.ABIType is ArrayType arrayAbiType)
                {
                   
                    value = arrayAbiType.DecodePackedUsingElementPacked(bytes, dynamicFields[i].Parameter.DecodedType);

                }
                else
                {
                    value = dynamicFields[i].Parameter.ABIType.DecodePacked(bytes, dynamicFields[i].Parameter.DecodedType);

                }

                dynamicFields[i].Result = value;
                currentIndex += fieldSize;
            }

            return outputParameters.ToList();
        }

        public static List<FieldValue> DecodeValuesToFieldValues(byte[] outputBytes, List<FieldInfo> fields)
        {
            var staticFields = fields.Where(f => f.ABIType.IsDynamic() == false && f.IsKey == false).OrderBy(f => f.Order);
            var fieldValues = new List<FieldValue>();
            //var values = new List<object>();
            var currentIndex = 0;
            foreach (var field in staticFields)
            {
                var abiType = field.ABIType;
                var fieldSize = abiType.StaticSize;
                object value = DecodeValue(outputBytes, currentIndex, abiType);
                fieldValues.Add(new FieldValue(field.Type, value, field.Name, field.Order));
                currentIndex += fieldSize;
            }

            var dynamicFields = fields.Where(f => f.ABIType.IsDynamic() == true && f.IsKey == false).OrderBy(f => f.Order).ToArray();
            var encodedLengths = EncodedLengthsEncoderDecoder.Decode(outputBytes.Skip(currentIndex).ToArray());
            currentIndex += 32;
            for(int i = 0; i < dynamicFields.Length; i++)
            {
                var fieldSize = encodedLengths.Lengths[i];
                var bytes = outputBytes.Skip(currentIndex).Take(fieldSize).ToArray();
                object value;
                if(bytes.Length == 0)
                {
                    //check what direction is the padding or just empty
                    bytes = bytes.PadBytes(fieldSize);
                }

                if (dynamicFields[i].ABIType is ArrayType arrayAbiType)
                {
                   value = arrayAbiType.DecodePackedUsingElementPacked(bytes, arrayAbiType.GetDefaultDecodingType());
                   
                }
                else
                {
                   value = dynamicFields[i].ABIType.DecodePacked(bytes, dynamicFields[i].ABIType.GetDefaultDecodingType());
                    
                }

                fieldValues.Add(new FieldValue(dynamicFields[i].Type, value, dynamicFields[i].Name, dynamicFields[i].Order));
                currentIndex += fieldSize;
            }
        
            return fieldValues;
        }

        private static object DecodeValue(byte[] outputBytes, int currentIndex, ABIType abiType)
        {
            var bytes = outputBytes.Skip(currentIndex).Take(abiType.StaticSize).ToArray();
            var value = abiType.DecodePacked(bytes, abiType.GetDefaultDecodingType());
            return value;
        }
    }
}
