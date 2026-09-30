using System;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.History
{
    public sealed class HistoryReorgDecoder : IHistoryReorgDecoder
    {
        private readonly IBlockEncodingProvider _provider;

        public HistoryReorgDecoder(IBlockEncodingProvider provider)
            => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        public byte[] BlockHash(byte[] blockMetaValue) => BlockMetaCodec.Decode(blockMetaValue)?.BlockHash;

        public byte[] TxHash(byte[] txBodyValue) => _provider.DecodeTransaction(txBodyValue)?.Hash;
    }
}
