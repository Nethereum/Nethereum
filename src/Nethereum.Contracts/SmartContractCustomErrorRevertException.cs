using Nethereum.ABI.Model;
using System;
using System.Linq;

namespace Nethereum.Contracts
{
    public class SmartContractCustomErrorRevertException<TCustomError> : SmartContractCustomErrorRevertExceptionErrorDecoded
    {
        public TCustomError CustomError { get; }
        public SmartContractCustomErrorRevertException(TCustomError customError, ErrorABI errorABI, SmartContractCustomErrorRevertException innerCustomErrorRevert)
            : base(innerCustomErrorRevert.ExceptionEncodedData, errorABI, innerCustomErrorRevert)
        {
            CustomError = customError;
        }
    }

    public static class SmartContractCustomErrorTypedFactory
    {
        public static Exception CreateTypedException(this SmartContractCustomErrorRevertException exception, params Type[] errorTypes)
        {
            var errorType = errorTypes.FirstOrDefault(x => exception.IsCustomErrorFor(x));
            if (errorType == null) return null;
            var errorABI = ABITypedRegistry.GetError(errorType);
            var decodedError = exception.DecodeError(errorType);
            return CreateException(exception, decodedError, errorABI);
        }

        public static Exception CreateException(SmartContractCustomErrorRevertException exception, object decodedError, ErrorABI errorABI)
        {
            var exceptionType = typeof(SmartContractCustomErrorRevertException<>).MakeGenericType(decodedError.GetType());
            return (Exception)Activator.CreateInstance(exceptionType, decodedError, errorABI, exception);
        }
    }

}