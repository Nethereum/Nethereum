using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class Permit2ServiceForkTests
{
    private const int ChainId = 84532;
    private static string? ForkRpc => Environment.GetEnvironmentVariable("X402_FORK_RPC");

    [Fact]
    [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "Permit2: read and write the canonical Permit2 via GetPermit2Service", SkillName = "built-in-standards", Order = 20)]
    public async Task GetPermit2Service_ReadsAndWritesCanonicalPermit2()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var ownerKey = EthECKey.GenerateKey();
        var owner = new Account(ownerKey.GetPrivateKey(), ChainId);
        var web3 = new Nethereum.Web3.Web3(owner, rpc);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(
            owner.Address, new HexBigInteger(BigInteger.Parse("100000000000000000000")));

        var permit2 = web3.Eth.GetPermit2Service();

        var domainSeparator = await permit2.DomainSeparatorQueryAsync();
        Assert.Equal(32, domainSeparator.Length);
        Assert.Contains(domainSeparator, b => b != 0);

        var before = await permit2.NonceBitmapQueryAsync(owner.Address, BigInteger.Zero);
        Assert.Equal(BigInteger.Zero, before);

        var receipt = await permit2.InvalidateUnorderedNoncesRequestAndWaitForReceiptAsync(
            wordPos: BigInteger.Zero, mask: BigInteger.One);
        Assert.Equal(1, receipt.Status.Value);

        var after = await permit2.NonceBitmapQueryAsync(owner.Address, BigInteger.Zero);
        Assert.Equal(BigInteger.One, after & BigInteger.One);
    }
}
