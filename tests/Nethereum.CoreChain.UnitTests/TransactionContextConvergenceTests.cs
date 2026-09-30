using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class TransactionContextConvergenceTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Recipient = "0x3c44cdddb6a900fa2b585dd299e03d12fa4293bc";
        private const string AccessListAddress = "0x70997970c51812dc3a010c7d01b50e0d17dc79c8";
        private const string InitCode = "0x60806040";
        private const long GasPrice = 1000000000;
        private const long MaxPriorityFee = 2000000000;
        private const long MaxFee = 5000000000;
        private static readonly BigInteger ChainId = 1337;
        private static readonly string Sender = new EthECKey(PrivateKey).GetPublicAddress();

        private const long BlockNumber = 21000000;
        private const long Timestamp = 1700000000;
        private const long BaseFee = 7;
        private const long BlockGasLimit = 30000000;
        private const long ExcessBlobGasValue = 786432;
        private const ulong SlotNumberValue = 12345;
        private const string Coinbase = "0x0000000000000000000000000000000000000042";

        private static BlockWitnessData WitnessBlock(byte[] mixHash = null) => new BlockWitnessData
        {
            BlockNumber = BlockNumber,
            Timestamp = Timestamp,
            BaseFee = BaseFee,
            BlockGasLimit = BlockGasLimit,
            ChainId = (long)ChainId,
            Coinbase = Coinbase,
            MixHash = mixHash,
            ExcessBlobGas = ExcessBlobGasValue,
            SlotNumber = SlotNumberValue
        };

        private static BlockContext FollowerBlock() => new BlockContext
        {
            BlockNumber = BlockNumber,
            Timestamp = Timestamp,
            BaseFee = BaseFee,
            GasLimit = BlockGasLimit,
            ChainId = ChainId,
            Coinbase = Coinbase,
            Difficulty = BigInteger.Zero,
            ExcessBlobGas = ExcessBlobGasValue,
            SlotNumber = SlotNumberValue
        };


        private static TransactionExecutionContext MapThroughTheWitnessBlockEnvironment(
            byte[] rlpEncoded, BlockWitnessData block)
            => TransactionContextFactory.FromBlockWitnessTransaction(
                new BlockWitnessTransaction { From = Sender, RlpEncoded = rlpEncoded }, block, null);

        private static TransactionExecutionContext MapThroughTheFollowerBlockEnvironment(
            byte[] rlpEncoded, BlockContext block)
            => TransactionContextFactory.From(
                TransactionFactory.CreateTransaction(rlpEncoded), Sender, block, null);


        private const int MaxCompositeDepth = 3;

        private static SortedDictionary<string, string> Fields(TransactionExecutionContext ctx)
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in ReadableProperties(typeof(TransactionExecutionContext)))
                fields[property.Name] = Render(property.GetValue(ctx), 0);
            return fields;
        }

        private static IEnumerable<PropertyInfo> ReadableProperties(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                   .OrderBy(property => property.Name, StringComparer.Ordinal);

        private static string Render(object value, int depth)
        {
            if (value == null) return "null";
            if (value is string text) return text;
            if (value is byte[] bytes) return bytes.ToHex(true);
            if (value is IEnumerable items) return "[" + string.Join(", ", RenderEach(items, depth)) + "]";
            if (value.GetType().IsValueType) return value.ToString();
            if (depth >= MaxCompositeDepth) return "<" + value.GetType().Name + ">";
            return "{" + string.Join(", ", RenderMembers(value, depth + 1)) + "}";
        }

        private static IEnumerable<string> RenderEach(IEnumerable items, int depth)
        {
            foreach (var item in items) yield return Render(item, depth);
        }

        private static IEnumerable<string> RenderMembers(object value, int depth)
        {
            foreach (var property in ReadableProperties(value.GetType()))
                yield return property.Name + "=" + Render(property.GetValue(value), depth);
        }

        private static List<string> DifferingFields(
            TransactionExecutionContext witness, TransactionExecutionContext follower)
        {
            var mappedByWitness = Fields(witness);
            var mappedByFollower = Fields(follower);
            return mappedByWitness.Keys
                .Where(name => mappedByWitness[name] != mappedByFollower[name])
                .ToList();
        }

        private static string Describe(
            string field, TransactionExecutionContext witness, TransactionExecutionContext follower)
            => field + ": witness=" + Fields(witness)[field] + " follower=" + Fields(follower)[field];


        public sealed class MappingCase
        {
            public string Name { get; set; }
            public byte[] RlpEncoded { get; set; }
        }

        private static readonly Dictionary<string, MappingCase> Cases = BuildCases();

        public static IEnumerable<object[]> CaseNames => Cases.Keys.Select(name => new object[] { name });

        private static Dictionary<string, MappingCase> BuildCases()
        {
            var cases = new List<MappingCase>();
            foreach (var creation in new[] { false, true })
            {
                cases.Add(Case("LegacyTransaction", creation, null, SignLegacy(creation)));
                cases.Add(Case("LegacyTransactionChainId", creation, null, SignLegacyChainId(creation)));
                foreach (var withAccessList in new[] { false, true })
                {
                    cases.Add(Case("Transaction2930", creation, withAccessList, Sign2930(creation, withAccessList)));
                    cases.Add(Case("Transaction1559", creation, withAccessList, Sign1559(creation, withAccessList)));
                    cases.Add(Case("Transaction4844", creation, withAccessList, Sign4844(creation, withAccessList)));
                    cases.Add(Case("Transaction7702", creation, withAccessList, Sign7702(creation, withAccessList)));
                }
            }
            return cases.ToDictionary(mappingCase => mappingCase.Name, StringComparer.Ordinal);
        }

        private static MappingCase Case(
            string type, bool creation, bool? withAccessList, byte[] rlpEncoded)
            => new MappingCase
            {
                Name = type + "/" + (creation ? "creation" : "call") + AccessListSuffix(withAccessList),
                RlpEncoded = rlpEncoded
            };

        private static string AccessListSuffix(bool? withAccessList)
            => withAccessList == null ? "" : withAccessList.Value ? "/access-list" : "/no-access-list";

        private static string To(bool creation) => creation ? "" : Recipient;
        private static string Calldata(bool creation) => creation ? InitCode : "";

        private static List<AccessListItem> AccessList(bool withAccessList) => withAccessList
            ? new List<AccessListItem> { new AccessListItem(AccessListAddress, new List<byte[]> { new byte[32] }) }
            : new List<AccessListItem>();

        private static byte[] SignLegacy(bool creation)
            => new LegacyTransactionSigner().SignTransaction(
                PrivateKey.HexToByteArray(),
                new LegacyTransaction(To(creation), 1000, 3, GasPrice, 100000, Calldata(creation)))
               .HexToByteArray();

        private static byte[] SignLegacyChainId(bool creation)
            => new LegacyTransactionSigner().SignTransaction(
                PrivateKey.HexToByteArray(),
                new LegacyTransactionChainId(To(creation), 1000, 3, GasPrice, 100000, Calldata(creation), ChainId))
               .HexToByteArray();

        private static byte[] Sign2930(bool creation, bool withAccessList)
            => new TypeTransactionSigner<Transaction2930>().SignTransaction(
                PrivateKey,
                new Transaction2930(ChainId, 3, GasPrice, 100000,
                    To(creation), 1000, Calldata(creation), AccessList(withAccessList)))
               .HexToByteArray();

        private static byte[] Sign1559(bool creation, bool withAccessList)
            => new TypeTransactionSigner<Transaction1559>().SignTransaction(
                PrivateKey,
                new Transaction1559(ChainId, 3, MaxPriorityFee, MaxFee, 100000,
                    To(creation), 1000, Calldata(creation), AccessList(withAccessList)))
               .HexToByteArray();

        private static byte[] Sign4844(bool creation, bool withAccessList)
            => new TypeTransactionSigner<Transaction4844>().SignTransaction(
                PrivateKey,
                new Transaction4844(ChainId, 3, MaxPriorityFee, MaxFee, 100000,
                    To(creation), 1000, Calldata(creation), AccessList(withAccessList),
                    1000000, new List<byte[]> { VersionedHash() }))
               .HexToByteArray();

        private static byte[] Sign7702(bool creation, bool withAccessList)
            => new TypeTransactionSigner<Transaction7702>().SignTransaction(
                PrivateKey,
                new Transaction7702(ChainId, 3, MaxPriorityFee, MaxFee, 100000,
                    To(creation), 1000, Calldata(creation), AccessList(withAccessList),
                    new List<Authorisation7702Signed> { Authorisation() }))
               .HexToByteArray();

        private static byte[] VersionedHash()
        {
            var hash = new byte[32];
            hash[0] = 0x01;
            hash[31] = 0x07;
            return hash;
        }

        private static Authorisation7702Signed Authorisation()
        {
            var r = new byte[32];
            r[31] = 0x11;
            var s = new byte[32];
            s[31] = 0x22;
            return new Authorisation7702Signed(ChainId, AccessListAddress, 1, r, s, new byte[] { 0 });
        }

        private static void Map(
            string caseName,
            out MappingCase mappingCase,
            out TransactionExecutionContext witness,
            out TransactionExecutionContext follower)
        {
            mappingCase = Cases[caseName];
            witness = MapThroughTheWitnessBlockEnvironment(mappingCase.RlpEncoded, WitnessBlock());
            follower = MapThroughTheFollowerBlockEnvironment(mappingCase.RlpEncoded, FollowerBlock());
        }


        [Theory]
        [MemberData(nameof(CaseNames))]
        public void Given_TheSameRlpTransaction_When_MappedThroughEachBlockEnvironment_Then_EveryContextFieldIsIdentical(
            string caseName)
        {
            Map(caseName, out _, out var witness, out var follower);

            var differing = DifferingFields(witness, follower)
                .Select(field => Describe(field, witness, follower))
                .ToList();

            Assert.Equal(new List<string>(), differing);
        }


        [Fact]
        public void Given_AContractCreation_When_MappedThroughEitherBlockEnvironment_Then_NeitherCarriesARecipient()
        {
            Map("Transaction1559/creation/no-access-list", out _, out var witness, out var follower);

            Assert.Null(witness.To);
            Assert.Null(follower.To);
            Assert.True(witness.IsContractCreation);
        }

        [Fact]
        public void Given_ATransactionWithNoCalldata_When_MappedThroughEitherBlockEnvironment_Then_BothCarryAnEmptyByteString()
        {
            Map("Transaction1559/call/no-access-list", out _, out var witness, out var follower);

            Assert.Empty(witness.Data);
            Assert.NotNull(follower.Data);
            Assert.Empty(follower.Data);
        }

        [Fact]
        public void Given_ALegacyTransaction_When_MappedThroughEitherBlockEnvironment_Then_NeitherCarriesAMaxFeePerGas()
        {
            Map("LegacyTransaction/call", out _, out var witness, out var follower);

            Assert.Equal(EvmUInt256.Zero, witness.MaxFeePerGas);
            Assert.Equal(EvmUInt256.Zero, follower.MaxFeePerGas);
            Assert.False(follower.IsEip1559);
            Assert.Equal(new EvmUInt256(GasPrice), follower.GasPrice);
        }

        [Fact]
        public void Given_AnEip2930Transaction_When_MappedThroughEitherBlockEnvironment_Then_NeitherCarriesAMaxFeePerGas()
        {
            Map("Transaction2930/call/access-list", out _, out var witness, out var follower);

            Assert.Equal(EvmUInt256.Zero, witness.MaxFeePerGas);
            Assert.Equal(EvmUInt256.Zero, follower.MaxFeePerGas);
            Assert.False(follower.IsEip1559);
            Assert.Equal(new EvmUInt256(GasPrice), follower.GasPrice);
        }

        /// <summary>
        /// EIP-4399: <i>"the DIFFICULTY opcode ... returns the value of the
        /// prevRandao field"</i>. The witness mapping decides that from the
        /// difficulty VALUE, inside the mapping; the follower's copy is handed a
        /// block context whose difficulty was already resolved outside it, by a
        /// fork ordinal in <c>BlockExecutor.BuildBlockContext</c>. Row
        /// <c>AMS-FORK-07</c> owns the gap and the rule it must settle on.
        /// </summary>
        [Fact]
        public void Given_APostMergeHeader_When_MappedThroughEitherBlockEnvironment_Then_OnlyTheWitnessMappingSubstitutesPrevRandao_SeeAmsFork07()
        {
            var prevRandao = new byte[32];
            prevRandao[31] = 0x5a;
            var mappingCase = Cases["Transaction1559/call/no-access-list"];

            var witness = MapThroughTheWitnessBlockEnvironment(mappingCase.RlpEncoded, WitnessBlock(prevRandao));
            var follower = MapThroughTheFollowerBlockEnvironment(mappingCase.RlpEncoded, FollowerBlock());

            Assert.Equal(EvmUInt256.FromBigEndian(prevRandao), witness.Difficulty);
            Assert.Equal(EvmUInt256.Zero, follower.Difficulty);
        }


        [Fact]
        public void Given_AnEmptyAccessList_When_TheRlpIsDecoded_Then_NeitherBlockEnvironmentCarriesOne_SoTheReadInventorysGapDoesNotReachThisPath()
        {
            Map("Transaction1559/call/no-access-list", out _, out var witness, out var follower);

            Assert.Null(witness.AccessList);
            Assert.Null(follower.AccessList);
        }

        [Fact]
        public void Given_ALegacyTransaction_When_MappedThroughEitherBlockEnvironment_Then_TheyAgreeOnMaxPriorityFeePerGas_SoTheGapIsMaxFeePerGasAlone()
        {
            Map("LegacyTransaction/call", out _, out var witness, out var follower);

            Assert.Equal(EvmUInt256.Zero, witness.MaxPriorityFeePerGas);
            Assert.Equal(EvmUInt256.Zero, follower.MaxPriorityFeePerGas);
        }


        [Fact]
        public void Given_TheDifferentialInstrument_When_OneMappedFieldIsAltered_Then_ItIsReportedAsDiffering()
        {
            Map("Transaction4844/call/access-list", out _, out var witness, out var follower);

            follower.BlobVersionedHashes = new List<string> { "0xdeadbeef" };

            Assert.Contains("BlobVersionedHashes", DifferingFields(witness, follower));
        }

        [Fact]
        public void Given_TheContextType_When_TheDifferentialRuns_Then_EveryPublicPropertyIsCompared()
        {
            Map("Transaction7702/call/access-list", out _, out var witness, out _);

            var expected = ReadableProperties(typeof(TransactionExecutionContext))
                .Select(property => property.Name)
                .ToList();

            Assert.Equal(expected, Fields(witness).Keys.ToList());
        }
    }
}
