using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Precompiles.Backends;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.Bls.Herumi;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution.Precompiles
{
    public class PrecompileOutcomeTests
    {
        private const int EcRecover = 0x01;
        private const int Bn254Add = 0x06;
        private const int Bn254Pairing = 0x08;
        private const int Blake2f = 0x09;
        private const int KzgPointEvaluation = 0x0a;
        private const int BlsG1Add = 0x0b;
        private const int BlsG1Msm = 0x0c;
        private const int BlsPairing = 0x0f;
        private const int BlsMapFpToG1 = 0x10;
        private const int BlsMapFp2ToG2 = 0x11;
        private const int P256Verify = 0x100;

        private static readonly PrecompileRegistry Registry =
            DefaultHardforkConfigs.Osaka
                .WithBlsBackend(new Bls12381Operations())
                .WithKzgBackend()
                .Precompiles;

        private static List<Exception> ExceptionsRaisedOnThisThreadDuring(Action action)
        {
            var raised = new List<Exception>();
            var thread = Environment.CurrentManagedThreadId;
            EventHandler<FirstChanceExceptionEventArgs> observe = (_, e) =>
            {
                if (Environment.CurrentManagedThreadId == thread) raised.Add(e.Exception);
            };
            AppDomain.CurrentDomain.FirstChanceException += observe;
            try { action(); }
            finally { AppDomain.CurrentDomain.FirstChanceException -= observe; }
            return raised;
        }

        private static void AssertFailsWithoutThrowing(int address, byte[] input)
        {
            byte[] output = null;
            var succeeded = true;
            var raised = ExceptionsRaisedOnThisThreadDuring(() => succeeded = Registry.TryExecute(address, input, out output));
            Assert.False(succeeded);
            Assert.Null(output);
            Assert.Empty(raised);
        }

        private static byte[] FpElement(int size, byte value)
        {
            var element = new byte[size];
            element[size - 1] = value;
            return element;
        }

        public static IEnumerable<object[]> InvalidInputs()
        {
            yield return new object[] { "BLS G1ADD wrong length", BlsG1Add, new byte[255] };
            yield return new object[] { "BLS G1MSM length not a multiple of 160", BlsG1Msm, new byte[161] };
            yield return new object[] { "BLS G1MSM point not on the curve", BlsG1Msm, Concat(FpElement(64, 1), FpElement(64, 1), new byte[32]) };
            yield return new object[] { "BLS PAIRING wrong length", BlsPairing, new byte[383] };
            yield return new object[] { "BLS MAP_FP_TO_G1 coordinate with non-zero padding", BlsMapFpToG1, FilledWith(64, 0xff) };
            yield return new object[] { "BN254 ADD point not on the curve", Bn254Add, Concat(FpElement(32, 1), FpElement(32, 1), new byte[64]) };
            yield return new object[] { "BN254 PAIRING length not a multiple of 192", Bn254Pairing, new byte[191] };
            yield return new object[] { "BLAKE2F length 212", Blake2f, new byte[212] };
            yield return new object[] { "BLAKE2F final flag 2", Blake2f, WithLastByte(new byte[213], 2) };
            yield return new object[] { "KZG length 191", KzgPointEvaluation, new byte[191] };
            yield return new object[] { "KZG versioned hash version 0x02", KzgPointEvaluation, WithFirstByte(new byte[192], 0x02) };
            yield return new object[] { "KZG proof that does not verify", KzgPointEvaluation, WithFirstByte(new byte[192], 0x01) };
        }

        [Theory]
        [MemberData(nameof(InvalidInputs))]
        public void Given_a_precompile_input_the_spec_rejects_When_executed_through_the_registry_Then_it_fails_as_a_value_and_no_exception_is_thrown(string _, int address, byte[] input)
            => AssertFailsWithoutThrowing(address, input);

        [Fact]
        public void Given_valid_BLS_points_that_do_not_pair_When_PAIRING_executes_Then_it_succeeds_with_output_zero()
        {
            var g1 = Registry.Execute(BlsMapFpToG1, FpElement(64, 1));
            var g2 = Registry.Execute(BlsMapFp2ToG2, FpElement(128, 1));

            Assert.True(Registry.TryExecute(BlsPairing, Concat(g1, g2), out var output));
            Assert.Equal(32, output.Length);
            Assert.Equal(0, output[31]);
        }

        [Fact]
        public void Given_the_G1_point_at_infinity_paired_with_any_G2_point_When_PAIRING_executes_Then_it_succeeds_with_output_one()
        {
            var g2 = Registry.Execute(BlsMapFp2ToG2, FpElement(128, 1));

            Assert.True(Registry.TryExecute(BlsPairing, Concat(new byte[128], g2), out var output));
            Assert.Equal(1, output[31]);
        }

        [Fact]
        public void Given_an_ECRECOVER_signature_the_curve_rejects_When_executed_Then_it_succeeds_with_empty_output_and_no_exception_is_thrown()
        {
            EthECKey.SignRecoverable = true;
            var outputs = new List<byte[]>();
            var raised = ExceptionsRaisedOnThisThreadDuring(() =>
            {
                for (byte r = 1; r <= 64; r++)
                {
                    var input = new byte[128];
                    input[63] = 27;
                    input[95] = r;
                    input[127] = 1;
                    Assert.True(Registry.TryExecute(EcRecover, input, out var output));
                    outputs.Add(output);
                }
            });
            Assert.Empty(raised);
            Assert.Contains(outputs, o => o.Length == 0);
        }

        [Fact]
        public void Given_P256VERIFY_with_qx_not_below_the_field_prime_When_executed_Then_it_succeeds_with_empty_output_and_no_exception_is_thrown()
        {
            var input = new byte[160];
            input[63] = 1;
            input[95] = 1;
            var p = "ffffffff00000001000000000000000000000000ffffffffffffffffffffffff".HexToByteArray();
            Array.Copy(p, 0, input, 96, 32);
            input[159] = 1;

            byte[] output = null;
            var raised = ExceptionsRaisedOnThisThreadDuring(() => Assert.True(Registry.TryExecute(P256Verify, input, out output)));
            Assert.Empty(raised);
            Assert.Empty(output);
        }

        [Fact]
        public void Given_a_built_in_handler_When_Execute_is_called_with_invalid_input_Then_ArgumentException_is_still_thrown()
            => Assert.Throws<ArgumentException>(() => Registry.Execute(Blake2f, new byte[212]));

        [Fact]
        public void Given_a_third_party_handler_that_throws_ArgumentException_When_executed_through_the_registry_Then_it_fails_as_a_precompile_failure()
        {
            var registry = Registry.WithHandlers(new ThrowingHandler(new ArgumentException("bad input")));
            Assert.False(registry.TryExecute(0x7f, new byte[0], out var output));
            Assert.Null(output);
        }

        [Fact]
        public void Given_a_third_party_handler_that_throws_a_host_fault_When_executed_through_the_registry_Then_the_fault_surfaces()
        {
            var registry = Registry.WithHandlers(new ThrowingHandler(new EvmHostException("backend missing")));
            Assert.Throws<EvmHostException>(() => registry.TryExecute(0x7f, new byte[0], out _));
        }

        [Fact]
        public void Given_a_registry_with_a_placeholder_When_TryExecute_targets_it_Then_UnwiredPrecompileException_surfaces()
            => Assert.Throws<UnwiredPrecompileException>(
                () => DefaultHardforkConfigs.Prague.Precompiles.TryExecute(BlsG1Add, new byte[256], out _));

        [Fact]
        public async Task Given_a_CALL_to_BN254ADD_with_a_point_off_the_curve_When_the_frame_executes_Then_the_call_returns_zero_and_the_caller_continues_without_any_exception()
        {
            var program = await RunCallToBn254AddAsync(pointX: 1);

            Assert.False(program.IsExceptionalHalt);
            Assert.Equal(EvmUInt256.Zero, program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_a_CALL_to_BN254ADD_with_the_point_at_infinity_When_the_frame_executes_Then_the_call_returns_one()
        {
            var program = await RunCallToBn254AddAsync(pointX: 0);

            Assert.False(program.IsExceptionalHalt);
            Assert.Equal(EvmUInt256.One, program.StackPeekAtU256(0));
        }

        [Fact]
        public void Given_a_CALL_to_BN254ADD_with_a_point_off_the_curve_When_the_frame_executes_Then_no_exception_is_raised_on_the_way()
        {
            var raised = ExceptionsRaisedOnThisThreadDuring(() => RunCallToBn254AddAsync(pointX: 1).GetAwaiter().GetResult());
            Assert.Empty(raised);
        }

        private static async Task<Program> RunCallToBn254AddAsync(byte pointX)
        {
            var code = new byte[]
            {
                0x60, pointX, 0x60, 0x00, 0x52,
                0x60, 0x00, 0x60, 0x00, 0x60, 0x40, 0x60, 0x00, 0x60, 0x00, 0x60, 0x06, 0x61, 0xff, 0xff, 0xf1
            };
            const string caller = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            const string contract = "0xcccccccccccccccccccccccccccccccccccccccc";
            var accounts = new Dictionary<string, AccountState>
            {
                [caller] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000) },
                [contract] = new AccountState { Code = code }
            };
            var executionState = new ExecutionStateService(new InMemoryStateReader(accounts));
            var callContext = new EvmCallContext
            {
                From = caller,
                To = contract,
                Data = new byte[0],
                Value = EvmUInt256.Zero,
                Gas = 1_000_000,
                ChainId = EvmUInt256.One
            };
            var programContext = new ProgramContext(callContext, executionState,
                blockNumber: new EvmUInt256(1), timestamp: new EvmUInt256(1_000));
            var program = new Program(code, programContext);
            return await new EVMSimulator(DefaultHardforkConfigs.Osaka).ExecuteWithCallStackAsync(program, traceEnabled: false);
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var total = 0;
            foreach (var part in parts) total += part.Length;
            var result = new byte[total];
            var offset = 0;
            foreach (var part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

        private static byte[] FilledWith(int size, byte value)
        {
            var bytes = new byte[size];
            for (int i = 0; i < size; i++) bytes[i] = value;
            return bytes;
        }

        private static byte[] WithLastByte(byte[] bytes, byte value)
        {
            bytes[bytes.Length - 1] = value;
            return bytes;
        }

        private static byte[] WithFirstByte(byte[] bytes, byte value)
        {
            bytes[0] = value;
            return bytes;
        }

        private sealed class ThrowingHandler : IPrecompileHandler
        {
            private readonly Exception _exception;
            public ThrowingHandler(Exception exception) => _exception = exception;
            public int AddressNumeric => 0x7f;
            public byte[] Execute(byte[] input) => throw _exception;
        }
    }
}
