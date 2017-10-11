using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Nethereum.ABI.FunctionEncoding.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EventAttribute : Attribute
    {
        public EventAttribute(string name)
        {
            this.Name = name;
        }

        public string Name { get; set; }

        public static EventAttribute GetAttribute<T>()
        {
            var type = typeof(T);
            return GetAttribute(type);
        }

        public static EventAttribute GetAttribute(Type type)
        {
            return type.GetTypeInfo().GetCustomAttribute<EventAttribute>();
        }

        public static EventAttribute GetAttribute(object instance)
        {
            var type = instance.GetType();
            return GetAttribute(type);
        }

        public static bool IsEventType<T>()
        {
            return GetAttribute<T>() != null;
        }

        public static bool IsEventType(Type type)
        {
            return GetAttribute(type) != null;
        }

        public static bool IsEventType(object type)
        {
            return GetAttribute(type) != null;
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
#if DOTNET35
            var properties = contractMessageType.GetTypeInfo().DeclaredProperties();
#else
            var properties = contractMessageType.GetTypeInfo().DeclaredProperties;
#endif
            var parameters = new List<Parameter>();

            foreach (var property in properties)
            {
                if (property.IsDefined(typeof(ParameterAttribute), false))
                {
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>();
                    parameters.Add(parameterAttribute.Parameter);
                }
            }

            return parameters.ToArray();
        }
    }
}