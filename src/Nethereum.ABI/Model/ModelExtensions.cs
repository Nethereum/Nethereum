using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.ABI.Model
{
    public static class ModelExtensions
    {
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

        public static FunctionABI FindFunctionABIFromInputData(this ContractABI contractABI, string inputData)
        {
            if (string.IsNullOrEmpty(inputData)) return null;
            inputData = inputData.EnsureHexPrefix();
            if(inputData.Length < 10) return null;
            var signature = inputData.Substring(0, 10);
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
