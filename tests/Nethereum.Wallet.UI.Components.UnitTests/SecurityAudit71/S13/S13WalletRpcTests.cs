using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.UI.Components.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Wallet.UI.Components.UnitTests.SecurityAudit71.S13
{
    public class S13WalletRpcTests
    {
        private const string Unauthorized = "4100";

        private static string TypedData(string chainIdJson, string chainType = "uint256") =>
            "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"},{\"name\":\"chainId\",\"type\":\"" + chainType + "\"}]," +
            "\"Mail\":[{\"name\":\"contents\",\"type\":\"string\"}]},\"primaryType\":\"Mail\"," +
            "\"domain\":{\"name\":\"x\",\"chainId\":" + chainIdJson + "},\"message\":{\"contents\":\"hi\"}}";

        private static JObject ChainParam(string chainId, string rpc, string name = "Test Chain") => new JObject
        {
            ["chainId"] = chainId,
            ["chainName"] = name,
            ["rpcUrls"] = new JArray(rpc),
            ["nativeCurrency"] = new JObject { ["name"] = "Ether", ["symbol"] = "ETH", ["decimals"] = 18 },
            ["blockExplorerUrls"] = new JArray("https://explorer.example")
        };

        [Fact]
        public async Task Given_AnotherDappBecomesTheSelectedDappWhileAChainAddIsAwaiting_When_ThePromptIsQueued_Then_ItNamesTheRequestingOrigin()
        {
            using var wallet = await S13Wallet.CreateAsync();
            wallet.SetOrigin(S13Wallet.EvilOrigin);
            var context = new SwitchingContext(wallet.Host, host => host.SelectedDapp = new Nethereum.Wallet.UI.DappConnectionContext { Origin = S13Wallet.GoodOrigin });
            var request = new RpcRequestMessage(1, "wallet_addEthereumChain", ChainParam("0x4d2", "https://rpc.chain.example"));

            var pending = new WalletAddEthereumChainHandler().HandleAsync(request, context);
            await Task.WhenAny(pending, Task.Delay(1000));
            var prompt = wallet.Queue.PendingPrompts.Single();
            await wallet.Queue.RejectAllAsync();
            await pending;

            var info = (ChainAdditionPromptInfo)prompt.Data;
            Assert.Equal(S13Wallet.EvilOrigin, info.Origin);
        }

        [Fact]
        public async Task Given_ATypedDataRequestNamingAnotherAccount_When_ItIsHandled_Then_NoPromptIsQueued()
        {
            using var wallet = await S13Wallet.CreateAsync();
            await wallet.GrantAsync(S13Wallet.GoodOrigin);
            wallet.SetOrigin(S13Wallet.GoodOrigin);

            var observed = await wallet.ObserveAsync("eth_signTypedData_v4", wallet.Harness.SecondAddress, TypedData("31337"));

            Assert.Equal(0, observed.Queued);
            Assert.True(observed.Response.HasError);
        }

        [Theory]
        [InlineData("1", "uint256")]
        [InlineData("\"1\"", "uint256")]
        [InlineData("\"0x1\"", "uint256")]
        [InlineData("115792089237316195423570985008687907853269984665640564039457584007913129639935", "uint256")]
        public async Task Given_ATypedDataDomainForAnotherChain_When_ItIsHandled_Then_ItIsRefusedWithoutAPrompt(string chainIdJson, string chainType)
        {
            using var wallet = await S13Wallet.CreateAsync();
            await wallet.GrantAsync(S13Wallet.GoodOrigin);
            wallet.SetOrigin(S13Wallet.GoodOrigin);

            var observed = await wallet.ObserveAsync("eth_signTypedData_v4", wallet.Harness.FirstAddress, TypedData(chainIdJson, chainType));

            Assert.Equal(0, observed.Queued);
            Assert.True(observed.Response.HasError);
        }

        [Fact]
        public async Task Given_ABareHexTextMessage_When_PersonalSignIsPrompted_Then_ItIsTreatedAsText()
        {
            using var wallet = await S13Wallet.CreateAsync();
            await wallet.GrantAsync(S13Wallet.GoodOrigin);
            wallet.SetOrigin(S13Wallet.GoodOrigin);

            var observed = await wallet.ObserveAsync("personal_sign", "cafe", wallet.Harness.FirstAddress);

            var info = (SignaturePromptInfo)observed.Prompts.Single().Data;
            Assert.False(info.IsMessageHex);
        }

        [Fact]
        public async Task Given_AChainAddForAKnownChainWithAnotherRpc_When_ItIsHandled_Then_OnlyASwitchIsPromptedAndTheRpcIsUnchanged()
        {
            using var wallet = await S13Wallet.CreateAsync();
            var known = await wallet.Harness.Services.GetRequiredService<Nethereum.Wallet.Services.Network.IChainManagementService>().GetChainAsync(WalletDevChainHarness.ChainId);
            await wallet.Host.Configuration.AddOrUpdateChainAsync(known!);
            wallet.SetOrigin(S13Wallet.EvilOrigin);

            var observed = await wallet.ObserveAsync("wallet_addEthereumChain", ChainParam("0x7a69", "https://evil-rpc.example"));

            var info = (ChainSwitchPromptInfo)observed.Prompts.Single().Data;
            Assert.False(info.AllowAdd);
            Assert.DoesNotContain("https://evil-rpc.example", info.TargetChain!.HttpRpcs);
        }

        [Fact]
        public async Task Given_ALockedWallet_When_AccountsAreRequested_Then_TheCallDoesNotAnswerWithAnEmptyList()
        {
            using var wallet = await S13Wallet.CreateAsync();
            wallet.SetOrigin(S13Wallet.GoodOrigin);
            await wallet.Host.LogoutAsync();

            var pending = wallet.CallAsync("eth_requestAccounts");
            await Task.WhenAny(pending, Task.Delay(1000));

            var answeredEmpty = pending.IsCompleted && pending.Result.Result is JArray accounts && accounts.Count == 0;
            Assert.False(answeredEmpty);
        }

        [Theory]
        [InlineData("0xzz")]
        [InlineData("-0x1")]
        [InlineData("1")]
        public async Task Given_AMalformedChainId_When_TheChainIsSwitched_Then_TheCallAnswersInvalidParams(string chainId)
        {
            using var wallet = await S13Wallet.CreateAsync();
            wallet.SetOrigin(S13Wallet.GoodOrigin);

            var pending = wallet.CallAsync("wallet_switchEthereumChain", new JObject { ["chainId"] = chainId });
            await Task.WhenAny(pending, Task.Delay(1000));
            var completed = pending.IsCompleted;
            var response = await wallet.DrainAsync(pending);

            Assert.True(completed);
            Assert.Equal("-32602", response.Error.Code.ToString());
        }
    }
}
