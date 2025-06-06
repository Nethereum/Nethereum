using Nethereum.ABI.Model;
using System;
using System.Linq;


namespace Nethereum.ABI.ABIDeserialisation
{
    public class ABIDeserialiserFactory
    {
        public static ContractABI DeserialiseContractABI(string jsonOrStringSignatureABI)
        {
            try
            {
                if (IsJson(jsonOrStringSignatureABI))
                {
                    return new ABIJsonDeserialiser().DeserialiseContract(jsonOrStringSignatureABI);
                }

                return new ABIStringSignatureDeserialiser().ExtractContractABIWithLineBreakSplitSignatures(jsonOrStringSignatureABI);
            }
            catch(Exception ex)
            {
                throw new FormatException("Invalid ABI, could not be parsed", ex);
            }
            
        }

        public static FunctionABI DeserialiseFunctionABI(string jsonOrStringSignatureABI)
        {
            try
            {
                if (IsJson(jsonOrStringSignatureABI))
                {
                    return new ABIJsonDeserialiser().DeserialiseContract(jsonOrStringSignatureABI).Functions.FirstOrDefault();
                }
                if(!jsonOrStringSignatureABI.Trim().StartsWith("function "))
                {
                    jsonOrStringSignatureABI = "function " + jsonOrStringSignatureABI.Trim();
                }
                return new ABIStringSignatureDeserialiser().ExtractContractABIWithLineBreakSplitSignatures(jsonOrStringSignatureABI).Functions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new FormatException("Invalid Function ABI, could not be parsed", ex);
            }
        }

        private static bool IsJson(string value)
        {
            value = value.Trim();
            return value.StartsWith("{") && value.EndsWith("}")
                   || value.StartsWith("[") && value.EndsWith("]");
        }

    }
}