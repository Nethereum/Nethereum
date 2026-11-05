using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.UnitTests.Oracle
{
    public static class TreeDecodePhase3Oracle
    {
        public static Log DecodeLog(byte[] rawdata)
        {
            var decodedList = RLP.RLP.Decode(rawdata);
            var decodedElements = (RLPCollection)decodedList;

            var log = new Log();
            log.Address = decodedElements[0].RLPData.ToHex(true);

            var topicsCollection = (RLPCollection)decodedElements[1];
            log.Topics = new List<byte[]>();
            foreach (var topic in topicsCollection)
                log.Topics.Add(topic.RLPData);

            log.Data = decodedElements[2].RLPData;
            return log;
        }

        public static Withdrawal DecodeWithdrawal(byte[] data)
        {
            var items = (RLPCollection)RLP.RLP.Decode(data);
            return new Withdrawal
            {
                Index = (ulong)items[0].RLPData.ToLongFromRLPDecoded(),
                ValidatorIndex = (ulong)items[1].RLPData.ToLongFromRLPDecoded(),
                Address = items[2].RLPData,
                AmountInGwei = (ulong)items[3].RLPData.ToLongFromRLPDecoded()
            };
        }

        public static Receipt DecodeReceipt(byte[] rawdata)
        {
            if (rawdata == null || rawdata.Length == 0)
                return null;

            if (rawdata[0] <= 0x7f)
            {
                var transactionType = rawdata[0];
                var innerData = new byte[rawdata.Length - 1];
                Array.Copy(rawdata, 1, innerData, 0, innerData.Length);
                var receipt = DecodeReceiptRlp(innerData);
                receipt.TransactionType = transactionType;
                return receipt;
            }

            return DecodeReceiptRlp(rawdata);
        }

        private static Receipt DecodeReceiptRlp(byte[] rawdata)
        {
            var decodedList = RLP.RLP.Decode(rawdata, RLP.RLP.UntrustedElementBudget(rawdata.Length));
            var decodedElements = (RLPCollection)decodedList;

            var receipt = new Receipt();
            receipt.PostStateOrStatus = decodedElements[0].RLPData ?? new byte[0];
            receipt.CumulativeGasUsed = decodedElements[1].RLPData.ToEvmUInt256FromRLPDecoded();
            receipt.Bloom = decodedElements[2].RLPData;

            var logsCollection = (RLPCollection)decodedElements[3];
            receipt.Logs = new List<Log>();
            foreach (var logData in logsCollection)
                receipt.Logs.Add(DecodeLog(logData.RLPData));

            return receipt;
        }

        public static Receipt DecodeEth69Receipt(RLPCollection receiptFields)
        {
            var txTypeBytes = receiptFields[0].RLPData ?? new byte[0];
            var receipt = new Receipt
            {
                TransactionType = txTypeBytes.Length == 0 ? (byte)0 : txTypeBytes[txTypeBytes.Length - 1],
                PostStateOrStatus = receiptFields[1].RLPData ?? new byte[0],
                CumulativeGasUsed = receiptFields[2].RLPData.ToEvmUInt256FromRLPDecoded()
            };

            var logsCollection = (RLPCollection)receiptFields[3];
            receipt.Logs = new List<Log>();
            foreach (var logData in logsCollection)
                receipt.Logs.Add(DecodeLog(logData.RLPData));

            var bloom = new LogBloomFilter();
            foreach (var log in receipt.Logs)
                bloom.AddLog(log);
            receipt.Bloom = bloom.Data;

            return receipt;
        }

        public static ReceiptsMessage DecodeReceiptsMessage(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));

            var msg = new ReceiptsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var blockList = (RLPCollection)outer[1];
            foreach (RLPCollection blockReceipts in blockList)
            {
                var receipts = new List<Receipt>();
                foreach (var receiptRlp in blockReceipts)
                    receipts.Add(DecodeReceipt(receiptRlp.RLPData));
                msg.ReceiptsByBlock.Add(receipts);
            }

            return msg;
        }

        public static ReceiptsMessage DecodeReceiptsMessageEth69(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data,
                RLP.RLP.UntrustedElementBudget(data.Length) + PeerResponseBounds.MaxTransactionsPerBlock + 1);
            var msg = new ReceiptsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var blockList = (RLPCollection)outer[1];
            foreach (RLPCollection blockReceipts in blockList)
            {
                if (PeerResponseBounds.ExceedsWhatABlockCanHold(blockReceipts)) break;
                msg.ReceiptsByBlock.Add(DecodeEth69BlockReceipts(blockReceipts));
            }

            return msg;
        }

        public static ReceiptsMessage70 DecodeReceiptsMessageEth70(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data,
                RLP.RLP.UntrustedElementBudget(data.Length) + PeerResponseBounds.MaxTransactionsPerBlock + 1);
            var incompleteData = outer[1].RLPData ?? new byte[0];

            var msg = new ReceiptsMessage70
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                LastBlockIncomplete = incompleteData.Length > 0 && incompleteData[0] != 0
            };

            var blockList = (RLPCollection)outer[2];
            foreach (RLPCollection blockReceipts in blockList)
            {
                if (PeerResponseBounds.ExceedsWhatABlockCanHold(blockReceipts)) break;
                msg.ReceiptsByBlock.Add(DecodeEth69BlockReceipts(blockReceipts));
            }

            return msg;
        }

        private static List<Receipt> DecodeEth69BlockReceipts(RLPCollection blockReceipts)
        {
            var receipts = new List<Receipt>();
            foreach (RLPCollection receiptFields in blockReceipts)
                receipts.Add(DecodeEth69Receipt(receiptFields));
            return receipts;
        }

        public static BlockBodiesMessage DecodeBlockBodiesMessage(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data,
                RLP.RLP.UntrustedElementBudget(data.Length) + PeerResponseBounds.MaxTransactionsPerBlock + 1);

            var msg = new BlockBodiesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var bodiesList = (RLPCollection)outer[1];
            foreach (RLPCollection bodyRlp in bodiesList)
            {
                var body = new BlockBody();

                var txList = (RLPCollection)bodyRlp[0];

                if (PeerResponseBounds.ExceedsWhatABlockCanHold(txList)) break;

                foreach (var txRlp in txList)
                    body.Transactions.Add(TreeDecodeOracle.DecodeTransaction(txRlp.RLPData, false));

                var uncleList = (RLPCollection)bodyRlp[1];
                foreach (RLPCollection uncleRlp in uncleList)
                    body.Uncles.Add(TreeDecodeOracle.DecodeHeader(uncleRlp.RLPData));

                if (bodyRlp.Count > 2)
                {
                    body.Withdrawals = new List<Withdrawal>();
                    var withdrawalList = (RLPCollection)bodyRlp[2];
                    foreach (var wRlp in withdrawalList)
                        body.Withdrawals.Add(DecodeWithdrawal(wRlp.RLPData));
                }

                msg.Bodies.Add(body);
            }

            return msg;
        }

        public static BlockBody DecodeBody(byte[] bytes, bool allowBlobNetworkWrapper)
        {
            var bodyRlp = (RLPCollection)RLP.RLP.Decode(bytes);

            var body = new BlockBody();
            foreach (var txRlp in (RLPCollection)bodyRlp[0])
                body.Transactions.Add(TreeDecodeOracle.DecodeTransaction(txRlp.RLPData, allowBlobNetworkWrapper));

            foreach (RLPCollection uncleRlp in (RLPCollection)bodyRlp[1])
                body.Uncles.Add(TreeDecodeOracle.DecodeHeader(uncleRlp.RLPData));

            if (bodyRlp.Count > 2)
            {
                body.Withdrawals = new List<Withdrawal>();
                foreach (var wRlp in (RLPCollection)bodyRlp[2])
                    body.Withdrawals.Add(DecodeWithdrawal(wRlp.RLPData));
            }

            return body;
        }

        public static NewBlockMessage DecodeNewBlockMessage(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));
            var blockRlp = (RLPCollection)outer[0];

            var msg = new NewBlockMessage
            {
                Header = TreeDecodeOracle.DecodeHeader(blockRlp[0].RLPData),
                TotalDifficulty = (outer[1].RLPData ?? new byte[0]).ToBigIntegerFromUnsignedBigEndian()
            };

            var txList = (RLPCollection)blockRlp[1];
            foreach (var txRlp in txList)
                msg.Transactions.Add(TreeDecodeOracle.DecodeTransaction(txRlp.RLPData, false));

            var uncleList = (RLPCollection)blockRlp[2];
            foreach (RLPCollection uncleRlp in uncleList)
                msg.Uncles.Add(TreeDecodeOracle.DecodeHeader(uncleRlp.RLPData));

            if (blockRlp.Count > 3)
            {
                msg.Withdrawals = new List<Withdrawal>();
                var withdrawalList = (RLPCollection)blockRlp[3];
                foreach (var wRlp in withdrawalList)
                    msg.Withdrawals.Add(DecodeWithdrawal(wRlp.RLPData));
            }

            return msg;
        }

        public static NewBlockHashesMessage DecodeNewBlockHashesMessage(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));
            var msg = new NewBlockHashesMessage();
            foreach (RLPCollection entry in outer)
            {
                msg.Entries.Add(new NewBlockHashesMessage.BlockHashEntry
                {
                    Hash = entry[0].RLPData,
                    Number = (ulong)entry[1].RLPData.ToLongFromRLPDecoded()
                });
            }
            return msg;
        }

        public static Nethereum.Model.P2P.BlockAccessListsMessage DecodeEthBlockAccessListsMessage(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));

            var msg = new Nethereum.Model.P2P.BlockAccessListsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var balList = (RLPCollection)outer[1];
            foreach (var balRlp in balList)
                msg.BlockAccessListsByBlock.Add(balRlp.RLPData ?? new byte[0]);

            return msg;
        }

        public static Nethereum.Model.P2P.Snap.BlockAccessListsMessage DecodeSnapBlockAccessListsMessage(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));

            var msg = new Nethereum.Model.P2P.Snap.BlockAccessListsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var balList = (RLPCollection)outer[1];
            foreach (var balRlp in balList)
                msg.BlockAccessListsByBlock.Add(balRlp.RLPData ?? new byte[0]);

            return msg;
        }

        public static AccountRangeMessage DecodeAccountRangeMessage(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));
            var m = new AccountRangeMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection accRlp in (RLPCollection)o[1])
            {
                if (accRlp.Count != 2 || !(accRlp[1] is RLPCollection bodyList) || bodyList.Count != 4)
                    throw new InvalidOperationException("AccountRange entry is not [hash, slim account list]");
                var items = new byte[bodyList.Count][];
                for (int i = 0; i < bodyList.Count; i++)
                {
                    if (bodyList[i] is RLPCollection)
                        throw new InvalidOperationException("AccountRange slim account holds a list where a string is expected");
                    items[i] = RLP.RLP.EncodeElement(bodyList[i].RLPData);
                }
                var bodyBytes = RLP.RLP.EncodeList(items);
                m.Accounts.Add(new AccountRangeMessage.AccountEntry
                {
                    Hash = accRlp[0].RLPData,
                    Body = bodyBytes
                });
            }
            foreach (var pNode in (RLPCollection)o[2])
                m.Proof.Add(pNode.RLPData);
            return m;
        }

        public static StorageRangesMessage DecodeStorageRangesMessage(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));
            var m = new StorageRangesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection acctRlp in (RLPCollection)o[1])
            {
                var slotsForAccount = new List<StorageRangesMessage.SlotEntry>();
                foreach (RLPCollection slotRlp in acctRlp)
                {
                    slotsForAccount.Add(new StorageRangesMessage.SlotEntry
                    {
                        Hash = slotRlp[0].RLPData,
                        Data = slotRlp[1].RLPData ?? new byte[0]
                    });
                }
                m.Slots.Add(slotsForAccount);
            }
            foreach (var p in (RLPCollection)o[2])
                m.Proof.Add(p.RLPData);
            return m;
        }

        public static ByteCodesMessage DecodeByteCodesMessage(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));
            var m = new ByteCodesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var c in (RLPCollection)o[1])
                m.Codes.Add(c.RLPData);
            return m;
        }

        public static TrieNodesMessage DecodeTrieNodesMessage(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data, RLP.RLP.UntrustedElementBudget(data.Length));
            var m = new TrieNodesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var n in (RLPCollection)o[1])
                m.Nodes.Add(n.RLPData);
            return m;
        }

        public static List<AccountChanges> DecodeBlockAccessList(byte[] data)
        {
            var accounts = (RLPCollection)RLP.RLP.Decode(data);
            var result = new List<AccountChanges>(accounts.Count);
            foreach (RLPCollection accountRlp in accounts)
                result.Add(DecodeBalAccount(accountRlp));
            return result;
        }

        private static AccountChanges DecodeBalAccount(RLPCollection accountRlp)
        {
            return new AccountChanges(accountRlp[0].RLPData.ToHex(true))
            {
                StorageChanges = DecodeBalStorageChanges((RLPCollection)accountRlp[1]),
                StorageReads = DecodeBalStorageReads((RLPCollection)accountRlp[2]),
                BalanceChanges = DecodeBalIndexed((RLPCollection)accountRlp[3],
                    (index, value) => new BalanceChange(index, value.ToEvmUInt256FromRLPDecoded())),
                NonceChanges = DecodeBalIndexed((RLPCollection)accountRlp[4],
                    (index, value) => new NonceChange(index, value.ToULongFromRLPDecoded())),
                CodeChanges = DecodeBalIndexed((RLPCollection)accountRlp[5],
                    (index, value) => new CodeChange(index, value ?? RLP.RLP.EMPTY_BYTE_ARRAY))
            };
        }

        private static List<SlotChanges> DecodeBalStorageChanges(RLPCollection storageChangesRlp)
        {
            var result = new List<SlotChanges>(storageChangesRlp.Count);
            foreach (RLPCollection slotRlp in storageChangesRlp)
            {
                result.Add(new SlotChanges(slotRlp[0].RLPData.ToEvmUInt256FromRLPDecoded())
                {
                    Changes = DecodeBalIndexed((RLPCollection)slotRlp[1],
                        (index, value) => new StorageChange(index, value.ToEvmUInt256FromRLPDecoded()))
                });
            }
            return result;
        }

        private static List<EvmUInt256> DecodeBalStorageReads(RLPCollection storageReadsRlp)
        {
            var result = new List<EvmUInt256>(storageReadsRlp.Count);
            foreach (var slotRlp in storageReadsRlp)
                result.Add(slotRlp.RLPData.ToEvmUInt256FromRLPDecoded());
            return result;
        }

        private static List<T> DecodeBalIndexed<T>(RLPCollection changesRlp, Func<ulong, byte[], T> factory)
        {
            var result = new List<T>(changesRlp.Count);
            foreach (RLPCollection changeRlp in changesRlp)
                result.Add(factory(changeRlp[0].RLPData.ToULongFromRLPDecoded(), changeRlp[1].RLPData));
            return result;
        }
    }
}
