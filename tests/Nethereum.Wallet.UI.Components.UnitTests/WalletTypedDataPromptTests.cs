using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Services;
using Xunit;

namespace Nethereum.Wallet.UI.Components.UnitTests
{
    public class WalletTypedDataPromptTests
    {
        private const string TypedDataWithUndeclaredKeys = @"{
  ""types"": {
    ""EIP712Domain"": [
      { ""name"": ""name"", ""type"": ""string"" },
      { ""name"": ""chainId"", ""type"": ""uint256"" }
    ],
    ""Mail"": [
      { ""name"": ""from"", ""type"": ""Person"" },
      { ""name"": ""contents"", ""type"": ""string"" }
    ],
    ""Person"": [
      { ""name"": ""name"", ""type"": ""string"" },
      { ""name"": ""wallet"", ""type"": ""address"" }
    ]
  },
  ""primaryType"": ""Mail"",
  ""domain"": { ""name"": ""Ether Mail"", ""chainId"": 31337, ""undeclaredDomainKey"": ""hiddenDomainValue"" },
  ""message"": {
    ""from"": { ""name"": ""Cow"", ""wallet"": ""0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826"", ""undeclaredNestedKey"": ""hiddenNestedValue"" },
    ""contents"": ""Hello, Bob!"",
    ""undeclaredMessageKey"": ""hiddenMessageValue""
  }
}";

        [Fact]
        public async Task Given_ATypedDataRequestWithUndeclaredKeys_When_ThePromptIsQueued_Then_TheDisplayedMessageHasTheDeclaredFieldsOnly()
        {
            using var harness = await WalletDevChainHarness.CreateAsync();
            var signaturePrompts = harness.Services.GetRequiredService<ISignaturePromptService>();
            var queue = harness.Services.GetRequiredService<IPromptQueueService>();

            var pending = signaturePrompts.PromptTypedDataSignAsync(new TypedDataSignPromptContext
            {
                Address = harness.FirstAddress,
                TypedDataJson = TypedDataWithUndeclaredKeys,
                PrimaryType = "Mail"
            });
            var prompt = queue.PendingPrompts.Single();
            var info = (SignaturePromptInfo)prompt.Data;
            await queue.CompletePromptAsync(prompt.Id, "0xsignature");
            await pending;

            Assert.Contains("Hello, Bob!", info.DecodedMessage);
            Assert.Contains("Cow", info.DecodedMessage);
            Assert.Contains("Ether Mail", info.DecodedMessage);
            Assert.DoesNotContain("undeclaredMessageKey", info.DecodedMessage);
            Assert.DoesNotContain("hiddenMessageValue", info.DecodedMessage);
            Assert.DoesNotContain("undeclaredNestedKey", info.DecodedMessage);
            Assert.DoesNotContain("hiddenNestedValue", info.DecodedMessage);
            Assert.DoesNotContain("undeclaredDomainKey", info.DecodedMessage);
            Assert.DoesNotContain("hiddenDomainValue", info.DecodedMessage);
        }
    }
}
