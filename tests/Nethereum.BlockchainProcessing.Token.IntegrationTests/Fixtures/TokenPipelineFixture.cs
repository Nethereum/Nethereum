using System.Numerics;
using Nethereum.BlockchainProcessing.Token.IntegrationTests.Contracts;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using ERC1155Defs = Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.BlockchainProcessing.Token.IntegrationTests.Fixtures
{
    [CollectionDefinition("TokenPipeline")]
    public class TokenPipelineCollection : ICollectionFixture<TokenPipelineFixture> { }

    public class TokenPipelineFixture : IAsyncLifetime
    {
        public DevChainNode Node { get; private set; } = null!;
        public IWeb3 Web3 { get; private set; } = null!;

        public string PrivateKey => "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string Address => "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public string Address2 => "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        public string Address3 => "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        public string ERC20Address { get; private set; } = null!;
        public string ERC721Address { get; private set; } = null!;
        public string ERC1155Address { get; private set; } = null!;

        public TransactionReceipt ERC20MintReceipt { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig { ChainId = 31337, BlockGasLimit = 30_000_000, AutoMine = true };
            Node = new DevChainNode(config);
            await Node.StartAsync(new[] { Address, Address2 }, BigInteger.Parse("10000000000000000000000"));

            Web3 = Node.CreateWeb3(PrivateKey);
            Web3.TransactionManager.UseLegacyAsDefault = true;

            await DeployContractsAsync();
            await ExecuteERC20TransfersAsync();
            await ExecuteERC721TransfersAsync();
            await ExecuteERC1155TransfersAsync();
        }

        private async Task DeployContractsAsync()
        {
            var erc20Receipt = await Web3.Eth.GetContractDeploymentHandler<ERC20Deployment>()
                .SendRequestAndWaitForReceiptAsync(new ERC20Deployment());
            ERC20Address = erc20Receipt.ContractAddress;

            var erc721Receipt = await Web3.Eth.GetContractDeploymentHandler<ERC721Deployment>()
                .SendRequestAndWaitForReceiptAsync(new ERC721Deployment { Name = "TestNFT", Symbol = "TNFT" });
            ERC721Address = erc721Receipt.ContractAddress;

            var erc1155Receipt = await Web3.Eth.GetContractDeploymentHandler<ERC1155Deployment>()
                .SendRequestAndWaitForReceiptAsync(new ERC1155Deployment());
            ERC1155Address = erc1155Receipt.ContractAddress;
        }

        private async Task ExecuteERC20TransfersAsync()
        {
            var mintHandler = Web3.Eth.GetContractTransactionHandler<ERC20MintFunction>();
            var transferHandler = Web3.Eth.GetContractTransactionHandler<ERC20TransferFunction>();

            ERC20MintReceipt = await mintHandler.SendRequestAndWaitForReceiptAsync(ERC20Address,
                new ERC20MintFunction { To = Address, Amount = BigInteger.Parse("1000000000000000000000") });

            await transferHandler.SendRequestAndWaitForReceiptAsync(ERC20Address,
                new ERC20TransferFunction { To = Address2, Value = BigInteger.Parse("100000000000000000000") });

            await transferHandler.SendRequestAndWaitForReceiptAsync(ERC20Address,
                new ERC20TransferFunction { To = Address3, Value = BigInteger.Parse("50000000000000000000") });
        }

        private async Task ExecuteERC721TransfersAsync()
        {
            var safeMintHandler = Web3.Eth.GetContractTransactionHandler<SafeMintFunction>();
            var transferFromHandler = Web3.Eth.GetContractTransactionHandler<TransferFromFunction>();

            await safeMintHandler.SendRequestAndWaitForReceiptAsync(ERC721Address,
                new SafeMintFunction { To = Address, Uri = "" });

            await safeMintHandler.SendRequestAndWaitForReceiptAsync(ERC721Address,
                new SafeMintFunction { To = Address, Uri = "" });

            await safeMintHandler.SendRequestAndWaitForReceiptAsync(ERC721Address,
                new SafeMintFunction { To = Address2, Uri = "" });

            await transferFromHandler.SendRequestAndWaitForReceiptAsync(ERC721Address,
                new TransferFromFunction { From = Address, To = Address2, TokenId = 0 });
        }

        private async Task ExecuteERC1155TransfersAsync()
        {
            var mintHandler = Web3.Eth.GetContractTransactionHandler<ERC1155Defs.MintFunction>();
            var transferHandler = Web3.Eth.GetContractTransactionHandler<ERC1155Defs.SafeTransferFromFunction>();

            await mintHandler.SendRequestAndWaitForReceiptAsync(ERC1155Address,
                new ERC1155Defs.MintFunction { Account = Address, Id = 1, Amount = 100, Data = Array.Empty<byte>() });

            await mintHandler.SendRequestAndWaitForReceiptAsync(ERC1155Address,
                new ERC1155Defs.MintFunction { Account = Address, Id = 2, Amount = 50, Data = Array.Empty<byte>() });

            await transferHandler.SendRequestAndWaitForReceiptAsync(ERC1155Address,
                new ERC1155Defs.SafeTransferFromFunction { From = Address, To = Address2, Id = 1, Amount = 30, Data = Array.Empty<byte>() });
        }

        public Task DisposeAsync()
        {
            Node?.Dispose();
            return Task.CompletedTask;
        }
    }
}
