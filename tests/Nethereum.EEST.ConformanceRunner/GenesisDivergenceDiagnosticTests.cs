using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EEST.ConformanceRunner
{
    public class GenesisDivergenceDiagnosticTests
    {
        private readonly ITestOutputHelper _output;
        public GenesisDivergenceDiagnosticTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task DiagnoseFirstAmsterdamGenesisDivergence()
        {
            Assert.True(FixtureCatalog.CacheIsPresent);

            BlockchainTestLoader.BlockchainTest? found = null;
            string? foundFile = null;

            foreach (var file in FixtureCatalog.AllFixtureFiles)
            {
                List<BlockchainTestLoader.BlockchainTest> tests;
                try { tests = BlockchainTestLoader.LoadFromFile(file); }
                catch { continue; }

                foreach (var candidate in tests.Where(t => t.Network == "Amsterdam"))
                {
                    var doc = EestGenesisBuilder.BuildGenesisDocument(candidate);
                    await using var probe = await FullNodeHarness.ComposeAsync(
                        doc, Nethereum.EVM.HardforkName.Amsterdam, EestBlockchainTestsRlpDriver.FixtureRegistry);
                    var probeHeader = await probe.Node.Blocks.GetByNumberAsync(0);
                    var probeHash = Nethereum.CoreChain.BlockHashCalculator.ForFork(probeHeader, Nethereum.EVM.HardforkName.Amsterdam);
                    if (!probeHash.SequenceEqual(candidate.GenesisBlockHeader.Hash))
                    {
                        found = candidate;
                        foundFile = file;
                        break;
                    }
                }
                if (found != null) break;
            }

            if (found == null)
            {
                _output.WriteLine("No Amsterdam genesis divergence remains: every fixture's genesisBlockHeader matches the real node's BlockManager.CreateGenesisBlockAsync.");
                return;
            }

            _output.WriteLine($"Using MISMATCHING fixture: {foundFile} :: {found!.Name}");

            var document = EestGenesisBuilder.BuildGenesisDocument(found);
            await using var harness = await FullNodeHarness.ComposeAsync(
                document, Nethereum.EVM.HardforkName.Amsterdam, EestBlockchainTestsRlpDriver.FixtureRegistry);

            var actual = await harness.Node.Blocks.GetByNumberAsync(0);
            var expected = found.GenesisBlockHeader;

            _output.WriteLine("=== Genesis header field diff (expected fixture -> actual real-node) ===");
            void Cmp(string name, string exp, string act)
            {
                var same = string.Equals(exp, act, System.StringComparison.OrdinalIgnoreCase);
                _output.WriteLine($"  {(same ? "OK  " : "DIFF")} {name}: expected={exp} actual={act}");
            }

            Cmp("parentHash", "0x" + expected.ParentHash.ToHex(), "0x" + actual.ParentHash.ToHex());
            Cmp("unclesHash", "0x" + expected.UncleHash.ToHex(), "0x" + actual.UnclesHash.ToHex());
            Cmp("coinbase", "0x" + expected.Coinbase.ToHex(), actual.Coinbase);
            Cmp("stateRoot", "0x" + expected.StateRoot.ToHex(), "0x" + actual.StateRoot.ToHex());
            Cmp("transactionsRoot", "0x" + expected.TransactionsRoot.ToHex(), "0x" + actual.TransactionsHash.ToHex());
            Cmp("receiptsRoot", "0x" + expected.ReceiptsRoot.ToHex(), "0x" + actual.ReceiptHash.ToHex());
            Cmp("logsBloom", "0x" + expected.LogsBloom.ToHex(), "0x" + actual.LogsBloom.ToHex());
            Cmp("difficulty", expected.Difficulty.ToString(), actual.Difficulty.ToString());
            Cmp("number", expected.Number.ToString(), actual.BlockNumber.ToString());
            Cmp("gasLimit", expected.GasLimit.ToString(), actual.GasLimit.ToString());
            Cmp("gasUsed", expected.GasUsed.ToString(), actual.GasUsed.ToString());
            Cmp("timestamp", expected.Timestamp.ToString(), actual.Timestamp.ToString());
            Cmp("extraData", "0x" + expected.ExtraData.ToHex(), "0x" + actual.ExtraData.ToHex());
            Cmp("mixHash", "0x" + expected.MixHash.ToHex(), "0x" + actual.MixHash.ToHex());
            Cmp("nonce", "0x" + expected.Nonce.ToHex(), "0x" + actual.Nonce.ToHex());
            Cmp("baseFeePerGas", expected.BaseFee?.ToString() ?? "null", actual.BaseFee?.ToString() ?? "null");
            Cmp("withdrawalsRoot", expected.WithdrawalsRoot != null ? "0x" + expected.WithdrawalsRoot.ToHex() : "null",
                actual.WithdrawalsRoot != null ? "0x" + actual.WithdrawalsRoot.ToHex() : "null");
            Cmp("blobGasUsed", expected.BlobGasUsed?.ToString() ?? "null", actual.BlobGasUsed?.ToString() ?? "null");
            Cmp("excessBlobGas", expected.ExcessBlobGas?.ToString() ?? "null", actual.ExcessBlobGas?.ToString() ?? "null");
            Cmp("parentBeaconBlockRoot", expected.ParentBeaconBlockRoot != null ? "0x" + expected.ParentBeaconBlockRoot.ToHex() : "null",
                actual.ParentBeaconBlockRoot != null ? "0x" + actual.ParentBeaconBlockRoot.ToHex() : "null");
            Cmp("requestsHash", expected.RequestsHash != null ? "0x" + expected.RequestsHash.ToHex() : "null",
                actual.RequestsHash != null ? "0x" + actual.RequestsHash.ToHex() : "null");
            Cmp("blockAccessListHash", expected.BlockAccessListHash != null ? "0x" + expected.BlockAccessListHash.ToHex() : "null",
                actual.BlockAccessListHash != null ? "0x" + actual.BlockAccessListHash.ToHex() : "null");
            Cmp("slotNumber", expected.SlotNumber?.ToString() ?? "null", actual.SlotNumber?.ToString() ?? "null");
        }
    }
}
