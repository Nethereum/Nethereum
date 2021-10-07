using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;

namespace Nethereum.Contracts
{
    public static class ABITypedRegistry
    {
        private static readonly ConcurrentDictionary<Type, FunctionABI> _functionAbiRegistry = new ConcurrentDictionary<Type, FunctionABI>();
        private static readonly ConcurrentDictionary<Type, EventABI> _eventAbiRegistry = new ConcurrentDictionary<Type, EventABI>();
        private static readonly ConcurrentDictionary<Type, ErrorABI> _errorAbiRegistry = new ConcurrentDictionary<Type, ErrorABI>();

        private static readonly AttributesToABIExtractor _abiExtractor = new AttributesToABIExtractor();

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

        public static ErrorABI GetError<TError>()
        {
            return GetError(typeof(TError));
        }

        public static ErrorABI GetError(Type type)
        {
            if (!_errorAbiRegistry.ContainsKey(type))
            {
                var errorABI = _abiExtractor.ExtractErrorABI(type);
                if (null == errorABI)
                {
                    throw new ArgumentException(type.ToString() + " is not a valid Error Type");
                }

                _errorAbiRegistry[type] = errorABI;
            }
            return _errorAbiRegistry[type];
        }
    }
}