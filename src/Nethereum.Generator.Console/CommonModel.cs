using System;
using System.Linq;
using Microsoft.CodeAnalysis.Semantics;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.ABI.Model;

namespace Nethereum.Generator.Console
{

    public class ABITypeToCSharpType
    {
        public string GetTypeMap(string typeName, bool outputMapType = false)
        {
            var indexFirstBracket = typeName.IndexOf("[");
            if (indexFirstBracket > -1)
            {
                var elementTypeName = typeName.Substring(0, indexFirstBracket);
                if (outputMapType)
                {
                    return "List<" + GetTypeMap(elementTypeName, true) + ">";
                }
                else
                {
                    return GetTypeMap(elementTypeName) + "[]";
                }
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
    }

    public class FunctionABIGeneratorModel
    {
        private ABITypeToCSharpType abiTypeToCSharpType;

        public FunctionABIGeneratorModel(ABITypeToCSharpType abiTypeToCSharpType)
        {
            this.abiTypeToCSharpType = abiTypeToCSharpType;
        }

        public string GetSingleOutputGenericReturnType(FunctionABI item)
        {
            if (item == null) return String.Empty;
            return $"<{GetSingleOutputReturnType(item)}>";
        }

        public string GetSingleOutputReturnType(FunctionABI functionABI)
        {
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1)
            {
                return abiTypeToCSharpType.GetTypeMap(functionABI.OutputParameters[0].Type, true);
            }
            return null;
        }
    }

    public class FunctionABIFunctionOutputDTOGeneratorService
    {
        public FunctionABIFunctionOutputDTOClassTemplate template;

        public FunctionABIFunctionOutputDTOGeneratorService()
        {
            
        }
    }

    public class FunctionABIFunctionOutputDTOModel
    {
        private CommonGenerators commonGenerators;

        public FunctionABIFunctionOutputDTOModel()
        {
            commonGenerators = new CommonGenerators();
        }
        
        public string GetFunctionOutputTypeName(FunctionABI functionABI)
        {
            return GetFunctionOutputTypeName(functionABI.Name);
        }

        public string GetFunctionOutputTypeName(string functionName)
        {
            return $"{commonGenerators.GenerateClassName(functionName)}OutputDTO";
        }

        public bool CanGenerateOutputDTO(FunctionABI functionABI)
        {
            return functionABI.OutputParameters != null && functionABI.OutputParameters.Length > 0;
        }
    }

    public class FunctionABIFunctionOutputDTOClassTemplate
    {
        private ParameterABIFunctionDTOClassTemplate parameterABIFunctionDtoClassTemplate;
        private FunctionABIFunctionOutputDTOModel functionABIFunctionOutputDtoModel;
        public FunctionABIFunctionOutputDTOClassTemplate()
        {
            parameterABIFunctionDtoClassTemplate = new ParameterABIFunctionDTOClassTemplate();
            functionABIFunctionOutputDtoModel = new FunctionABIFunctionOutputDTOModel();
        }
        public string GenerateClass(FunctionABI functionABI)
        {
            if (functionABIFunctionOutputDtoModel.CanGenerateOutputDTO(functionABI))
            {
                return
 $@"[FunctionOutput]
    public class {functionABIFunctionOutputDtoModel.GetFunctionOutputTypeName(functionABI)}
    {{
        {parameterABIFunctionDtoClassTemplate.GenerateAllProperties(functionABI.OutputParameters)}
    }}";
            }
            return null;
        }
    }

    public class ParameterABIFunctionDTOClassTemplate
    {
        private ParameterABIGeneratorModel parameterModel;

        public ParameterABIFunctionDTOClassTemplate()
        {
            parameterModel = new ParameterABIGeneratorModel();    
        }

        public string GenerateAllProperties(Parameter[] parameters)
        {
            return string.Join(Environment.NewLine, parameters.Select(GenerateProperty));
        }

        public string GenerateProperty(Parameter parameter)
        {
            return $@"
[Parameter(""{parameter.Type}"", ""{@parameter.Name}"", {parameter.Order})]
public {parameterModel.GetParameterCSharpOutputMapType(parameter)} {parameterModel.GetParameterPropertyName(parameter)} {{get; set;}}";
        }
    }

    public class ParameterABIGeneratorModel
    {
        private CommonGenerators commonGenerators;
        private ABITypeToCSharpType abiTypeToCSharpType;

        public ParameterABIGeneratorModel()
        {
            commonGenerators = new CommonGenerators();
            abiTypeToCSharpType = new ABITypeToCSharpType();
        }

        public string GetParameterVariableName(string name, int order)
        {
            return commonGenerators.GenerateVariableName(GetParameterName(name, order));
        }

        public string GetParameterPropertyName(Parameter parameter)
        {
            return GetParameterPropertyName(parameter.Name, parameter.Order);
        }

        public string GetParameterVariableName(Parameter parameter)
        {
            return GetParameterVariableName(parameter.Name, parameter.Order);
        }

        public string GetParameterPropertyName(string name, int order)
        {
            return commonGenerators.GeneratePropertyName(GetParameterName(name, order));
        }

        public string GetParameterCSharpOutputMapType(Parameter parameter)
        {
            return abiTypeToCSharpType.GetTypeMap(parameter.Type, true);
        }

        public string GetParameterCSharpInputMapType(Parameter parameter)
        {
            return abiTypeToCSharpType.GetTypeMap(parameter.Type, false);
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

    }


    public class CommonGenerators
    {
        private Utils utils;

        public CommonGenerators()
        {
            utils = new Utils();
        }

        public string GenerateVariableName(string value)
        {
            return utils.LowerCaseFirstCharAndRemoveUnderscorePrefix(value);
        }

        public string GeneratePropertyName(string value)
        {
            return utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(value);
        }

        public string GenerateClassName(string value)
        {
            return utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(value);
        }
    }

    public class CommonModel
    {
        private readonly Utils utils;

        public GlobalNames GlobalNames { get; }

        public const string DEFAULT_NAMESPACE = "DefaultNamespace";
        public string Namespace { get; }
        public string Abi { get; }
        public string ByteCode { get; }
        public ContractABI Contract { get; }

        public Utils Utils
        {
            get { return utils; }
        }

        public CommonModel(string abi, string byteCode,
            string namespaceName = DEFAULT_NAMESPACE)
        {
            utils = new Utils();
            GlobalNames = new GlobalNames();
            Namespace = Utils.CapitaliseFirstChar(namespaceName);
            Abi = abi;
            ByteCode = byteCode;
            var des = new ABIDeserialiser();
            Contract = des.DeserialiseContract(abi);
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
                    parametersOuput = parametersOuput + prefix + GetTypeMap(parameter.Type, false) + ' ' + GetParameterName(parameter.Name, parameter.Order);
                }
            }

            return parametersOuput + " ";
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
            if (Contract.Constructor != null)
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
            return parametersOutput + " ";
        }

        public string GetReturnType(FunctionABI functionABI)
        {
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1)
            {
                return GetTypeMap(functionABI.OutputParameters[0].Type, true);
            }
            return null;
        }

        public string GetSingleAbiReturnType(FunctionABI functionABI)
        {
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1)
            {
                return functionABI.OutputParameters[0].Type;
            }
            return null;
        }

        public string GetTypeMap(string typeName, bool output = false)
        {
            var indexFirstBracket = typeName.IndexOf("[");
            if (indexFirstBracket > -1)
            {
                var elementTypeName = typeName.Substring(0, indexFirstBracket);
                if (output)
                {
                    return "List<" + GetTypeMap(elementTypeName, true) + ">";
                }
                else
                {
                    return GetTypeMap(elementTypeName) + "[]";
                }
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
    }
}