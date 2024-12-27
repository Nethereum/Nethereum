using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts.World.ContractDefinition;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Nethereum.Mud.Contracts.Core
{
    public static class FunctionMessageMudExtensions
    {

        public static string GetFunctionNameWithoutMudNamespacePrefix(this FunctionABI functionABI)
        {
            var functionName = functionABI.Name;
            if (functionName.IndexOf("__") > 1)
            {
                return functionName.Substring(functionName.IndexOf("__") + 2);
            }

            return functionName;
        }


        public static string GetFunctionSignatureWithoutMudNamespacePrefix(this FunctionABI functionABI)
        {
            var functionNameWithoutNamespacePrefix = functionABI.GetFunctionNameWithoutMudNamespacePrefix();
            var signatureEncoder = new SignatureEncoder();
            return signatureEncoder.GenerateSignature(functionNameWithoutNamespacePrefix, functionABI.InputParameters);
        }

        public static string GetFunctionSha3SignatureWithoutMudNamespacePrefix(this FunctionABI functionABI)
        {
            var signatureWithoutNamePrefix = GetFunctionSignatureWithoutMudNamespacePrefix(functionABI);
            var signatureEncoder = new SignatureEncoder();
            return signatureEncoder.GenerateSha3Signature(signatureWithoutNamePrefix, functionABI.InputParameters, 4);
        }

        public static string GetFunctionNameWithoutMudNamespacePrefix<TFunctionMessage>(this TFunctionMessage functionMessage)
            where TFunctionMessage: FunctionMessage
        {
            var functionABI = ABITypedRegistry.GetFunctionABI<TFunctionMessage>();
            return functionABI.GetFunctionNameWithoutMudNamespacePrefix();
        }

        public static string GetFunctionSignatureWithoutMudNamespacePrefix<TFunctionMessage>(this TFunctionMessage functionMessage)
            where TFunctionMessage : FunctionMessage
        {
            var functionABI = ABITypedRegistry.GetFunctionABI<TFunctionMessage>();
            return functionABI.GetFunctionSignatureWithoutMudNamespacePrefix();
        }
        
        public static string GetFunctionSha3SignatureWithoutMudNamespacePrefix<TFunctionMessage>(this TFunctionMessage functionMessage)
           where TFunctionMessage : FunctionMessage
        {
            var functionABI = ABITypedRegistry.GetFunctionABI<TFunctionMessage>();
            return functionABI.GetFunctionSha3SignatureWithoutMudNamespacePrefix();
        }

        public static byte[] GetCallDataWithoutMudNamespaceSignaturePrefix<TFunctionMessage>(this TFunctionMessage functionMessage)
           where TFunctionMessage : FunctionMessage
        {
            var callData = functionMessage.GetCallData();
            var sha3Signature = functionMessage.GetFunctionSha3SignatureWithoutMudNamespacePrefix();

            //replace first 4 bytes with new sha3 signature
            var bytes = sha3Signature.HexToByteArray();
            callData[0] = bytes[0];
            callData[1] = bytes[1];
            callData[2] = bytes[2];
            callData[3] = bytes[3];
            return callData;
        }
    }
}
