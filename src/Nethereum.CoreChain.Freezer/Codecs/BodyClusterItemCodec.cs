using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RLP;

namespace Nethereum.CoreChain.Freezer.Codecs
{
    public sealed class BodyClusterItemCodec : IItemCodec<BlockBodyCluster>
    {
        public byte[] Encode(BlockBodyCluster item)
        {
            var body = new BlockBody
            {
                Transactions = item.Txs.ToList(),
                Uncles = item.Uncles.ToList(),
                Withdrawals = item.Withdrawals?.ToList()
            };
            return BlockBodiesMessageEncoder.EncodeBody(body);
        }

        public BlockBodyCluster Decode(ReadOnlySpan<byte> bytes)
        {
            var body = BlockBodiesMessageEncoder.DecodeBody(bytes.ToArray());
            return new BlockBodyCluster(body.Transactions, body.Uncles, body.Withdrawals);
        }
    }
}
