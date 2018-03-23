using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Generators;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Newtonsoft.Json;

namespace Nethereum.Generator.Console.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var contractByteCode =
               "6060604052341561000f57600080fd5b6040516107ae3803806107ae833981016040528080519190602001805182019190602001805191906020018051600160a060020a03331660009081526001602052604081208790558690559091019050600383805161007292916020019061009f565b506004805460ff191660ff8416179055600581805161009592916020019061009f565b505050505061013a565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106100e057805160ff191683800117855561010d565b8280016001018555821561010d579182015b8281111561010d5782518255916020019190600101906100f2565b5061011992915061011d565b5090565b61013791905b808211156101195760008155600101610123565b90565b610665806101496000396000f3006060604052600436106100ae5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100b3578063095ea7b31461013d57806318160ddd1461017357806323b872dd1461019857806327e235e3146101c0578063313ce567146101df5780635c6581651461020857806370a082311461022d57806395d89b411461024c578063a9059cbb1461025f578063dd62ed3e14610281575b600080fd5b34156100be57600080fd5b6100c66102a6565b60405160208082528190810183818151815260200191508051906020019080838360005b838110156101025780820151838201526020016100ea565b50505050905090810190601f16801561012f5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b341561014857600080fd5b61015f600160a060020a0360043516602435610344565b604051901515815260200160405180910390f35b341561017e57600080fd5b6101866103b0565b60405190815260200160405180910390f35b34156101a357600080fd5b61015f600160a060020a03600435811690602435166044356103b6565b34156101cb57600080fd5b610186600160a060020a03600435166104bc565b34156101ea57600080fd5b6101f26104ce565b60405160ff909116815260200160405180910390f35b341561021357600080fd5b610186600160a060020a03600435811690602435166104d7565b341561023857600080fd5b610186600160a060020a03600435166104f4565b341561025757600080fd5b6100c661050f565b341561026a57600080fd5b61015f600160a060020a036004351660243561057a565b341561028c57600080fd5b610186600160a060020a036004358116906024351661060e565b60038054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561033c5780601f106103115761010080835404028352916020019161033c565b820191906000526020600020905b81548152906001019060200180831161031f57829003601f168201915b505050505081565b600160a060020a03338116600081815260026020908152604080832094871680845294909152808220859055909291907f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9259085905190815260200160405180910390a350600192915050565b60005481565b600160a060020a0380841660008181526002602090815260408083203390951683529381528382205492825260019052918220548390108015906103fa5750828110155b151561040557600080fd5b600160a060020a038085166000908152600160205260408082208054870190559187168152208054849003905560001981101561046a57600160a060020a03808616600090815260026020908152604080832033909416835292905220805484900390555b83600160a060020a031685600160a060020a03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef8560405190815260200160405180910390a3506001949350505050565b60016020526000908152604090205481565b60045460ff1681565b600260209081526000928352604080842090915290825290205481565b600160a060020a031660009081526001602052604090205490565b60058054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561033c5780601f106103115761010080835404028352916020019161033c565b600160a060020a033316600090815260016020526040812054829010156105a057600080fd5b600160a060020a033381166000818152600160205260408082208054879003905592861680825290839020805486019055917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9085905190815260200160405180910390a350600192915050565b600160a060020a039182166000908152600260209081526040808320939094168252919091522054905600a165627a7a723058201145b253e40a502d8bd264f98d66de641dec0c9e4a25e35eaba523821e0fb6ad0029";
            var abi =
                "[{'constant':true,'inputs':[],'name':'name','outputs':[{'name':'','type':'string'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'_spender','type':'address'},{'name':'_value','type':'uint256'}],'name':'approve','outputs':[{'name':'success','type':'bool'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'totalSupply','outputs':[{'name':'','type':'uint256'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'_from','type':'address'},{'name':'_to','type':'address'},{'name':'_value','type':'uint256'}],'name':'transferFrom','outputs':[{'name':'success','type':'bool'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[{'name':'','type':'address'}],'name':'balances','outputs':[{'name':'','type':'uint256'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'decimals','outputs':[{'name':'','type':'uint8'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'','type':'address'},{'name':'','type':'address'}],'name':'allowed','outputs':[{'name':'','type':'uint256'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'_owner','type':'address'}],'name':'balanceOf','outputs':[{'name':'balance','type':'uint256'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'symbol','outputs':[{'name':'','type':'string'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'_to','type':'address'},{'name':'_value','type':'uint256'}],'name':'transfer','outputs':[{'name':'success','type':'bool'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[{'name':'_owner','type':'address'},{'name':'_spender','type':'address'}],'name':'allowance','outputs':[{'name':'remaining','type':'uint256'}],'payable':false,'stateMutability':'view','type':'function'},{'inputs':[{'name':'_initialAmount','type':'uint256'},{'name':'_tokenName','type':'string'},{'name':'_decimalUnits','type':'uint8'},{'name':'_tokenSymbol','type':'string'}],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':true,'name':'_from','type':'address'},{'indexed':true,'name':'_to','type':'address'},{'indexed':false,'name':'_value','type':'uint256'}],'name':'Transfer','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'_owner','type':'address'},{'indexed':true,'name':'_spender','type':'address'},{'indexed':false,'name':'_value','type':'uint256'}],'name':'Approval','type':'event'}]";
            var contractAbi = new ABIDeserialiser().DeserialiseContract(abi);
            var basePath = @"C:\Users\juanf\Documents\source\repos\superTest";
            var projectGenerator = new CsharpLibraryGenerator("StandardToken.csproj");
            var generatedProject = projectGenerator.GenerateFileContent(basePath);

            OutputFile(generatedProject);

            var generator = new Generators.ContractProjectGenerator(contractAbi, "StandardToken", contractByteCode, "MyStandardToken", "Contract2.Service", "Contract2.CQS", "Contract2.DTOs", basePath, '\\');
            var generatedClasses = generator.GenerateAll();
     
            foreach (var generatedClass in generatedClasses)
            {
                OutputFile(generatedClass);
            }
        }

        private static void OutputFile(GeneratedFile generatedFile)
        {
            if (!Directory.Exists(generatedFile.OutputFolder))
                Directory.CreateDirectory(generatedFile.OutputFolder);

            using (var file = File.CreateText(Path.Combine(generatedFile.OutputFolder, generatedFile.FileName)))
            {
                file.Write(generatedFile.GeneratedCode);
                file.Flush();
            }
        }
    }

    public class ABIDeserialiser
    {
        public ConstructorABI BuildConstructor(IDictionary<string, object> constructor)
        {
            var constructorABI = new ConstructorABI();
            constructorABI.InputParameters = BuildFunctionParameters((List<object>)constructor["inputs"]);
            return constructorABI;
        }

        public EventABI BuildEvent(IDictionary<string, object> eventobject)
        {
            var eventABI = new EventABI((string)eventobject["name"]);
            eventABI.InputParameters = BuildEventParameters((List<object>)eventobject["inputs"]);

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
                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }

        public FunctionABI BuildFunction(IDictionary<string, object> function)
        {
            var functionABI = new FunctionABI((string)function["name"], (bool)function["constant"],
                TryGetSerpentValue(function));
            functionABI.InputParameters = BuildFunctionParameters((List<object>)function["inputs"]);
            functionABI.OutputParameters = BuildFunctionParameters((List<object>)function["outputs"]);
            return functionABI;
        }

        public Parameter[] BuildFunctionParameters(List<object> inputs)
        {
            var parameters = new List<Parameter>();
            var parameterOrder = 0;
            foreach (IDictionary<string, object> input in inputs)
            {
                parameterOrder = parameterOrder + 1;
                var parameter = new Parameter((string)input["type"], (string)input["name"], parameterOrder,
                    TryGetSignatureValue(input));
                parameters.Add(parameter);
            }

            return parameters.ToArray();
        }

        public ContractABI DeserialiseContract(string abi)
        {
            var convertor = new ExpandoObjectConverter();
            var contract = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(abi, convertor);
            var functions = new List<FunctionABI>();
            var events = new List<EventABI>();
            ConstructorABI constructor = null;

            foreach (IDictionary<string, object> element in contract)
            {
                if ((string)element["type"] == "function")
                    functions.Add(BuildFunction(element));
                if ((string)element["type"] == "event")
                    events.Add(BuildEvent(element));
                if ((string)element["type"] == "constructor")
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
                if (function.ContainsKey("serpent")) return (bool)function["serpent"];
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
                if (parameter.ContainsKey("signature")) return (string)parameter["signature"];
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    ///     This is a replication (copy) of Newtonsoft ExpandoObjectConverter to allow for PCL compilaton
    /// </summary>
    public class ExpandoObjectConverter : JsonConverter
    {
        /// <summary>
        ///     Gets a value indicating whether this <see cref="JsonConverter" /> can write JSON.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this <see cref="JsonConverter" /> can write JSON; otherwise, <c>false</c>.
        /// </value>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        ///     Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        ///     <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<string, object>);
        }

        /// <summary>
        ///     Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="JsonReader" /> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            return ReadValue(reader);
        }

        /// <summary>
        ///     Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="JsonWriter" /> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // can write is set to false
        }

        private bool IsPrimitiveToken(JsonToken token)
        {
            switch (token)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Undefined:
                case JsonToken.Null:
                case JsonToken.Date:
                case JsonToken.Bytes:
                    return true;
                default:
                    return false;
            }
        }

        private object ReadList(JsonReader reader)
        {
            IList<object> list = new List<object>();

            while (reader.Read())
                switch (reader.TokenType)
                {
                    case JsonToken.Comment:
                        break;
                    default:
                        var v = ReadValue(reader);

                        list.Add(v);
                        break;
                    case JsonToken.EndArray:
                        return list;
                }

            throw new Exception("Unexpected end.");
        }

        private object ReadObject(JsonReader reader)
        {
            IDictionary<string, object> expandoObject = new Dictionary<string, object>();

            while (reader.Read())
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        var propertyName = reader.Value.ToString();

                        if (!reader.Read())
                            throw new Exception("Unexpected end.");

                        var v = ReadValue(reader);

                        expandoObject[propertyName] = v;
                        break;
                    case JsonToken.Comment:
                        break;
                    case JsonToken.EndObject:
                        return expandoObject;
                }

            throw new Exception("Unexpected end.");
        }

        private object ReadValue(JsonReader reader)
        {
            while (reader.TokenType == JsonToken.Comment)
                if (!reader.Read())
                    throw new Exception("Unexpected end.");

            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    return ReadObject(reader);
                case JsonToken.StartArray:
                    return ReadList(reader);
                default:
                    if (IsPrimitiveToken(reader.TokenType))
                        return reader.Value;

                    throw new Exception("Unexpected token when converting ExpandoObject");
            }
        }
    }
}
