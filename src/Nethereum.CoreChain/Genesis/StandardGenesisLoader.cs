using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.Genesis
{
    public static class StandardGenesisLoader
    {
        public static StandardGenesisDocument LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A genesis.json path is required", nameof(path));

            return Parse(File.ReadAllText(path));
        }

        public static StandardGenesisDocument Parse(string json)
        {
            var root = JObject.Parse(json);
            return Parse(root);
        }

        public static StandardGenesisDocument Parse(JObject root)
        {
            var config = root["config"] as JObject ?? new JObject();

            return new StandardGenesisDocument
            {
                Config = new StandardGenesisForkConfig
                {
                    ChainId = ParseDecimalOrNull(config["chainId"]),
                    HomesteadBlock = ParseLongOrNull(config["homesteadBlock"]),
                    DaoForkBlock = ParseLongOrNull(config["daoForkBlock"]),
                    Eip150Block = ParseLongOrNull(config["eip150Block"]),
                    Eip155Block = ParseLongOrNull(config["eip155Block"]),
                    Eip158Block = ParseLongOrNull(config["eip158Block"]),
                    ByzantiumBlock = ParseLongOrNull(config["byzantiumBlock"]),
                    ConstantinopleBlock = ParseLongOrNull(config["constantinopleBlock"]),
                    PetersburgBlock = ParseLongOrNull(config["petersburgBlock"]),
                    IstanbulBlock = ParseLongOrNull(config["istanbulBlock"]),
                    MuirGlacierBlock = ParseLongOrNull(config["muirGlacierBlock"]),
                    BerlinBlock = ParseLongOrNull(config["berlinBlock"]),
                    LondonBlock = ParseLongOrNull(config["londonBlock"]),
                    ArrowGlacierBlock = ParseLongOrNull(config["arrowGlacierBlock"]),
                    GrayGlacierBlock = ParseLongOrNull(config["grayGlacierBlock"]),
                    MergeNetsplitBlock = ParseLongOrNull(config["mergeNetsplitBlock"]),
                    ShanghaiTime = ParseUlongOrNull(config["shanghaiTime"]),
                    CancunTime = ParseUlongOrNull(config["cancunTime"]),
                    PragueTime = ParseUlongOrNull(config["pragueTime"]),
                    OsakaTime = ParseUlongOrNull(config["osakaTime"]),
                    AmsterdamTime = ParseUlongOrNull(config["amsterdamTime"]),
                    TerminalTotalDifficulty = ParseDecimalOrNull(config["terminalTotalDifficulty"]),
                    BlobSchedule = config["blobSchedule"] as JObject,
                    DepositContractAddress = config["depositContractAddress"]?.ToString(),
                },
                Alloc = root["alloc"] as JObject ?? new JObject(),
                GasLimit = ParseHexOrDecimal(root["gasLimit"]?.ToString(), 0x1C9C380),
                BaseFeePerGas = root["baseFeePerGas"] != null ? ParseHexOrDecimal(root["baseFeePerGas"].ToString(), 0) : (BigInteger?)null,
                Timestamp = (long)ParseHexOrDecimal(root["timestamp"]?.ToString(), 0),
                ExtraData = ParseHexBytes(root["extraData"]?.ToString()),
                MixHash = ParseHexBytes(root["mixHash"]?.ToString(), 32),
                Nonce = ParseHexBytes(root["nonce"]?.ToString(), 8),
                Difficulty = ParseHexOrDecimal(root["difficulty"]?.ToString(), 0),
                Coinbase = NormalizeAddressOrDefault(root["coinbase"]?.ToString()),
                ExcessBlobGas = ParseUlongHexOrNull(root["excessBlobGas"]),
                BlobGasUsed = ParseUlongHexOrNull(root["blobGasUsed"]),
                SlotNumber = ParseUlongHexOrNull(root["slotNumber"]),
            };
        }

        public static ChainForkSchedule BuildForkSchedule(StandardGenesisDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var config = document.Config;
            var schedule = new ChainForkSchedule
            {
                ChainId = (long)(config.ChainId ?? 0),
                GenesisFork = HardforkName.Frontier.ToString(),
            };

            AddBlockActivation(schedule, "Homestead", config.HomesteadBlock);
            AddBlockActivation(schedule, "DaoFork", config.DaoForkBlock);
            AddBlockActivation(schedule, "EIP150", config.Eip150Block);
            AddBlockActivation(schedule, "EIP155", config.Eip155Block);
            AddBlockActivation(schedule, "EIP158", config.Eip158Block);
            AddBlockActivation(schedule, "Byzantium", config.ByzantiumBlock);
            AddBlockActivation(schedule, "Constantinople", config.ConstantinopleBlock);
            AddBlockActivation(schedule, "Petersburg", config.PetersburgBlock);
            AddBlockActivation(schedule, "Istanbul", config.IstanbulBlock);
            AddBlockActivation(schedule, "MuirGlacier", config.MuirGlacierBlock);
            AddBlockActivation(schedule, "Berlin", config.BerlinBlock);
            AddBlockActivation(schedule, "London", config.LondonBlock);
            AddBlockActivation(schedule, "ArrowGlacier", config.ArrowGlacierBlock);
            AddBlockActivation(schedule, "GrayGlacier", config.GrayGlacierBlock);

            if (config.MergeNetsplitBlock.HasValue)
                AddBlockActivation(schedule, "Paris", config.MergeNetsplitBlock);
            else if (config.TerminalTotalDifficulty.HasValue
                     && document.Difficulty >= config.TerminalTotalDifficulty.Value)
                AddBlockActivation(schedule, "Paris", 0);

            AddTimestampActivation(schedule, "Shanghai", config.ShanghaiTime);
            AddTimestampActivation(schedule, "Cancun", config.CancunTime);
            AddTimestampActivation(schedule, "Prague", config.PragueTime);
            AddTimestampActivation(schedule, "Osaka", config.OsakaTime);
            AddTimestampActivation(schedule, "Amsterdam", config.AmsterdamTime);

            return schedule;
        }

        public static void ApplyToChainConfig(ChainConfig target, StandardGenesisDocument document)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (document == null) throw new ArgumentNullException(nameof(document));

            target.ChainId = document.Config.ChainId ?? target.ChainId;
            target.Coinbase = document.Coinbase;
            target.BlockGasLimit = document.GasLimit;
            target.BaseFee = document.BaseFeePerGas ?? target.BaseFee;
            target.GenesisTimestamp = document.Timestamp;
            target.GenesisDifficulty = document.Difficulty;
            target.GenesisNonce = document.Nonce;
            target.GenesisMixHash = document.MixHash;
            target.GenesisExtraData = document.ExtraData;
            target.GenesisExcessBlobGas = document.ExcessBlobGas.HasValue
                ? unchecked((long)document.ExcessBlobGas.Value) : (long?)null;
            target.GenesisBlobGasUsed = document.BlobGasUsed.HasValue
                ? unchecked((long)document.BlobGasUsed.Value) : (long?)null;
            target.GenesisSlotNumber = document.SlotNumber.HasValue
                ? unchecked((long)document.SlotNumber.Value) : (long?)null;
            target.DepositContractAddress = document.Config.DepositContractAddress ?? target.DepositContractAddress;
            target.ForkSchedule = BuildForkSchedule(document);
        }

        public static async Task<int> PopulateAllocAsync(IStateStore stateStore, JObject alloc)
        {
            if (stateStore == null) throw new ArgumentNullException(nameof(stateStore));
            if (alloc == null) return 0;

            var n = 0;
            foreach (var prop in alloc.Properties())
            {
                var addr = prop.Name.StartsWith("0x") ? prop.Name : "0x" + prop.Name;
                var entry = (JObject)prop.Value;

                var balance = ParseHexOrDecimal(entry["balance"]?.ToString(), 0);
                ulong nonce = 0;
                if (entry["nonce"] != null)
                {
                    var nVal = new HexBigInteger(NormalizeQuantity(entry["nonce"].ToString())).Value;
                    nonce = nVal.IsZero ? 0UL : (ulong)nVal;
                }

                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (entry["code"] != null)
                {
                    var code = entry["code"].ToString().HexToByteArray();
                    var keccak = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();
                    codeHash = keccak.ComputeHash(code);
                    await stateStore.SaveCodeAsync(codeHash, code);
                }

                await stateStore.SaveAccountAsync(addr, new Account
                {
                    Nonce = (EvmUInt256)nonce,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = codeHash
                });

                if (entry["storage"] is JObject storage)
                {
                    foreach (var slot in storage.Properties())
                    {
                        var slotKey = new BigInteger(slot.Name.HexToByteArray(), isUnsigned: true, isBigEndian: true);
                        var slotValue = slot.Value.ToString().HexToByteArray();
                        await stateStore.SaveStorageAsync(addr, slotKey, slotValue);
                    }
                }

                n++;
            }

            return n;
        }

        private static void AddBlockActivation(ChainForkSchedule schedule, string fork, long? block)
        {
            if (!block.HasValue) return;
            schedule.Schedule.Add(new ForkActivationEntry { Fork = fork, Block = block.Value });
        }

        private static void AddTimestampActivation(ChainForkSchedule schedule, string fork, ulong? timestamp)
        {
            if (!timestamp.HasValue) return;
            schedule.Schedule.Add(new ForkActivationEntry { Fork = fork, Timestamp = timestamp.Value });
        }

        private static long? ParseLongOrNull(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return token.Value<long>();
        }

        private static ulong? ParseUlongOrNull(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return token.Value<ulong>();
        }

        private static ulong? ParseUlongHexOrNull(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return unchecked((ulong)ParseHexOrDecimal(token.ToString(), 0));
        }

        private static BigInteger? ParseDecimalOrNull(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            var text = token.ToString();
            return ParseHexOrDecimal(text, 0);
        }

        private static BigInteger ParseHexOrDecimal(string value, long fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            return (value.StartsWith("0x") || value.StartsWith("0X"))
                ? new HexBigInteger(NormalizeQuantity(value)).Value
                : BigInteger.Parse(value);
        }

        private static string NormalizeQuantity(string value) =>
            value == "0x" ? "0x0" : value;

        private static byte[] ParseHexBytes(string value, int? padToLength = null)
        {
            if (string.IsNullOrEmpty(value) || value == "0x")
                return padToLength.HasValue ? new byte[padToLength.Value] : Array.Empty<byte>();

            var bytes = value.HexToByteArray();
            if (padToLength.HasValue && bytes.Length < padToLength.Value)
            {
                var padded = new byte[padToLength.Value];
                Array.Copy(bytes, 0, padded, padToLength.Value - bytes.Length, bytes.Length);
                return padded;
            }
            return bytes;
        }

        private static string NormalizeAddressOrDefault(string address) =>
            string.IsNullOrEmpty(address) ? AddressUtil.ZERO_ADDRESS : address;
    }
}
