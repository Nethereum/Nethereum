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


        [Fact]
        public async Task TestSignatureStandard1559()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";
            await Test1559(addressFrom, privateKey, false).ConfigureAwait(false);
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

            var ledgerManager = await LedgerFactory.GetWindowsConnectedLedgerManagerAsync().ConfigureAwait(false);
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

            var ledgerManager = await LedgerFactory.GetWindowsConnectedLedgerManagerAsync().ConfigureAwait(false);
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
        public async Task TestSignatureStandard()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";

            await Test(addressFrom, privateKey, false).ConfigureAwait(false);
        }


        [Fact]
        public async Task TestSignatureStandardChainIdBigger109()
        {
            var addressFrom = "0x07dCc60Ec5179f30ba30a2Ec25B683d5C5276025";
            var privateKey = "0x32b28a67ec29294be914a356a9f439cf1fd8c56a400d897f75f46694fb06a9c9";

            await Test(addressFrom, privateKey, false, 43114).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestSignatureLegacyLedgerPath()
        {
           
            var addressFrom = "0x169Bd01cdC01b9178EB8E53129469A598D7d840A";
            var privateKey = "0x9ec15934765f15b8a52bca0c444e6effacdb1a82cfeae451ffbae1b679791e45";

            await Test(addressFrom, privateKey, true).ConfigureAwait(false);

        }
    }
}
