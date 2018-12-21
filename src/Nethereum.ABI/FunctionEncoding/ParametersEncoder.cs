using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.ABI.Util;
using Nethereum.Util;

namespace Nethereum.ABI.FunctionEncoding
{

    public static class PropertyInfoExtensions
    {
#if DOTNET35
        public static bool IsHidingMember(this PropertyInfo self)
        {
            Type baseType = self.DeclaringType.GetTypeInfo().BaseType;
            PropertyInfo baseProperty = baseType.GetProperty(self.Name);

            if (baseProperty == null)
            {
                return false;
            }

            if (baseProperty.DeclaringType == self.DeclaringType)
            {
                return false;
            }

            var baseMethodDefinition = baseProperty.GetGetMethod().GetBaseDefinition();
            var thisMethodDefinition = self.GetGetMethod().GetBaseDefinition();


            return baseMethodDefinition.DeclaringType != thisMethodDefinition.DeclaringType;
        }
#else
        public static bool IsHidingMember(this PropertyInfo self)
        {
            Type baseType = self.DeclaringType.GetTypeInfo().BaseType;
            PropertyInfo baseProperty = baseType.GetRuntimeProperty(self.Name);

            if (baseProperty == null)
            {
                return false;
            }

            if (baseProperty.DeclaringType == self.DeclaringType)
            {
                return false;
            }

            var baseMethodDefinition = baseProperty.GetMethod.GetRuntimeBaseDefinition();
            var thisMethodDefinition = self.GetMethod.GetRuntimeBaseDefinition();

            return baseMethodDefinition.DeclaringType != thisMethodDefinition.DeclaringType;
        }
#endif
    }

    public class ParametersEncoder
    {
        private readonly IntTypeEncoder intTypeEncoder;
        private readonly AttributesToABIExtractor attributesToABIExtractor;

        public ParametersEncoder()
        {
            intTypeEncoder = new IntTypeEncoder();
            attributesToABIExtractor = new AttributesToABIExtractor();
        }

        public byte[] EncodeParameters(Parameter[] parameters, params object[] values)
        {
            if ((values == null) && (parameters.Length > 0))
                throw new ArgumentNullException(nameof(values), "No values specified for encoding");

            if (values == null) return new byte[] {};

            if (values.Length > parameters.Length)
                throw new Exception("Too many arguments: " + values.Length + " > " + parameters.Length);

            var staticSize = 0;
            var dynamicCount = 0;
            // calculating static size and number of dynamic params
            for (var i = 0; i < values.Length; i++)
            {
                var parameter = parameters[i];
                var parameterSize = parameter.ABIType.FixedSize;
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

            var encodedBytes = new byte[values.Length + dynamicCount][];

            var currentDynamicPointer = staticSize;
            var currentDynamicCount = 0;
            for (var i = 0; i < values.Length; i++)
                if (parameters[i].ABIType.IsDynamic())
                {
                    var dynamicValueBytes = parameters[i].ABIType.Encode(values[i]);
                    encodedBytes[i] = intTypeEncoder.EncodeInt(currentDynamicPointer);
                    encodedBytes[values.Length + currentDynamicCount] = dynamicValueBytes;
                    currentDynamicCount++;
                    currentDynamicPointer += dynamicValueBytes.Length;
                }
                else
                {
                    try
                    {
                        encodedBytes[i] = parameters[i].ABIType.Encode(values[i]);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            $"An error occurred encoding parameter value. Parameter Order: '{parameters[i].Order}', Name: '{parameters[i].Name}', Value: '{values[i] ?? "null"}'.  Ensure the value is valid for the parameter type.",
                            ex);
                    }
                }
            return ByteUtil.Merge(encodedBytes);
        }

        

        public byte[] EncodeParametersFromTypeAttributes(Type type, object instanceValue)
        {

            var parameterObjects = GetParameterAttributeValues(type, instanceValue);
            var abiParameters = GetParametersInOrder(parameterObjects);
            var objectValues = GetValuesInOrder(parameterObjects);
            return EncodeParameters(abiParameters, objectValues);
        }

        public object[] GetValuesInOrder(List<ParameterAttributeValue> parameterObjects)
        {
            return parameterObjects.OrderBy(x => x.ParameterAttribute.Order).Select(x => x.Value).ToArray();
        }

        public Parameter[] GetParametersInOrder(List<ParameterAttributeValue> parameterObjects)
        {
            return parameterObjects.OrderBy(x => x.ParameterAttribute.Order)
                .Select(x => x.ParameterAttribute.Parameter)
                .ToArray();
        }

        public List<ParameterAttributeValue> GetParameterAttributeValues(Type type, object instanceValue)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(type);
            var parameterObjects = new List<ParameterAttributeValue>();

            foreach (var property in properties)
            {
                var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>(true);
#if DOTNET35
                    var propertyValue = property.GetValue(instanceValue, null);
#else
                var propertyValue = property.GetValue(instanceValue);
#endif

                if (parameterAttribute.Parameter.ABIType is TupleType tupleType)
                {
                    attributesToABIExtractor.InitTupleComponentsFromTypeAttributes(property.PropertyType, tupleType);
                    propertyValue = GetTupleComponentValuesFromTypeAttributes(property.PropertyType, propertyValue);
                }

                parameterObjects.Add(new ParameterAttributeValue
                {
                    ParameterAttribute = parameterAttribute,
                    Value = propertyValue
                });
            }

            return parameterObjects;
        }

        public object[] GetTupleComponentValuesFromTypeAttributes(Type type, object instanceValue)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(type);

            var propertiesInOrder = properties.Where(x => x.IsDefined(typeof(ParameterAttribute), true))
                .OrderBy(x => x.GetCustomAttribute<ParameterAttribute>(true).Order);

            var parameterObjects = new List<object>();

            foreach (var property in propertiesInOrder)
            {
                var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>(true);

#if DOTNET35
                var propertyValue = property.GetValue(instanceValue, null);
#else
                var propertyValue = property.GetValue(instanceValue);
#endif

                if (parameterAttribute.Parameter.ABIType is TupleType)
                {
                    propertyValue = GetTupleComponentValuesFromTypeAttributes(property.PropertyType, propertyValue);
                }

                parameterObjects.Add(propertyValue);
            }

            return parameterObjects.ToArray();
        }
 
    }
 }

   