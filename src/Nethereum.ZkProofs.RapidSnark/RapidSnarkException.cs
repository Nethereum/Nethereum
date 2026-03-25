using System;

namespace Nethereum.ZkProofs.RapidSnark
{
    public class RapidSnarkException : Exception
    {
        public RapidSnarkException(string message) : base(message) { }
        public RapidSnarkException(string message, Exception innerException) : base(message, innerException) { }
    }
}
