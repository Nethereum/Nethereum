using System.Collections.Generic;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Generators.Model;
//using System.Dynamic;
using Newtonsoft.Json;

namespace Nethereum.Generators.Net
{
    public class GeneratorModelABIDeserialiser
    {
        private  StructABIDeserialiser _structDeserialiser = new StructABIDeserialiser();

        public ConstructorABI BuildConstructor(IDictionary<string, object> constructor)
        {
            return new ConstructorABI()
            {
                InputParameters = this.BuildFunctionParameters((List<object>)constructor["inputs"])
            };
        }

        public EventABI BuildEvent(IDictionary<string, object> eventobject, ContractABI contractAbi)
        {
            return new EventABI((string)eventobject["name"], contractAbi)
            {
                InputParameters = this.BuildEventParameters((List<object>)eventobject["inputs"])
            };
        }

        public ParameterABI[] BuildEventParameters(List<object> inputs)
        {
            var parameterList = new List<ParameterABI>();
            var order = 0;
            foreach (IDictionary<string, object> input in inputs)
            {
                ++order;
                var parameter = new ParameterABI((string)input["type"], (string)input["name"], order, _structDeserialiser.TryGetStructInternalType(input))
                {
                    Indexed = (bool)input["indexed"]
                };
                parameterList.Add(parameter);
            }
            return parameterList.ToArray();
        }

        public FunctionABI BuildFunction(IDictionary<string, object> function, ContractABI contractAbi)
        {
            var constant = false;
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

            return new FunctionABI((string)function["name"], constant, contractAbi)
            {
                InputParameters = this.BuildFunctionParameters((List<object>)function["inputs"]),
                OutputParameters = this.BuildFunctionParameters((List<object>)function["outputs"])
            };
        }

        public ParameterABI[] BuildFunctionParameters(List<object> inputs)
        {
            var parameterList = new List<ParameterABI>();
            var order = 0;
            foreach (IDictionary<string, object> input in inputs)
            {
                ++order;
                var parameter = new ParameterABI((string)input["type"], (string)input["name"], order, _structDeserialiser.TryGetStructInternalType(input));
                parameterList.Add(parameter);
            }
            return parameterList.ToArray();
        }

        public ContractABI DeserialiseABI(string abi)
        {
            var expandoObjectConverter = new ExpandoObjectConverter();
            var dictionaryList = JsonConvert.DeserializeObject<List<IDictionary<string, object>>>(abi, expandoObjectConverter);
            var functionAbiList = new List<FunctionABI>();
            var eventAbiList = new List<EventABI>();
            var constructorAbi = new ConstructorABI();
            var contractAbi = new ContractABI();
            foreach (IDictionary<string, object> dictionary in dictionaryList)
            {
                if ((string)dictionary["type"] == "function")
                    functionAbiList.Add(this.BuildFunction(dictionary, contractAbi));
                if ((string)dictionary["type"] == "event")
                    eventAbiList.Add(this.BuildEvent(dictionary, contractAbi));
                if ((string)dictionary["type"] == "constructor")
                    constructorAbi = this.BuildConstructor(dictionary);
            }

            contractAbi.Functions = functionAbiList.ToArray();
            contractAbi.Constructor = constructorAbi;
            contractAbi.Events = eventAbiList.ToArray();
        

           
            var structs = _structDeserialiser.GetStructsFromAbi(abi);
            contractAbi.Structs = structs;
            _structDeserialiser.SetTupleTypeSameAsNameIfRequired(contractAbi);

            return contractAbi;
        }


       
    }
}