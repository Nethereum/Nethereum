using System;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal sealed class StatelessReplyException : Exception
    {
        public StatelessReplyException(string message) : base(message) { }
    }
}
