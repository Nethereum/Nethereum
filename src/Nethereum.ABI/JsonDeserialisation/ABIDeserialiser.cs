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
            var functionABI = new FunctionABI(function.name, function.constant);
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
                var parameter = new Parameter(input.type, input.name, parameterOrder);
                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }

        public EventABI BuildEvent(dynamic eventj)
        {
            var eventABI = new EventABI(eventj.name);
            eventABI.InputParameters = BuildFunctionParameters(eventj.inputs);
       
            return eventABI;
        }

        public EventParameter[] BuildEventParameters(dynamic inputs)
        {
            var parameters = new List<EventParameter>();
            var parameterOrder = 0;
            foreach (dynamic input in inputs)
            {
                parameterOrder = parameterOrder + 1;
                var parameter = new EventParameter(input.type, input.indexed, input.name, parameterOrder);
                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }
    }
}