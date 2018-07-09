using System;
using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;

namespace Nethereum.Contracts
{
    public static class ABITypedRegistry
    {
        private static Dictionary<Type, FunctionABI> _functionAbiRegistry = new Dictionary<Type, FunctionABI>();
        private static Dictionary<Type, EventABI> _eventAbiRegistry = new Dictionary<Type, EventABI>();
        private static AttributesToABIExtractor _abiExtractor = new AttributesToABIExtractor();

        public static FunctionABI GetFunctionABI<TFunctionMessage>()
        {
            return GetFunctionABI(typeof(TFunctionMessage));
        }

        public static FunctionABI GetFunctionABI(Type functionABIType)
        {
            if (!_functionAbiRegistry.ContainsKey(functionABIType))
            {
                var functionAbi = _abiExtractor.ExtractFunctionABI(functionABIType);
                if (functionAbi == null)
                {
                    throw new ArgumentException(functionABIType.ToString() + " is not a valid Function Type");
                }

                _functionAbiRegistry[functionABIType] = functionAbi;
            }
            return _functionAbiRegistry[functionABIType];
        }

        public static EventABI GetEvent<TEvent>()
        {
            return GetEvent(typeof(TEvent));
        }

        public static EventABI GetEvent(Type type)
        {
            if (!_eventAbiRegistry.ContainsKey(type))
            {
                var eventABI = _abiExtractor.ExtractEventABI(type);
                if (null == eventABI)
                {
                    throw new ArgumentException(type.ToString() + " is not a valid Event Type");
                }

                _eventAbiRegistry[type] = eventABI;
            }
            return _eventAbiRegistry[type];
        }
    }
}