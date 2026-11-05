using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.StandardNonFungibleTokenERC721;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Xunit;

namespace Nethereum.StandardTokenEIP20.IntegrationTests
{
    public class StandardTokenServicesDevChainTests
    {
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string OwnerAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
        private const string OtherAddress = "0x12890d2cce102216644c59daE5baed380d84830e";

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "standard-token-eip20", "Deploy, transfer, approve and read events with StandardTokenService", Order = 1)]
        public async Task ShouldDeployATokenTransferApproveAndReadTheTransferEvent()
        {
            using var node = await DevChainNode.CreateAndStartAsync(new EthECKey(PrivateKey));
            var web3 = (Web3.Web3)node.CreateWeb3(PrivateKey);

            ulong totalSupply = 1000000;
            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(web3,
                new EIP20Deployment
                {
                    InitialAmount = totalSupply,
                    TokenName = "TestToken",
                    TokenSymbol = "TST"
                });

            var name = await tokenService.NameQueryAsync();
            var symbol = await tokenService.SymbolQueryAsync();
            var supply = await tokenService.TotalSupplyQueryAsync();
            var ownerBalance = await tokenService.BalanceOfQueryAsync(OwnerAddress);

            var transferReceipt = await tokenService.TransferRequestAndWaitForReceiptAsync(OtherAddress, 1000);
            var ownerBalanceAfter = await tokenService.BalanceOfQueryAsync(OwnerAddress);
            var otherBalance = await tokenService.BalanceOfQueryAsync(OtherAddress);

            var transferEvent = tokenService.GetTransferEvent();
            var filter = await transferEvent.CreateFilterAsync(new BlockParameter(transferReceipt.BlockNumber));
            var logs = await transferEvent.GetAllChangesAsync(filter);

            await tokenService.ApproveRequestAndWaitForReceiptAsync(OtherAddress, 500);
            var allowance = await tokenService.AllowanceQueryAsync(OwnerAddress, OtherAddress);

            Assert.Equal("TestToken", name);
            Assert.Equal("TST", symbol);
            Assert.Equal(totalSupply, supply);
            Assert.Equal(totalSupply, ownerBalance);
            Assert.Equal(totalSupply - 1000, ownerBalanceAfter);
            Assert.Equal(1000, otherBalance);
            Assert.Single(logs);
            Assert.Equal(OtherAddress.ToLower(), logs.Single().Event.To.ToLower());
            Assert.Equal(1000, logs.Single().Event.Value);
            Assert.Equal(500, allowance);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "standard-token-erc721", "Deploy ERC721Service and read interface support, balances and operator approvals", Order = 1)]
        public async Task ShouldDeployAnErc721ContractAndReadItsInterfaceBalancesAndApprovals()
        {
            using var node = await DevChainNode.CreateAndStartAsync(new EthECKey(PrivateKey));
            var web3 = (Web3.Web3)node.CreateWeb3(PrivateKey);

            var service = await ERC721Service.DeployContractAndGetServiceAsync(web3,
                new Nethereum.StandardNonFungibleTokenERC721.ContractDefinition.ERC721Deployment());

            var supportsErc721 = await service.SupportsInterfaceQueryAsync("0x80ac58cd".HexToByteArray());
            var supportsUnknown = await service.SupportsInterfaceQueryAsync("0xffffffff".HexToByteArray());
            var balance = await service.BalanceOfQueryAsync(OwnerAddress);
            var approvedBefore = await service.IsApprovedForAllQueryAsync(OwnerAddress, OtherAddress);

            await service.SetApprovalForAllRequestAndWaitForReceiptAsync(OtherAddress, true);
            var approvedAfter = await service.IsApprovedForAllQueryAsync(OwnerAddress, OtherAddress);

            Assert.True(supportsErc721);
            Assert.False(supportsUnknown);
            Assert.Equal(BigInteger.Zero, balance);
            Assert.False(approvedBefore);
            Assert.True(approvedAfter);
        }
    }
}
