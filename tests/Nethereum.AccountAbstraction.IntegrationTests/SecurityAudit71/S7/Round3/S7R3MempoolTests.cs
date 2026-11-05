using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3
{
    public class S7R3MempoolTests
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

        private static MempoolEntry Entry(string hash)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = BigInteger.One,
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x1111111111111111111111111111111111111111",
                    Nonce = BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = Array.Empty<byte>(),
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(1, 2),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = Array.Empty<byte>()
                }
            };
        }

        [Fact]
        [Trait("Rule", "S7-R3-12")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationByHash")]
        public async Task PruneAsync_WhenAnOpWaitedLongerThanTheRetentionBeforeItWasIncluded_KeepsItForTheRetentionAfterInclusion()
        {
            var mempool = new InMemoryUserOpMempool(100);
            var hash = "0x" + new string('a', 64);
            var entry = Entry(hash);
            await mempool.AddAsync(entry);
            entry.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            await mempool.MarkIncludedAsync(new[] { hash }, "0x" + new string('b', 64), 7);

            var pruned = await mempool.PruneAsync();

            Assert.Equal(0, pruned);
            Assert.NotNull(await mempool.GetAsync(hash));
        }

        [Fact]
        [Trait("Rule", "S7-R3-12")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationByHash")]
        public async Task PruneAsync_WhenAnOpWaitedLongerThanTheRetentionBeforeItFailed_KeepsItForTheRetentionAfterTheFailure()
        {
            var mempool = new InMemoryUserOpMempool(100);
            var hash = "0x" + new string('c', 64);
            var entry = Entry(hash);
            await mempool.AddAsync(entry);
            entry.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            await mempool.MarkFailedAsync(new[] { hash }, "AA25");

            var pruned = await mempool.PruneAsync();

            Assert.Equal(0, pruned);
            Assert.NotNull(await mempool.GetAsync(hash));
        }
    }
}
