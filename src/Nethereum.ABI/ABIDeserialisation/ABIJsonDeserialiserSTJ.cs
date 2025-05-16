using System;
using System.Collections.Generic;
using Nethereum.ABI.Model;
using System.Linq;

#if NET6_0_OR_GREATER
using System.Text.Json;

namespace Nethereum.ABI.ABIDeserialisation
{

    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    [JsonSerializable(typeof(ABIElementSTJ))]
    [JsonSerializable(typeof(List<ABIElementSTJ>))]
    [JsonSerializable(typeof(ABIParameterSTJ))]
    [JsonSerializable(typeof(List<ABIParameterSTJ>))]
    public partial class ABIJsonDeserialiserSTJContext : JsonSerializerContext
    {
    }

    public class ABIElementSTJ
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("constant")]
        public bool Constant { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("anonymous")]
        public bool Anonymous { get; set; }

        [JsonPropertyName("stateMutability")]
        public string StateMutability { get; set; }

        [JsonPropertyName("payable")]
        public bool Payable { get; set; }

        [JsonPropertyName("inputs")]
        public List<ABIParameterSTJ> Inputs { get; set; }

        [JsonPropertyName("outputs")]
        public List<ABIParameterSTJ> Outputs { get; set; }

        public ABIElementSTJ()
        {
            Inputs = new List<ABIParameterSTJ>();
            Outputs = new List<ABIParameterSTJ>();
        }
    }

    public class ABIParameterSTJ
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("indexed")]
        public bool Indexed { get; set; }

        [JsonPropertyName("internalType")]
        public string InternalType { get; set; }

        [JsonPropertyName("components")]
        public List<ABIParameterSTJ> Components { get; set; } // For tuple types

        public ABIParameterSTJ()
        {
            Components = new List<ABIParameterSTJ>();
        }
    }


    public class ABIJsonDeserialiserSTJ
    {
        public ConstructorABI BuildConstructor(ABIElementSTJ constructorElement)
        {
            if (constructorElement != null && constructorElement.Type == "constructor")
            {
                var constructorABI = new ConstructorABI();
                constructorABI.InputParameters = MapParameters(constructorElement.Inputs).ToArray();
                return constructorABI;
            }

            return null;
        }

        public ContractABI DeserialiseContract(string abiJson)
        {
            abiJson = abiJson.Replace('\'', '"'); // fix for strict JSON
            var abiElements = JsonSerializer.Deserialize(
                    abiJson,
                    ABIJsonDeserialiserSTJContext.Default.ListABIElementSTJ
            );
            if (abiElements == null)
            {
                throw new InvalidOperationException("Failed to deserialize ABI JSON");
            }

            return new ContractABI
            {
                Functions = MapToFunctionAbi(abiElements).ToArray(),
                Events = MapToEventAbi(abiElements).ToArray(),
                Errors = MapToErrorAbi(abiElements).ToArray(),
                Constructor = BuildConstructor(abiElements.FirstOrDefault(e => e.Type == "constructor"))
            };
        }

        private IEnumerable<FunctionABI> MapToFunctionAbi(IEnumerable<ABIElementSTJ> abiElements)
        {
            return abiElements
                .Where(e => e.Type == "function")
                .Select(e => BuildFunction(e));
        }

        public FunctionABI BuildFunction(ABIElementSTJ functionElement)
        {
            var constant = functionElement.Constant;

            // For solidity >=0.6.0, check stateMutability
            if (!constant && functionElement.StateMutability == "view" || functionElement.StateMutability == "pure")
            {
                constant = true;
            }

            var functionABI = new FunctionABI(functionElement.Name, constant);
            functionABI.InputParameters = MapParameters(functionElement.Inputs).ToArray();
            functionABI.OutputParameters = MapParameters(functionElement.Outputs).ToArray();
            return functionABI;
        }

        private IEnumerable<EventABI> MapToEventAbi(IEnumerable<ABIElementSTJ> abiElements)
        {
            return abiElements
                .Where(e => e.Type == "event")
                .Select(BuildEvent);
        }

        public EventABI BuildEvent(ABIElementSTJ eventElement)
        {
            var eventABI = new EventABI(eventElement.Name, eventElement.Anonymous);
            eventABI.InputParameters = MapParameters(eventElement.Inputs).ToArray();

            return eventABI;
        }

        private IEnumerable<ErrorABI> MapToErrorAbi(IEnumerable<ABIElementSTJ> abiElements)
        {
            return abiElements
                .Where(e => e.Type == "error")
                .Select(e => new ErrorABI(e.Name)
                {
                    InputParameters = MapParameters(e.Inputs).ToArray()
                });
        }

        private IEnumerable<Parameter> MapParameters(List<ABIParameterSTJ> abiParameters)
        {
            var parameters = new List<Parameter>();
            int parameterOrder = 0;
            foreach (var input in abiParameters)
            {
                parameterOrder++;
                var parameter = new Parameter(input.Type, input.Name, parameterOrder, input.InternalType)
                {
                    Indexed = input.Indexed
                };

                InitialiseTupleComponents(input, parameter);

                parameters.Add(parameter);
            }

            return parameters;
        }

        private void InitialiseTupleComponents(ABIParameterSTJ abiParameter, Parameter parameter)
        {
            // Assuming abiParameter.Components is a List<ABIParameterSTJ> representing the tuple components
            if (parameter.ABIType is TupleType tupleType && abiParameter.Components != null)
            {
                tupleType.SetComponents(MapParameters(abiParameter.Components).ToArray());
            }

            // Handling nested array types
            var arrayType = parameter.ABIType as ArrayType;
            while (arrayType != null)
            {
                if (arrayType.ElementType is TupleType arrayTupleType && abiParameter.Components != null)
                {
                    arrayTupleType.SetComponents(MapParameters(abiParameter.Components).ToArray());
                    break; // Exit the loop once the components are set
                }
                else
                {
                    arrayType = arrayType.ElementType as ArrayType;
                }
            }
        }
    }
}

#endif
