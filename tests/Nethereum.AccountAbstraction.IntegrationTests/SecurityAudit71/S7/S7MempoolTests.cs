using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7
{
    public class S7MempoolTests
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

        private static MempoolEntry Entry(string hash, string sender, BigInteger nonce, BigInteger priorityFee, BigInteger maxFee)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = priorityFee,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = nonce,
                    InitCode = Array.Empty<byte>(),
                    CallData = Array.Empty<byte>(),
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(priorityFee, maxFee),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = Array.Empty<byte>()
                }
            };
        }

        private static string Hash(int i) => "0x" + i.ToString("x64");
        private static string Sender(int i) => "0x" + i.ToString("x40");

        [Fact]
        [Trait("Rule", "S7-3")]
        [Trait("Erc", "ERC-4337 replacement")]
        public async Task ReplaceByFee_WhenBumpIsBelowTenPercentBecauseOfFloorRounding_IsRejected()
        {
            var mempool = new InMemoryUserOpMempool(100);
            Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(1), Sender(1), 0, 15, 15)));

            var outcome = await mempool.AddAsync(Entry(Hash(2), Sender(1), 0, 16, 16));

            Assert.Equal(MempoolAddOutcome.RejectedUnderpriced, outcome);
        }

        [Fact]
        [Trait("Rule", "S7-3")]
        [Trait("Erc", "ERC-4337 replacement")]
        public async Task ReplaceByFee_WhenBothFeesAreZeroAndUnchanged_IsRejected()
        {
            var mempool = new InMemoryUserOpMempool(100);
            Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(1), Sender(1), 0, 0, 0)));

            var outcome = await mempool.AddAsync(Entry(Hash(2), Sender(1), 0, 0, 0));

            Assert.Equal(MempoolAddOutcome.RejectedUnderpriced, outcome);
        }

        [Fact]
        [Trait("Rule", "S7-4")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task AddAsync_WhenMempoolIsFullOfLowFeeOps_AHigherPayingOpIsStillAdmitted()
        {
            var mempool = new InMemoryUserOpMempool(3);
            for (var i = 1; i <= 3; i++)
            {
                Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(i), Sender(i), 0, 1, 1)));
            }

            var outcome = await mempool.AddAsync(Entry(Hash(99), Sender(99), 0, 1_000_000_000, 2_000_000_000));

            Assert.True(
                outcome == MempoolAddOutcome.Added,
                $"a full mempool of minimum-fee ops locked out a 1000x higher paying op: {outcome}");
        }

        [Fact]
        [Trait("Rule", "S7-4")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task AddAsync_WhenMempoolHoldsFailedAndIncludedOps_TheyDoNotConsumePendingCapacity()
        {
            var mempool = new InMemoryUserOpMempool(2);
            Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(1), Sender(1), 0, 1, 1)));
            Assert.Equal(MempoolAddOutcome.Added, await mempool.AddAsync(Entry(Hash(2), Sender(2), 0, 1, 1)));
            await mempool.MarkFailedAsync(new[] { Hash(1), Hash(2) }, "AA24");

            var outcome = await mempool.AddAsync(Entry(Hash(3), Sender(3), 0, 1, 1));

            Assert.Equal(MempoolAddOutcome.Added, outcome);
        }

        [Fact]
        [Trait("Rule", "S7-5")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task GetAsync_WithUpperCaseHashOfKnownOp_FindsTheSameEntryAsLowerCase()
        {
            var mempool = new InMemoryUserOpMempool(10);
            var hash = "0x" + new string('a', 63) + "b";
            await mempool.AddAsync(Entry(hash, Sender(1), 0, 1, 1));

            var viaUpper = await mempool.GetAsync("0x" + hash.Substring(2).ToUpperInvariant());

            Assert.NotNull(viaUpper);
        }
    }
}
