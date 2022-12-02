using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Extensions;
using Nethereum.Web3.Accounts;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Xunit;
using Ledger.Net;

namespace Nethereum.Ledger.IntegrationTests
{
    public class AccountSigningTest
    {
        [Function("transfer", "bool")]
        public class TransferFunctionBase : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }
            [Parameter("uint256", "_value", 2)]
            public BigInteger Value { get; set; }
        }

        public partial class TransferFunction : TransferFunctionBase
        {

        }


       


        public async Task Test1559(string addressFrom, string privateKey, bool legacy)
        {
            var transfer = new TransferFunction();
            transfer.To = "0x12890d2cce102216644c59daE5baed380d848301";
            transfer.FromAddress = addressFrom;
            transfer.Value = 1;
            transfer.Nonce = 1;
            transfer.MaxFeePerGas = 100;
            transfer.MaxPriorityFeePerGas = 100;
            transfer.Gas = 1000;
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            var transactionInput = transfer.CreateTransactionInput("0x12890d2cce102216644c59daE5baed380d84830c");

            var account = new Account(privateKey, Chain.MainNet);

            account.TransactionManager.Client = rpcClient;
            var signature = await account.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);

            var ledgerManagerBroker = LedgerManagerBrokerFactory.CreateWindowsHidUsb();
            var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();
            var externalAccount = new ExternalAccount(new LedgerExternalSigner(ledgerManager, 0, legacy), 1);
            await externalAccount.InitialiseAsync().ConfigureAwait(false);
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);
            //Ensure contract data is enable in the settings of ledger nano
            var signature2 = await externalAccount.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);

            Assert.Equal(signature, signature2);

            //Signing just transfer
            var signature3 = await externalAccount.TransactionManager.SignTransactionAsync(new TransactionInput()
            {
                From = addressFrom,
                MaxFeePerGas = new HexBigInteger(LegacyTransaction.DEFAULT_GAS_PRICE),
                MaxPriorityFeePerGas = new HexBigInteger(LegacyTransaction.DEFAULT_GAS_PRICE),
                Gas = new HexBigInteger(LegacyTransaction.DEFAULT_GAS_LIMIT),
                Nonce = new HexBigInteger(1),
                To = "0x12890d2cce102216644c59daE5baed380d848301",
                Value = new HexBigInteger(100)

            }).ConfigureAwait(false);
        }

        public class StandardTokenDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x60606040526040516020806106f5833981016040528080519060200190919050505b80600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005081905550806000600050819055505b506106868061006f6000396000f360606040523615610074576000357c010000000000000000000000000000000000000000000000000000000090048063095ea7b31461008157806318160ddd146100b657806323b872dd146100d957806370a0823114610117578063a9059cbb14610143578063dd62ed3e1461017857610074565b61007f5b610002565b565b005b6100a060048080359060200190919080359060200190919050506101ad565b6040518082815260200191505060405180910390f35b6100c36004805050610674565b6040518082815260200191505060405180910390f35b6101016004808035906020019091908035906020019091908035906020019091905050610281565b6040518082815260200191505060405180910390f35b61012d600480803590602001909190505061048d565b6040518082815260200191505060405180910390f35b61016260048080359060200190919080359060200190919050506104cb565b6040518082815260200191505060405180910390f35b610197600480803590602001909190803590602001909190505061060b565b6040518082815260200191505060405180910390f35b600081600260005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008573ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905061027b565b92915050565b600081600160005060008673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561031b575081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505410155b80156103275750600082115b1561047c5781600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a381600160005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505403925050819055506001905061048656610485565b60009050610486565b5b9392505050565b6000600160005060008373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505490506104c6565b919050565b600081600160005060003373ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561050c5750600082115b156105fb5781600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a36001905061060556610604565b60009050610605565b5b92915050565b6000600260005060008473ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005054905061066e565b92915050565b60006000600050549050610683565b9056";

            public StandardTokenDeployment() : base(BYTECODE)
            {
            }

            [Parameter("uint256", "totalSupply")] public int TotalSupply { get; set; }
        }

        public async Task TestWithDeployment(string addressFrom, string privateKey, bool legacy, int chainId = 1)
        {
            var deployment = new StandardTokenDeployment();
            deployment.FromAddress = addressFrom;
            deployment.TotalSupply = 1000;
            deployment.Nonce = 1;
            deployment.GasPrice = 100;
            deployment.Gas = 1000;
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            var transactionInput = deployment.CreateTransactionInput();

            var account = new Account(privateKey, chainId);

            account.TransactionManager.Client = rpcClient;
            var signature = await account.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);

            var ledgerManagerBroker = LedgerManagerBrokerFactory.CreateWindowsHidUsb();
            var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();
            var externalAccount = new ExternalAccount(new LedgerExternalSigner(ledgerManager, 0, legacy), chainId);
            await externalAccount.InitialiseAsync().ConfigureAwait(false);
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);
            //Ensure contract data is enable in the settings of ledger nano
            externalAccount.TransactionManager.UseLegacyAsDefault = true;
            var signature2 = await externalAccount.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);

            Assert.Equal(signature, signature2);
        }

        public async Task Test(string addressFrom, string privateKey, bool legacy, int chainId = 1)
        {
            var transfer = new TransferFunction();
            transfer.To = "0x12890d2cce102216644c59daE5baed380d848301";
            transfer.FromAddress = addressFrom;
            transfer.Value = 1;
            transfer.Nonce = 1;
            transfer.GasPrice = 100;
            transfer.Gas = 1000;
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            var transactionInput = transfer.CreateTransactionInput("0x12890d2cce102216644c59daE5baed380d84830c");

            var account = new Account(privateKey, chainId);

            account.TransactionManager.Client = rpcClient;
            var signature = await account.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);

            var ledgerManagerBroker = LedgerManagerBrokerFactory.CreateWindowsHidUsb();
            var ledgerManager = (LedgerManager)await ledgerManagerBroker.WaitForFirstDeviceAsync();
            var externalAccount = new ExternalAccount(new LedgerExternalSigner(ledgerManager, 0, legacy), chainId);
            await externalAccount.InitialiseAsync().ConfigureAwait(false);
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);
            //Ensure contract data is enable in the settings of ledger nano
            externalAccount.TransactionManager.UseLegacyAsDefault = true;
            var signature2 = await externalAccount.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);

            Assert.Equal(signature, signature2);

            //Signing just transfer
            var signature3 = await externalAccount.TransactionManager.SignTransactionAsync(new TransactionInput()
            {
                From = addressFrom,
                GasPrice
                    = new HexBigInteger(LegacyTransaction.DEFAULT_GAS_PRICE),
                Gas = new HexBigInteger(LegacyTransaction.DEFAULT_GAS_LIMIT),
                Nonce = new HexBigInteger(1),
                To = "0x12890d2cce102216644c59daE5baed380d848301",
                Value = new HexBigInteger(100)

            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSignatureStandardDeployment()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";

            await TestWithDeployment(addressFrom, privateKey, false).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSignatureStandard()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";

            await Test(addressFrom, privateKey, false).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSignatureStandard1559()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";
            await Test1559(addressFrom, privateKey, false).ConfigureAwait(false);
        }


        [Fact]
        public async Task TestSignatureStandardChainIdBigger109()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";

            await Test(addressFrom, privateKey, false, 43114).ConfigureAwait(false);
        }

        //[Fact]
        //public async Task TestSignatureLegacyLedgerPath()
        //{
           
        //    var addressFrom = "0x169Bd01cdC01b9178EB8E53129469A598D7d840A";
        //    var privateKey = "0x9ec15934765f15b8a52bca0c444e6effacdb1a82cfeae451ffbae1b679791e45";

        //    await Test(addressFrom, privateKey, true).ConfigureAwait(false);

        //}
    }
}
