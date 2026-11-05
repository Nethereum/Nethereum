using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class DecoderAndJumpDestinationParityTests
    {
        private const byte Stop = 0x00;
        private const byte Jumpdest = 0x5b;
        private const byte Push0 = 0x5f;
        private const byte Push1 = 0x60;
        private const byte Push32 = 0x7f;
        private const byte Dupn = 0xe6;
        private const byte Swapn = 0xe7;
        private const byte Exchange = 0xe8;

        private static readonly byte[] InterestingBytes =
        {
            Stop, 0x01, 0x50, 0x56, 0x57, Jumpdest, Push0, Push1, 0x61, 0x62, Push32,
            0x80, 0x8f, 0x90, 0x9f, 0xa0, 0xfd, 0xfe, 0xff, 0x0c, 0xe5, Dupn, Swapn, Exchange, 0xe9
        };

        private static readonly Lazy<Dictionary<string, byte[]>> MainnetCorpus = new Lazy<Dictionary<string, byte[]>>(() =>
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "DecoderCorpus");
            return Directory.GetFiles(directory, "*.hex")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => File.ReadAllText(path).Trim().HexToByteArray());
        });

        private static Dictionary<string, byte[]> EdgeCases()
        {
            var cases = new Dictionary<string, byte[]>
            {
                ["empty"] = new byte[0],
                ["single-stop"] = new byte[] { Stop },
                ["single-jumpdest"] = new byte[] { Jumpdest },
                ["push0-then-jumpdest"] = new byte[] { Push0, Jumpdest },
                ["push1-at-end-no-data"] = new byte[] { Push1 },
                ["push32-at-end-no-data"] = new byte[] { Push32 },
                ["push32-with-one-data-byte"] = new byte[] { Push32, 0xaa },
                ["push32-truncated-by-one"] = new byte[] { Push32 }.Concat(Enumerable.Repeat((byte)0xbb, 31)).ToArray(),
                ["push32-complete"] = new byte[] { Push32 }.Concat(Enumerable.Repeat((byte)0xcc, 32)).Concat(new byte[] { Jumpdest }).ToArray(),
                ["jumpdest-inside-push1-data"] = new byte[] { Push1, Jumpdest, Jumpdest },
                ["jumpdest-inside-push2-data"] = new byte[] { 0x61, Jumpdest, Jumpdest, Jumpdest },
                ["jumpdest-inside-push32-data"] = new byte[] { Push32 }.Concat(Enumerable.Repeat(Jumpdest, 32)).Concat(new byte[] { Jumpdest }).ToArray(),
                ["jumpdest-as-last-byte-of-push-data"] = new byte[] { 0x62, 0x00, 0x00, Jumpdest, Jumpdest },
                ["push-opcode-inside-push-data"] = new byte[] { 0x62, Push1, Push1, Push1, Jumpdest },
                ["invalid-opcodes-between-valid"] = new byte[] { 0x0c, Jumpdest, 0x0d, 0xef, Jumpdest, 0xe5, 0xe9, 0xfc },
                ["dupn-missing-immediate"] = new byte[] { Dupn },
                ["swapn-missing-immediate"] = new byte[] { Swapn },
                ["exchange-missing-immediate"] = new byte[] { Exchange },
                ["dupn-swallows-jumpdest-lookalike"] = new byte[] { Dupn, Jumpdest, Jumpdest },
                ["eip8024-opcodes-back-to-back"] = new byte[] { Dupn, Swapn, Exchange, Dupn, Swapn, Exchange, Jumpdest },
                ["eip8024-opcode-as-immediate"] = new byte[] { Dupn, Dupn, Jumpdest },
                ["push-data-looks-like-eip8024"] = new byte[] { 0x62, Dupn, 0x00, 0x00, Jumpdest },
                ["eip8024-immediate-looks-like-push"] = new byte[] { Dupn, Push1, Jumpdest, Jumpdest }
            };
            return cases;
        }

        private static IEnumerable<KeyValuePair<string, byte[]>> ExhaustiveEip8024Cases()
        {
            var tails = new[]
            {
                new byte[0],
                new[] { Jumpdest },
                new[] { Push1, Jumpdest },
                new[] { Dupn, Jumpdest },
                new[] { Exchange, Swapn, Jumpdest }
            };
            foreach (var opcode in new[] { Dupn, Swapn, Exchange })
            {
                for (var immediate = 0; immediate < 256; immediate++)
                {
                    for (var t = 0; t < tails.Length; t++)
                    {
                        var code = new[] { opcode, (byte)immediate }.Concat(tails[t]).ToArray();
                        yield return new KeyValuePair<string, byte[]>($"eip8024-{opcode:x2}-imm-{immediate:x2}-tail{t}", code);
                    }
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, byte[]>> FuzzCases(int count)
        {
            for (var seed = 0; seed < count; seed++)
            {
                var random = new Random(seed);
                var length = random.Next(1, 49);
                var code = new byte[length];
                for (var i = 0; i < length; i++)
                {
                    code[i] = random.Next(2) == 0
                        ? InterestingBytes[random.Next(InterestingBytes.Length)]
                        : (byte)random.Next(256);
                }
                yield return new KeyValuePair<string, byte[]>($"fuzz-{seed}", code);
            }
        }

        private static IEnumerable<KeyValuePair<string, byte[]>> EveryCorpus()
        {
            foreach (var edge in EdgeCases()) yield return edge;
            foreach (var mainnet in MainnetCorpus.Value) yield return mainnet;
            foreach (var exhaustive in ExhaustiveEip8024Cases()) yield return exhaustive;
            foreach (var fuzz in FuzzCases(3000)) yield return fuzz;
        }

        public static IEnumerable<object[]> NamedCodes()
        {
            foreach (var edge in EdgeCases()) yield return new object[] { edge.Key };
            foreach (var mainnet in MainnetCorpus.Value) yield return new object[] { mainnet.Key };
        }

        private static byte[] CodeNamed(string name)
        {
            if (EdgeCases().TryGetValue(name, out var edge)) return edge;
            return MainnetCorpus.Value[name];
        }

        private static bool IsPush(byte opcode) => opcode >= Push1 && opcode <= Push32;

        private static bool ReferenceImmediateIsValid(byte opcode, byte immediate)
        {
            if (opcode == Exchange) return immediate <= 81 || immediate >= 128;
            return immediate <= 90 || immediate >= 128;
        }

        private sealed class ReferenceInstruction
        {
            public int Pc;
            public byte Opcode;
            public byte[] Arguments;
            public int ImmediateLength;
        }

        private static List<ReferenceInstruction> ReferenceWalk(byte[] code, bool eip8024)
        {
            var walk = new List<ReferenceInstruction>();
            var pc = 0;
            while (pc < code.Length)
            {
                var opcode = code[pc];
                var immediateLength = 0;
                byte[] arguments = null;
                if (IsPush(opcode))
                {
                    immediateLength = opcode - Push0;
                    arguments = new byte[immediateLength];
                    for (var k = 0; k < immediateLength && pc + 1 + k < code.Length; k++)
                        arguments[k] = code[pc + 1 + k];
                }
                else if (eip8024 && (opcode == Dupn || opcode == Swapn || opcode == Exchange))
                {
                    var immediate = pc + 1 < code.Length ? code[pc + 1] : (byte)0;
                    arguments = new[] { immediate };
                    immediateLength = ReferenceImmediateIsValid(opcode, immediate) ? 1 : 0;
                }
                walk.Add(new ReferenceInstruction { Pc = pc, Opcode = opcode, Arguments = arguments, ImmediateLength = immediateLength });
                pc += 1 + immediateLength;
            }
            return walk;
        }

        private static HashSet<int> ReferenceJumpDestinations(byte[] code)
        {
            var isPushData = new bool[code.Length];
            var pc = 0;
            while (pc < code.Length)
            {
                var opcode = code[pc];
                if (IsPush(opcode))
                {
                    var length = opcode - Push0;
                    for (var k = 1; k <= length && pc + k < code.Length; k++) isPushData[pc + k] = true;
                    pc += length + 1;
                }
                else
                {
                    pc++;
                }
            }
            var destinations = new HashSet<int>();
            for (var i = 0; i < code.Length; i++)
                if (code[i] == Jumpdest && !isPushData[i]) destinations.Add(i);
            return destinations;
        }

        private static HashSet<int> DecodedJumpDestinations(byte[] code, bool eip8024)
        {
            return ProgramInstructionsUtils.GetProgramInstructions(code, eip8024)
                .Where(i => i.Instruction == Instruction.JUMPDEST)
                .Select(i => i.Step)
                .ToHashSet();
        }

        private static void AssertDecodedMatchesReference(string name, byte[] code, bool eip8024)
        {
            var expected = ReferenceWalk(code, eip8024);
            var actual = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024);

            Assert.True(expected.Count == actual.Count, $"{name} eip8024={eip8024}: expected {expected.Count} instructions, decoded {actual.Count}");
            for (var i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];
                Assert.True(e.Pc == a.Step, $"{name} eip8024={eip8024} #{i}: pc {e.Pc} vs {a.Step}");
                Assert.True(e.Opcode == a.Value, $"{name} eip8024={eip8024} #{i}: opcode {e.Opcode:x2} vs {a.Value:x2}");
                Assert.True(Enum.IsDefined(typeof(Instruction), (int)e.Opcode) == a.Instruction.HasValue,
                    $"{name} eip8024={eip8024} #{i}: opcode {e.Opcode:x2} defined-ness");
                if (a.Instruction.HasValue)
                    Assert.True((int)a.Instruction.Value == e.Opcode, $"{name} eip8024={eip8024} #{i}: enum value");
                if (e.Arguments == null)
                    Assert.True(a.Arguments == null, $"{name} eip8024={eip8024} #{i}: unexpected arguments");
                else
                    Assert.True(a.Arguments != null && e.Arguments.SequenceEqual(a.Arguments),
                        $"{name} eip8024={eip8024} #{i}: arguments {(a.Arguments == null ? "null" : a.Arguments.ToHex())} vs {e.Arguments.ToHex()}");
            }
        }

        private static bool ProgramAcceptsJump(byte[] code, int destination, bool eip8024)
        {
            var program = new Program(code);
            if (eip8024) program.ApplyEip8024Decoding();
            try
            {
                program.GoToJumpDestination(destination);
            }
            catch (Exception)
            {
                return false;
            }
            return program.HaltReason == EvmHaltReason.None;
        }

        [Theory]
        [MemberData(nameof(NamedCodes))]
        public void Given_a_named_code_When_decoded_Then_instruction_boundaries_arguments_and_opcodes_match_the_reference_walk(string name)
        {
            var code = CodeNamed(name);
            AssertDecodedMatchesReference(name, code, eip8024: false);
            AssertDecodedMatchesReference(name, code, eip8024: true);
        }

        [Theory]
        [MemberData(nameof(NamedCodes))]
        public void Given_a_named_code_When_decoded_Then_the_jumpdest_set_is_the_push_only_reference_set_with_and_without_eip8024(string name)
        {
            var code = CodeNamed(name);
            var reference = ReferenceJumpDestinations(code);
            Assert.True(reference.SetEquals(DecodedJumpDestinations(code, eip8024: false)), $"{name}: eip8024 off");
            Assert.True(reference.SetEquals(DecodedJumpDestinations(code, eip8024: true)), $"{name}: eip8024 on");
        }

        [Fact]
        public void Given_the_exhaustive_eip8024_and_fuzz_corpora_When_decoded_Then_boundaries_match_the_reference_walk()
        {
            var inspected = 0;
            foreach (var entry in ExhaustiveEip8024Cases().Concat(FuzzCases(3000)))
            {
                AssertDecodedMatchesReference(entry.Key, entry.Value, eip8024: false);
                AssertDecodedMatchesReference(entry.Key, entry.Value, eip8024: true);
                inspected++;
            }
            Assert.Equal(3 * 256 * 5 + 3000, inspected);
        }

        [Fact]
        public void Given_every_corpus_code_When_the_jumpdest_set_is_taken_with_and_without_eip8024_Then_the_sets_are_identical_and_equal_the_push_only_reference()
        {
            var inspected = 0;
            foreach (var entry in EveryCorpus())
            {
                var code = entry.Value;
                var reference = ReferenceJumpDestinations(code);
                var withoutEip8024 = DecodedJumpDestinations(code, eip8024: false);
                var withEip8024 = DecodedJumpDestinations(code, eip8024: true);
                Assert.True(withoutEip8024.SetEquals(withEip8024), $"{entry.Key}: JUMPDEST set differs between eip8024 off and on");
                Assert.True(reference.SetEquals(withEip8024), $"{entry.Key}: JUMPDEST set differs from the push-only reference");
                inspected++;
            }
            Assert.True(inspected > 4000);
        }

        [Fact]
        public void Given_codes_where_eip8024_swallows_an_immediate_When_decoded_Then_instruction_boundaries_differ_but_the_jumpdest_set_does_not()
        {
            var code = new byte[] { Dupn, Dupn, Jumpdest };
            var off = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024Enabled: false);
            var on = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024Enabled: true);
            Assert.Equal(new[] { 0, 1, 2 }, off.Select(i => i.Step).ToArray());
            Assert.Equal(new[] { 0, 2 }, on.Select(i => i.Step).ToArray());
            Assert.Equal(new[] { 2 }, DecodedJumpDestinations(code, false).ToArray());
            Assert.Equal(new[] { 2 }, DecodedJumpDestinations(code, true).ToArray());
        }

        [Fact]
        public void Given_every_corpus_code_When_a_jump_targets_each_jumpdest_byte_Then_program_accepts_exactly_the_reference_destinations_with_and_without_eip8024()
        {
            foreach (var entry in EdgeCases().Concat(MainnetCorpus.Value).Concat(FuzzCases(300)))
            {
                var code = entry.Value;
                var reference = ReferenceJumpDestinations(code);
                var candidates = Enumerable.Range(0, code.Length).Where(pc => code[pc] == Jumpdest).ToList();
                candidates.Add(code.Length);
                candidates.Add(code.Length + 1);
                foreach (var candidate in candidates)
                {
                    var expected = reference.Contains(candidate);
                    Assert.True(expected == ProgramAcceptsJump(code, candidate, eip8024: false), $"{entry.Key}: jump to {candidate} eip8024 off");
                    Assert.True(expected == ProgramAcceptsJump(code, candidate, eip8024: true), $"{entry.Key}: jump to {candidate} eip8024 on");
                }
            }
        }

        [Fact]
        public void Given_a_non_jumpdest_target_When_program_jumps_Then_it_is_refused()
        {
            var code = new byte[] { Push1, 0x01, Stop, Jumpdest };
            Assert.False(ProgramAcceptsJump(code, 1, false));
            Assert.False(ProgramAcceptsJump(code, 2, false));
            Assert.True(ProgramAcceptsJump(code, 3, false));
        }

        [Fact]
        public void Given_every_byte_value_When_decoded_alone_Then_only_defined_opcodes_carry_an_instruction()
        {
            for (var value = 0; value < 256; value++)
            {
                var code = new[] { (byte)value };
                var instructions = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024Enabled: false);
                Assert.Single(instructions);
                Assert.Equal((byte)value, instructions[0].Value);
                Assert.Equal(0, instructions[0].Step);
                Assert.Equal(Enum.IsDefined(typeof(Instruction), value), instructions[0].Instruction.HasValue);
            }
        }

        [Fact]
        public void Given_each_push_width_and_each_count_of_available_data_bytes_When_decoded_Then_the_data_is_right_zero_padded_and_the_next_instruction_starts_after_the_declared_width()
        {
            for (var width = 1; width <= 32; width++)
            {
                for (var available = 0; available <= width + 1; available++)
                {
                    var code = new[] { (byte)(Push0 + width) }
                        .Concat(Enumerable.Range(1, available).Select(i => (byte)(0xa0 + i)))
                        .ToArray();
                    var instructions = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024Enabled: false);
                    Assert.True(available >= width + 1 ? instructions.Count == 2 : instructions.Count == 1, $"PUSH{width}/{available}");
                    var expected = new byte[width];
                    for (var k = 0; k < Math.Min(width, available); k++) expected[k] = (byte)(0xa0 + k + 1);
                    Assert.Equal(expected, instructions[0].Arguments);
                    if (instructions.Count == 2) Assert.Equal(width + 1, instructions[1].Step);
                }
            }
        }

        [Theory]
        [InlineData(Dupn)]
        [InlineData(Swapn)]
        [InlineData(Exchange)]
        public void Given_every_immediate_byte_of_an_eip8024_opcode_When_decoded_Then_a_valid_immediate_is_swallowed_and_an_invalid_one_becomes_its_own_instruction(byte opcode)
        {
            for (var immediate = 0; immediate < 256; immediate++)
            {
                var code = new[] { opcode, (byte)immediate, Jumpdest };
                var valid = ReferenceImmediateIsValid(opcode, (byte)immediate);

                var on = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024Enabled: true);
                Assert.Equal(ReferenceWalk(code, true).Select(i => i.Pc).ToArray(), on.Select(i => i.Step).ToArray());
                Assert.Equal(valid, on.All(i => i.Step != 1));
                Assert.Equal(new[] { (byte)immediate }, on[0].Arguments);

                var off = ProgramInstructionsUtils.GetProgramInstructions(code, eip8024Enabled: false);
                Assert.Equal(ReferenceWalk(code, false).Select(i => i.Pc).ToArray(), off.Select(i => i.Step).ToArray());
                Assert.Contains(off, i => i.Step == 1);
                Assert.Null(off[0].Arguments);
            }
        }

        [Theory]
        [InlineData(Dupn)]
        [InlineData(Swapn)]
        [InlineData(Exchange)]
        public void Given_an_eip8024_opcode_at_the_end_of_code_When_decoded_Then_the_missing_immediate_reads_as_zero_and_nothing_follows(byte opcode)
        {
            var instructions = ProgramInstructionsUtils.GetProgramInstructions(new[] { opcode }, eip8024Enabled: true);
            Assert.Single(instructions);
            Assert.Equal(new byte[] { 0 }, instructions[0].Arguments);
        }

        [Fact]
        public void Given_the_corpus_When_it_is_loaded_Then_it_contains_the_mainnet_contracts_and_the_edge_cases()
        {
            Assert.True(MainnetCorpus.Value.Count >= 9);
            Assert.True(MainnetCorpus.Value.Values.Any(code => code.Length > 20_000));
            Assert.True(EdgeCases().Count >= 20);
        }
    }
}
