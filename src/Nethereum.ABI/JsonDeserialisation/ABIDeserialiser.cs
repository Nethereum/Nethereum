using System.Collections.Generic;
using Nethereum.ABI.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.ABI.JsonDeserialisation
{
    public class ABIDeserialiser
    {
        public ConstructorABI BuildConstructor(IDictionary<string, object> constructor)
        {
            var constructorABI = new ConstructorABI();
            constructorABI.InputParameters = BuildFunctionParameters((List<object>) constructor["inputs"]);
            return constructorABI;
        }

        public EventABI BuildEvent(IDictionary<string, object> eventobject)
        {
            var eventABI = new EventABI((string) eventobject["name"], (bool) eventobject["anonymous"]);
            eventABI.InputParameters = BuildEventParameters((List<object>) eventobject["inputs"]);

            return eventABI;
        }

        public Parameter[] BuildEventParameters(List<object> inputs)
        {
            var parameters = new List<Parameter>();
            var parameterOrder = 0;
            foreach (IDictionary<string, object> input in inputs)
            {
                parameterOrder = parameterOrder + 1;
                var parameter = new Parameter((string)input["type"], (string)input["name"], parameterOrder)
                {
                    Indexed = (bool)input["indexed"]
                };
                InitialiseTupleComponents(input, parameter);

                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }

        private void InitialiseTupleComponents(IDictionary<string, object> input, Parameter parameter)
        {
            if (parameter.ABIType is TupleType tupleType)
            {
                tupleType.SetComponents(BuildFunctionParameters((List<object>)input["components"]));
            }

            var arrayType = parameter.ABIType as ArrayType;

            while (arrayType != null)
            {

                if (arrayType.ElementType is TupleType arrayTupleType)
                {
                    arrayTupleType.SetComponents(BuildFunctionParameters((List<object>)input["components"]));
                    arrayType = null;
                }
                else
                {
                    arrayType = arrayType.ElementType as ArrayType;
                }
            }
        }

        public FunctionABI BuildFunction(IDictionary<string, object> function)
        {
            bool constant = false;
            if (function.ContainsKey("constant"))
            {
                constant = (bool)function["constant"];
            }
            else
            {
                // for solidity >=0.6.0
                if (function.ContainsKey("stateMutability") && ((string)function["stateMutability"] == "view" || (string)function["stateMutability"] == "pure"))
                    constant = true;
            }

            var functionABI = new FunctionABI((string) function["name"], constant,
                TryGetSerpentValue(function));
            functionABI.InputParameters = BuildFunctionParameters((List<object>) function["inputs"]);
            functionABI.OutputParameters = BuildFunctionParameters((List<object>) function["outputs"]);
            return functionABI;
        }

        public Parameter[] BuildFunctionParameters(List<object> inputs)
        {
            var parameters = new List<Parameter>();
            var parameterOrder = 0;
            foreach (IDictionary<string, object> input in inputs)
            {
                parameterOrder = parameterOrder + 1;
                var parameter = new Parameter((string) input["type"], (string) input["name"], parameterOrder,
                    TryGetSignatureValue(input));

                InitialiseTupleComponents(input, parameter);

                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }

        public ContractABI DeserialiseContract(string abi) 
        {
            var convertor = new ExpandoObjectConverter();
            var contract = JsonConvert.DeserializeObject<List<IDictionary<string, object>>>(abi, convertor);
            return DeserialiseContractBody(contract);
        }
        public ContractABI DeserialiseContract(JArray abi) {
            var convertor = new JObjectToExpandoConverter();
            return DeserialiseContractBody(convertor.JObjectArray(abi));
        }

        private ContractABI DeserialiseContractBody(List<IDictionary<string, object>> contract)
        {
            var functions = new List<FunctionABI>();
            var events = new List<EventABI>();
            ConstructorABI constructor = null;

            foreach (IDictionary<string, object> element in contract)
            {
                if ((string) element["type"] == "function")
                    functions.Add(BuildFunction(element));
                if ((string) element["type"] == "event")
                    events.Add(BuildEvent(element));
                if ((string) element["type"] == "constructor")
                    constructor = BuildConstructor(element);
            }

            var contractABI = new ContractABI();
            contractABI.Functions = functions.ToArray();
            contractABI.Constructor = constructor;
            contractABI.Events = events.ToArray();

            return contractABI;
        }

        public bool TryGetSerpentValue(IDictionary<string, object> function)
        {
            try
            {
                if (function.ContainsKey("serpent")) return (bool) function["serpent"];
                return false;
            }
            catch
            {
                return false;
            }
        }

        public string TryGetSignatureValue(IDictionary<string, object> parameter)
        {
            try
            {
                if (parameter.ContainsKey("signature")) return (string) parameter["signature"];
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}