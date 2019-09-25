using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetStorageAtTester : RPCRequestTester<string>
    {
        public EthGetStorageAtTester() : base(TestSettingsCategory.hostedTestNet)
        {

        }

        [Fact]
        public async void ShouldReturnStorage()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            //cannot verify it will be the same content but the lenght yes
            Assert.True(result.Length == "0x4d756c7469706c69657200000000000000000000000000000000000000000014".Length);

        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var ethGetStorageAt = new EthGetStorageAt(client);
            return await ethGetStorageAt.SendRequestAsync(Settings.GetContractAddress(), new HexBigInteger(1));
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetStorageAt);
        }
    }
}