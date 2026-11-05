using Nethereum.Documentation;
using System;

namespace Nethereum.EVM.Exceptions
{
    [NethereumDocExample(DocSection.EvmSimulator, "exceptional-halt", "The one exception the async loop treats as a halt")]
    public class EvmHaltException : Exception
    {
        public EvmHaltReason Reason { get; }

        public EvmHaltException(EvmHaltReason reason)
            : base(reason.ToString())
        {
            Reason = reason;
        }

        public EvmHaltException(EvmHaltReason reason, string message)
            : base(message)
        {
            Reason = reason;
        }

        public EvmHaltException(EvmHaltReason reason, string message, Exception innerException)
            : base(message, innerException)
        {
            Reason = reason;
        }
    }
}
