using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BasicTests
{
    public class BasicTestRunner
    {
        private readonly ITestOutputHelper _output;

        private static readonly string TestPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "BasicTests");

        public BasicTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "BasicTests")]
        public void Crypto_Keccak256HashesMatch()
        {
            var filePath = Path.Combine(TestPath, "crypto.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine("crypto.json not found");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var keccak = new Sha3Keccack();
            var passed = 0;

            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testData = testProp.Value;
                if (!testData.TryGetProperty("hash", out var hashProp) ||
                    !testData.TryGetProperty("in", out var inProp))
                    continue;

                var expectedHash = hashProp.GetString() ?? "";
                var input = inProp.GetString() ?? "";
                var inputBytes = input.HexToByteArray();
                var actualHash = keccak.CalculateHash(inputBytes).ToHex(true);

                Assert.True(
                    expectedHash.IsTheSameHex(actualHash),
                    $"Keccak mismatch for '{testProp.Name}'. Expected: {expectedHash}, Actual: {actualHash}");
                passed++;
            }

            _output.WriteLine($"Crypto: {passed} keccak tests passed");
        }

        [Fact]
        [Trait("Category", "BasicTests")]
        public void KeyAddress_PrivateKeyToAddressDerivation()
        {
            var filePath = Path.Combine(TestPath, "keyaddrtest.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine("keyaddrtest.json not found");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var passed = 0;

            foreach (var testEl in doc.RootElement.EnumerateArray())
            {
                if (!testEl.TryGetProperty("addr", out var addrProp) ||
                    !testEl.TryGetProperty("key", out var keyProp))
                    continue;

                var expectedAddr = addrProp.GetString() ?? "";
                var privateKey = keyProp.GetString() ?? "";
                var seed = "";
                if (testEl.TryGetProperty("seed", out var seedProp))
                    seed = seedProp.GetString() ?? "";

                var key = new EthECKey(privateKey);
                var actualAddr = key.GetPublicAddress();

                Assert.True(
                    expectedAddr.IsTheSameHex(actualAddr),
                    $"Address mismatch for seed='{seed}'. Expected: {expectedAddr}, Actual: {actualAddr}");
                passed++;
            }

            _output.WriteLine($"KeyAddress: {passed} key→address tests passed");
        }

        [Fact]
        [Trait("Category", "BasicTests")]
        public void TxTest_TransactionSigningAndEncoding()
        {
            var filePath = Path.Combine(TestPath, "txtest.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine("txtest.json not found");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var root = doc.RootElement;
            var passed = 0;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var txEl in root.EnumerateArray())
                {
                    ValidateTxTest(txEl, ref passed);
                }
            }
            else
            {
                foreach (var prop in root.EnumerateObject())
                {
                    ValidateTxTest(prop.Value, ref passed);
                }
            }

            _output.WriteLine($"TxTest: {passed} transaction signing tests passed");
        }

        private void ValidateTxTest(JsonElement txEl, ref int passed)
        {
            if (!txEl.TryGetProperty("signed", out var signedProp) ||
                !txEl.TryGetProperty("key", out var keyProp))
                return;

            var signedHex = signedProp.GetString() ?? "";
            var privateKey = keyProp.GetString() ?? "";

            var signedBytes = signedHex.HexToByteArray();
            var signedTx = TransactionFactory.CreateTransaction(signedBytes);
            Assert.NotNull(signedTx);

            var senderAddr = TransactionVerificationAndRecovery.GetSenderAddress(signedTx);
            Assert.False(string.IsNullOrEmpty(senderAddr), "Failed to recover sender from signed tx");

            var key = new EthECKey(privateKey);
            var expectedAddr = key.GetPublicAddress();
            Assert.True(
                expectedAddr.IsTheSameHex(senderAddr),
                $"Sender mismatch. Expected: {expectedAddr}, Actual: {senderAddr}");

            if (txEl.TryGetProperty("unsigned", out var unsignedProp))
            {
                var unsignedHex = unsignedProp.GetString() ?? "";
                _output.WriteLine($"  TX: unsigned={unsignedHex.Substring(0, Math.Min(40, unsignedHex.Length))}... sender={senderAddr}");
            }

            passed++;
        }

        [Fact]
        [Trait("Category", "BasicTests")]
        public void GenesisHashes_MatchExpected()
        {
            var filePath = Path.Combine(TestPath, "genesishashestest.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine("genesishashestest.json not found");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var root = doc.RootElement;
            var keccak = new Sha3Keccack();

            if (root.TryGetProperty("genesis_rlp_hex", out var rlpProp))
            {
                var genesisRlp = rlpProp.GetString() ?? "";
                var genesisBytes = genesisRlp.HexToByteArray();

                var decoded = RLP.RLP.Decode(genesisBytes);
                var blockRlp = decoded as RLPCollection;
                Assert.NotNull(blockRlp);
                Assert.True(blockRlp.Count >= 1, "Genesis block RLP should have at least a header");

                var headerItem = blockRlp[0];
                byte[] headerRlpBytes;
                if (headerItem is RLPCollection headerCollection)
                {
                    headerRlpBytes = RLP.RLP.EncodeList(
                        headerCollection.SelectMany(x => RLP.RLP.EncodeElement(x.RLPData)).ToArray());
                }
                else
                {
                    headerRlpBytes = RLP.RLP.EncodeElement(headerItem.RLPData);
                }

                var hash = keccak.CalculateHash(headerRlpBytes);

                if (root.TryGetProperty("genesis_hash", out var hashProp))
                {
                    var expectedHash = hashProp.GetString() ?? "";
                    Assert.True(
                        expectedHash.IsTheSameHex(hash.ToHex(true)),
                        $"Genesis hash mismatch. Expected: {expectedHash}, Actual: {hash.ToHex(true)}");
                    _output.WriteLine($"Genesis hash matches: {expectedHash}");
                }
            }
            else
            {
                _output.WriteLine("No genesis_rlp_hex field found");
            }
        }
    }
}
