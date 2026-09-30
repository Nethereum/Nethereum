using System;

namespace Nethereum.EVM.Execution
{
    public class MalformedDepositLogException : Exception
    {
        public MalformedDepositLogException(string message) : base(message)
        {
        }
    }
}
