using System;
using System.Linq;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.ABI.Model;
using RazorLight;

namespace Nethereum.Generator.Console
{
    class Program
    {
        static void Main(string[] args)
        {

            var engine = EngineFactory.CreateEmbedded(typeof(ServiceModel));
         
            var contractByteCode =
              "0x6060604052604051602080610213833981016040528080519060200190919050505b806000600050819055505b506101d88061003b6000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806361325dbc1461004f578063c23f4e3e1461007b578063c6888fa1146100b05761004d565b005b61006560048080359060200190919050506100dc565b6040518082815260200191505060405180910390f35b61009a60048080359060200190919080359060200190919050506100f2565b6040518082815260200191505060405180910390f35b6100c66004808035906020019091905050610104565b6040518082815260200191505060405180910390f35b6000600060005054820290506100ed565b919050565b600081830290506100fe565b92915050565b600060006000505482029050805080827f51ae5c4fa89d1aa731ff280d425357e6e5c838c6fc8ed6ca0139ea31716bbd5760405180905060405180910390a360405180807f48656c6c6f20776f726c64000000000000000000000000000000000000000000815260200150600b019050604051809103902081837f74053123e4f45ba0f8cbf86301034a4ab00cdc75cd155a0df7c5d815bd97dcb533604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a48090506101d3565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply1"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""},{""name"":""b"",""type"":""uint256""}],""name"":""multiply2"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""},{""name"":""e"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""}],""name"":""Multiplied"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""},{""indexed"":true,""name"":""sender"",""type"":""string""},{""indexed"":false,""name"":""hello"",""type"":""address""}],""name"":""MultipliedLog"",""type"":""event""}]";

            var model = new ServiceModel(abi, contractByteCode);
            
            //Note: pass the name of the view without extension
            var result = engine.Parse("Service", model);

            System.Console.WriteLine(result);
            System.Console.ReadLine();
        }
    }

    public class ServiceModel
    {
        public string Namespace { get; }
        public string ContractName { get; }
        public string Abi { get; }
        public string ByteCode { get; }
        public ContractABI Contract { get; }

        public ServiceModel(string abi, string byteCode, string contractName = "Contract",
            string namespaceName = "DefaultNamespace")
        {
            ContractName = CapitaliseFirstChar(contractName);
            Namespace = CapitaliseFirstChar(namespaceName);
            Abi = abi;
            ByteCode = byteCode;
            var des = new ABIDeserialiser();
            Contract = des.DeserialiseContract(abi);
        }

        public string CapitaliseFirstChar(string value)
        {
            return value.Substring(0, 1).ToUpper() + value.Substring(1);
        }
         
        public string GetGenericReturnType(FunctionABI item)
        { 
            if (item == null) return String.Empty;
            var returnType = GetReturnType(item);
            return "<" + returnType + ">";
        }

        public string GenerateFunctionInputParameters(Parameter[] parameters)
        {
            var parametersOuput = String.Empty;
            if (parameters != null && parameters.Length > 0)
            {
                foreach (var parameter in parameters)
                {
                    var prefix = ", ";
                    if (parametersOuput == String.Empty) prefix = String.Empty;
                    parametersOuput = parametersOuput + prefix + GetTypeMap(parameter.Type) + ' ' + GetParameterName(parameter.Name, parameter.Order);
                }
             }
                
            return parametersOuput;
        }

        public string GetParameterName(string name, int order)
        {
            if (name != "") return name;
            switch (order)
            {
                case 0:
                    return "a";
                case 1:
                    return "b";
                case 2:
                    return "c";
                case 3:
                    return "d";
                case 4:
                    return "e";
                case 5:
                    return "f";
                case 6:
                    return "g";
            }
            return "h";
        }

        public string GenerateFunctionTransactionInputParameters(Parameter[] inputParameters)
        {
            var parameters = GenerateFunctionInputParameters(inputParameters);
            if (parameters != "") return parameters + ",";
            return parameters;
        }

        public string GenerateConstructorInputParameters()
        {
           return GenerateFunctionTransactionInputParameters(Contract.Constructor.InputParameters);
        }

        public string GenerateFunctionParametersCommaPrefix(Parameter[] parameters)
        {
            var ret = GenerateFunctionParameters(parameters);
            if (ret != "")
            {
                ret = ", " + ret;
            }
            return ret;
        }

        public string GenerateConstructorParameters()
        {
            var ret = "";
            if(Contract.Constructor != null)
                 ret = GenerateFunctionParametersCommaPrefix(Contract.Constructor.InputParameters);    
            return ret;
        }
       
        public string GenerateFunctionParameters(Parameter[] parameters)
        {
            var parametersOutput = "";
            if (parameters != null && parameters.Length > 0)
            {
                foreach (var parameter in parameters)
                {
                    var prefix = ", ";
                    if (parametersOutput == String.Empty) prefix = String.Empty;
                    parametersOutput = parametersOutput + prefix + GetParameterName(parameter.Name, parameter.Order);
                }
            }
            return parametersOutput;
        }

        public string GetReturnType(FunctionABI functionABI)
        {
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1)
            {
                return GetTypeMap(functionABI.OutputParameters[0].Type);
            }
            return null;
        }

        public string GetTypeMap(string typeName)
        {
            var indexFirstBracket = typeName.IndexOf("[");
            if (indexFirstBracket > -1)
            {
                var elementTypeName = typeName.Substring(0, indexFirstBracket);
                return GetTypeMap(elementTypeName) + "[]";
            }
            if ("bool" == typeName)
            {
                return typeName;
            }
            if (typeName.StartsWith("int"))
            {
                //default
                if (typeName.Length == 3)
                {
                    return "BigInteger";
                }
                var length = Int32.Parse(typeName.Substring(3));

                if (length > 64)
                {
                    return "BigInteger";
                }
                if (length <= 64 && length > 32)
                {
                    return "long";
                }
                //ints are in 8 bits
                if (length == 32)
                {
                    return "int";
                }
                if (length == 16)
                {
                    return "short";
                }
                if (length == 8)
                {
                    return "sbyte";
                }
            }
            if (typeName.StartsWith("uint"))
            {

                if (typeName.Length == 4)
                {
                    return "BigInteger";
                }
                var length = Int32.Parse(typeName.Substring(4));

                if (length > 64)
                {
                    return "BigInteger";
                }
                if (length <= 64 && length > 32)
                {
                    return "ulong";
                }
                //uints are in 8 bits steps
                if (length == 32)
                {
                    return "int";
                }
                if (length == 16)
                {
                    return "short";
                }
                if (length == 8)
                {
                    return "byte";
                }
            }
            if (typeName == "address")
            {
                return "string";
            }
            if (typeName == "string")
            {
                return "string";
            }
            if (typeName == "bytes")
            {
                return "byte[]";
            }
            if (typeName.StartsWith("bytes"))
            {
                return "byte[]";
            }
            return null;
        }

        public string GetBooleanAsString(bool value)
        {
            if (value) return "true";
            return "false";
        }
    }   
}