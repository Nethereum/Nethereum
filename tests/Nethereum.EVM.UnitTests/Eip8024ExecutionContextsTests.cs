using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    /// <summary>
    /// AMS-8024-09 of <c>docs/internal/glamsterdam-traceability-matrix.md</c>:
    /// DUPN/SWAPN/EXCHANGE behave identically whichever kind of frame the
    /// code arrives in. EIP-8024 §Specification states the opcodes without
    /// qualification — <i>"the immediate value is read from the byte
    /// following the opcode"</i> — so a frame kind that never re-decodes is
    /// not a variant of the rule, it is a hole in it.
    ///
    /// <para>
    /// The decoding gate exists at two sites in <c>EVMSimulator</c>: the
    /// INITIAL frame (which is how 7702-delegated code and a creation
    /// transaction's initcode both arrive) and every CALL/CREATE-family
    /// sub-frame. Every other EIP-8024 test in this assembly runs a single
    /// top-level frame, so neither site is distinguished from the other and
    /// the sub-frame one is unasserted.
    /// </para>
    ///
    /// <para>
    /// One probe body is run five ways and every run must produce the same
    /// 64 bytes. The immediate is 0x80 (SWAPN depth 17): if it is left at
    /// its own instruction boundary instead of being absorbed it decodes as
    /// DUP1, and the second word the probe returns changes from 17 to 1 —
    /// the discriminator the whole file rests on.
    /// </para>
    /// </summary>
    public class Eip8024ExecutionContextsTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string ProbeAddress = "0x2222222222222222222222222222222222222222";
        private const string CallerAddress = "0x3333333333333333333333333333333333333333";
        private const string DelegatedEoaAddress = "0x4444444444444444444444444444444444444444";
        private const string FactoryAddress = "0x5555555555555555555555555555555555555555";

        private const long GasLimit = 2_000_000;

        private static byte[] Concat(params byte[][] parts)
        {
            var bytes = new List<byte>();
            foreach (var part in parts) bytes.AddRange(part);
            return bytes.ToArray();
        }

        private static byte[] Push20(string address)
        {
            var code = new List<byte> { 0x73 };
            code.AddRange(address.HexToByteArray());
            return code.ToArray();
        }

        private static byte[] PushSequence(int count)
        {
            var code = new List<byte>();
            for (int v = 1; v <= count; v++)
            {
                code.Add(0x60);
                code.Add((byte)v);
            }
            return code.ToArray();
        }

        private static readonly byte[] StackAccessProbe = Concat(
            PushSequence(18),
            new byte[] { 0xE7, 0x80 },
            new byte[] { 0x60, 0x00, 0x52 },
            new byte[] { 0x60, 0x20, 0x52 },
            new byte[] { 0x60, 0x40, 0x60, 0x00, 0xF3 });

        private static byte[] ExpectedProbeOutput()
        {
            var expected = new byte[64];
            expected[31] = 1;
            expected[63] = 17;
            return expected;
        }

        private static readonly byte[] CallerReturningTheProbeOutput = Concat(
            new byte[] { 0x60, 0x40, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00 },
            Push20(ProbeAddress),
            new byte[] { 0x5A, 0xF1, 0x50 },
            new byte[] { 0x60, 0x40, 0x60, 0x00, 0xF3 });

        private static readonly byte[] FactoryCreatingFromCalldata = new byte[]
        {
            0x36, 0x60, 0x00, 0x60, 0x00, 0x37,
            0x36, 0x60, 0x00, 0x60, 0x00, 0xF0,
            0x60, 0x00, 0x52,
            0x60, 0x20, 0x60, 0x00, 0xF3
        };

        private static byte[] DelegationCode(string target)
        {
            var code = new byte[23];
            code[0] = 0xEF; code[1] = 0x01; code[2] = 0x00;
            target.HexToByteArray().CopyTo(code, 3);
            return code;
        }

        private static async Task<EIP7702TestNodeDataService> NodeAsync()
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(ProbeAddress, StackAccessProbe);
            await node.SetCodeAsync(CallerAddress, CallerReturningTheProbeOutput);
            await node.SetCodeAsync(DelegatedEoaAddress, DelegationCode(ProbeAddress));
            await node.SetCodeAsync(FactoryAddress, FactoryCreatingFromCalldata);
            return node;
        }

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            string to, byte[] data, bool isContractCreation)
        {
            var node = await NodeAsync();
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = data,
                IsContractCreation = isContractCreation,
                GasLimit = GasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };

            var executor = new TransactionExecutor(
                HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase()));
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        [Fact]
        public async Task Given_TheStackAccessProbe_When_ItIsTheTopLevelFramesOwnCode_Then_ItReturnsTheDecodedResult()
        {
            var (_, result) = await RunAsync(ProbeAddress, data: null, isContractCreation: false);

            Assert.True(result.Success, result.Error);
            Assert.Equal(ExpectedProbeOutput(), result.ReturnData);
        }

        [Fact]
        public async Task Given_TheStackAccessProbe_When_ItRunsInAPlainCallSubFrame_Then_ItReturnsTheDecodedResult()
        {
            var (_, result) = await RunAsync(CallerAddress, data: null, isContractCreation: false);

            Assert.True(result.Success, result.Error);
            Assert.Equal(ExpectedProbeOutput(), result.ReturnData);
        }

        [Fact]
        public async Task Given_TheStackAccessProbe_When_ItRunsAsARecipientsDelegatedCode_Then_ItReturnsTheDecodedResult()
        {
            var (ctx, result) = await RunAsync(DelegatedEoaAddress, data: null, isContractCreation: false);

            Assert.Equal(ProbeAddress, ctx.DelegateAddress);
            Assert.True(result.Success, result.Error);
            Assert.Equal(ExpectedProbeOutput(), result.ReturnData);
        }

        [Fact]
        public async Task Given_TheStackAccessProbe_When_ItIsACreationTransactionsInitCode_Then_TheDeployedCodeIsTheDecodedResult()
        {
            var (ctx, result) = await RunAsync(to: null, data: StackAccessProbe, isContractCreation: true);

            Assert.True(result.Success, result.Error);
            var deployed = await ctx.ExecutionState.GetCodeAsync(result.ContractAddress);
            Assert.Equal(ExpectedProbeOutput(), deployed);
        }

        [Fact]
        public async Task Given_TheStackAccessProbe_When_ItIsInitCodeRunByCreate_Then_TheDeployedCodeIsTheDecodedResult()
        {
            var (ctx, result) = await RunAsync(FactoryAddress, data: StackAccessProbe, isContractCreation: false);

            Assert.True(result.Success, result.Error);
            var createdAddress = "0x" + result.ReturnData.ToHex().Substring(24);
            var deployed = await ctx.ExecutionState.GetCodeAsync(createdAddress);
            Assert.Equal(ExpectedProbeOutput(), deployed);
        }
    }
}
