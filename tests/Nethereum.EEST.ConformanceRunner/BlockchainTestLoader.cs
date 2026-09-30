using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EEST.ConformanceRunner
{
    public class BlockchainTestLoader
    {
        public class BlockchainTest
        {
            public string Name { get; set; } = "";
            public string Network { get; set; } = "";
            public BigInteger ChainId { get; set; }
            public Dictionary<string, AccountData> Pre { get; set; } = new();
            public Dictionary<string, AccountData> PostState { get; set; } = new();
            public BlockHeader GenesisBlockHeader { get; set; } = new();
            public List<BlockData> Blocks { get; set; } = new();
            public byte[] LastBlockHash { get; set; } = Array.Empty<byte>();
            public string SealEngine { get; set; } = "";
            public Dictionary<string, BlobScheduleEntry>? BlobSchedule { get; set; }
        }

        public class BlobScheduleEntry
        {
            public string? Target { get; set; }
            public string? Max { get; set; }
            public string? BaseFeeUpdateFraction { get; set; }
        }

        public class AccountData
        {
            public BigInteger Balance { get; set; }
            public BigInteger Nonce { get; set; }
            public byte[] Code { get; set; } = Array.Empty<byte>();
            public Dictionary<BigInteger, BigInteger> Storage { get; set; } = new();
        }

        public class BlockData
        {
            public BlockHeader BlockHeader { get; set; } = new();
            public List<TransactionData> Transactions { get; set; } = new();
            public List<WithdrawalData> Withdrawals { get; set; } = new();
            public byte[] Rlp { get; set; } = Array.Empty<byte>();
            public int BlockNumber { get; set; }
            public string? ExpectException { get; set; }

            public List<AccountChanges>? BlockAccessList { get; set; }
        }

        public class WithdrawalData
        {
            public ulong Index { get; set; }
            public ulong ValidatorIndex { get; set; }
            public string Address { get; set; } = "";
            public ulong Amount { get; set; }
        }

        public class BlockHeader
        {
            public byte[] Hash { get; set; } = Array.Empty<byte>();
            public byte[] ParentHash { get; set; } = Array.Empty<byte>();
            public byte[] StateRoot { get; set; } = Array.Empty<byte>();
            public byte[] TransactionsRoot { get; set; } = Array.Empty<byte>();
            public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
            public byte[] UncleHash { get; set; } = Array.Empty<byte>();
            public byte[] Coinbase { get; set; } = Array.Empty<byte>();
            public byte[] LogsBloom { get; set; } = Array.Empty<byte>();
            public BigInteger Difficulty { get; set; }
            public BigInteger Number { get; set; }
            public BigInteger GasLimit { get; set; }
            public BigInteger GasUsed { get; set; }
            public BigInteger Timestamp { get; set; }
            public byte[] ExtraData { get; set; } = Array.Empty<byte>();
            public byte[] MixHash { get; set; } = Array.Empty<byte>();
            public byte[] Nonce { get; set; } = Array.Empty<byte>();
            public BigInteger? BaseFee { get; set; }
            public byte[]? WithdrawalsRoot { get; set; }
            public long? BlobGasUsed { get; set; }
            public long? ExcessBlobGas { get; set; }
            public byte[]? ParentBeaconBlockRoot { get; set; }
            public byte[]? RequestsHash { get; set; }
            public byte[]? BlockAccessListHash { get; set; }
            public BigInteger? SlotNumber { get; set; }
        }

        public class TransactionData
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public BigInteger GasLimit { get; set; }
            public BigInteger GasPrice { get; set; }
            public BigInteger? MaxFeePerGas { get; set; }
            public BigInteger? MaxPriorityFeePerGas { get; set; }
            public BigInteger Nonce { get; set; }
            public string? To { get; set; }
            public BigInteger Value { get; set; }
            public byte[] R { get; set; } = Array.Empty<byte>();
            public byte[] S { get; set; } = Array.Empty<byte>();
            public BigInteger V { get; set; }
            public string Sender { get; set; } = "";
        }

        public static List<BlockchainTest> LoadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }

        public static List<BlockchainTest> LoadFromJson(string json)
        {
            var tests = new List<BlockchainTest>();

            using var doc = JsonDocument.Parse(json);
            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testData = testProp.Value;

                var test = new BlockchainTest
                {
                    Name = testName,
                    Network = GetStringOrDefault(testData, "network"),
                    SealEngine = GetStringOrDefault(testData, "sealEngine")
                };

                if (testData.TryGetProperty("config", out var config))
                {
                    if (config.TryGetProperty("chainid", out var chainId))
                    {
                        test.ChainId = ParseBigInteger(chainId.GetString());
                    }

                    if (config.TryGetProperty("blobSchedule", out var blobSchedule))
                    {
                        test.BlobSchedule = ParseBlobSchedule(blobSchedule);
                    }
                }

                if (testData.TryGetProperty("lastblockhash", out var lastHash))
                {
                    test.LastBlockHash = lastHash.GetString()?.HexToByteArray() ?? Array.Empty<byte>();
                }

                if (testData.TryGetProperty("pre", out var pre))
                {
                    test.Pre = ParseAccounts(pre);
                }

                if (testData.TryGetProperty("postState", out var postState))
                {
                    test.PostState = ParseAccounts(postState);
                }

                if (testData.TryGetProperty("genesisBlockHeader", out var genesis))
                {
                    test.GenesisBlockHeader = ParseHeader(genesis);
                }

                if (testData.TryGetProperty("blocks", out var blocks))
                {
                    test.Blocks = ParseBlocks(blocks);
                }

                tests.Add(test);
            }

            return tests;
        }

        private static Dictionary<string, BlobScheduleEntry> ParseBlobSchedule(JsonElement element)
        {
            var schedule = new Dictionary<string, BlobScheduleEntry>();

            foreach (var forkProp in element.EnumerateObject())
            {
                schedule[forkProp.Name] = new BlobScheduleEntry
                {
                    Target = GetStringOrNull(forkProp.Value, "target"),
                    Max = GetStringOrNull(forkProp.Value, "max"),
                    BaseFeeUpdateFraction = GetStringOrNull(forkProp.Value, "baseFeeUpdateFraction"),
                };
            }

            return schedule;
        }

        private static Dictionary<string, AccountData> ParseAccounts(JsonElement element)
        {
            var accounts = new Dictionary<string, AccountData>();

            foreach (var accountProp in element.EnumerateObject())
            {
                var address = accountProp.Name;
                var accountData = accountProp.Value;

                var account = new AccountData
                {
                    Balance = ParseBigInteger(GetStringOrDefault(accountData, "balance")),
                    Nonce = ParseBigInteger(GetStringOrDefault(accountData, "nonce")),
                    Code = GetStringOrDefault(accountData, "code").HexToByteArray()
                };

                if (accountData.TryGetProperty("storage", out var storage))
                {
                    foreach (var storageProp in storage.EnumerateObject())
                    {
                        var slot = ParseBigInteger(storageProp.Name);
                        var value = ParseBigInteger(storageProp.Value.GetString());
                        account.Storage[slot] = value;
                    }
                }

                accounts[address] = account;
            }

            return accounts;
        }

        private static BlockHeader ParseHeader(JsonElement element)
        {
            var header = new BlockHeader
            {
                Hash = GetBytesOrDefault(element, "hash"),
                ParentHash = GetBytesOrDefault(element, "parentHash"),
                StateRoot = GetBytesOrDefault(element, "stateRoot"),
                TransactionsRoot = GetBytesOrDefault(element, "transactionsTrie"),
                ReceiptsRoot = GetBytesOrDefault(element, "receiptTrie"),
                UncleHash = GetBytesOrDefault(element, "uncleHash"),
                Coinbase = GetBytesOrDefault(element, "coinbase"),
                LogsBloom = GetBytesOrDefault(element, "bloom"),
                Difficulty = ParseBigInteger(GetStringOrDefault(element, "difficulty")),
                Number = ParseBigInteger(GetStringOrDefault(element, "number")),
                GasLimit = ParseBigInteger(GetStringOrDefault(element, "gasLimit")),
                GasUsed = ParseBigInteger(GetStringOrDefault(element, "gasUsed")),
                Timestamp = ParseBigInteger(GetStringOrDefault(element, "timestamp")),
                ExtraData = GetBytesOrDefault(element, "extraData"),
                MixHash = GetBytesOrDefault(element, "mixHash"),
                Nonce = GetBytesOrDefault(element, "nonce")
            };

            if (element.TryGetProperty("baseFeePerGas", out var baseFee))
            {
                header.BaseFee = ParseBigInteger(baseFee.GetString());
            }

            if (element.TryGetProperty("withdrawalsRoot", out var withdrawals))
            {
                header.WithdrawalsRoot = withdrawals.GetString()?.HexToByteArray();
            }

            if (element.TryGetProperty("blobGasUsed", out var blobGasUsed))
            {
                header.BlobGasUsed = ToInt64Wrapping(ParseBigInteger(blobGasUsed.GetString()));
            }

            if (element.TryGetProperty("excessBlobGas", out var excessBlobGas))
            {
                header.ExcessBlobGas = ToInt64Wrapping(ParseBigInteger(excessBlobGas.GetString()));
            }

            if (element.TryGetProperty("parentBeaconBlockRoot", out var beaconRoot))
            {
                header.ParentBeaconBlockRoot = beaconRoot.GetString()?.HexToByteArray();
            }

            if (TryGetRequestsHash(element, out var requestsRoot))
            {
                header.RequestsHash = requestsRoot.GetString()?.HexToByteArray();
            }

            if (element.TryGetProperty("blockAccessListHash", out var blockAccessListHash))
            {
                header.BlockAccessListHash = blockAccessListHash.GetString()?.HexToByteArray();
            }

            if (element.TryGetProperty("slotNumber", out var slotNumber))
            {
                header.SlotNumber = ParseBigInteger(slotNumber.GetString());
            }

            return header;
        }

        private static bool TryGetRequestsHash(JsonElement element, out JsonElement value) =>
            element.TryGetProperty("requestsHash", out value) || element.TryGetProperty("requestsRoot", out value);

        private static List<BlockData> ParseBlocks(JsonElement element)
        {
            var blocks = new List<BlockData>();

            foreach (var blockEl in element.EnumerateArray())
            {
                var block = new BlockData
                {
                    Rlp = GetBytesOrDefault(blockEl, "rlp")
                };

                if (blockEl.TryGetProperty("blocknumber", out var blockNum))
                {
                    block.BlockNumber = int.Parse(blockNum.GetString() ?? "0");
                }

                if (blockEl.TryGetProperty("expectException", out var expectException))
                {
                    block.ExpectException = expectException.GetString();
                }

                if (blockEl.TryGetProperty("blockHeader", out var header))
                {
                    block.BlockHeader = ParseHeader(header);
                }

                if (blockEl.TryGetProperty("blockAccessList", out var accessListEl))
                {
                    block.BlockAccessList = ReadAccessList(accessListEl);
                }
                else if (blockEl.TryGetProperty("rlp_decoded", out var decodedBlock)
                         && decodedBlock.TryGetProperty("blockAccessList", out var decodedAccessList))
                {
                    block.BlockAccessList = ReadAccessList(decodedAccessList);
                }

                if (blockEl.TryGetProperty("transactions", out var txs)
                    || (blockEl.TryGetProperty("rlp_decoded", out var decodedForTransactions)
                        && decodedForTransactions.TryGetProperty("transactions", out txs)))
                {
                    foreach (var txEl in txs.EnumerateArray())
                    {
                        var tx = new TransactionData
                        {
                            Data = GetBytesOrDefault(txEl, "data"),
                            GasLimit = ParseBigInteger(GetStringOrDefault(txEl, "gasLimit")),
                            GasPrice = ParseBigInteger(GetStringOrDefault(txEl, "gasPrice")),
                            Nonce = ParseBigInteger(GetStringOrDefault(txEl, "nonce")),
                            To = GetStringOrNull(txEl, "to"),
                            Value = ParseBigInteger(GetStringOrDefault(txEl, "value")),
                            R = GetBytesOrDefault(txEl, "r"),
                            S = GetBytesOrDefault(txEl, "s"),
                            V = ParseBigInteger(GetStringOrDefault(txEl, "v")),
                            Sender = GetStringOrDefault(txEl, "sender")
                        };

                        if (txEl.TryGetProperty("maxFeePerGas", out var maxFee))
                        {
                            tx.MaxFeePerGas = ParseBigInteger(maxFee.GetString());
                        }

                        if (txEl.TryGetProperty("maxPriorityFeePerGas", out var maxPriority))
                        {
                            tx.MaxPriorityFeePerGas = ParseBigInteger(maxPriority.GetString());
                        }

                        block.Transactions.Add(tx);
                    }
                }

                if (blockEl.TryGetProperty("withdrawals", out var wdls))
                {
                    foreach (var wEl in wdls.EnumerateArray())
                    {
                        block.Withdrawals.Add(new WithdrawalData
                        {
                            Index = (ulong)ParseBigInteger(GetStringOrDefault(wEl, "index")),
                            ValidatorIndex = (ulong)ParseBigInteger(GetStringOrDefault(wEl, "validatorIndex")),
                            Address = GetStringOrDefault(wEl, "address"),
                            Amount = (ulong)ParseBigInteger(GetStringOrDefault(wEl, "amount"))
                        });
                    }
                }

                blocks.Add(block);
            }

            return blocks;
        }

        private static string GetStringOrDefault(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString() ?? "";
            }
            return "";
        }

        private static string? GetStringOrNull(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                var value = prop.GetString();
                return string.IsNullOrEmpty(value) ? null : value;
            }
            return null;
        }

        private static byte[] GetBytesOrDefault(JsonElement element, string propertyName)
        {
            var str = GetStringOrDefault(element, propertyName);
            if (string.IsNullOrEmpty(str)) return Array.Empty<byte>();
            return str.HexToByteArray();
        }

        internal static long ToInt64Wrapping(BigInteger value) =>
            unchecked((long)(ulong)(value & ((BigInteger.One << 64) - 1)));

        private static BigInteger ParseBigInteger(string? value)
        {
            if (string.IsNullOrEmpty(value)) return BigInteger.Zero;

            if (value.StartsWith("0x") || value.StartsWith("0X"))
            {
                var hex = value.Substring(2);
                if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
                return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
            }

            return BigInteger.Parse(value);
        }

        private static List<AccountChanges> ReadAccessList(JsonElement array)
        {
            var list = new List<AccountChanges>();
            foreach (var a in array.EnumerateArray())
            {
                var account = new AccountChanges(a.GetProperty("address").GetString());

                foreach (var slotEl in a.GetProperty("storageChanges").EnumerateArray())
                {
                    var slot = new SlotChanges(AccessListValue(slotEl.GetProperty("slot").GetString()));
                    foreach (var c in slotEl.GetProperty("slotChanges").EnumerateArray())
                        slot.Changes.Add(new StorageChange(
                            AccessListIndex(c.GetProperty("blockAccessIndex").GetString()),
                            AccessListValue(c.GetProperty("postValue").GetString())));
                    account.StorageChanges.Add(slot);
                }

                foreach (var r in a.GetProperty("storageReads").EnumerateArray())
                    account.StorageReads.Add(AccessListValue(r.GetString()));

                foreach (var c in a.GetProperty("balanceChanges").EnumerateArray())
                    account.BalanceChanges.Add(new BalanceChange(
                        AccessListIndex(c.GetProperty("blockAccessIndex").GetString()),
                        AccessListValue(c.GetProperty("postBalance").GetString())));

                foreach (var c in a.GetProperty("nonceChanges").EnumerateArray())
                    account.NonceChanges.Add(new NonceChange(
                        AccessListIndex(c.GetProperty("blockAccessIndex").GetString()),
                        AccessListIndex(c.GetProperty("postNonce").GetString())));

                foreach (var c in a.GetProperty("codeChanges").EnumerateArray())
                    account.CodeChanges.Add(new CodeChange(
                        AccessListIndex(c.GetProperty("blockAccessIndex").GetString()),
                        c.GetProperty("newCode").GetString().HexToByteArray()));

                list.Add(account);
            }
            return list;
        }

        private static EvmUInt256 AccessListValue(string hex)
        {
            var bytes = hex.HexToByteArray();
            if (bytes.Length == 32) return EvmUInt256.FromBigEndian(bytes);
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return EvmUInt256.FromBigEndian(padded);
        }

        private static ulong AccessListIndex(string hex)
        {
            ulong v = 0;
            foreach (var b in hex.HexToByteArray()) v = (v << 8) | b;
            return v;
        }

        public static IEnumerable<string> GetTestFilesInDirectory(string directory)
        {
            return Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
        }
    }
}
