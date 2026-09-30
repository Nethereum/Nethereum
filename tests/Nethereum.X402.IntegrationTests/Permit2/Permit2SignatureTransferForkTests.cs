using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712.Permit2;
using Nethereum.Web3.Accounts;
using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class Permit2SignatureTransferForkTests
{
    private const int ChainId = 84532;
    private static string? ForkRpc => Environment.GetEnvironmentVariable("X402_FORK_RPC");

    [Fact]
    [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "Permit2 SignatureTransfer: sign with spender and settle on-chain", SkillName = "built-in-standards", Order = 21)]
    public async Task SignPermitTransferFrom_WithSpender_VerifiesAndTransfersOnChain()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var ownerKey = EthECKey.GenerateKey();
        var spenderKey = EthECKey.GenerateKey();
        var owner = new Account(ownerKey.GetPrivateKey(), ChainId);
        var spender = new Account(spenderKey.GetPrivateKey(), ChainId);
        var recipient = EthECKey.GenerateKey().GetPublicAddress();

        var ownerWeb3 = new Nethereum.Web3.Web3(owner, rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await ownerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(owner.Address, oneHundredEth);
        await ownerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(spender.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(ownerWeb3, owner);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");
        await usdc.MintAsync(owner.Address, BigInteger.Parse("10000000"));
        await ownerWeb3.Eth.ERC20.GetContractService(token)
            .ApproveRequestAndWaitForReceiptAsync(CommonAddresses.PERMIT2_ADDRESS, BigInteger.Pow(2, 96));

        var amount = BigInteger.Parse("1000000");
        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var deadline = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);

        var permitted = new TokenPermissions { Token = token, Amount = amount };

        var signature = PermitSigner.SignPermitTransferFrom(
            ChainId, CommonAddresses.PERMIT2_ADDRESS,
            new PermitTransferFromWithSpender
            {
                Permitted = permitted,
                Spender = spender.Address,
                Nonce = nonce,
                Deadline = deadline
            },
            ownerKey);

        var permit = new PermitTransferFrom { Permitted = permitted, Nonce = nonce, Deadline = deadline };

        var spenderWeb3 = new Nethereum.Web3.Web3(spender, rpc);
        var permit2 = spenderWeb3.Eth.GetPermit2Service();

        var before = await usdc.GetBalanceAsync(recipient);
        var receipt = await permit2.PermitTransferFromRequestAndWaitForReceiptAsync(
            permit,
            new SignatureTransferDetails { To = recipient, RequestedAmount = amount },
            owner.Address,
            signature.HexToByteArray());

        Assert.Equal(1, receipt.Status.Value);
        Assert.Equal(amount, await usdc.GetBalanceAsync(recipient) - before);
    }
}
