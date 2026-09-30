using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Newtonsoft.Json;

namespace WitnessConverter
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: WitnessConverter <json-test-file|dir> <output-dir>");
                return 1;
            }

            var input = args[0];
            var outputDir = args[1];
            Directory.CreateDirectory(outputDir);

            if (Directory.Exists(input))
            {
                var files = Directory.GetFiles(input, "*.json");
                int total = 0;
                foreach (var file in files)
                    total += ConvertFile(file, outputDir);
                Console.WriteLine($"Converted {total} test vectors from {files.Length} files");
            }
            else
            {
                var count = ConvertFile(input, outputDir);
                Console.WriteLine($"Converted {count} test vectors");
            }
            return 0;
        }

        static int ConvertFile(string jsonFile, string outputDir)
        {
            var json = File.ReadAllText(jsonFile);
            var scenarios = JsonConvert.DeserializeObject<Dictionary<string, TestScenario>>(json);
            int count = 0;

            foreach (var kvp in scenarios)
            {
                var name = kvp.Key;
                var scenario = kvp.Value;
                if (scenario.Tests?.Berlin == null) continue;

                foreach (var test in scenario.Tests.Berlin)
                {
                    if (test.Transaction == null) continue;

                    var witness = new BlockWitnessData();

                    if (scenario.Env != null)
                    {
                        witness.BlockNumber = (long)HexToBigInt(scenario.Env.CurrentNumber);
                        witness.Timestamp = (long)HexToBigInt(scenario.Env.CurrentTimestamp);
                        witness.BaseFee = (long)HexToBigInt(scenario.Env.CurrentBaseFee);
                        witness.BlockGasLimit = (long)HexToBigInt(scenario.Env.CurrentGasLimit);
                        witness.Difficulty = ToBytes(HexToBigInt(scenario.Env.CurrentDifficulty));
                        witness.Coinbase = scenario.Env.CurrentCoinbase;
                    }
                    witness.ChainId = 1;
                    // These retesteth-style vectors are Berlin state tests.
                    witness.Features = new BlockFeatureConfig { Fork = HardforkName.Berlin };

                    // The block-witness format carries each transaction RLP-encoded with a
                    // pre-recovered sender (BlockWitnessTransaction.From), so the zkVM executor
                    // reads the sender directly instead of recovering it. These state-test
                    // vectors are single legacy transactions.
                    var legacyTx = new LegacyTransaction(
                        nonce: ToBytes(HexToBigInt(test.Transaction.Nonce)),
                        gasPrice: ToBytes(HexToBigInt(test.Transaction.GasPrice)),
                        gasLimit: ToBytes(HexToBigInt(test.Transaction.GasLimit)),
                        receiveAddress: string.IsNullOrEmpty(test.Transaction.To)
                            ? Array.Empty<byte>()
                            : test.Transaction.To.HexToByteArray(),
                        value: ToBytes(HexToBigInt(test.Transaction.Value)),
                        data: !string.IsNullOrEmpty(test.Transaction.Input)
                            ? test.Transaction.Input.HexToByteArray()
                            : Array.Empty<byte>());
                    witness.Transactions.Add(new BlockWitnessTransaction
                    {
                        From = test.Transaction.Sender,
                        RlpEncoded = legacyTx.GetRLPEncoded()
                    });

                    if (scenario.PreAccountsStorage != null)
                    {
                        foreach (var accKvp in scenario.PreAccountsStorage)
                        {
                            var acc = new WitnessAccount
                            {
                                Address = accKvp.Key,
                                Balance = HexToBigInt(accKvp.Value.Balance),
                                Nonce = (ulong)HexToBigInt(accKvp.Value.Nonce),
                                Code = !string.IsNullOrEmpty(accKvp.Value.Code)
                                    ? accKvp.Value.Code.HexToByteArray()
                                    : Array.Empty<byte>()
                            };
                            if (accKvp.Value.Storage != null)
                            {
                                foreach (var storKvp in accKvp.Value.Storage)
                                {
                                    acc.Storage.Add(new WitnessStorageSlot
                                    {
                                        Key = HexToBigInt(storKvp.Key),
                                        Value = HexToBigInt(storKvp.Value)
                                    });
                                }
                            }
                            witness.Accounts.Add(acc);
                        }
                    }

                    var bytes = BinaryBlockWitness.Serialize(witness);

                    var id = test.Id ?? count.ToString();
                    var safeName = name.Replace("/", "_").Replace("\\", "_");
                    var outputFile = Path.Combine(outputDir, $"{safeName}_{id}.bin");

                    // Zisk legacy input format at 0x40000000:
                    // ReadInputLegacy reads: size = *(u64*)(Input+8), data = Input+16
                    // So file = [u64 padding/ignored][u64 dataLen][data padded to 8]
                    using var ms = new MemoryStream();
                    using var bw = new BinaryWriter(ms);

                    var paddedLen = ((bytes.Length + 7) / 8) * 8;

                    bw.Write((ulong)0);                  // offset 0: ignored by ReadInputLegacy
                    bw.Write((ulong)bytes.Length);        // offset 8: data length
                    bw.Write(bytes);                     // offset 16: data
                    if (paddedLen > bytes.Length)
                        bw.Write(new byte[paddedLen - bytes.Length]);

                    File.WriteAllBytes(outputFile, ms.ToArray());
                    count++;
                }
            }
            return count;
        }

        static byte[] ToBytes(BigInteger value) =>
            value.IsZero ? Array.Empty<byte>() : value.ToByteArray(isUnsigned: true, isBigEndian: true);

        static BigInteger HexToBigInt(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
            if (hex.StartsWith("0x") || hex.StartsWith("0X"))
                hex = hex.Substring(2);
            if (hex.Length == 0) return BigInteger.Zero;
            if (hex.Length % 2 != 0) hex = "0" + hex;
            return new BigInteger(hex.HexToByteArray(), isUnsigned: true, isBigEndian: true);
        }
    }

    public class TestScenario
    {
        [JsonProperty("pre")] public Dictionary<string, AccountStorage> PreAccountsStorage { get; set; }
        [JsonProperty("tests")] public TestSets Tests { get; set; }
        [JsonProperty("env")] public TestEnv Env { get; set; }
    }
    public class TestSets { [JsonProperty("Berlin")] public List<BerlinTest> Berlin { get; set; } }
    public class BerlinTest
    {
        [JsonProperty("expect")] public string Expect { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("transaction")] public TestTransaction Transaction { get; set; }
    }
    public class TestTransaction
    {
        [JsonProperty("gasLimit")] public string GasLimit { get; set; }
        [JsonProperty("input")] public string Input { get; set; }
        [JsonProperty("sender")] public string Sender { get; set; }
        [JsonProperty("to")] public string To { get; set; }
        [JsonProperty("nonce")] public string Nonce { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
        [JsonProperty("gasPrice")] public string GasPrice { get; set; }
    }
    public class TestEnv
    {
        [JsonProperty("currentBaseFee")] public string CurrentBaseFee { get; set; }
        [JsonProperty("currentTimestamp")] public string CurrentTimestamp { get; set; }
        [JsonProperty("currentCoinbase")] public string CurrentCoinbase { get; set; }
        [JsonProperty("currentNumber")] public string CurrentNumber { get; set; }
        [JsonProperty("currentDifficulty")] public string CurrentDifficulty { get; set; }
        [JsonProperty("currentGasLimit")] public string CurrentGasLimit { get; set; }
    }
    public class AccountStorage
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("balance")] public string Balance { get; set; }
        [JsonProperty("storage")] public Dictionary<string, string> Storage { get; set; }
        [JsonProperty("nonce")] public string Nonce { get; set; }
    }
}
