using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;


namespace Nethereum.ABI.ABIDeserialisation
{

    public class ABIStringSignatureDeserialiser
    {
        //https://stackoverflow.com/questions/546433/regular-expression-to-match-balanced-parentheses
        //balanced parameters to support tuples
     
        public List<Parameter> ExtractParameters(string parameters, bool canIndex = false)
        {
            return ExtractParametersChecked(parameters, canIndex);
        }

        private static List<string> SplitTopLevel(string text, bool onWhitespace)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '(')
                {
                    if (++depth > AbiParsingLimits.MaxNestingDepth)
                        throw new ArgumentException("ABI type nesting exceeds " + AbiParsingLimits.MaxNestingDepth + " levels");
                }
                else if (c == ')')
                {
                    if (depth > 0) depth--;
                }
                else if (depth == 0 && (onWhitespace ? AbiSignatureScanner.IsSpace(c) : c == ','))
                {
                    parts.Add(text.Substring(start, i - start));
                    if (onWhitespace)
                        while (i + 1 < text.Length && AbiSignatureScanner.IsSpace(text[i + 1])) i++;
                    start = i + 1;
                }
                i++;
            }
            parts.Add(text.Substring(start));
            return parts;
        }

        private List<Parameter> ExtractParametersChecked(string parameters, bool canIndex)
        {

            var parametersArray = SplitTopLevel(parameters, false);
            var returnParameters = new List<Parameter>();
            var parameterOrder = 0;
            foreach(var parameter in parametersArray)
            {
                parameterOrder = parameterOrder + 1;
                bool isIndexed = false;
                string name = null;
                var nameTypeIndex = SplitTopLevel(parameter.Trim(), true);
                string type = GetType(nameTypeIndex[0]);
                if (canIndex && nameTypeIndex.Count == 3 && parameter.IndexOf("indexed") > -1)
                {
                    name = nameTypeIndex[2];
                    isIndexed = true;
                    
                }
                else if (nameTypeIndex.Count == 2)
                {
                    name = nameTypeIndex[1];
                    type = GetType(nameTypeIndex[0]);
                }
                
                var returnParameter = new Parameter(type, name, parameterOrder);
                var tupleType = returnParameter.ABIType as TupleType;
                if(tupleType != null)
                {
                    AbiSignatureScanner.TryFirstBalanced(parameter, AbiSignatureScanner.BuildPartners(parameter), 0, out var inner);
                    tupleType.SetComponents(ExtractParametersChecked(inner, false).ToArray());
                }

                var arrayType = returnParameter.ABIType as ArrayType;

                while (arrayType != null)
                {

                    if (arrayType.ElementType is TupleType arrayTupleType)
                    {
                        AbiSignatureScanner.TryFirstBalanced(parameter, AbiSignatureScanner.BuildPartners(parameter), 0, out var inner);
                        arrayTupleType.SetComponents(ExtractParametersChecked(inner, false).ToArray());
                        arrayType = null;
                    }
                    else
                    {
                        arrayType = arrayType.ElementType as ArrayType;
                    }
                }

                returnParameter.Indexed = isIndexed;
                returnParameters.Add(returnParameter);

                
            }
            return returnParameters;
        }

        private string GetType(string type)
        {
            type = type.Trim();
            if (type.StartsWith("tuple") || type.StartsWith("("))
            {
                if (type.EndsWith("]"))
                {
                    var index = type.LastIndexOf("[");
                    return "tuple" + type.Substring(index, type.Length - index);
                }
                else
                {
                    return "tuple";
                }
            }
            return type;
        }

        public ErrorABI ExtractErrorABI(string signature, string name, string parameters)
        {
            var errorABI = new ErrorABI(name);
            errorABI.InputParameters = new List<Parameter>().ToArray();
            if (!string.IsNullOrEmpty(parameters))
            {
                errorABI.InputParameters = ExtractParameters(parameters).ToArray();
            }
            return errorABI;
        }

        public EventABI ExtractEventABI(string signature, string name, string parameters)
        {
            var eventAbi = new EventABI(name, false);
            eventAbi.InputParameters = new List<Parameter>().ToArray();
            if (!string.IsNullOrEmpty(parameters))
            {
                eventAbi.InputParameters = ExtractParameters(parameters, true).ToArray();
            }
            return eventAbi;
        }

        public ConstructorABI ExtractConstructorABI(string signature, string name, string parameters)
        {
            var constructor = new ConstructorABI();
            constructor.InputParameters = new List<Parameter>().ToArray();
            if (!string.IsNullOrEmpty(parameters))
            {
                constructor.InputParameters = ExtractParameters(parameters).ToArray();
            }
            return constructor;
        }

        public FunctionABI ExtractFunctionABI(string signature, string name, string parameters)
        {
            AbiParsingLimits.EnsureParenthesesDepth(signature, AbiParsingLimits.MaxNestingDepth + 1);
            var parameterReturns = AbiSignatureScanner.ReturnsParameters(signature, AbiSignatureScanner.BuildPartners(signature));
            var function = new FunctionABI(name, AbiSignatureScanner.HasStateModifier(signature));
            function.InputParameters = new List<Parameter>().ToArray();
            function.OutputParameters = new List<Parameter>().ToArray();
            if (!string.IsNullOrEmpty(parameterReturns))
            {
                function.OutputParameters = ExtractParameters(parameterReturns).ToArray();
            }

            if (!string.IsNullOrEmpty(parameters))
            {
                function.InputParameters = ExtractParameters(parameters).ToArray();
            }
            
            return function;
        }

        public ContractABI ExtractContractABIWithLineBreakSplitSignatures(string signatures)
        {
            return ExtractContractABI(signatures.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
        }
       
        public ContractABI ExtractContractABI(params string[] signatures)
        {
            var contractABI = new ContractABI();
            var functions = new List<FunctionABI>();
            var events = new List<EventABI>();
            var errors = new List<ErrorABI>();
            
            foreach(var signature in signatures)
            {
                AbiParsingLimits.EnsureParenthesesDepth(signature, AbiParsingLimits.MaxNestingDepth + 1);
                var trimmed = signature.Trim();
                AbiSignatureScanner.TryMatchHead(trimmed, AbiSignatureScanner.BuildPartners(trimmed), out var scope, out var name, out var parameters);
 
                if (scope == "function")
                {
                    functions.Add(ExtractFunctionABI(signature.Trim(), name, parameters));
                }

                if (scope == "event")
                {
                    events.Add(ExtractEventABI(signature.Trim(), name, parameters));
                }

                if (scope == "error")
                {
                    errors.Add(ExtractErrorABI(signature.Trim(), name, parameters));
                }

                if (scope == "constructor")
                {
                    contractABI.Constructor = ExtractConstructorABI(signature.Trim(), name, parameters);
                }
            }

            contractABI.Functions = functions.ToArray();
            contractABI.Events = events.ToArray();
            contractABI.Errors = errors.ToArray();
            return contractABI;
        }

       
    }
}