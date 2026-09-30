using System;
using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "IHotBlockWindowSource — the not-yet-frozen tip-band read seam")]
    public interface IHotBlockWindowSource
    {
        long HotTipNumber { get; }

        HotBlock ReadHotBlock(long blockNumber);
    }

    public interface IHotBlockWindowEvict
    {
        void EvictAtOrBelow(long number);
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "HotBlock — the typed source cluster promotion freezes")]
    public sealed class HotBlock
    {
        public BlockHeader Header { get; }
        public byte[] BlockHash { get; }
        public BlockBodyCluster Body { get; }

        public IReadOnlyList<Receipt> Receipts { get; }

        public byte[] BalRlp { get; }

        public HotBlock(BlockHeader header, byte[] blockHash, BlockBodyCluster body, IReadOnlyList<Receipt> receipts, byte[] balRlp)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            BlockHash = blockHash ?? throw new ArgumentNullException(nameof(blockHash));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Receipts = receipts ?? new List<Receipt>();
            BalRlp = balRlp ?? Array.Empty<byte>();
        }
    }
}
