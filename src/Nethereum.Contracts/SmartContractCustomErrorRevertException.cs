using System;

namespace Nethereum.Contracts
{
    public class SmartContractCustomErrorRevertException<TCustomError> : Exception
    {
        public TCustomError CustomError { get; }
        public SmartContractCustomErrorRevertException InnerCustomErrorRevert { get; }
        public SmartContractCustomErrorRevertException(TCustomError customError, SmartContractCustomErrorRevertException innerCustomErrorRevert)
        {
            CustomError = customError;
            InnerCustomErrorRevert = innerCustomErrorRevert;
        }
    }
}