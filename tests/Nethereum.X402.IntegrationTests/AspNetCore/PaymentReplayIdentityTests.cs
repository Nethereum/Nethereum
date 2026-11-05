using System.Text.Json;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class PaymentReplayIdentityTests
{
    private const string Payer = "0xAbCdEf0123456789aBcDeF0123456789AbCdEf01";
    private const string Hex64 = "0x0000000000000000000000000000000000000000000000000000000000000064";

    [Theory]
    [Trait("Rule", "S4")]
    [InlineData("100")]
    [InlineData("0100")]
    [InlineData("0000100")]
    public void Given_APermit2DecimalNonceWithLeadingZeros_When_TheIdentityIsExtracted_Then_ItIsTheCanonicalDecimal(string nonce)
    {
        var identity = PaymentReplayIdentity.Extract(Permit2(nonce));

        Assert.Equal(Payer.ToLowerInvariant() + ":100", identity);
    }

    [Theory]
    [Trait("Rule", "S4")]
    [InlineData("0x64")]
    [InlineData("0X64")]
    [InlineData("0x0064")]
    [InlineData(Hex64)]
    public void Given_APermit2HexNonce_When_TheIdentityIsExtracted_Then_ItIsThe32ByteLowerCaseHex(string nonce)
    {
        var identity = PaymentReplayIdentity.Extract(Permit2(nonce));

        Assert.Equal(Payer.ToLowerInvariant() + ":" + Hex64, identity);
    }

    [Theory]
    [Trait("Rule", "S4")]
    [InlineData("0x64")]
    [InlineData("0X64")]
    [InlineData(Hex64)]
    public void Given_AnEip3009Nonce_When_TheIdentityIsExtracted_Then_ItIsThe32ByteLowerCaseHex(string nonce)
    {
        var identity = PaymentReplayIdentity.Extract(Eip3009(nonce));

        Assert.Equal(Payer.ToLowerInvariant() + ":" + Hex64, identity);
    }

    [Fact]
    [Trait("Rule", "S4")]
    public void Given_AnEip3009NonceInUpperCaseHex_When_TheIdentityIsExtracted_Then_ItEqualsTheLowerCaseSpelling()
    {
        var upper = "0x" + new string('A', 64);
        var lower = "0x" + new string('a', 64);

        Assert.Equal(PaymentReplayIdentity.Extract(Eip3009(lower)), PaymentReplayIdentity.Extract(Eip3009(upper)));
    }

    [Fact]
    [Trait("Rule", "S4")]
    public void Given_ThePermit2DecimalAndHexSpellingsOfTheSameNumber_When_TheIdentityIsExtracted_Then_TheyAreDistinctShapes()
    {
        Assert.NotEqual(PaymentReplayIdentity.Extract(Permit2("100")), PaymentReplayIdentity.Extract(Permit2("0x64")));
    }

    [Theory]
    [Trait("Rule", "S4")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("0xzz")]
    [InlineData("0x")]
    [InlineData("0x" + "11111111111111111111111111111111111111111111111111111111111111111")]
    [InlineData("")]
    public void Given_ANonceThatIsNeitherANonNegativeIntegerNorHexOf32Bytes_When_TheIdentityIsExtracted_Then_ThereIsNoIdentity(string nonce)
    {
        Assert.Null(PaymentReplayIdentity.Extract(Permit2(nonce)));
        Assert.Null(PaymentReplayIdentity.Extract(Eip3009(nonce)));
    }

    [Theory]
    [Trait("Rule", "S4")]
    [InlineData("0100", "100")]
    [InlineData("0x0064", Hex64)]
    public void Given_APermit2PayloadReceivedAsJson_When_TheIdentityIsExtracted_Then_ItIsTheSameAsForTheTypedPayload(string nonce, string canonical)
    {
        var typed = Permit2(nonce);
        var wire = JsonSerializer.Deserialize<PaymentPayload>(JsonSerializer.Serialize(typed))!;

        Assert.IsType<JsonElement>(wire.Payload);
        Assert.Equal(PaymentReplayIdentity.Extract(typed), PaymentReplayIdentity.Extract(wire));
        Assert.Equal(Payer.ToLowerInvariant() + ":" + canonical, PaymentReplayIdentity.Extract(wire));
    }

    private static PaymentPayload Permit2(string nonce) => new()
    {
        X402Version = 2,
        Payload = new Permit2SchemePayload
        {
            Signature = "0x00",
            Permit2Authorization = new Permit2Authorization { From = Payer, Nonce = nonce }
        }
    };

    private static PaymentPayload Eip3009(string nonce) => new()
    {
        X402Version = 2,
        Payload = new ExactSchemePayload
        {
            Signature = "0x00",
            Authorization = new Nethereum.X402.Models.Authorization { From = Payer, Nonce = nonce }
        }
    };
}
