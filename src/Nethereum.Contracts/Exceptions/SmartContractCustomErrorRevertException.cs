using System;

namespace Nethereum.Contracts
{
    public class SmartContractCustomErrorRevertException : Exception
    {
        private const string ERROR_PREFIX = "Smart contract error";
        public string ExceptionEncodedData { get; set; }

        public SmartContractCustomErrorRevertException(string encodedData) : base(ERROR_PREFIX)
        {
            this.ExceptionEncodedData = encodedData;
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