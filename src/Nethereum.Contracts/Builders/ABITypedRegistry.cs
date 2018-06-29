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
            if (!_functionAbiRegistry.ContainsKey(typeof(TFunctionMessage)))
            {
                _functionAbiRegistry[typeof(TFunctionMessage)] =_abiExtractor.ExtractFunctionABI(typeof(TFunctionMessage));
            }
            return _functionAbiRegistry[typeof(TFunctionMessage)];
        }

        public static EventABI GetEvent<TEvent>()
        {
            if (!_eventAbiRegistry.ContainsKey(typeof(TEvent)))
            {
                _eventAbiRegistry[typeof(TEvent)] = _abiExtractor.ExtractEventABI(typeof(TEvent));
            }
            return _eventAbiRegistry[typeof(TEvent)];
        }
    }
}