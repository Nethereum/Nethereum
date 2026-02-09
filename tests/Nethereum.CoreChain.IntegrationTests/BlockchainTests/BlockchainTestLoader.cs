using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
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
            public GenesisBlockHeader GenesisBlockHeader { get; set; } = new();
            public List<BlockData> Blocks { get; set; } = new();
            public byte[] LastBlockHash { get; set; } = Array.Empty<byte>();
            public string SealEngine { get; set; } = "";
        }

        public class AccountData
        {
            public BigInteger Balance { get; set; }
            public BigInteger Nonce { get; set; }
            public byte[] Code { get; set; } = Array.Empty<byte>();
            public Dictionary<BigInteger, BigInteger> Storage { get; set; } = new();
        }

        public class GenesisBlockHeader
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
        }

        public class BlockData
        {
            public BlockHeader BlockHeader { get; set; } = new();
            public List<TransactionData> Transactions { get; set; } = new();
            public byte[] Rlp { get; set; } = Array.Empty<byte>();
            public int BlockNumber { get; set; }
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
            public byte[]? ParentBeaconBlockRoot { get; set; }
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

                if (testData.TryGetProperty("config", out var config) &&
                    config.TryGetProperty("chainid", out var chainId))
                {
                    test.ChainId = ParseBigInteger(chainId.GetString());
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
                    test.GenesisBlockHeader = ParseGenesisHeader(genesis);
                }

                if (testData.TryGetProperty("blocks", out var blocks))
                {
                    test.Blocks = ParseBlocks(blocks);
                }

                tests.Add(test);
            }

            return tests;
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

        private static GenesisBlockHeader ParseGenesisHeader(JsonElement element)
        {
            var header = new GenesisBlockHeader
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

            return header;
        }

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

                if (blockEl.TryGetProperty("blockHeader", out var header))
                {
                    block.BlockHeader = new BlockHeader
                    {
                        Hash = GetBytesOrDefault(header, "hash"),
                        ParentHash = GetBytesOrDefault(header, "parentHash"),
                        StateRoot = GetBytesOrDefault(header, "stateRoot"),
                        TransactionsRoot = GetBytesOrDefault(header, "transactionsTrie"),
                        ReceiptsRoot = GetBytesOrDefault(header, "receiptTrie"),
                        UncleHash = GetBytesOrDefault(header, "uncleHash"),
                        Coinbase = GetBytesOrDefault(header, "coinbase"),
                        LogsBloom = GetBytesOrDefault(header, "bloom"),
                        Difficulty = ParseBigInteger(GetStringOrDefault(header, "difficulty")),
                        Number = ParseBigInteger(GetStringOrDefault(header, "number")),
                        GasLimit = ParseBigInteger(GetStringOrDefault(header, "gasLimit")),
                        GasUsed = ParseBigInteger(GetStringOrDefault(header, "gasUsed")),
                        Timestamp = ParseBigInteger(GetStringOrDefault(header, "timestamp")),
                        ExtraData = GetBytesOrDefault(header, "extraData"),
                        MixHash = GetBytesOrDefault(header, "mixHash"),
                        Nonce = GetBytesOrDefault(header, "nonce")
                    };

                    if (header.TryGetProperty("baseFeePerGas", out var baseFee))
                    {
                        block.BlockHeader.BaseFee = ParseBigInteger(baseFee.GetString());
                    }

                    if (header.TryGetProperty("withdrawalsRoot", out var withdrawals))
                    {
                        block.BlockHeader.WithdrawalsRoot = withdrawals.GetString()?.HexToByteArray();
                    }

                    if (header.TryGetProperty("parentBeaconBlockRoot", out var beaconRoot))
                    {
                        block.BlockHeader.ParentBeaconBlockRoot = beaconRoot.GetString()?.HexToByteArray();
                    }
                }

                if (blockEl.TryGetProperty("transactions", out var txs))
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

        public static IEnumerable<string> GetTestFilesInDirectory(string directory)
        {
            return Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
        }
    }
}
