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

        /* margin
        multiply
        license
        alien
        close
        rain
        master
        violin
        cheese
        bonus
        soccer
        museum
        eight
        roof
        defy
        ghost
        venue
        obey
        twelve
        another
        tattoo
        inflict
        sting
        glue 

        //legacy
        m/44'/60'/0'/0
        0x1996a57077877D38e18A1BE44A55100D77b8fA1D
        0x128c6818917d98a3b933de1d400e777963424ce71f0a58755b092d1b670394eb 

        //standard
        m/44'/60'/0'/0/0
        0x76579b7aD091747F9aF144C207e640136c47A6b8	
        0x105023ddd0e214d9e79bd94639a896e5ef19d24e9cb9fe59baf12969ffe0101e
        */

        [Fact]
        public async Task TestSignatureLegacy()
        {
            var addressFrom = "0x1996a57077877D38e18A1BE44A55100D77b8fA1D";
            var privateKey = "0x128c6818917d98a3b933de1d400e777963424ce71f0a58755b092d1b670394eb";

            await Test(addressFrom, privateKey, true);
        }
        
        public async Task Test(string addressFrom, string privateKey, bool legacy)
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

            var account = new Account(privateKey, Chain.MainNet);

            account.TransactionManager.Client = rpcClient;
            var signature = await account.TransactionManager.SignTransactionAsync(transactionInput);

            var ledgerManager = await LedgerFactory.GetWindowsConnectedLedgerManager();
            var externalAccount = new ExternalAccount(new LedgerExternalSigner(ledgerManager, 0, legacy)), 1);
            await externalAccount.InitialiseAsync();
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);
            //Ensure contract data is enable in the settings of ledger nano
            var signature2 = await externalAccount.TransactionManager.SignTransactionAsync(transactionInput);

            Assert.Equal(signature, signature2);

            //Signing just transfer
            var signature3 = await externalAccount.TransactionManager.SignTransactionAsync(new TransactionInput()
            {
                From = addressFrom,
                GasPrice
                    = new HexBigInteger(Transaction.DEFAULT_GAS_PRICE),
                Gas = new HexBigInteger(Transaction.DEFAULT_GAS_LIMIT),
                Nonce = new HexBigInteger(1),
                To = "0x12890d2cce102216644c59daE5baed380d848301",
                Value = new HexBigInteger(100)

            });
        }


        [Fact]
        public async Task TestSignatureStandard()
        {
            var addressFrom = "0x76579b7aD091747F9aF144C207e640136c47A6b8";
            var privateKey = "0x105023ddd0e214d9e79bd94639a896e5ef19d24e9cb9fe59baf12969ffe0101e";

            await Test(addressFrom, privateKey, false);

        }
    }
}
