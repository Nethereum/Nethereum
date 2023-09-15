using System;
using System.Runtime.Serialization;

namespace Nethereum.WalletConnect
{
    [Serializable]
    public class InvalidWalletConnectSessionException : Exception
    {
        public InvalidWalletConnectSessionException()
        {
        }

        public InvalidWalletConnectSessionException(string message) : base(message)
        {
        }

        public InvalidWalletConnectSessionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidWalletConnectSessionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }



}
