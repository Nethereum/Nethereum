using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using EthBlockAccessListsMessage = Nethereum.Model.P2P.BlockAccessListsMessage;
using EthBlockAccessListsMessageEncoder = Nethereum.Model.P2P.BlockAccessListsMessageEncoder;
using EthGetBlockAccessListsMessage = Nethereum.Model.P2P.GetBlockAccessListsMessage;
using EthGetBlockAccessListsMessageEncoder = Nethereum.Model.P2P.GetBlockAccessListsMessageEncoder;
using SnapBlockAccessListsMessage = Nethereum.Model.P2P.Snap.BlockAccessListsMessage;
using SnapBlockAccessListsMessageEncoder = Nethereum.Model.P2P.Snap.BlockAccessListsMessageEncoder;
using SnapGetBlockAccessListsMessage = Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage;
using SnapGetBlockAccessListsMessageEncoder = Nethereum.Model.P2P.Snap.GetBlockAccessListsMessageEncoder;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public sealed class NethereumVerdict
    {
        public bool Accepted;
        public string Error;
        public string Canonical;
        public string Hash;
        public string Sender;
        public long? Count;
        public ulong? Id;
        public string[] Hashes;
        public string[] Bodies;

        public static NethereumVerdict Refused(Exception e) =>
            new NethereumVerdict { Error = e.GetType().Name + ": " + e.Message };
    }

    public static class NethereumDecoders
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        public static readonly IReadOnlyDictionary<string, Func<byte[], NethereumVerdict>> ByKind =
            new Dictionary<string, Func<byte[], NethereumVerdict>>
            {
                ["tx_binary"] = Guard(TxBinary),
                ["tx_rlp"] = Guard(TxRlp),
                ["header"] = Guard(Header),
                ["block"] = Guard(Block),
                ["body"] = Guard(Body),
                ["withdrawal"] = Guard(Withdrawal),
                ["log"] = Guard(Log),
                ["receipt_binary"] = Guard(ReceiptBinary),
                ["receipt_rlp"] = Guard(ReceiptRlp),
                ["receipt_eth69"] = Guard(ReceiptEth69),
                ["bal"] = Guard(Bal),
                ["eth_Status"] = Guard(EthStatus),
                ["eth_Transactions"] = Guard(EthTransactions),
                ["eth_GetBlockHeaders"] = Guard(EthGetBlockHeaders),
                ["eth_BlockHeaders"] = Guard(EthBlockHeaders),
                ["eth_GetBlockBodies"] = Guard(EthGetBlockBodies),
                ["eth_BlockBodies"] = Guard(EthBlockBodies),
                ["eth_NewPooledTransactionHashes"] = Guard(EthNewPooledTransactionHashes),
                ["eth_GetPooledTransactions"] = Guard(EthGetPooledTransactions),
                ["eth_PooledTransactions"] = Guard(EthPooledTransactions),
                ["eth_GetReceipts69"] = Guard(EthGetReceipts69),
                ["eth_GetReceipts70"] = Guard(EthGetReceipts70),
                ["eth_Receipts69"] = Guard(EthReceipts69),
                ["eth_Receipts70"] = Guard(EthReceipts70),
                ["eth_BlockRangeUpdate"] = Guard(EthBlockRangeUpdate),
                ["eth_GetBlockAccessLists"] = Guard(EthGetBlockAccessLists),
                ["eth_BlockAccessLists"] = Guard(EthBlockAccessLists),
                ["snap_GetAccountRange"] = Guard(SnapGetAccountRange),
                ["snap_AccountRange"] = Guard(SnapAccountRange),
                ["snap_GetStorageRanges"] = Guard(SnapGetStorageRanges),
                ["snap_StorageRanges"] = Guard(SnapStorageRanges),
                ["snap_GetByteCodes"] = Guard(SnapGetByteCodes),
                ["snap_ByteCodes"] = Guard(SnapByteCodes),
                ["snap_GetTrieNodes"] = Guard(SnapGetTrieNodes),
                ["snap_TrieNodes"] = Guard(SnapTrieNodes),
                ["snap_GetAccessLists"] = Guard(SnapGetAccessLists),
                ["snap_AccessLists"] = Guard(SnapAccessLists)
            };

        public static readonly IReadOnlyList<string> KindsWithoutNethereumDecoder = new[] { "receipt_storage" };

        private static Func<byte[], NethereumVerdict> Guard(Func<byte[], NethereumVerdict> decode)
        {
            return data =>
            {
                try
                {
                    var verdict = decode(data);
                    verdict.Accepted = true;
                    return verdict;
                }
                catch (Exception e)
                {
                    return NethereumVerdict.Refused(e);
                }
            };
        }

        private static string Hex(byte[] bytes) => bytes == null ? null : bytes.ToHex();

        private static string[] HexAll(IEnumerable<byte[]> items) => items.Select(Hex).ToArray();

        private static byte[] TxBinaryBytes(ISignedTransaction tx) =>
            tx is Transaction4844 blob && blob.Sidecar != null ? blob.GetRLPEncodedWithSidecar() : tx.GetRLPEncoded();

        private static byte[] TxRlpBytes(ISignedTransaction tx)
        {
            var binary = TxBinaryBytes(tx);
            return binary.Length > 0 && binary[0] < 0xc0 ? RLP.RLP.EncodeElement(binary) : binary;
        }

        private static string SenderOf(ISignedTransaction tx)
        {
            try
            {
                var sender = tx.GetSenderAddress();
                return sender?.EnsureHexPrefix().Substring(2).ToLowerInvariant();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static NethereumVerdict Transaction(ISignedTransaction tx, byte[] canonical) => new NethereumVerdict
        {
            Canonical = Hex(canonical),
            Hash = Hex(tx.Hash),
            Sender = SenderOf(tx)
        };

        private static NethereumVerdict TxBinary(byte[] data)
        {
            if (data.Length == 0) throw new InvalidOperationException("empty transaction");
            byte[] element;
            if (data[0] >= 0xc0) element = data;
            else if (data[0] > 0x7f) throw new InvalidOperationException("a canonical binary transaction opens with a type byte or a list");
            else element = RLP.RLP.EncodeElement(data);
            var reader = new RlpReader(element);
            var tx = TransactionFactory.ReadTransaction(reader, true);
            reader.EnsureEnd();
            return Transaction(tx, TxBinaryBytes(tx));
        }

        private static NethereumVerdict TxRlp(byte[] data)
        {
            var reader = new RlpReader(data);
            var tx = TransactionFactory.ReadTransaction(reader, true);
            reader.EnsureEnd();
            return Transaction(tx, TxRlpBytes(tx));
        }

        private static NethereumVerdict Header(byte[] data)
        {
            var reader = new RlpReader(data);
            var header = BlockHeaderEncoder.Current.Decode(reader);
            reader.EnsureEnd();
            var canonical = BlockHeaderEncoder.Current.Encode(header);
            return new NethereumVerdict { Canonical = Hex(canonical), Hash = Hex(Keccak.CalculateHash(canonical)) };
        }

        private static NethereumVerdict Block(byte[] data)
        {
            var block = NewBlockMessageEncoder.DecodeBlock(data, false);
            var canonical = NewBlockMessageEncoder.EncodeBlock(block.Header, block.Transactions, block.Uncles, block.Withdrawals);
            return new NethereumVerdict
            {
                Canonical = Hex(canonical),
                Hash = Hex(Keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block.Header)))
            };
        }

        private static NethereumVerdict Body(byte[] data)
        {
            var body = BlockBodiesMessageEncoder.DecodeBody(data, false);
            return new NethereumVerdict { Canonical = Hex(BlockBodiesMessageEncoder.EncodeBody(body)) };
        }

        private static NethereumVerdict Withdrawal(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(WithdrawalEncoder.Current.Encode(WithdrawalEncoder.Current.Decode(data))) };

        private static NethereumVerdict Log(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(LogEncoder.Current.Encode(LogEncoder.Current.Decode(data))) };

        private static byte[] EncodeConsensus(Receipt receipt) =>
            receipt.TransactionType > 0
                ? ReceiptEncoder.Current.EncodeTyped(receipt, receipt.TransactionType)
                : ReceiptEncoder.Current.Encode(receipt);

        private static NethereumVerdict ReceiptBinary(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(EncodeConsensus(ReceiptEncoder.Current.Decode(data))) };

        private static NethereumVerdict ReceiptRlp(byte[] data)
        {
            var reader = new RlpReader(data);
            var receipt = ReceiptEncoder.Current.Decode(reader);
            reader.EnsureEnd();
            return new NethereumVerdict { Canonical = Hex(EncodeConsensus(receipt)) };
        }

        private static NethereumVerdict ReceiptEth69(byte[] data)
        {
            var reader = new RlpReader(data);
            var receipt = ReceiptsMessageEth69Encoder.ReadSingleReceipt(reader);
            reader.EnsureEnd();
            return new NethereumVerdict { Canonical = Hex(EncodeConsensus(receipt)) };
        }

        private static NethereumVerdict Bal(byte[] data)
        {
            var list = BlockAccessListRLPEncoder.Current.Decode(data);
            return new NethereumVerdict { Canonical = Hex(BlockAccessListRLPEncoder.Current.Encode(list)), Count = list.Count };
        }

        private static NethereumVerdict EthStatus(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(Eth69StatusMessageEncoder.Encode(Eth69StatusMessageEncoder.Decode(data))) };

        private static NethereumVerdict Transactions(IReadOnlyCollection<ISignedTransaction> txs, ulong? id) => new NethereumVerdict
        {
            Count = txs.Count,
            Id = id,
            Hashes = txs.Select(t => Hex(t.Hash)).ToArray()
        };

        private static NethereumVerdict EthTransactions(byte[] data) =>
            Transactions(TransactionsMessageEncoder.Decode(data).Transactions, null);

        private static NethereumVerdict EthPooledTransactions(byte[] data)
        {
            var message = PooledTransactionsMessageEncoder.Decode(data);
            return Transactions(message.Transactions, message.RequestId);
        }

        private static NethereumVerdict EthGetBlockHeaders(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(GetBlockHeadersMessageEncoder.Encode(GetBlockHeadersMessageEncoder.Decode(data))) };

        private static NethereumVerdict EthBlockHeaders(byte[] data)
        {
            var message = BlockHeadersMessageEncoder.Decode(data);
            return new NethereumVerdict
            {
                Count = message.Headers.Count,
                Id = message.RequestId,
                Hashes = message.Headers.Select(h => Hex(Keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(h)))).ToArray()
            };
        }

        private static NethereumVerdict EthGetBlockBodies(byte[] data)
        {
            var message = GetBlockBodiesMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(GetBlockBodiesMessageEncoder.Encode(message)), Count = message.BlockHashes.Length };
        }

        private static NethereumVerdict EthBlockBodies(byte[] data)
        {
            var message = BlockBodiesMessageEncoder.Decode(data);
            return new NethereumVerdict
            {
                Count = message.Bodies.Count,
                Id = message.RequestId,
                Bodies = message.Bodies.Select(b => Hex(BlockBodiesMessageEncoder.EncodeBody(b))).ToArray()
            };
        }

        private static NethereumVerdict EthNewPooledTransactionHashes(byte[] data)
        {
            var message = NewPooledTransactionHashesMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(NewPooledTransactionHashesMessageEncoder.Encode(message)), Count = message.Hashes.Count };
        }

        private static NethereumVerdict EthGetPooledTransactions(byte[] data)
        {
            var message = GetPooledTransactionsMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(GetPooledTransactionsMessageEncoder.Encode(message)), Count = message.Hashes.Count };
        }

        private static NethereumVerdict EthGetReceipts69(byte[] data)
        {
            var message = GetReceiptsMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(GetReceiptsMessageEncoder.Encode(message)), Count = message.BlockHashes.Length };
        }

        private static NethereumVerdict EthGetReceipts70(byte[] data)
        {
            var message = GetReceiptsMessage70Encoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(GetReceiptsMessage70Encoder.Encode(message)), Count = message.BlockHashes.Length };
        }

        private static NethereumVerdict EthReceipts69(byte[] data)
        {
            var message = ReceiptsMessageEth69Encoder.Decode(data);
            return new NethereumVerdict { Count = message.ReceiptsByBlock.Count, Id = message.RequestId };
        }

        private static NethereumVerdict EthReceipts70(byte[] data)
        {
            var message = ReceiptsMessageEth70Encoder.Decode(data);
            return new NethereumVerdict { Count = message.ReceiptsByBlock.Count, Id = message.RequestId };
        }

        private static NethereumVerdict EthBlockRangeUpdate(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(BlockRangeUpdateMessageEncoder.Encode(BlockRangeUpdateMessageEncoder.Decode(data))) };

        private static NethereumVerdict EthGetBlockAccessLists(byte[] data)
        {
            var message = EthGetBlockAccessListsMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(EthGetBlockAccessListsMessageEncoder.Encode(message)), Count = message.BlockHashes.Length };
        }

        private static NethereumVerdict EthBlockAccessLists(byte[] data)
        {
            var message = EthBlockAccessListsMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.BlockAccessListsByBlock.Count, Id = message.RequestId };
        }

        private static NethereumVerdict SnapGetAccountRange(byte[] data) =>
            new NethereumVerdict { Canonical = Hex(GetAccountRangeMessageEncoder.Encode(GetAccountRangeMessageEncoder.Decode(data))) };

        private static NethereumVerdict SnapAccountRange(byte[] data)
        {
            var message = AccountRangeMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.Accounts.Count, Id = message.RequestId };
        }

        private static NethereumVerdict SnapGetStorageRanges(byte[] data)
        {
            var message = GetStorageRangesMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(GetStorageRangesMessageEncoder.Encode(message)), Count = message.AccountHashes.Count };
        }

        private static NethereumVerdict SnapStorageRanges(byte[] data)
        {
            var message = StorageRangesMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.Slots.Count, Id = message.RequestId };
        }

        private static NethereumVerdict SnapGetByteCodes(byte[] data)
        {
            var message = GetByteCodesMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(GetByteCodesMessageEncoder.Encode(message)), Count = message.Hashes.Count };
        }

        private static NethereumVerdict SnapByteCodes(byte[] data)
        {
            var message = ByteCodesMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.Codes.Count, Id = message.RequestId };
        }

        private static NethereumVerdict SnapGetTrieNodes(byte[] data)
        {
            var message = GetTrieNodesMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.Paths.Count, Id = message.RequestId };
        }

        private static NethereumVerdict SnapTrieNodes(byte[] data)
        {
            var message = TrieNodesMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.Nodes.Count, Id = message.RequestId };
        }

        private static NethereumVerdict SnapGetAccessLists(byte[] data)
        {
            var message = SnapGetBlockAccessListsMessageEncoder.Decode(data);
            return new NethereumVerdict { Canonical = Hex(SnapGetBlockAccessListsMessageEncoder.Encode(message)), Count = message.Hashes.Count };
        }

        private static NethereumVerdict SnapAccessLists(byte[] data)
        {
            var message = SnapBlockAccessListsMessageEncoder.Decode(data);
            return new NethereumVerdict { Count = message.BlockAccessListsByBlock.Count, Id = message.RequestId };
        }
    }
}
