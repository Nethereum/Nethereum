using System.Collections.Generic;
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Nethereum.ABI.FunctionEncoding
{
    public class ABIDeserialiser
    {
        public ContractABI DeserialiseContract(string abi)
        {
            var convertor = new ExpandoObjectConverter();
            var contract = JsonConvert.DeserializeObject<List<ExpandoObject>>(abi, convertor);
            var functions = new List<FunctionABI>();
            var events = new List<EventABI>();
            ConstructorABI constructor = null;

            foreach (dynamic element in contract)
            {
                if (element.type == "function")
                {
                    functions.Add(BuildFunction(element));
                }
                if (element.type == "event")
                {
                    events.Add(BuildEvent(element));
                }
                if (element.type == "constructor")
                {
                    constructor = BuildConstructor(element);
                }
            }

            var contractABI = new ContractABI();
            contractABI.Functions = functions.ToArray();
            contractABI.Constructor = constructor;
            contractABI.Events = events.ToArray();

            return contractABI;

        }

        public ConstructorABI BuildConstructor(dynamic constructor)
        {
            var constructorABI = new ConstructorABI();
            constructorABI.InputParameters = BuildFunctionParameters(constructor.inputs);
            return constructorABI;
        }

        public FunctionABI BuildFunction(dynamic function)
        {
            var functionABI = new FunctionABI(function.name, function.constant, TryGetSerpentValue(function));
            functionABI.InputParameters = BuildFunctionParameters(function.inputs);
            functionABI.OutputParameters = BuildFunctionParameters(function.outputs);
            return functionABI;
        }

        public Parameter[] BuildFunctionParameters(dynamic inputs)
        {
            var parameters = new List<Parameter>();
            var parameterOrder = 0;
            foreach (dynamic input in inputs)
            {
                parameterOrder = parameterOrder + 1;
                var parameter = new Parameter(input.type, input.name, parameterOrder, TryGetSignatureValue(input));
                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }

        public bool TryGetSerpentValue(dynamic function)
        {
            try
            {
                return function.serpent;
            }
            catch
            {
                return false;
            }
        }

        public string TryGetSignatureValue(dynamic parameter)
        {
            try
            {
                return parameter.signature;
            }
            catch
            {
                return null;
            }
        }

        
        public EventABI BuildEvent(dynamic eventobject)
        {
            var eventABI = new EventABI(eventobject.name);
            eventABI.InputParameters = BuildEventParameters(eventobject.inputs);
       
            return eventABI;
        }

        public Parameter[] BuildEventParameters(dynamic inputs)
        {
            var parameters = new List<Parameter>();
            var parameterOrder = 0;
            foreach (dynamic input in inputs)
            {
                parameterOrder = parameterOrder + 1;
                var parameter = new Parameter(input.type, input.name, parameterOrder) {Indexed = input.indexed};
                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }
    }
}