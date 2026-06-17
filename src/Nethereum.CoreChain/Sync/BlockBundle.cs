using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// One block's worth of fetched data ready for the executor stage.
    /// Headers and bodies are decoded; transactions arrive as
    /// <see cref="ISignedTransaction"/> so the executor can run them
    /// without re-parsing wire bytes. Withdrawals are wire-shaped
    /// (<see cref="Withdrawal"/>); the call site converts them to the
    /// engine-shaped <c>WithdrawalEntry</c> via <c>WithdrawalAdapter</c>.
    /// </summary>
    /// <param name="Header">Decoded block header.</param>
    /// <param name="Transactions">Signed transactions in canonical order.</param>
    /// <param name="Uncles">Ommer headers; empty post-Merge.</param>
    /// <param name="Withdrawals">Shanghai+ withdrawals; <c>null</c> pre-Shanghai.</param>
    /// <param name="HeaderHash">Keccak-256 of the RLP-encoded header (canonical block hash).</param>
    public sealed record BlockBundle(
        BlockHeader Header,
        IList<ISignedTransaction> Transactions,
        IList<BlockHeader> Uncles,
        IList<Withdrawal> Withdrawals,
        byte[] HeaderHash);
}
