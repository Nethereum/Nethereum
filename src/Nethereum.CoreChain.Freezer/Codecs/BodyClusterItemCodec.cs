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
            var bodyRlp = (RLPCollection)RLP.RLP.Decode(bytes.ToArray());

            var txs = new List<ISignedTransaction>();
            foreach (var txRlp in (RLPCollection)bodyRlp[0])
                txs.Add(TransactionFactory.CreateTransaction(txRlp.RLPData));

            var uncles = new List<BlockHeader>();
            foreach (RLPCollection uncleRlp in (RLPCollection)bodyRlp[1])
                uncles.Add(BlockHeaderEncoder.Current.Decode(uncleRlp.RLPData));

            List<Withdrawal> withdrawals = null;
            if (bodyRlp.Count > 2)
            {
                withdrawals = new List<Withdrawal>();
                foreach (var wRlp in (RLPCollection)bodyRlp[2])
                    withdrawals.Add(WithdrawalEncoder.Current.Decode(wRlp.RLPData));
            }

            return new BlockBodyCluster(txs, uncles, withdrawals);
        }
    }
}
