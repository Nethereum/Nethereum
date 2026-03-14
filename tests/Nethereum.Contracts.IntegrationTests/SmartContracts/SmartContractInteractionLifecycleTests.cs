using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class SmartContractInteractionLifecycleTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public SmartContractInteractionLifecycleTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        // *** CONTRACT DEFINITIONS ***

        public class StandardTokenDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x60606040526040516020806106f5833981016040528080519060200190919050505b80600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005081905550806000600050819055505b506106868061006f6000396000f360606040523615610074576000357c010000000000000000000000000000000000000000000000000000000090048063095ea7b31461008157806318160ddd146100b657806323b872dd146100d957806370a0823114610117578063a9059cbb14610143578063dd62ed3e1461017857610074565b61007f5b610002565b565b005b6100a060048080359060200190919080359060200190919050506101ad565b6040518082815260200191505060405180910390f35b6100c36004805050610674565b6040518082815260200191505060405180910390f35b6101016004808035906020019091908035906020019091908035906020019091905050610281565b6040518082815260200191505060405180910390f35b61012d600480803590602001909190505061048d565b6040518082815260200191505060405180910390f35b61016260048080359060200190919080359060200190919050506104cb565b6040518082815260200191505060405180910390f35b610197600480803590602001909190803590602001909190505061060b565b6040518082815260200191505060405180910390f35b600081600260005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008573ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905061027b565b92915050565b600081600160005060008673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561031b575081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505410155b80156103275750600082115b1561047c5781600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a381600160005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505403925050819055506001905061048656610485565b60009050610486565b5b9392505050565b6000600160005060008373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505490506104c6565b919050565b600081600160005060003373ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561050c5750600082115b156105fb5781600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a36001905061060556610604565b60009050610605565b5b92915050565b6000600260005060008473ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005054905061066e565b92915050565b60006000600050549050610683565b9056";

            public StandardTokenDeployment() : base(BYTECODE) { }

            [Parameter("uint256", "totalSupply")]
            public BigInteger TotalSupply { get; set; }
        }

        [Function("balanceOf", "uint256")]
        public class BalanceOfFunction : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public string Owner { get; set; }
        }

        [Function("transfer", "bool")]
        public class TransferFunction : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 2)]
            public BigInteger TokenAmount { get; set; }
        }

        [Event("Transfer")]
        public class TransferEventDTO : IEventDTO
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value { get; set; }
        }

        [FunctionOutput]
        public class BalanceOfOutputDTO : IFunctionOutputDTO
        {
            [Parameter("uint256", "balance", 1)]
            public BigInteger Balance { get; set; }
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Define DTOs and deploy a smart contract",
            SkillName = "smart-contract-interaction", Order = 1)]
        public async Task DeploySmartContract_WithTypedDeploymentMessage()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment
            {
                TotalSupply = 100000
            };

            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);

            var contractAddress = transactionReceipt.ContractAddress;
            Assert.NotNull(contractAddress);

            var balanceOfMessage = new BalanceOfFunction { Owner = EthereumClientIntegrationFixture.AccountAddress };
            var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            var balance = await balanceHandler.QueryAsync<BigInteger>(contractAddress, balanceOfMessage);

            Assert.Equal(100000, balance);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Query a smart contract using typed handlers",
            SkillName = "smart-contract-interaction", Order = 2)]
        public async Task QuerySmartContract_WithTypedQueryHandler()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = receipt.ContractAddress;

            var balanceOfMessage = new BalanceOfFunction { Owner = EthereumClientIntegrationFixture.AccountAddress };
            var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

            var balance = await balanceHandler.QueryAsync<BigInteger>(contractAddress, balanceOfMessage);
            Assert.Equal(100000, balance);

            var balanceOutput = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
                balanceOfMessage, contractAddress);
            Assert.Equal(100000, balanceOutput.Balance);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Send a transaction to a smart contract",
            SkillName = "smart-contract-interaction", Order = 3)]
        public async Task TransactWithSmartContract_WithTypedTransactionHandler()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = receipt.ContractAddress;

            var receiverAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();

            var transfer = new TransferFunction
            {
                To = receiverAddress,
                TokenAmount = 100
            };

            var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer);
            Assert.NotNull(transferReceipt.TransactionHash);

            var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            var ownerBalance = await balanceHandler.QueryAsync<BigInteger>(contractAddress,
                new BalanceOfFunction { Owner = EthereumClientIntegrationFixture.AccountAddress });
            Assert.Equal(99900, ownerBalance);

            var receiverBalance = await balanceHandler.QueryAsync<BigInteger>(contractAddress,
                new BalanceOfFunction { Owner = receiverAddress });
            Assert.Equal(100, receiverBalance);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Query historical state from a previous block",
            SkillName = "smart-contract-interaction", Order = 4)]
        public async Task QueryHistoricalState_WithBlockParameter()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var deployReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = deployReceipt.ContractAddress;

            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
            await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, new TransferFunction
            {
                To = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe",
                TokenAmount = 100
            });

            var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            var balanceOfMessage = new BalanceOfFunction { Owner = EthereumClientIntegrationFixture.AccountAddress };

            var currentBalance = await balanceHandler.QueryAsync<BigInteger>(contractAddress, balanceOfMessage);
            Assert.Equal(99900, currentBalance);

            var historicalBalance = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
                balanceOfMessage, contractAddress, new BlockParameter(deployReceipt.BlockNumber));
            Assert.Equal(100000, historicalBalance.Balance);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Estimate gas and set gas price for a transaction",
            SkillName = "smart-contract-interaction", Order = 5)]
        public async Task EstimateGasAndSetGasPrice()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = receipt.ContractAddress;

            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var transfer = new TransferFunction
            {
                To = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe",
                TokenAmount = 100
            };

            var estimate = await transferHandler.EstimateGasAsync(contractAddress, transfer);
            Assert.True(estimate.Value > 0);

            transfer.Gas = estimate.Value;
            transfer.GasPrice = Web3.Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei);

            var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer);
            Assert.NotNull(transferReceipt.TransactionHash);
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Sign a transaction offline",
            SkillName = "smart-contract-interaction", Order = 6)]
        public async Task SignTransactionOffline()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = receipt.ContractAddress;

            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var transfer = new TransferFunction
            {
                To = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe",
                TokenAmount = 100,
                Nonce = 2,
                Gas = 60000,
                GasPrice = Web3.Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei)
            };

            var signedTransaction = await transferHandler.SignTransactionAsync(contractAddress, transfer);
            Assert.NotNull(signedTransaction);
           
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "smart-contract-interaction",
            "Decode events from a transaction receipt",
            SkillName = "smart-contract-interaction", Order = 7)]
        public async Task DecodeEventsFromReceipt()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = receipt.ContractAddress;

            var receiverAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress,
                new TransferFunction { To = receiverAddress, TokenAmount = 100 });

            var transferEvents = transferReceipt.DecodeAllEvents<TransferEventDTO>();
            Assert.Single(transferEvents);

            var transferEvent = transferEvents[0];
            Assert.Equal(EthereumClientIntegrationFixture.AccountAddress.ToLower(), transferEvent.Event.From.ToLower());
            Assert.Equal(receiverAddress.ToLower(), transferEvent.Event.To.ToLower());
            Assert.Equal(100, transferEvent.Event.Value);
        }
    }
}
