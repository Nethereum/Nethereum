using System.Collections.Generic;
using System.Numerics;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/68 NewBlock (0x07): announces a newly produced block to peers.
    /// Format: [block, totalDifficulty]
    /// where block = [header, txs, uncles] or [header, txs, uncles, withdrawals]
    /// per the active hardfork. Typed transactions inside txs are wrapped as
    /// RLP byte strings per EIP-2718.
    /// </summary>
    public class NewBlockMessage
    {
        public BlockHeader Header { get; set; }
        public List<ISignedTransaction> Transactions { get; set; } = new();
        public List<BlockHeader> Uncles { get; set; } = new();
        public List<Withdrawal> Withdrawals { get; set; }
        public BigInteger TotalDifficulty { get; set; }
    }

    public static class NewBlockMessageEncoder
    {
        public static byte[] EncodeBlock(
            BlockHeader header,
            System.Collections.Generic.IList<ISignedTransaction> transactions,
            System.Collections.Generic.IList<BlockHeader> uncles,
            System.Collections.Generic.IList<Withdrawal> withdrawals)
        {
            var headerBytes = BlockHeaderEncoder.Current.Encode(header);

            var txs = transactions ?? new System.Collections.Generic.List<ISignedTransaction>();
            var encodedTxs = new byte[txs.Count][];
            for (int i = 0; i < txs.Count; i++)
                encodedTxs[i] = EncodeTxForBlock(txs[i]);

            var uncleList = uncles ?? new System.Collections.Generic.List<BlockHeader>();
            var encodedUncles = new byte[uncleList.Count][];
            for (int i = 0; i < uncleList.Count; i++)
                encodedUncles[i] = BlockHeaderEncoder.Current.Encode(uncleList[i]);

            if (withdrawals == null)
            {
                return RLP.RLP.EncodeList(
                    headerBytes,
                    RLP.RLP.EncodeList(encodedTxs),
                    RLP.RLP.EncodeList(encodedUncles));
            }

            var encodedWithdrawals = new byte[withdrawals.Count][];
            for (int i = 0; i < withdrawals.Count; i++)
                encodedWithdrawals[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);

            return RLP.RLP.EncodeList(
                headerBytes,
                RLP.RLP.EncodeList(encodedTxs),
                RLP.RLP.EncodeList(encodedUncles),
                RLP.RLP.EncodeList(encodedWithdrawals));
        }

        public static byte[] Encode(NewBlockMessage msg)
        {
            var blockBytes = EncodeBlock(msg.Header, msg.Transactions, msg.Uncles, msg.Withdrawals);

            return RLP.RLP.EncodeList(
                blockBytes,
                RLP.RLP.EncodeElement(msg.TotalDifficulty.IsZero
                    ? new byte[0]
                    : msg.TotalDifficulty.ToByteArrayUnsignedBigEndian())
            );
        }

        public static NewBlockMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var blockRlp = (RLPCollection)outer[0];

            var msg = new NewBlockMessage
            {
                Header = BlockHeaderEncoder.Current.Decode(blockRlp[0].RLPData),
                TotalDifficulty = (outer[1].RLPData ?? new byte[0]).ToBigIntegerFromUnsignedBigEndian()
            };

            var txList = (RLPCollection)blockRlp[1];
            foreach (var txRlp in txList)
                msg.Transactions.Add(TransactionFactory.CreateTransaction(txRlp.RLPData, allowBlobNetworkWrapper: false));

            var uncleList = (RLPCollection)blockRlp[2];
            foreach (RLPCollection uncleRlp in uncleList)
                msg.Uncles.Add(BlockHeaderEncoder.Current.Decode(uncleRlp.RLPData));

            if (blockRlp.Count > 3)
            {
                msg.Withdrawals = new List<Withdrawal>();
                var withdrawalList = (RLPCollection)blockRlp[3];
                foreach (var wRlp in withdrawalList)
                    msg.Withdrawals.Add(WithdrawalEncoder.Current.Decode(wRlp.RLPData));
            }

            return msg;
        }

        private static byte[] EncodeTxForBlock(ISignedTransaction tx)
        {
            var raw = tx.GetRLPEncoded();
            if (raw.Length > 0 && raw[0] < 0xc0)
                return RLP.RLP.EncodeElement(raw);
            return raw;
        }
    }
}
