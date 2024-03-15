using System;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;

namespace Nethereum.Contracts
{

    public class Error<TError> : ErrorBase where TError : class, IErrorDTO, new()
    {
        public Error() : base(typeof(TError))
        {

        }

        public TError DecodeExceptionEncodedData(string data) 
        {
            if (ErrorABI.IsExceptionEncodedDataForError(data))
            {
                return data.DecodeExceptionEncodedData<TError>();
            }

            return null;
        }
    }

    public class Error: ErrorBase
    {
        public Error(ErrorABI errorABI):base(errorABI)
        {
            
        }

        public Error(Type errorTypeAbi):base(errorTypeAbi)
        {
            
        }

        public bool IsErrorForErrorType<TError>()
        {
            return ErrorABI.IsErrorABIForErrorType<TError>();
        }

        public TError DecodeExceptionEncodedData<TError>(string data) where TError : class, IErrorDTO, new()
        {
            if (IsErrorForErrorType<TError>())
            {
                if (ErrorABI.IsExceptionEncodedDataForError(data))
                {
                    return data.DecodeExceptionEncodedData<TError>();
                }
            }
            return null;
        }
    }
}