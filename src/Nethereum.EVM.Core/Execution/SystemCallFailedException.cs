using System;

namespace Nethereum.EVM.Execution
{
    public class SystemCallFailedException : Exception
    {
        public SystemCallFailedException(string targetAddress, string reason)
            : base($"System call to {targetAddress} failed: {reason}")
        {
            TargetAddress = targetAddress;
            Reason = reason;
        }

        protected SystemCallFailedException(string targetAddress, string reason, string message)
            : base(message)
        {
            TargetAddress = targetAddress;
            Reason = reason;
        }

        public string TargetAddress { get; }

        public string Reason { get; }
    }
}
