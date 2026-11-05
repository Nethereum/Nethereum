using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;

namespace Nethereum.X402.IntegrationTests.Facilitator;

public class X402FacilitatorAllowedAssetsCanonicalKeyTests
{
    private const string Usdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string Other = "0x0000000000000000000000000000000000000001";

    [Theory]
    [Trait("Rule", "Z1")]
    [InlineData("eip155:084532")]
    [InlineData("eip155:84532 ")]
    [InlineData(" eip155:84532")]
    [InlineData("base-sepolia")]
    [InlineData("84532")]
    [InlineData("eip155:84532/")]
    public void Given_AnAllowedAssetsKeyThatIsNotACanonicalEip155Network_When_TheOptionsAreValidated_Then_ItThrowsNamingTheKey(string key)
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets[key] = new List<string> { Usdc };

        var error = Assert.Throws<ArgumentException>(() => options.Validate());

        Assert.Contains(key, error.Message);
    }

    [Theory]
    [Trait("Rule", "Z1")]
    [InlineData("eip155:84532")]
    [InlineData("EIP155:84532")]
    public void Given_ACanonicalAllowedAssetsKey_When_AnAssetOutsideTheListIsChecked_Then_TheOptionsValidateAndTheAssetIsRejected(string key)
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets[key] = new List<string> { Usdc };

        options.Validate();

        Assert.True(options.IsAssetAllowed("eip155:84532", Usdc));
        Assert.False(options.IsAssetAllowed("eip155:84532", Other));
    }

    [Fact]
    [Trait("Rule", "Z1")]
    public void Given_ANonCanonicalAllowedAssetsKey_When_TheFacilitatorOptionsAreRegistered_Then_RegistrationFailsAtStartup()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddX402FacilitatorOptions(options => options.AllowedAssets["eip155:084532"] = new List<string> { Usdc }));
    }
}
