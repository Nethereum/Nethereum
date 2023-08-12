using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nethereum.ABI.Model
{
    public static class ModelExtensions
    {
        public static string GetSignatureFromData(string data)
        {
            if (string.IsNullOrEmpty(data)) throw new Exception("Invalid data cannot be null");

            data = data.EnsureHexPrefix();
            if (data.Length < 10) throw new Exception("Invalid data cannot be less than 4 bytes or 8 hex characters");
            return data.Substring(0, 10);
        }

        public static FunctionABI FindFunctionABI(this ContractABI contractABI, string signature) 
        {
            foreach (var functionABI in contractABI.Functions)
            {
                if (functionABI.IsSignatureForFunction(signature))
                {
                    return functionABI;
                }
            }
            return null;
        }

        public static bool HasTheSameSignatureValues(this FunctionABI first, FunctionABI other)
        {
            if (first.Sha3Signature.ToLowerInvariant() != other.Sha3Signature.ToLowerInvariant()) return false;
            if(first.Name != other.Name) return false;
            if(!first.InputParameters.AreTheSameSignatureValues(other.InputParameters)) return false;
            if (!first.OutputParameters.AreTheSameSignatureValues(other.OutputParameters)) return false;
            return true;
        }

        public static bool HasTheSameSignatureValues(this EventABI first, EventABI other)
        {
            if (first.Sha3Signature.ToLowerInvariant() != other.Sha3Signature.ToLowerInvariant()) return false;
            if (first.Name != other.Name) return false;
            if (!first.InputParameters.AreTheSameSignatureValues(other.InputParameters)) return false;
            return true;
        }

        public static bool HasTheSameSignatureValues(this ErrorABI first, ErrorABI other)
        {
            if (first.Sha3Signature.ToLowerInvariant() != other.Sha3Signature.ToLowerInvariant()) return false;
            if (first.Name != other.Name) return false;
            if (!first.InputParameters.AreTheSameSignatureValues(other.InputParameters)) return false;
            return true;
        }

        public static bool AreTheSameSignatureValues(this IEnumerable<Parameter> first, IEnumerable<Parameter> other)
        {   
            if (first.Count() != other.Count()) return false;
            foreach (var parameter in first)
            {
                var otherParameter = other.FirstOrDefault(x => x.Name == parameter.Name);
                if(otherParameter == null) return false;
                if(!parameter.HasTheSameSignatureValues(otherParameter)) return false;
            }
            return true;
        }

        public static bool HasTheSameSignatureValues(this Parameter parameter, Parameter other)
        {
            if (parameter.Order != other.Order) return false;
            if (parameter.ABIType != other.ABIType) return false;
            if (parameter.Indexed != other.Indexed) return false;
            return true;
        }

        public static FunctionABI FindFunctionABIFromInputData(this ContractABI contractABI, string inputData)
        {
            if (string.IsNullOrEmpty(inputData)) return null;
            var signature = GetSignatureFromData(inputData);
            return contractABI.FindFunctionABI(signature); 
        }


        public static EventABI FindEventABI(this ContractABI contractABI, string signature)
        {
            foreach (var eventABI in contractABI.Events)
            {
                if (eventABI.IsSignatureForEvent(signature))
                {
                    return eventABI;
                }
            }
            return null;
        }

        public static ErrorABI FindErrorABI(this ContractABI contractABI, string signature)
        {
            foreach (var errorAbi in contractABI.Errors)
            {
                if (errorAbi.IsSignatureForError(signature))
                {
                    return errorAbi;
                }
            }
            return null;
        }

        public static bool IsDataForFunction(this FunctionABI functionABI, string data)
        {
            var sha3Signature = GetSignatureFromData(data);
            var functionSignature = functionABI.Sha3Signature.EnsureHexPrefix();

            if (sha3Signature == "0x") return false;

            if (string.Equals(sha3Signature.ToLower(), functionSignature.ToLower(), StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool IsSignatureForFunction(this FunctionABI functionABI, string sha3Signature)
        {
            sha3Signature = sha3Signature.EnsureHexPrefix();
            var functionSignature = functionABI.Sha3Signature.EnsureHexPrefix();

            if (sha3Signature == "0x") return false;

            if (string.Equals(sha3Signature.ToLower(), functionSignature.ToLower(), StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool IsSignatureForEvent(this EventABI eventABI, string sha3Signature)
        {
            sha3Signature = sha3Signature.EnsureHexPrefix();
            var eventSignature = eventABI.Sha3Signature.EnsureHexPrefix();

            if (sha3Signature == "0x") return false;

            if (string.Equals(sha3Signature.ToLower(), eventSignature.ToLower(), StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool IsSignatureForError(this ErrorABI errorABI, string sha3Signature)
        {
            sha3Signature = sha3Signature.EnsureHexPrefix();
            var errorSignature = errorABI.Sha3Signature.EnsureHexPrefix();

            if (sha3Signature == "0x") return false;

            if (string.Equals(sha3Signature.ToLower(), errorSignature.ToLower(), StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
