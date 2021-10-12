using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using System.Collections.Generic;

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
            return _functionCallDecoder.IsDataForFunction(signature, data);
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
            return _functionCallDecoder.DecodeFunctionInput(errorABI.Sha3Signature, data,
                errorABI.InputParameters);
        }

        public static bool IsExceptionEncodedDataForError(this ErrorABI errorABI, string data)
        {
            return data.IsExceptionEncodedDataForError(errorABI.Sha3Signature);
        }

        public static bool IsExceptionForError(this ErrorABI errorABI, RpcResponseException exception)
        {
            if (exception.RpcError.Data == null) return false;
            var encodedData = exception.RpcError.Data.ToObject<string>();
            if (!encodedData.IsHex()) return false;
            return encodedData.IsExceptionEncodedDataForError(errorABI.Sha3Signature);
        }

    }
}