using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public partial class TraceDigestParityTests
    {
        private const string Caller = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
        private const string Contract = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string Child = "0xdddddddddddddddddddddddddddddddddddddddd";

        private sealed class Scenario
        {
            public byte[] Code;
            public byte[] Calldata = new byte[0];
            public HardforkName Fork = HardforkName.Osaka;
            public long Gas = 1_000_000;
            public Dictionary<EvmUInt256, byte[]> Storage = new Dictionary<EvmUInt256, byte[]>();
        }

        private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

        private static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        private static byte[] Push(int width, byte fill) => Cat(Bytes(0x5f + width), Enumerable.Repeat(fill, width).ToArray());

        private static byte[] Address20(string address) => address.HexToByteArray();

        private static readonly byte[] ChildReturnsSeven = Bytes(0x60, 0x07, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3);

        private static readonly byte[] SimpleOwnerRuntime = (
            "6080604052348015600f57600080fd5b506004361060285760003560e01c8063893d20e814602d575b600080fd5b60336047565b604051603e919060ad565b60405180910390f35b60008060009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16905090565b600073ffffffffffffffffffffffffffffffffffffffff82169050919050565b60006099826070565b9050919050565b60a7816090565b82525050565b600060208201905060c0600083018460a0565b9291505056fea2646970667358221220571729c39bd05edb51c651a38d2adafe1822fa3b0e4ae39601f2901a6d95fd3064736f6c63430008090033").HexToByteArray();

        private static readonly byte[] CreateInitcode = Bytes(0x60, 0x01, 0x60, 0x00, 0x53, 0x60, 0x01, 0x60, 0x00, 0xf3);

        private static IEnumerable<KeyValuePair<string, Scenario>> HandAssembled()
        {
            yield return Named("empty-code", new Scenario { Code = new byte[0] });

            yield return Named("countdown-loop", new Scenario
            {
                Code = Bytes(0x60, 0x05, 0x5b, 0x60, 0x01, 0x90, 0x03, 0x80, 0x60, 0x02, 0x57, 0x00)
            });

            yield return Named("every-push-width", new Scenario
            {
                Code = Cat(Cat(Enumerable.Range(1, 32).Select(w => Cat(Push(w, (byte)(0xa0 + w)), Bytes(0x50))).ToArray()), Bytes(0x5f, 0x50, 0x00))
            });

            yield return Named("every-dup-and-swap", new Scenario
            {
                Code = Cat(
                    Cat(Enumerable.Range(1, 17).Select(i => Bytes(0x60, i)).ToArray()),
                    Cat(Enumerable.Range(0, 16).Select(i => Bytes(0x80 + i)).ToArray()),
                    Cat(Enumerable.Range(0, 16).Select(i => Bytes(0x90 + i)).ToArray()),
                    Bytes(0x00))
            });

            yield return Named("every-log-width", new Scenario
            {
                Code = Cat(
                    Bytes(0x60, 0x2a, 0x60, 0x00, 0x52),
                    Cat(Enumerable.Range(0, 5).Select(n =>
                        Cat(Cat(Enumerable.Range(0, n).Select(t => Bytes(0x60, 0x10 + t)).ToArray()), Bytes(0x60, 0x20, 0x60, 0x00, 0xa0 + n))).ToArray()),
                    Bytes(0x00))
            });

            yield return Named("truncated-push-ends-implicitly", new Scenario { Code = Bytes(0x60, 0x01, 0x7f, 0xaa, 0xbb) });

            yield return Named("push-at-end-of-code", new Scenario { Code = Bytes(0x60) });

            yield return Named("invalid-opcode-halts", new Scenario { Code = Bytes(0x60, 0x01, 0x0c) });

            yield return Named("jump-into-push-data-halts", new Scenario { Code = Bytes(0x60, 0x04, 0x56, 0x60, 0x5b, 0x00) });

            yield return Named("jump-past-end-halts", new Scenario { Code = Bytes(0x60, 0x40, 0x56) });

            yield return Named("jump-to-non-jumpdest-halts", new Scenario { Code = Bytes(0x60, 0x03, 0x56, 0x00) });

            yield return Named("jumpi-not-taken", new Scenario { Code = Bytes(0x60, 0x00, 0x60, 0x06, 0x57, 0x00, 0x5b) });

            yield return Named("jumpi-taken", new Scenario { Code = Bytes(0x60, 0x01, 0x60, 0x06, 0x57, 0x00, 0x5b, 0x60, 0x09, 0x00) });

            yield return Named("jumpdest-byte-inside-push-data-is-data", new Scenario
            {
                Code = Bytes(0x60, 0x08, 0x56, 0x61, 0x5b, 0x5b, 0x00, 0x00, 0x5b, 0x00)
            });

            yield return Named("out-of-gas-in-loop", new Scenario { Code = Bytes(0x5b, 0x60, 0x00, 0x56), Gas = 300 });

            yield return Named("pc-opcode", new Scenario { Code = Bytes(0x58, 0x58, 0x60, 0x01, 0x58, 0x00) });

            yield return Named("return-data", new Scenario { Code = Bytes(0x60, 0x2a, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3) });

            yield return Named("revert", new Scenario { Code = Bytes(0x60, 0x00, 0x60, 0x00, 0xfd) });

            yield return Named("call-into-child-frame", new Scenario
            {
                Code = Cat(
                    Bytes(0x60, 0x20, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00),
                    Bytes(0x73), Address20(Child),
                    Bytes(0x5a, 0xf1, 0x50, 0x60, 0x20, 0x60, 0x00, 0xf3))
            });

            yield return Named("staticcall-into-child-frame", new Scenario
            {
                Code = Cat(
                    Bytes(0x60, 0x20, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00),
                    Bytes(0x73), Address20(Child),
                    Bytes(0x5a, 0xfa, 0x50, 0x60, 0x20, 0x60, 0x00, 0xf3))
            });

            yield return Named("delegatecall-into-child-frame", new Scenario
            {
                Code = Cat(
                    Bytes(0x60, 0x20, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00),
                    Bytes(0x73), Address20(Child),
                    Bytes(0x5a, 0xf4, 0x50, 0x60, 0x20, 0x60, 0x00, 0xf3))
            });

            yield return Named("create-runs-uncached-initcode", new Scenario
            {
                Code = Cat(
                    Bytes(0x69), CreateInitcode, Bytes(0x60, 0x00, 0x52),
                    Bytes(0x60, 0x0a, 0x60, 0x16, 0x60, 0x00, 0xf0, 0x50, 0x00))
            });

            yield return Named("eip8024-dupn-swapn-exchange", new Scenario
            {
                Fork = HardforkName.Amsterdam,
                Code = Cat(
                    Cat(Enumerable.Range(1, 20).Select(i => Bytes(0x60, i)).ToArray()),
                    Bytes(0xe6, 0x80, 0xe7, 0x80, 0xe8, 0x00, 0xe6, 0x81, 0x00))
            });

            yield return Named("eip8024-invalid-immediate-halts", new Scenario
            {
                Fork = HardforkName.Amsterdam,
                Code = Cat(Enumerable.Range(1, 20).Select(i => Bytes(0x60, i)).ToArray().Concat(new[] { Bytes(0xe6, 0x5b, 0x5b, 0x00) }).ToArray())
            });

            yield return Named("eip8024-opcodes-before-the-fork-are-invalid", new Scenario
            {
                Fork = HardforkName.Osaka,
                Code = Cat(Enumerable.Range(1, 20).Select(i => Bytes(0x60, i)).ToArray().Concat(new[] { Bytes(0xe6, 0x80, 0x00) }).ToArray())
            });

            yield return Named("eip8024-missing-immediate-at-end", new Scenario
            {
                Fork = HardforkName.Amsterdam,
                Code = Cat(Enumerable.Range(1, 150).Select(i => Bytes(0x60, i % 256)).ToArray().Concat(new[] { Bytes(0xe6) }).ToArray())
            });

            yield return Named("stack-underflow-on-last-instruction-halts", new Scenario
            {
                Fork = HardforkName.Amsterdam,
                Code = Cat(Enumerable.Range(1, 20).Select(i => Bytes(0x60, i)).ToArray().Concat(new[] { Bytes(0xe6) }).ToArray())
            });

            yield return Named("pop-underflow-on-last-instruction-halts", new Scenario { Code = Bytes(0x50) });

            yield return Named("stack-overflow-on-last-instruction-halts", new Scenario
            {
                Code = Enumerable.Repeat((byte)0x5f, 1025).ToArray()
            });

            yield return Named("codecopy-reads-raw-code-bytes", new Scenario
            {
                Code = Bytes(0x60, 0x20, 0x60, 0x00, 0x60, 0x00, 0x39, 0x60, 0x05, 0x60, 0x03, 0x60, 0x40, 0x39, 0x00)
            });

            yield return Named("codecopy-past-the-end-pads-zero", new Scenario
            {
                Code = Bytes(0x60, 0x10, 0x60, 0x08, 0x60, 0x00, 0x39, 0x00)
            });

            yield return Named("extcodecopy-of-child", new Scenario
            {
                Code = Cat(Bytes(0x60, 0x0a, 0x60, 0x00, 0x60, 0x00, 0x73), Address20(Child), Bytes(0x3c, 0x00))
            });

            yield return Named("mcopy-overlapping", new Scenario
            {
                Code = Cat(Push(32, 0xab), Bytes(0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0x60, 0x10, 0x5e, 0x00))
            });

            yield return Named("mstore8-and-mload", new Scenario
            {
                Code = Bytes(0x60, 0xff, 0x60, 0x05, 0x53, 0x60, 0x00, 0x51, 0x00)
            });

            yield return Named("calldatacopy-into-memory", new Scenario
            {
                Code = Bytes(0x60, 0x20, 0x60, 0x00, 0x60, 0x00, 0x37, 0x00),
                Calldata = Enumerable.Range(1, 40).Select(i => (byte)i).ToArray()
            });

            yield return Named("simple-owner-get-owner", new Scenario
            {
                Code = SimpleOwnerRuntime,
                Calldata = Bytes(0x89, 0x3d, 0x20, 0xe8),
                Storage = new Dictionary<EvmUInt256, byte[]>
                {
                    [EvmUInt256.Zero] = Address20("0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b").PadTo32Bytes()
                }
            });

            yield return Named("simple-owner-unknown-selector-reverts", new Scenario
            {
                Code = SimpleOwnerRuntime,
                Calldata = Bytes(0xde, 0xad, 0xbe, 0xef)
            });
        }

        private static KeyValuePair<string, Scenario> Named(string name, Scenario scenario)
            => new KeyValuePair<string, Scenario>(name, scenario);

        private static readonly byte[][] MainnetCalldata =
        {
            new byte[0],
            Bytes(0x18, 0x16, 0x0d, 0xdd),
            Cat(Bytes(0x70, 0xa0, 0x82, 0x31), new byte[12], Address20(Caller)),
            Cat(Bytes(0xa9, 0x05, 0x9c, 0xbb), new byte[12], Address20(Child), new byte[31], Bytes(0x01)),
            Bytes(0xde, 0xad, 0xbe, 0xef)
        };

        private static IEnumerable<KeyValuePair<string, Scenario>> MainnetContracts()
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "DecoderCorpus");
            foreach (var path in Directory.GetFiles(directory, "*.hex").OrderBy(p => p, StringComparer.Ordinal))
            {
                var code = File.ReadAllText(path).Trim().HexToByteArray();
                for (var c = 0; c < MainnetCalldata.Length; c++)
                {
                    yield return Named(Path.GetFileNameWithoutExtension(path) + "-calldata" + c,
                        new Scenario { Code = code, Calldata = MainnetCalldata[c] });
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, Scenario>> AllScenarios()
            => HandAssembled().Concat(MainnetContracts());

        public static IEnumerable<object[]> ScenarioNames() => AllScenarios().Select(s => new object[] { s.Key });

        private static Program Run(Scenario scenario)
        {
            var config = DefaultMainnetHardforkRegistry.Instance.Get(scenario.Fork);
            var accounts = new Dictionary<string, AccountState>
            {
                [Caller] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000) },
                [Contract] = new AccountState { Code = scenario.Code, Storage = scenario.Storage },
                [Child] = new AccountState { Code = ChildReturnsSeven }
            };
            var executionState = new ExecutionStateService(new InMemoryStateReader(accounts));
            var callContext = new EvmCallContext
            {
                From = Caller,
                To = Contract,
                Data = scenario.Calldata,
                Value = EvmUInt256.Zero,
                Gas = scenario.Gas,
                ChainId = EvmUInt256.One
            };
            var programContext = new ProgramContext(callContext, executionState,
                blockNumber: new EvmUInt256(1), timestamp: new EvmUInt256(1_000))
            {
                EnforceSstoreGasStipend = config.EnforceSstoreGasStipend
            };
            return ExecuteTraced(new EVMSimulator(config), new Program(scenario.Code, programContext));
        }

        private static string Digest(Program program, out int traceCount)
        {
            var text = new StringBuilder();
            foreach (var trace in program.Trace)
            {
                text.Append(trace.Depth).Append('|')
                    .Append(trace.Instruction.Step).Append('|')
                    .Append(trace.Instruction.Value.ToString("x2")).Append('|')
                    .Append(trace.GasRemaining).Append('|')
                    .Append(trace.GasCost).Append('|')
                    .Append(trace.OutOfGas ? 1 : 0).Append('|')
                    .Append(string.Join(",", trace.Stack)).Append('|')
                    .Append(trace.Memory).Append('\n');
            }
            text.Append("final|").Append(program.GasRemaining)
                .Append('|').Append(program.HaltReason)
                .Append('|').Append(program.IsExceptionalHalt ? 1 : 0)
                .Append('|').Append(program.ProgramResult.IsRevert ? 1 : 0)
                .Append('|').Append(program.StoppedImplicitly ? 1 : 0)
                .Append('|').Append(program.ProgramResult.Result == null ? "-" : program.ProgramResult.Result.ToHex())
                .Append('|').Append(program.ProgramResult.Logs.Count)
                .Append('\n');
            traceCount = program.Trace.Count;
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).ToHex();
            }
        }

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Given_a_traced_execution_When_the_full_trace_is_hashed_Then_it_matches_the_recorded_digest(string name)
        {
            var scenario = AllScenarios().First(s => s.Key == name).Value;
            var digest = Digest(Run(scenario), out var traceCount);
            Assert.True(ExpectedDigests.TryGetValue(name, out var expected), $"no recorded digest for {name}: {traceCount}:{digest}");
            var actual = $"{traceCount}:{digest}";
            Assert.True(expected == actual, $"{name}: recorded {expected} but executed {actual}");
        }

        [Fact]
        public void Given_the_scenarios_When_counted_Then_every_one_has_a_recorded_digest_and_none_is_stale()
        {
            var names = AllScenarios().Select(s => s.Key).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), ExpectedDigests.Keys.OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void Given_the_hand_assembled_scenarios_When_traced_Then_the_executions_do_the_intended_thing()
        {
            Assert.Equal(EvmHaltReason.InvalidOpcode, Run(Find("invalid-opcode-halts")).HaltReason);
            Assert.Equal(EvmHaltReason.InvalidJumpDestination, Run(Find("jump-into-push-data-halts")).HaltReason);
            Assert.Equal(EvmHaltReason.InvalidJumpDestination, Run(Find("jump-to-non-jumpdest-halts")).HaltReason);
            Assert.Equal(EvmHaltReason.OutOfGas, Run(Find("out-of-gas-in-loop")).HaltReason);
            Assert.Equal(EvmHaltReason.InvalidOpcode, Run(Find("eip8024-opcodes-before-the-fork-are-invalid")).HaltReason);
            Assert.Equal(EvmHaltReason.None, Run(Find("eip8024-dupn-swapn-exchange")).HaltReason);
            Assert.True(Run(Find("truncated-push-ends-implicitly")).StoppedImplicitly);
            Assert.True(Run(Find("empty-code")).StoppedImplicitly);
            AssertHaltedOnTheLastInstructionWithoutAnImplicitStop("stack-underflow-on-last-instruction-halts", EvmHaltReason.StackUnderflow, 21);
            AssertHaltedOnTheLastInstructionWithoutAnImplicitStop("pop-underflow-on-last-instruction-halts", EvmHaltReason.StackUnderflow, 1);
            AssertHaltedOnTheLastInstructionWithoutAnImplicitStop("stack-overflow-on-last-instruction-halts", EvmHaltReason.StackOverflow, 1025);
            Assert.Equal(5, Run(Find("every-log-width")).ProgramResult.Logs.Count);
            Assert.Contains(Run(Find("call-into-child-frame")).Trace, t => t.Depth == 1);
            Assert.Contains(Run(Find("create-runs-uncached-initcode")).Trace, t => t.Depth == 1);
            Assert.Equal(Address20("0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b").PadTo32Bytes().ToHex(),
                Run(Find("simple-owner-get-owner")).ProgramResult.Result.ToHex());
        }

        [Fact]
        public void Given_a_traced_execution_When_the_trace_is_read_Then_each_traced_instruction_is_the_element_of_the_programs_instruction_list()
        {
            var program = Run(Find("countdown-loop"));

            Assert.NotEmpty(program.Trace);
            foreach (var trace in program.Trace)
                Assert.Same(program.Instructions.Single(i => i.Step == trace.Instruction.Step), trace.Instruction);
        }

        private static void AssertHaltedOnTheLastInstructionWithoutAnImplicitStop(string name, EvmHaltReason reason, int expectedTraces)
        {
            var program = Run(Find(name));
            Assert.Equal(reason, program.HaltReason);
            Assert.False(program.StoppedImplicitly);
            Assert.Equal(expectedTraces, program.Trace.Count);
        }

        private static Scenario Find(string name) => AllScenarios().First(s => s.Key == name).Value;

        private static readonly Dictionary<string, string> ExpectedDigests = new Dictionary<string, string>
        {
            ["pop-underflow-on-last-instruction-halts"] = "1:a09c3f6eb478e83ce19046612dc69f89cc47c8d46ed22d440960651b0624f3cb",
            ["stack-overflow-on-last-instruction-halts"] = "1025:65340ef4b3a3728f188750bbe9978bcec25b14d85aa5a73269514642b0564c2b",
            ["stack-underflow-on-last-instruction-halts"] = "21:efe9cd1fca1b3207cd0728fbe4fdfdbfc125ca6838884ce4b9682c4c7326d9c2",
            ["calldatacopy-into-memory"] = "5:0aab963e51e98c7f42a18ad94b7714778aebe29f74ad97e880fdb633c0ecd4c3",
            ["call-into-child-frame"] = "18:b66e4cf49bc8702d3d1f8c816d0a81394ced35bf22bd8a009682f71e0ec37092",
            ["codecopy-past-the-end-pads-zero"] = "5:195d9a12e241101cc6d484fa0cf171598f932ed8c831ff6ca880fb1c71222d39",
            ["codecopy-reads-raw-code-bytes"] = "9:65d05cc09d2840fdd0ba4a3c52ed3263571f2b2b5c53c6990526ba7f6e0a51de",
            ["countdown-loop"] = "37:874606ebeb43908fef0dbe736bd899ba35986da9f3d5fb14aebc9c0ef74cfa4d",
            ["create-runs-uncached-initcode"] = "15:df4ccb178cb11caedfbd9a73a989d5ecb8bfb3e7bf691bc74f0db15e8580b67a",
            ["delegatecall-into-child-frame"] = "17:91d88c2051963e35d189963dacca6fc6e8679168a5c65a5caeba0e6d0bcffe54",
            ["eip8024-dupn-swapn-exchange"] = "25:1a732c3fd129c65592d5f16629e2e700b11db53631ce50465ce731fa7de322e5",
            ["eip8024-invalid-immediate-halts"] = "21:60d334606f833b66ee5ba4a19d8cfda4d0cad55a4fd12a0ca39b0592dc7ba73e",
            ["eip8024-missing-immediate-at-end"] = "152:1cef4d711f6b5af09d50a5b70ac818251757b4d8016b57c87cd5832c21c00c66",
            ["eip8024-opcodes-before-the-fork-are-invalid"] = "21:71fff7f3ba8b384729cd1590307017ebab7e1e6b6b3ff634664217fd50fbd8d3",
            ["empty-code"] = "1:b1ef45c03177ead2b2a7c4f69998bc7f754611048297e155f17f38ecb99dd3d2",
            ["every-dup-and-swap"] = "50:de923e9930dada4107869739d985a708b1b22bb1d27bb6e686227c1749028875",
            ["every-log-width"] = "29:2b63b9a82f4d242a6a23cd5ca09a31e87496067ece016c9b43a4acb0c295b004",
            ["every-push-width"] = "67:d6cca08b015cf73f00ae1c1a101b5bc015454e50bae7a6001be821621448dafa",
            ["extcodecopy-of-child"] = "6:8272fb2e1560246471538e9f2062bcc607ed5580d8bc8920a32f630f5b6d3a00",
            ["invalid-opcode-halts"] = "2:dadf89cd8ba375410427961abd95fe578b478361abecb496ee5b23fa3816be6f",
            ["jumpdest-byte-inside-push-data-is-data"] = "4:240e7fd93fef2e9e581a2c560b947a596c305a1fde1a0d386d912b4334aff67a",
            ["jumpi-not-taken"] = "4:387b1dde625e380585ccba8ef613fd9a5c5e2eb485cbbf9e797b2a58a3681ab3",
            ["jump-into-push-data-halts"] = "2:feb99888a34a55012206e1081096a856debe7698cd952a3c016b0fdd065994a3",
            ["jumpi-taken"] = "6:39e605919b70c5a3bc271c02549e1bde19989f96b4921faec6a2a9255a3f1bc3",
            ["jump-past-end-halts"] = "2:feb99888a34a55012206e1081096a856debe7698cd952a3c016b0fdd065994a3",
            ["jump-to-non-jumpdest-halts"] = "2:feb99888a34a55012206e1081096a856debe7698cd952a3c016b0fdd065994a3",
            ["mainnet-1149150-first-calldata0"] = "36:3f8c70b1609c82c27e61f6084a99ea0e66142fc73bf99fbeb18d1f52451b7eb2",
            ["mainnet-1149150-first-calldata1"] = "36:844bb38dfc19cfa39731ad61aa83f6d27427707a91d80b833220a81a04250da5",
            ["mainnet-1149150-first-calldata2"] = "36:dc8bebabf2c4d32707faac52d353d4688e9d4b823b40a1a92e456fb2e1b7f22d",
            ["mainnet-1149150-first-calldata3"] = "36:0ac415c9aeb32c3b85c9f6425241e81f799c4ab21cf003c4cc754c9e1185c06d",
            ["mainnet-1149150-first-calldata4"] = "36:d2ec4893e8cc20e1d54c82f25d737f29ec7758965d4b33cf104a74fe0e337eba",
            ["mainnet-116525-first-calldata0"] = "72:e663565f9079b35c9b220a580b6f6baa69159007614950e8544057935eb85550",
            ["mainnet-116525-first-calldata1"] = "72:437aa5467fb59af7510a8049c8c4776fb7a04376522c7f5642536b0481afed10",
            ["mainnet-116525-first-calldata2"] = "72:787876d90e232cab7985dc547a4f789021ba1519775bcb2bf9ae58782da27b35",
            ["mainnet-116525-first-calldata3"] = "72:35c50b785a6d83a21514caea0c802b8e454b2a53f1f06e019f9a9b7c9ac574b2",
            ["mainnet-116525-first-calldata4"] = "72:a8c179aafffc5e5555f23b54fb2f3ec8cd9dca7fde6d690c313402f1736d9a8e",
            ["mainnet-20000000-1069bytes-calldata0"] = "55:6cd4e3f57648a09c83ad2dccfc9a67042aa9e3e24e584e9d84afcb0e6645e94d",
            ["mainnet-20000000-1069bytes-calldata1"] = "79:e4c857500aa095e18332342a0bf270d292bd3ef1ec123cba79cdb579e52b92d3",
            ["mainnet-20000000-1069bytes-calldata2"] = "79:527c4fddbd62298d315a67c6e8fea6fca8d953ac0f5516c2a731f586e51c94e0",
            ["mainnet-20000000-1069bytes-calldata3"] = "79:c7716abed09574cc90bab18e737bafb756ecbf91e5315f4e0765259277c41468",
            ["mainnet-20000000-1069bytes-calldata4"] = "79:37846b113bf63f1311230f032d4e7c8910dde43d3ef593a57b163a9409201a7c",
            ["mainnet-20000000-12013bytes-calldata0"] = "12:8e86da3d70ced875783ece58079d3d2099f4b83320be004ab552dd651da6eff2",
            ["mainnet-20000000-12013bytes-calldata1"] = "397:2e0341324c4727321c6198884f200501fb3629b93349f6eb488f9b322e2e9810",
            ["mainnet-20000000-12013bytes-calldata2"] = "397:4f04ae7743f94e5b92367c161cc5afcbd26f02f59e5e9a886133c734e27ff216",
            ["mainnet-20000000-12013bytes-calldata3"] = "397:cc3ee637a39204220a2a42d4b19f75f5249deea9fe2f2a1937a5862ee581a960",
            ["mainnet-20000000-12013bytes-calldata4"] = "397:1470e82edd79c516e1c4bdb2983fd5e3f51ad844808d2fa3d11fdf6caa204584",
            ["mainnet-20000000-212bytes-calldata0"] = "83:5c44d54efc374c09813454909e68da5756884d494f1062a0a3e697104f74cbba",
            ["mainnet-20000000-212bytes-calldata1"] = "83:3b765f7d40ea78dcecc7e5b8d41e5a3024946dd5291600fccfeb05ce3d734299",
            ["mainnet-20000000-212bytes-calldata2"] = "83:280da0adb0f8cd07448437105a030c74e2e1a2f420d87e682534326af145405d",
            ["mainnet-20000000-212bytes-calldata3"] = "83:570f2e3d52dbf413ccfab560b3b37ebfc5f1cd4830f7f8c9d720cb042d82bd8c",
            ["mainnet-20000000-212bytes-calldata4"] = "83:9422f63bfd84987222e5ba53daecb314d87e29717b15915f8d6d889582a617b2",
            ["mainnet-20000000-2188bytes-calldata0"] = "102:72b21955d1e1acc2fbffc9bed0b1acce3f94b64083888730d8ff18d0c06dcd1c",
            ["mainnet-20000000-2188bytes-calldata1"] = "101:f3af5d4ac753bb6482523b46f42dde5f717160d6c1c9fe14f5eb3e716968b053",
            ["mainnet-20000000-2188bytes-calldata2"] = "101:451c4322490b18d402fe65925b414b5c59e95a76ac77d277510de84e1dfc10ed",
            ["mainnet-20000000-2188bytes-calldata3"] = "101:0a075550a0bf96210c670179b58e4d939c2241fbe4cc9efd3db6cff0e30c516a",
            ["mainnet-20000000-2188bytes-calldata4"] = "101:2b814a525a62c6115e69af0e0122a349a21c0693df4b3f7aef8e31bae1eb2b73",
            ["mainnet-20000000-24523bytes-calldata0"] = "19:f680b092249ac366df3675f93b2632340829e6ad42e65a3aad6a2a005e46f9a0",
            ["mainnet-20000000-24523bytes-calldata1"] = "140:bb1f5d13db2f7e0a51f3924d075b51ad80a376919a25a3f53e1f2ea96e5dbcf9",
            ["mainnet-20000000-24523bytes-calldata2"] = "138:99bf3f9bf04d20fcf92679a614037ac0ecc71184dc9ef3c08a5bb6599ff5ffaf",
            ["mainnet-20000000-24523bytes-calldata3"] = "138:84a438bad3e843e1fe3922dec3d81c705eaf1ccf87df896f59618014e52d2705",
            ["mainnet-20000000-24523bytes-calldata4"] = "137:1bc435d1f433b5beac27c90d309e1b977bb64ee9c82551a7a5fab809f13ac458",
            ["mainnet-20000000-4664bytes-calldata0"] = "19:29018ea429ce95c1db3b129fd5e2d7cb8995e40e5d84d89e6e1afc25bf50d443",
            ["mainnet-20000000-4664bytes-calldata1"] = "108:4ac4537767b82e2b3b8bc86abd7a21091fed496fa5cdc6b0b9cb967748759922",
            ["mainnet-20000000-4664bytes-calldata2"] = "215:94983ac01fd0186f52d3b78188f9b80a67939425d4cb33d146759de728ea178f",
            ["mainnet-20000000-4664bytes-calldata3"] = "378:7a05a8f5638b2917a4e0e646d070e3950fc1e7f1f344ab10fadf5d4c0b1cd8cd",
            ["mainnet-20000000-4664bytes-calldata4"] = "60:48d68e82596fde850d20ad2fd4c8651b1c92ea30a7e5f5ac7c76a62cca1d908b",
            ["mainnet-20000000-7102bytes-calldata0"] = "13:c7442df921787dc2004b3a27b3a179ce107219becc47f3d67c70fa7bda58d207",
            ["mainnet-20000000-7102bytes-calldata1"] = "192:282f9edfdd1173d44f74084c5b31e16c61227ef6fc46327e2044f8e72bc79ed4",
            ["mainnet-20000000-7102bytes-calldata2"] = "144:91bdaa61bea8f72e218424c30591e2ab75c64e73514fc7b2dd08749f67fcb364",
            ["mainnet-20000000-7102bytes-calldata3"] = "643:345443151275634d531b71f46b75d9cf1e61598d4670ca939eda578c53c32f39",
            ["mainnet-20000000-7102bytes-calldata4"] = "55:b774350600314fa28787acd71b78e9f52f9fde3969d40068dc9cfb59788b6036",
            ["mcopy-overlapping"] = "8:054a1d91ff74a08d6897a150021214da0936d1c7fd9e400e7cd641746820d2a8",
            ["mstore8-and-mload"] = "6:6318488bc036b85eeeafdf4826dae105324df8b1cd349f52013b96b531063602",
            ["out-of-gas-in-loop"] = "76:13c7a7d16ca22bc0a847e1aa81f6e7b36ed90cfcc538412f30f2fa5326342f8f",
            ["pc-opcode"] = "5:2d41e91a3991dab39fe5d015a25342dc64c271f2aebeb70d66ec4ead5ef39457",
            ["push-at-end-of-code"] = "2:843123d27ccc135cc9e5fede9323c2b7e9be019f1dfb930ebe4c74105f3d068c",
            ["return-data"] = "6:6dd273ff3b17d76ec610d6b1cc4bff861037eb5586fdfaf605e1428b89d9317f",
            ["revert"] = "3:7a94ccc55fc7b8b2524a984b9d8dc3d0d5f34dc340c93ecce857547ca6ff65bd",
            ["simple-owner-get-owner"] = "116:4d2bf9d152ff41e1f4fb22d32b9ae957f0b90e1dfb7adfa0a6b1abc90c6e15e2",
            ["simple-owner-unknown-selector-reverts"] = "28:70f9ec63961c8e2cad3f9ecac8edb677df916a267d58529c21c5f053f3ac14c6",
            ["staticcall-into-child-frame"] = "17:ee453d7d4456ee876b9dd944b9f80167c02cb6ce2fbb1c18d592ae854351a65d",
            ["truncated-push-ends-implicitly"] = "3:fba17823b1b79e50064bf4b7452171c0d708a2f89572a625de3c98f189d0df52",
        };
    }
}
