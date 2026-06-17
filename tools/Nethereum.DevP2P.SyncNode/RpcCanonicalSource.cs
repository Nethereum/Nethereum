using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// <see cref="ICanonicalStateRootSource"/> backed by a trusted JSON-RPC
    /// node (Infura / Alchemy / local geth or erigon). Calls
    /// <c>eth_getBlockByNumber</c> to fetch the canonical block at the
    /// requested height and returns its <c>stateRoot</c> + <c>hash</c>.
    ///
    /// <para>
    /// Pair with <see cref="CanonicalStateRootDiagnostics.DiagnoseAsync"/>
    /// to classify a sync-time divergence as EVM bug vs bad peer.
    /// Stackable via <see cref="CompositeCanonicalStateRootSource"/> so an
    /// AppChain follower can prefer the L1 anchor and fall back to RPC.
    /// </para>
    /// </summary>
    public sealed class RpcCanonicalSource : ICanonicalStateRootSource
    {
        private readonly EthGetBlockWithTransactionsByNumber _getBlock;
        private readonly string _displayUrl;
        private readonly Action<string> _log;

        public RpcCanonicalSource(string rpcUrl, Action<string> log = null)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                throw new ArgumentException("RPC URL must be non-empty", nameof(rpcUrl));
            var uri = new Uri(rpcUrl);
            _displayUrl = uri.Host;
            var client = new RpcClient(uri);
            _getBlock = new EthGetBlockWithTransactionsByNumber(client);
            _log = log;
        }

        public string Name => $"RPC({_displayUrl})";

        public async Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            BlockWithTransactions block;
            try
            {
                block = await _getBlock.SendRequestAsync(
                    new BlockParameter(new HexBigInteger(blockNumber)))
                    .ConfigureAwait(false);
            }
            catch (RpcResponseException ex)
            {
                // Per ICanonicalStateRootSource contract, throws are transient
                // errors the caller (typically CompositeCanonicalStateRootSource)
                // is expected to handle via fallback. Silently swallowing meant
                // a 401/403 from a misconfigured API key looked identical to
                // "block not found at this height" — silent rewinds with no
                // operator signal. Logging the error first so post-mortems can
                // see WHY the source was unavailable, then re-throwing so the
                // composite layer can fall back to the next source.
                _log?.Invoke(
                    $"RpcCanonicalSource: {_displayUrl} returned error for block {blockNumber}: " +
                    $"code={ex.RpcError?.Code} message='{ex.RpcError?.Message ?? ex.Message}'");
                throw;
            }
            ct.ThrowIfCancellationRequested();
            // Genuine "block not at this height" — JSON-RPC returns a null result
            // (not an exception). This is the documented (null, null) case in the
            // interface contract.
            if (block == null || string.IsNullOrEmpty(block.StateRoot)) return (null, null);
            var stateRoot = block.StateRoot.HexToByteArray();
            var blockHash = string.IsNullOrEmpty(block.BlockHash) ? null : block.BlockHash.HexToByteArray();
            return (stateRoot, blockHash);
        }
    }
}
