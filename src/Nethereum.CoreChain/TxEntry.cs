using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain
{
    public readonly struct TxEntry
    {
        public TxEntry(ISignedTransaction tx, string? cachedSender = null, TransactionInput? simulateCall = null)
        {
            Tx = tx;
            CachedSender = cachedSender;
            SimulateCall = simulateCall;
        }

        public ISignedTransaction Tx { get; }
        public string? CachedSender { get; }
        public TransactionInput? SimulateCall { get; }
    }
}
