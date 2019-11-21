using System;

namespace Nethereum.ABI.FunctionEncoding
{
    public class SmartContractRevertException : Exception
    {
        private const string ERROR_PREFIX = "Smart contract error: ";
        public SmartContractRevertException(string message) : base(ERROR_PREFIX + message)
        {

        }
    }
}