using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.FunctionEncoding
{
    public class ParametersEncoder
    {
        private readonly IntTypeEncoder intTypeEncoder;

        public ParametersEncoder()
        {
            this.intTypeEncoder = new IntTypeEncoder();
        }

        public byte[] EncodeParametersFromTypeAttributes(Type type, object instanceValue)
        {
            var properties = type.GetProperties();

            var parameterObjects = new List<ParameterAttributeValue>();

            foreach (var property in properties)
            {
                if (property.IsDefined(typeof(ParameterAttribute), false))
                {
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>();
                    var propertyValue = property.GetValue(instanceValue);
                    parameterObjects.Add(new ParameterAttributeValue() { ParameterAttribute = parameterAttribute, Value = propertyValue });
                }
            }

            var abiParameters = parameterObjects.OrderBy(x => x.ParameterAttribute.Order).Select(x => x.ParameterAttribute.Parameter).ToArray();
            var objectValues = parameterObjects.OrderBy(x => x.ParameterAttribute.Order).Select(x => x.Value).ToArray();
            return EncodeParameters(abiParameters, objectValues);

        }

        public byte[] EncodeParameters(Parameter[] parameters, params object[] values)
        {

            if (values.Length > parameters.Length)
            {
                throw new Exception("Too many arguments: " + values.Length + " > " + parameters.Length);
            }

            int staticSize = 0;
            int dynamicCount = 0;
            // calculating static size and number of dynamic params
            for (int i = 0; i < values.Length; i++)
            {
                var parameter = parameters[i];
                int parameterSize = parameter.ABIType.FixedSize;
                if (parameterSize < 0)
                {
                    dynamicCount++;
                    staticSize += 32;
                }
                else
                {
                    staticSize += parameterSize;
                }
            }

            byte[][] encodedBytes = new byte[values.Length + dynamicCount][];

            int currentDynamicPointer = staticSize;
            int currentDynamicCount = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (parameters[i].ABIType.IsDynamic())
                {
                    byte[] dynamicValueBytes = parameters[i].ABIType.Encode(values[i]);
                    encodedBytes[i] = intTypeEncoder.EncodeInt(currentDynamicPointer);
                    encodedBytes[values.Length + currentDynamicCount] = dynamicValueBytes;
                    currentDynamicCount++;
                    currentDynamicPointer += dynamicValueBytes.Length;
                }
                else
                {
                    encodedBytes[i] = parameters[i].ABIType.Encode(values[i]);
                }
            }
            return ByteUtil.Merge(encodedBytes);

        }

        
    }
}