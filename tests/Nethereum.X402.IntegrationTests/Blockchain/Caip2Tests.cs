using System;
using Nethereum.X402.Blockchain;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

public class Caip2Tests
{
    [Fact]
    public void FormatEip155_ProducesNamespacedIdentifier()
    {
        Assert.Equal("eip155:8453", Caip2.FormatEip155(8453));
        Assert.Equal("eip155:84532", Caip2.FormatEip155(84532));
    }

    [Theory]
    [InlineData("eip155:8453", 8453)]
    [InlineData("eip155:1", 1)]
    [InlineData("EIP155:84532", 84532)]
    public void TryParseEip155ChainId_ParsesValidIdentifiers(string networkId, int expectedChainId)
    {
        Assert.True(Caip2.TryParseEip155ChainId(networkId, out var chainId));
        Assert.Equal(expectedChainId, chainId);
        Assert.True(Caip2.IsEip155(networkId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("base-sepolia")]
    [InlineData("eip155")]
    [InlineData("eip155:")]
    [InlineData(":8453")]
    [InlineData("eip155:abc")]
    [InlineData("eip155:-1")]
    [InlineData("solana:mainnet")]
    public void TryParseEip155ChainId_RejectsInvalidIdentifiers(string networkId)
    {
        Assert.False(Caip2.TryParseEip155ChainId(networkId, out var chainId));
        Assert.Equal(0, chainId);
        Assert.False(Caip2.IsEip155(networkId));
    }

    [Fact]
    public void ParseEip155ChainId_ThrowsOnInvalidIdentifier()
    {
        Assert.Throws<ArgumentException>(() => Caip2.ParseEip155ChainId("base-sepolia"));
    }

    [Fact]
    public void FormatAndParse_RoundTrip()
    {
        var chainId = Caip2.ParseEip155ChainId(Caip2.FormatEip155(42161));
        Assert.Equal(42161, chainId);
    }
}
