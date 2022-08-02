using System;

namespace Nethereum.RPC.Eth.Exceptions
{
    public class UnexpectedReplyException : Exception
    {
        public UnexpectedReplyException(string message, object resultValue) : base(message)
        {
            ResultValue = resultValue;
        }
        public object ResultValue { get; set; }
    }
}
