using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class GenesisStateRootIntegrationTests
    {
        private static readonly (string Address, string BalanceHex)[] GenesisAlloc = new[]
        {
            ("0x12890d2cce102216644c59daE5baed380d84830c", "0x900000000000000000000"),
            ("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", "0x9000000000000000000000000000000"),
            ("0xE65B318b9dECf504d1cb6Ea5C367Ca657a070Db1", "0x1000000000000000000000000000000")
        };

        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public GenesisStateRootIntegrationTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task SeedAllocIntoStateStore_ComputedRootMatchesGenesisHeader()
        {
            var stateStore = new InMemoryStateStore();

            foreach (var (address, balanceHex) in GenesisAlloc)
            {
                var balance = new HexBigInteger(balanceHex).Value;
                await stateStore.SaveAccountAsync(address, new Account
                {
                    Nonce = EvmUInt256.Zero,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = DefaultValues.EMPTY_DATA_HASH
                });
            }

            var calculator = new IncrementalStateRootCalculator(stateStore);
            var computed = await calculator.ComputeStateRootAsync();

            var expected = _fixture.GenesisStateRoot;
            _output.WriteLine($"Expected (Geth header): {expected.ToHex(true)}");
            _output.WriteLine($"Computed (seeded): {computed.ToHex(true)}");

            Assert.Equal(expected.ToHex(), computed.ToHex());
        }
    }
}
