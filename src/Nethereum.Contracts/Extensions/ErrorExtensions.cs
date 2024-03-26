using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Contracts
{
    public static class ErrorExtensions
    {
        private static FunctionCallDecoder _functionCallDecoder = new FunctionCallDecoder();

        public static bool IsExceptionEncodedDataForError<TError>(this string data)
        {
            var errorABI = ABITypedRegistry.GetError<TError>();
            return errorABI.IsExceptionEncodedDataForError(data);
        }

        public static bool IsExceptionEncodedDataForError(this string data, string signature)
        {
            return SignatureEncoder.IsDataForSignature(signature, data);
        }

        public static bool IsErrorABIForErrorType<TError>(this ErrorABI errorABI)
        {
            var errorTypeABI = ABITypedRegistry.GetError<TError>();
            return errorTypeABI.Sha3Signature.ToLowerInvariant() == errorABI.Sha3Signature.ToLowerInvariant();
        }
        public static TError DecodeExceptionEncodedData<TError>(this string data) where TError : class, new()
        {
            var errorABI = ABITypedRegistry.GetError<TError>();
            if (errorABI.IsExceptionEncodedDataForError(data))
            {
                return _functionCallDecoder.DecodeFunctionCustomError(new TError(), errorABI.Sha3Signature, data);
            }
            return null;
        }

        public static List<ParameterOutput> DecodeExceptionEncodedDataToDefault(this ErrorABI errorABI, string data)
        {
            return errorABI.DecodeErrorDataToDefault(data);
        }

        public static JObject DecodeExceptionEncodedDataToJObject(this ErrorABI errorABI, string data)
        {
            return errorABI.DecodeExceptionEncodedDataToJObject(data);
        }

        public static bool IsExceptionEncodedDataForError(this ErrorABI errorABI, string data)
        {
            return data.IsExceptionEncodedDataForError(errorABI.Sha3Signature);
        }

        public static ErrorABI FindErrorABIForExceptionData(this List<ErrorABI> errorABIs, string data)
        {
            return errorABIs.FirstOrDefault(x => x.IsExceptionEncodedDataForError(data));
        }

        public static object FindAndDecodeToErrorExceptionData(this List<Type> errorTypes, string data)
        {
            foreach (var errorType in errorTypes)
            {
                var errorABI = ABITypedRegistry.GetError(errorType);
                if (errorABI.IsExceptionEncodedDataForError(data))
                {
                  return errorType.DecodeErrorData(data);
                }
            }
            return null;
        }

        public static JObject FindAndDecodeExceptionDataToJObject(this List<ErrorABI> errorABIs, string data)
        {
            var errorABI = errorABIs.FindErrorABIForExceptionData(data);
            if (errorABI != null)
            {
                return errorABI.DecodeExceptionEncodedDataToJObject(data);
            }
            return null;
        }

        public static bool IsExceptionForError(this ErrorABI errorABI, RpcResponseException exception)
        {
            if (exception.RpcError.Data == null) return false;
            var encodedData = exception.RpcError.Data.ToString();
            if (!encodedData.IsHex()) return false;
            return encodedData.IsExceptionEncodedDataForError(errorABI.Sha3Signature);
        }

        public static List<ErrorABI> FindErrorABIFromRpcResponseException(this
         RpcResponseException exception, IABIInfoStorage abiInfoStorage)
        {
            if(exception.RpcError.Data == null) return null;
            var encodedData = exception.RpcError.Data.ToString();
            if (!encodedData.IsHex()) return null;
            var signature = SignatureEncoder.GetSignatureFromData(encodedData);
            return abiInfoStorage.FindErrorABI(signature);
        }

    }
}