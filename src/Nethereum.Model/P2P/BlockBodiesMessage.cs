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
                encodedBodies[i] = EncodeBody(msg.Bodies[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedBodies)
            );
        }

        public static byte[] EncodeBody(BlockBody body)
        {
            var encodedTxs = new byte[body.Transactions.Count][];
            for (int j = 0; j < body.Transactions.Count; j++)
                encodedTxs[j] = EncodeTxForBody(body.Transactions[j]);

            var encodedUncles = new byte[body.Uncles.Count][];
            for (int j = 0; j < body.Uncles.Count; j++)
                encodedUncles[j] = BlockHeaderEncoder.Current.Encode(body.Uncles[j]);

            if (body.Withdrawals == null)
            {
                return RLP.RLP.EncodeList(
                    RLP.RLP.EncodeList(encodedTxs),
                    RLP.RLP.EncodeList(encodedUncles));
            }

            var encodedWithdrawals = new byte[body.Withdrawals.Count][];
            for (int j = 0; j < body.Withdrawals.Count; j++)
                encodedWithdrawals[j] = WithdrawalEncoder.Current.Encode(body.Withdrawals[j]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeList(encodedTxs),
                RLP.RLP.EncodeList(encodedUncles),
                RLP.RLP.EncodeList(encodedWithdrawals));
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new BlockBodiesMessage { RequestId = reader.ReadUInt64() };

            var bodies = reader.ReadListHeader();
            while (!reader.IsAtEnd(bodies))
            {
                var body = BlockBodyRlpDecoder.ReadBody(reader, allowBlobNetworkWrapper: false,
                    PeerResponseBounds.MaxTransactionsPerBlock);
                msg.Bodies.Add(body);
            }

            reader.EnsureEnd(bodies);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }

        public static BlockBody ReadBody(RlpReader reader, bool allowBlobNetworkWrapper = true)
        {
            return BlockBodyRlpDecoder.ReadBody(reader, allowBlobNetworkWrapper, BlockBodyRlpDecoder.NoTransactionBound);
        }

        public static BlockBody DecodeBody(byte[] data, bool allowBlobNetworkWrapper = true)
        {
            var reader = new RlpReader(data);
            var body = ReadBody(reader, allowBlobNetworkWrapper);
            reader.EnsureEnd();
            return body;
        }
    }
}
