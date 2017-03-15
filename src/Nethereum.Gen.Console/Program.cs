using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Gen;
using Nethereum.ABI.Model;

namespace Nethereum.Gen.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var abi =
                        @"[{""constant"":false,""inputs"":[{""name"":""registeredAddress"",""type"":""address""}],""name"":""unregister"",""outputs"":[],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""registeredAddress"",""type"":""address""}],""name"":""register"",""outputs"":[],""type"":""function""},{""constant"":true,""inputs"":[{""name"":"""",""type"":""address""}],""name"":""records"",""outputs"":[{""name"":""registeredAddress"",""type"":""address""},{""name"":""owner"",""type"":""address""},{""name"":""time"",""type"":""uint256""},{""name"":""Id"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":"""",""type"":""uint256""}],""name"":""workRegistered"",""outputs"":[{""name"":"""",""type"":""address""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""numRecords"",""outputs"":[{""name"":"""",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""maxId"",""outputs"":[{""name"":"""",""type"":""uint256""}],""type"":""function""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""registeredAddress"",""type"":""address""},{""indexed"":true,""name"":""id"",""type"":""uint256""},{""indexed"":true,""name"":""owner"",""type"":""address""},{""indexed"":false,""name"":""time"",""type"":""uint256""}],""name"":""Registered"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""registeredAddress"",""type"":""address""},{""indexed"":true,""name"":""id"",""type"":""uint256""}],""name"":""Unregistered"",""type"":""event""}]";
            var contractName = "ContractRegistry";

            var generator = new ContractServiceGenerator();
            var fileName = contractName + "Service.cs";
            var genContract = generator.ContractGen(abi, contractName, "Nethereum.ContractRegistry");
            var fileOutput = System.IO.File.CreateText(fileName);
            fileOutput.Write(genContract);
            fileOutput.Flush();

            System.Console.WriteLine("Generated " + fileName);

        }

        public class ContractServiceGenerator
        {
            public string NameSpaceTemplate =
    @"
using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

namespace Nethereum.{0}
{{
    {1}
}}
";

            public string ContractTemplate =

    @"public class {1}Service
{{
        private readonly Web3.Web3 web3;
        private string abi = @""{0}"";
        private Contract contract;
        public {1}Service(Web3.Web3 web3, string address)
        {{
            this.web3 = web3;
            this.contract = web3.Eth.GetContract(abi, address);
        }}
        
        {2}
}}
";

            public string FunctionGetTemplate =
    @"public Function Get{1}Function()
{{
   return contract.GetFunction(""{0}"");
}}";

            public string EventGetTemplate =
    @"public Event Get{1}Event()
{{
   return contract.GetEvent(""{0}"");
}}";

            public string FunctionCallTemplate =
    @"public async Task{3} {0}AsyncCall({2})
{{
   var function = Get{0}Function();
   return await function.CallAsync{3}({1});
}}";

            public string FunctionTransactionTemplate =
    @"public async Task<string> {0}Async(string addressFrom, {2} HexBigInteger gas = null, HexBigInteger valueAmount = null)
{{
    var function = Get{0}Function();
    return await function.SendTransactionAsync(addressFrom, gas, valueAmount, {1});
}}";
            public string ContractGen(string abi, string contractName, string nameSpace)
            {
                contractName = MakeFirstCharUpper(contractName);
                var des = new ABI.JsonDeserialisation.ABIDeserialiser();
                var contract = des.DeserialiseContract(abi);
                var operations = EventsGen(contract) + FunctionsGen(contract);
                var genContract = string.Format(ContractTemplate, abi.Replace("\"", "\"\""), contractName, operations);
                return string.Format(NameSpaceTemplate, nameSpace, genContract);
            }

            public string EventsGen(ContractABI contract)
            {
                var builder = new StringBuilder();
                foreach (var eventAbi in contract.Events)
                {
                    builder.AppendLine(EventGet(eventAbi.Name));
                }
                return builder.ToString();
            }

            public string FunctionsGen(ContractABI contract)
            {
                var builder = new StringBuilder();
                foreach (var function in contract.Functions)
                {
                    builder.AppendLine(FunctionGen(function));
                }
                return builder.ToString();
            }

            public string FunctionGen(FunctionABI function)
            {
                var functionGet = FunctionGet(function.Name);
                var functionCall = FunctionCall(function);
                var functionTransaction = FunctionTransaction(function);
                var builder = new StringBuilder();
                builder.AppendLine(functionGet);
                builder.AppendLine(functionCall);
                builder.AppendLine(functionTransaction);
                return builder.ToString();
            }

            public string FunctionTransaction(FunctionABI function)
            {
                if (!function.Constant)
                {
                    var functionNameUpper = MakeFirstCharUpper(function.Name);
                    var parameters = GetFunctionParameters(function.InputParameters);
                    var callParameters = GetFunctionCallParameters(function.InputParameters);
                    if (!string.IsNullOrEmpty(callParameters)) callParameters = callParameters + ",";
                    return string.Format(FunctionTransactionTemplate, functionNameUpper, parameters, callParameters);
                }

                return "";
            }

            public string FunctionCall(FunctionABI function)
            {
                var functionNameUpper = MakeFirstCharUpper(function.Name);
                var parameters = GetFunctionParameters(function.InputParameters);
                var callParameters = GetFunctionCallParameters(function.InputParameters);
                var returnType = "";
                if (function.OutputParameters != null && function.OutputParameters.Length == 1)
                {
                    returnType = GetTypeMap(function.OutputParameters.FirstOrDefault().ABIType.Name);
                }

                return string.Format(FunctionCallTemplate, functionNameUpper, parameters, callParameters, "<" + returnType + ">");
            }

            public string FunctionGet(string functionName)
            {
                var functionNameUpper = MakeFirstCharUpper(functionName);
                return string.Format(FunctionGetTemplate, functionName, functionNameUpper);
            }

            public string EventGet(string eventName)
            {
                var eventNameUpper = MakeFirstCharUpper(eventName);
                return string.Format(EventGetTemplate, eventName, eventNameUpper);
            }

            public string MakeFirstCharUpper(string value)
            {
                return value.Substring(0, 1).ToUpper() + value.Substring(1);
            }

            public string GetFunctionCallParameters(Nethereum.ABI.Model.Parameter[] parameters)
            {
                return string.Join(",", parameters.Select(x => GetTypeMap(x.ABIType.Name) + "  " + x.Name).ToArray());
            }

            public string GetFunctionParameters(Nethereum.ABI.Model.Parameter[] parameters)
            {
                return string.Join(",", parameters.Select(x => x.Name).ToArray());
            }

            public string GetTypeMap(string typeName)
            {
                if (typeName.Contains("["))
                {
                    int indexFirstBracket = typeName.IndexOf("[", StringComparison.Ordinal);
                    string elementTypeName = typeName.Substring(0, indexFirstBracket);
                    return GetTypeMap(elementTypeName) + "[]";
                }

                if ("bool".Equals(typeName))
                {
                    return typeName;
                }
                if (typeName.StartsWith("int", StringComparison.Ordinal) || typeName.StartsWith("uint", StringComparison.Ordinal))
                {
                    return "Int64";
                }
                if ("address".Equals(typeName))
                {
                    return "string";
                }
                if ("string".Equals(typeName))
                {
                    return "string";
                }
                if ("bytes".Equals(typeName))
                {
                    return "byte[]";
                }
                if (typeName.StartsWith("bytes", StringComparison.Ordinal))
                {
                    return "byte[]";
                }
                throw new ArgumentException("Unknown type: " + typeName);

            }
        }
    }
}
