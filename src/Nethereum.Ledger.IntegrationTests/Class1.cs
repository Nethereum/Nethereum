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
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Ledger.IntegrationTests
{
    public class Test
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
        public async Task TestSignature()
        {
            var transfer = new TransferFunction();
            transfer.To = "0x12890d2cce102216644c59daE5baed380d848301";
            transfer.FromAddress = "0x1996a57077877D38e18A1BE44A55100D77b8fA1D";
            transfer.Value = 1;
            transfer.Nonce = 1;
            transfer.GasPrice = 100;
            transfer.Gas = 1000;
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            var transactionInput = transfer.CreateTransactionInput("0x12890d2cce102216644c59daE5baed380d84830c");

            var account = new Account("0x128c6818917d98a3b933de1d400e777963424ce71f0a58755b092d1b670394eb", Chain.MainNet);

            account.TransactionManager.Client = rpcClient;
            var signature = await account.TransactionManager.SignTransactionAsync(transactionInput);
           

            var externalAccount = new ExternalAccount(new EthECKeyExternalSigner(new LedgerExternalSigner()), 1);
            await externalAccount.InitialiseAsync();
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);
            var signature2 = await externalAccount.TransactionManager.SignTransactionAsync(new TransactionInput()
            {
                From = "0x1996a57077877D38e18A1BE44A55100D77b8fA1D",
                GasPrice
                    = new HexBigInteger(Transaction.DEFAULT_GAS_PRICE),
                Gas = new HexBigInteger(Transaction.DEFAULT_GAS_LIMIT),
                Nonce = new HexBigInteger(1),
                To = "0x12890d2cce102216644c59daE5baed380d848301",
                Value = new HexBigInteger(100)

            });
             var signature3 =await externalAccount.TransactionManager.SignTransactionAsync(transactionInput);

        }
    }
}
