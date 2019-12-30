using System;

namespace Nethereum.Quorum.Enclave
{
    public class QuorumEnclaveRequestException : Exception
    {
        public QuorumEnclaveRequestException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}