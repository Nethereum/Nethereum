using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AppChain.Anchoring.Finality
{
    public sealed class ContractAnchorRecordReader : IAnchorRecordReader
    {
        private readonly AppChainAnchorService _contract;
        private readonly ulong _appChainId;

        public ContractAnchorRecordReader(AppChainAnchorService contract, ulong appChainId)
        {
            _contract = contract ?? throw new ArgumentNullException(nameof(contract));
            _appChainId = appChainId;
        }

        public async Task<AnchorRecord?> GetLatestAnchorAsync(BlockParameter blockParameter, CancellationToken ct)
        {
            var result = await _contract
                .GetLatestAnchorQueryAsync(_appChainId, blockParameter)
                .ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            if (result == null || result.EndBlock == 0) return null;

            return new AnchorRecord
            {
                EndBlock = result.EndBlock,
                EndBlockHash = result.EndBlockHash ?? Array.Empty<byte>(),
                PostStateRoot = result.PostStateRoot ?? Array.Empty<byte>()
            };
        }
    }
}
