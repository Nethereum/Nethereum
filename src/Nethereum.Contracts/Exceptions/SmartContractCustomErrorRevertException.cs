using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding;
using static Nethereum.Contracts.SmartContractCustomErrorRevertExceptionErrorABI;

namespace Nethereum.Contracts
{

    public class SmartContractCustomErrorRevertExceptionErrorABI:Exception
    {
        private const string ERROR_PREFIX = "Smart contract error";
        public string ExceptionEncodedData { get; }
        public string DecodedError { get;  }
        public ErrorABI ErrorABI { get; }

        public SmartContractCustomErrorRevertExceptionErrorABI(string encodedData, ErrorABI errorABI)
            : base($"{ERROR_PREFIX} -- {DecodeErrorToDefaultString(errorABI, encodedData)}")
        {
            this.ExceptionEncodedData = encodedData;
            this.ErrorABI = errorABI;
            this.DecodedError = DecodeErrorToDefaultString(errorABI, encodedData);
        }
        private static string DecodeErrorToDefaultString(ErrorABI errorABI, string exceptionEncodedData)
        {
            var name = errorABI.Name;
            var values = errorABI.DecodeErrorDataToDefaultToJObject(exceptionEncodedData).ToString();
            return $"{name} - {values}";
        }
    }

    public class SmartContractCustomErrorRevertException : Exception
    {
        private const string ERROR_PREFIX = "Smart contract error";
        public string ExceptionEncodedData { get; set; }

        public SmartContractCustomErrorRevertException(string encodedData) : base(ERROR_PREFIX)
        {
            this.ExceptionEncodedData = encodedData;
        }

        public List<ParameterOutput> DecodeErrorToDefault(ErrorABI errorABI)
        {
           return errorABI.DecodeErrorDataToDefault(this.ExceptionEncodedData);
        }

        public string DecodeErrorToDefaultString(ErrorABI errorABI)
        {
            var name = errorABI.Name;
            var values = errorABI.DecodeErrorDataToDefaultToJObject(this.ExceptionEncodedData).ToString();
            return $"{name} - {values}";
        }

        public bool IsCustomErrorFor(ErrorABI errorAbi)
        {
            return this.ExceptionEncodedData.IsExceptionEncodedDataForError(errorAbi);
        }

        public bool IsCustomErrorFor<TError>()
        {
            return this.ExceptionEncodedData.IsExceptionEncodedDataForError<TError>();
        }

        public bool IsCustomErrorFor(Type errorType)
        {
            return this.ExceptionEncodedData.IsExceptionEncodedDataForError(errorType);
        }

        public TError DecodeError<TError>() where TError: class, new()
        {
            return this.ExceptionEncodedData.DecodeExceptionEncodedData<TError>();
        }

        
    }
}