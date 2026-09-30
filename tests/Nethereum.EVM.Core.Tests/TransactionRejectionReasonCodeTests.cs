using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class TransactionRejectionReasonCodeTests
    {
        private const string SenderAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string RecipientAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static readonly HardforkConfig PragueWithPrecompiles =
            HardforkConfig.Prague.WithPrecompiles(DefaultPrecompileRegistries.PragueBase());

        [Fact]
        [Trait("Rule", "AMS-REJ-05")]
        public void Given_ATransactionBelowTheIntrinsicFloor_When_Executed_Then_TheResultCarriesIntrinsicGasTooLow()
        {
            var data = new byte[100];
            for (var i = 0; i < data.Length; i++) data[i] = 0xFF;

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [RecipientAddress] = new AccountState { Code = new byte[0] }
            };
            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = SenderAddress,
                To = RecipientAddress,
                Data = data,
                GasLimit = new EvmUInt256(23_000),
                Value = EvmUInt256.Zero,
                GasPrice = 0,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ExecutionState = new ExecutionStateService(new InMemoryStateReader(accounts))
            };

            var result = new TransactionExecutor(PragueWithPrecompiles).Execute(ctx);

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.IntrinsicGasTooLow, result.ErrorCode);
            Assert.Contains("Intrinsic gas too low", result.Error);
        }

        [Fact]
        [Trait("Rule", "AMS-REJ-05")]
        public void Given_EveryTransactionValidationThrowSite_Then_EachCarriesADistinctReasonCode()
        {
            var sites = DiscoverThrowSites();

            Assert.True(
                sites.Count >= 20,
                $"IL walk found only {sites.Count} TransactionValidationException construction sites — " +
                "it is no longer discovering them, so the assertions below prove nothing.");

            var uncoded = sites.Where(s => s.Reason == TransactionError.None).ToList();
            Assert.True(
                uncoded.Count == 0,
                "Throw sites carrying no reason code: " +
                string.Join(", ", uncoded.Select(s => $"{s.Method} (\"{s.Label}\")")));

            var labelsWithSeveralCodes = sites
                .GroupBy(s => s.Label)
                .Where(g => g.Select(s => s.Reason).Distinct().Count() > 1)
                .ToList();
            Assert.True(
                labelsWithSeveralCodes.Count == 0,
                "Labels mapped to more than one reason code: " +
                string.Join(", ", labelsWithSeveralCodes.Select(g =>
                    $"{g.Key} -> {string.Join("/", g.Select(s => s.Reason).Distinct())}")));

            var codesWithSeveralLabels = sites
                .GroupBy(s => s.Reason)
                .Where(g => g.Select(s => s.Label).Distinct().Count() > 1)
                .ToList();
            Assert.True(
                codesWithSeveralLabels.Count == 0,
                "Reason codes shared by more than one label — the code no longer discriminates: " +
                string.Join(", ", codesWithSeveralLabels.Select(g =>
                    $"{g.Key} <- {string.Join("/", g.Select(s => s.Label).Distinct())}")));
        }

        private readonly struct ThrowSite
        {
            public ThrowSite(string method, TransactionError reason, string label)
            {
                Method = method;
                Reason = reason;
                Label = label;
            }

            public string Method { get; }
            public TransactionError Reason { get; }
            public string Label { get; }
        }

        private static List<ThrowSite> DiscoverThrowSites()
        {
            var assembly = typeof(TransactionValidationException).Assembly;
            var constructors = typeof(TransactionValidationException)
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(c => c.MetadataToken)
                .ToHashSet();

            var sites = new List<ThrowSite>();
            foreach (var type in GetLoadableTypes(assembly))
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.DeclaredOnly;
                var methods = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
                foreach (var method in methods)
                    CollectSites(method, constructors, sites);
            }
            return sites;
        }

        private static void CollectSites(MethodBase method, HashSet<int> constructors, List<ThrowSite> sites)
        {
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception)
            {
                return;
            }
            if (il == null) return;

            var module = method.Module;
            var typeArgs = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericArguments()
                : null;

            int? pendingInt = null;
            string pendingString = null;

            var pos = 0;
            while (pos < il.Length)
            {
                var opcode = ReadOpCode(il, ref pos);
                if (opcode == null) return;
                var operandStart = pos;
                pos += OperandSize(opcode.Value, il, operandStart);
                if (pos > il.Length) return;

                var value = opcode.Value.Value;
                if (value >= OpCodes.Ldc_I4_0.Value && value <= OpCodes.Ldc_I4_8.Value)
                    pendingInt = value - OpCodes.Ldc_I4_0.Value;
                else if (value == OpCodes.Ldc_I4_M1.Value)
                    pendingInt = -1;
                else if (value == OpCodes.Ldc_I4_S.Value)
                    pendingInt = (sbyte)il[operandStart];
                else if (value == OpCodes.Ldc_I4.Value)
                    pendingInt = BitConverter.ToInt32(il, operandStart);
                else if (value == OpCodes.Ldstr.Value)
                    pendingString = SafeResolveString(module, BitConverter.ToInt32(il, operandStart));
                else if (value == OpCodes.Newobj.Value)
                {
                    var token = BitConverter.ToInt32(il, operandStart);
                    if (IsTargetConstructor(module, token, typeArgs, constructors))
                    {
                        sites.Add(new ThrowSite(
                            $"{method.DeclaringType?.FullName}.{method.Name}",
                            pendingInt.HasValue ? (TransactionError)pendingInt.Value : TransactionError.None,
                            pendingString ?? "<no literal>"));
                    }
                    pendingInt = null;
                    pendingString = null;
                }
            }
        }

        private static bool IsTargetConstructor(Module module, int token, Type[] typeArgs, HashSet<int> constructors)
        {
            try
            {
                var resolved = module.ResolveMethod(token, typeArgs, null);
                return resolved != null && constructors.Contains(resolved.MetadataToken);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string SafeResolveString(Module module, int token)
        {
            try
            {
                return module.ResolveString(token);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly Dictionary<short, OpCode> OpCodeByValue = BuildOpCodeTable();

        private static Dictionary<short, OpCode> BuildOpCodeTable()
        {
            var table = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(OpCode)) continue;
                var opcode = (OpCode)field.GetValue(null);
                table[opcode.Value] = opcode;
            }
            return table;
        }

        private static OpCode? ReadOpCode(byte[] il, ref int pos)
        {
            var first = il[pos];
            short key;
            if (first == 0xFE && pos + 1 < il.Length)
            {
                key = unchecked((short)(0xFE00 | il[pos + 1]));
                pos += 2;
            }
            else
            {
                key = first;
                pos += 1;
            }
            return OpCodeByValue.TryGetValue(key, out var opcode) ? opcode : (OpCode?)null;
        }

        private static int OperandSize(OpCode opcode, byte[] il, int operandStart)
        {
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + 4 * BitConverter.ToInt32(il, operandStart);
                default:
                    return int.MaxValue;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
