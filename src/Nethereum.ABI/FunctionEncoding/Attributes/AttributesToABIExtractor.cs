using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.Model;

namespace Nethereum.ABI.FunctionEncoding.Attributes
{
    public static class PropertiesExtractor
    {
        public static IEnumerable<PropertyInfo> GetProperties(Type type)
        {
#if DOTNET35
            var hidingProperties = type.GetProperties().Where(x => x.IsHidingMember());
            var nonHidingProperties = type.GetProperties().Where(x => hidingProperties.All(y => y.Name != x.Name));
            return nonHidingProperties.Concat(hidingProperties);
#else
            var hidingProperties = type.GetRuntimeProperties().Where(x => x.IsHidingMember());
            var nonHidingProperties = type.GetRuntimeProperties().Where(x => hidingProperties.All(y => y.Name != x.Name));
            return nonHidingProperties.Concat(hidingProperties);
#endif
        }

        public static IEnumerable<PropertyInfo> GetPropertiesWithParameterAttribute(Type type)
        {
            return GetProperties(type).Where(x => x.IsDefined(typeof(ParameterAttribute), true));
        }
    }

    public class AttributesToABIExtractor
    {
        public ContractABI ExtractContractABI(params Type[] contractMessagesTypes)
        {
            var contractABI = new ContractABI();
            var functions = new List<FunctionABI>();
            var events = new List<EventABI>();

            foreach (var contractMessageType in contractMessagesTypes)
            {
                if (FunctionAttribute.IsFunctionType(contractMessageType))
                {
                    functions.Add(ExtractFunctionABI(contractMessageType));
                }

                if (EventAttribute.IsEventType(contractMessageType))
                {
                    events.Add(ExtractEventABI(contractMessageType));
                }
            }

            contractABI.Functions = functions.ToArray();
            contractABI.Events = events.ToArray();
            return contractABI;
        }

        public FunctionABI ExtractFunctionABI(Type contractMessageType)
        {
            if (FunctionAttribute.IsFunctionType(contractMessageType))
            {
                var functionAttribute = FunctionAttribute.GetAttribute(contractMessageType);
                var functionABI = new FunctionABI(functionAttribute.Name, false);
                functionABI.InputParameters = ExtractParametersFromAttributes(contractMessageType);

                if (functionAttribute.DTOReturnType != null)
                {
                    functionABI.OutputParameters = ExtractParametersFromAttributes(contractMessageType);
                }
                else if (functionAttribute.ReturnType != null)
                {
                    var parameter = new Parameter(functionAttribute.ReturnType);
                    functionABI.OutputParameters = new Parameter[] { parameter };
                }
                return functionABI;
            }
            return null;
        }

        public EventABI ExtractEventABI(Type contractMessageType)
        {
            if (EventAttribute.IsEventType(contractMessageType))
            {
                var eventAttribute = EventAttribute.GetAttribute(contractMessageType);
                var eventABI = new EventABI(eventAttribute.Name);
                eventABI.InputParameters = ExtractParametersFromAttributes(contractMessageType);
                return eventABI;
            }
            return null;
        }

        public Parameter[] ExtractParametersFromAttributes(Type contractMessageType)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(contractMessageType);
            var parameters = new List<Parameter>();

            foreach (var property in properties)
            {
                var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>(true);
                if (parameterAttribute.Parameter.ABIType is TupleType tupleType)
                {
                    InitTupleComponentsFromTypeAttributes(property.PropertyType, tupleType);
                }
                parameters.Add(parameterAttribute.Parameter);   
            }
            return parameters.ToArray();
        }


        public void InitTupleComponentsFromTypeAttributes(Type type, TupleType parameter)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(type);
            var parameters = new List<Parameter>();
                var parameterObjects = new List<Parameter>();

            foreach (var property in properties)
             {
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>(true);
                    parameterAttribute.Parameter.DecodedType = property.PropertyType;
                    if (parameterAttribute.Parameter.ABIType is TupleType tupleType)
                    {
                        InitTupleComponentsFromTypeAttributes(property.PropertyType, tupleType);
                    }
                   
                    parameterObjects.Add(parameterAttribute.Parameter);
            }
            parameter.SetComponents(parameterObjects.ToArray());
        }
    }
}