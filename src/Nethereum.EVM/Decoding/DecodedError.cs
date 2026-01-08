using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using System.Collections.Generic;

namespace Nethereum.EVM.Decoding
{
    public class DecodedError
    {
        public ErrorABI Error { get; set; }
        public List<ParameterOutput> Parameters { get; set; } = new List<ParameterOutput>();
        public string Message { get; set; }
        public bool IsStandardError { get; set; }
        public bool IsDecoded { get; set; }
        public string RawData { get; set; }

        public string GetErrorSignature()
        {
            if (Error == null) return null;
            return Error.Sha3Signature;
        }

        public string GetErrorName()
        {
            if (Error == null) return null;
            return Error.Name;
        }

        public string GetDisplayMessage()
        {
            if (!string.IsNullOrEmpty(Message))
            {
                return Message;
            }
            if (Error != null)
            {
                return Error.Name;
            }
            return RawData ?? "Unknown error";
        }

        public static DecodedError FromStandardError(string message, string rawData = null)
        {
            return new DecodedError
            {
                Message = message,
                IsStandardError = true,
                IsDecoded = true,
                RawData = rawData
            };
        }

        public static DecodedError FromUnknownError(string rawData)
        {
            return new DecodedError
            {
                IsStandardError = false,
                IsDecoded = false,
                RawData = rawData
            };
        }
    }
}
