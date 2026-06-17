using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class BlockBody
    {
        public List<ISignedTransaction> Transactions { get; set; } = new();
        public List<BlockHeader> Uncles { get; set; } = new();
        public List<Withdrawal> Withdrawals { get; set; }
    }

    public class BlockBodiesMessage
    {
        public ulong RequestId { get; set; }
        public List<BlockBody> Bodies { get; set; } = new();
    }

    public static class BlockBodiesMessageEncoder
    {
        public static byte[] Encode(BlockBodiesMessage msg)
        {
            var encodedBodies = new byte[msg.Bodies.Count][];
            for (int i = 0; i < msg.Bodies.Count; i++)
            {
                var body = msg.Bodies[i];

                var encodedTxs = new byte[body.Transactions.Count][];
                for (int j = 0; j < body.Transactions.Count; j++)
                    encodedTxs[j] = EncodeTxForBody(body.Transactions[j]);

                var encodedUncles = new byte[body.Uncles.Count][];
                for (int j = 0; j < body.Uncles.Count; j++)
                    encodedUncles[j] = BlockHeaderEncoder.Current.Encode(body.Uncles[j]);

                if (body.Withdrawals == null)
                {
                    encodedBodies[i] = RLP.RLP.EncodeList(
                        RLP.RLP.EncodeList(encodedTxs),
                        RLP.RLP.EncodeList(encodedUncles));
                }
                else
                {
                    var encodedWithdrawals = new byte[body.Withdrawals.Count][];
                    for (int j = 0; j < body.Withdrawals.Count; j++)
                        encodedWithdrawals[j] = WithdrawalEncoder.Current.Encode(body.Withdrawals[j]);

                    encodedBodies[i] = RLP.RLP.EncodeList(
                        RLP.RLP.EncodeList(encodedTxs),
                        RLP.RLP.EncodeList(encodedUncles),
                        RLP.RLP.EncodeList(encodedWithdrawals));
                }
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedBodies)
            );
        }

        private static byte[] EncodeTxForBody(ISignedTransaction tx)
        {
            // eth/68 body encoding: typed transactions (EIP-2718) are wrapped as RLP
            // byte strings; legacy transactions are nested RLP lists as-is.
            var raw = tx.GetRLPEncoded();
            if (raw.Length > 0 && raw[0] < 0xc0)
                return RLP.RLP.EncodeElement(raw);
            return raw;
        }

        public static BlockBodiesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);

            var msg = new BlockBodiesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var bodiesList = (RLPCollection)outer[1];
            foreach (RLPCollection bodyRlp in bodiesList)
            {
                var body = new BlockBody();

                var txList = (RLPCollection)bodyRlp[0];
                foreach (var txRlp in txList)
                    body.Transactions.Add(TransactionFactory.CreateTransaction(txRlp.RLPData));

                var uncleList = (RLPCollection)bodyRlp[1];
                foreach (RLPCollection uncleRlp in uncleList)
                    body.Uncles.Add(BlockHeaderEncoder.Current.Decode(uncleRlp.RLPData));

                if (bodyRlp.Count > 2)
                {
                    body.Withdrawals = new List<Withdrawal>();
                    var withdrawalList = (RLPCollection)bodyRlp[2];
                    foreach (var wRlp in withdrawalList)
                        body.Withdrawals.Add(WithdrawalEncoder.Current.Decode(wRlp.RLPData));
                }

                msg.Bodies.Add(body);
            }

            return msg;
        }
    }
}
