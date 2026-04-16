using System.Numerics;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.DevChain;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    /// <summary>
    /// Parity tests for the two <see cref="IInternalTransactionSource"/> implementations
    /// against an in-process DevChain. DevChain implements <c>debug_traceTransaction</c>
    /// via the same <c>TransactionExecutor</c> the local replay uses, so both sources
    /// must produce identical field-by-field output for the same tx hash. Divergence =
    /// formatting / semantic bug in one source.
    /// </summary>
    public class InternalTransactionSourceParityTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly ITestOutputHelper _output;

        public InternalTransactionSourceParityTests(DevChainNodeFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Erc20Transfer_BothSourcesMatch()
        {
            var erc20Address = await _fixture.DeployERC20Async(
                initialMintAmount: BigInteger.Parse("1000000000000000000000"));

            var transferFunction = new TransferFunction
            {
                To = _fixture.RecipientAddress,
                Value = BigInteger.Parse("1000000000000000000")
            };
            var callData = transferFunction.GetCallData();
            var signedTx = _fixture.CreateSignedTransaction(erc20Address, BigInteger.Zero, callData);
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, $"Transfer failed: {result.RevertReason}");

            var txHash = signedTx.Hash.ToHex(prefix: true);

            var web3 = _fixture.Node.CreateWeb3();
            var hardforkConfig = DefaultMainnetHardforkRegistry.Instance.Get(
                Nethereum.EVM.HardforkNames.Parse(_fixture.Node.Config.Hardfork));

            var debugSource = new DebugTraceInternalTransactionSource(web3.Client);
            var replaySource = new EvmReplayInternalTransactionSource(web3.Eth, hardforkConfig);

            var fromDebug = await debugSource.ProduceAsync(txHash);
            var fromReplay = await replaySource.ProduceAsync(txHash);

            _output.WriteLine($"debug_trace produced {fromDebug.Count} entries");
            _output.WriteLine($"replay      produced {fromReplay.Count} entries");
            for (int i = 0; i < System.Math.Max(fromDebug.Count, fromReplay.Count); i++)
            {
                var d = i < fromDebug.Count ? fromDebug[i] : null;
                var r = i < fromReplay.Count ? fromReplay[i] : null;
                _output.WriteLine($"[{i}] debug:  {Format(d)}");
                _output.WriteLine($"[{i}] replay: {Format(r)}");
            }

            Assert.Equal(fromDebug.Count, fromReplay.Count);
            for (int i = 0; i < fromDebug.Count; i++)
            {
                var d = fromDebug[i];
                var r = fromReplay[i];
                Assert.Equal(d.Type, r.Type);
                Assert.Equal(d.Depth, r.Depth);
                Assert.Equal(d.AddressFrom?.ToLowerInvariant(), r.AddressFrom?.ToLowerInvariant());
                Assert.Equal(d.AddressTo?.ToLowerInvariant(), r.AddressTo?.ToLowerInvariant());
                Assert.Equal(Normalise(d.Value), Normalise(r.Value));
                Assert.Equal(Normalise(d.GasUsed), Normalise(r.GasUsed));
                Assert.Equal(d.Input, r.Input);
                Assert.Equal(d.Output, r.Output);
                Assert.Equal(d.Error, r.Error);
                Assert.Equal(d.RevertReason, r.RevertReason);
            }
        }

        private static string Normalise(string value)
        {
            if (string.IsNullOrEmpty(value)) return "0";
            if (value.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return value.HexToBigInteger(isHexLittleEndian: false).ToString();
            return BigInteger.Parse(value).ToString();
        }

        private static string Format(InternalTransaction t)
        {
            if (t == null) return "<null>";
            return $"type={t.Type} depth={t.Depth} from={t.AddressFrom} to={t.AddressTo} val={t.Value} gasUsed={t.GasUsed} err={t.Error}";
        }
    }
}
