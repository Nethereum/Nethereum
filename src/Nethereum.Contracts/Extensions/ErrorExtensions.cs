using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

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

        public static TError DecodeExceptionEncodedData<TError>(this string data) where TError : class, new()
        {
            var errorABI = ABITypedRegistry.GetError<TError>();
            if (errorABI.IsExceptionEncodedDataForError(data))
            {
                return _functionCallDecoder.DecodeFunctionCustomError(new TError(), errorABI.Sha3Signature, data);
            }
            return null;
        }

        public static bool IsExceptionEncodedDataForError(this ErrorABI errorABI, string data)
        {
            return data.IsExceptionEncodedDataForError(errorABI.Sha3Signature);
        }

    }
}