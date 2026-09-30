using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Validation
{
    public sealed class RpcCanonicalSource : ICanonicalStateRootSource
    {
        private readonly EthGetBlockWithTransactionsByNumber _getBlock;
        private readonly EthBlockNumber _blockNumber;
        private readonly string _displayUrl;
        private readonly Action<string> _log;

        public RpcCanonicalSource(IClient client, string name = "rpc", Action<string> log = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _displayUrl = name;
            _getBlock = new EthGetBlockWithTransactionsByNumber(client);
            _blockNumber = new EthBlockNumber(client);
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
                _log?.Invoke(
                    $"RpcCanonicalSource: {_displayUrl} returned error for block {blockNumber}: " +
                    $"code={ex.RpcError?.Code} message='{ex.RpcError?.Message ?? ex.Message}'");
                throw;
            }
            ct.ThrowIfCancellationRequested();
            if (block == null || string.IsNullOrEmpty(block.StateRoot)) return (null, null);
            var stateRoot = block.StateRoot.HexToByteArray();
            var blockHash = string.IsNullOrEmpty(block.BlockHash) ? null : block.BlockHash.HexToByteArray();
            return (stateRoot, blockHash);
        }

        public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
        {
            HexBigInteger latest;
            try
            {
                latest = await _blockNumber.SendRequestAsync().ConfigureAwait(false);
            }
            catch (RpcResponseException ex)
            {
                _log?.Invoke(
                    $"RpcCanonicalSource: {_displayUrl} eth_blockNumber error: " +
                    $"code={ex.RpcError?.Code} message='{ex.RpcError?.Message ?? ex.Message}'");
                throw;
            }
            ct.ThrowIfCancellationRequested();

            BlockWithTransactions block;
            try
            {
                block = await _getBlock.SendRequestAsync(new BlockParameter(latest)).ConfigureAwait(false);
            }
            catch (RpcResponseException ex)
            {
                _log?.Invoke(
                    $"RpcCanonicalSource: {_displayUrl} eth_getBlockByNumber(latest) error: " +
                    $"code={ex.RpcError?.Code} message='{ex.RpcError?.Message ?? ex.Message}'");
                throw;
            }
            ct.ThrowIfCancellationRequested();

            if (block == null || string.IsNullOrEmpty(block.StateRoot)) return null;
            return new CanonicalTip
            {
                BlockNumber = (ulong)latest.Value,
                BlockHash = string.IsNullOrEmpty(block.BlockHash) ? Array.Empty<byte>() : block.BlockHash.HexToByteArray(),
                StateRoot = block.StateRoot.HexToByteArray(),
            };
        }
    }
}
