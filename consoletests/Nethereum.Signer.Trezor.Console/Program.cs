using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Trezor.Net.Contracts.Ethereum;

// ReSharper disable AsyncConverter.AsyncWait

namespace Nethereum.Signer.Trezor.Console
{
    class Program
    {
        private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => _ = builder.AddDebug().SetMinimumLevel(LogLevel.Trace));

        static async Task Main(string[] args)
        {
            // TestMessageSigning().Wait();
            await TestDeployment1559Async();
            await TestDeploymentAsync();
            await TestTransactionSigning();
            await TestTransferTokenSigning();
        }

        /*
        deposit
        van
        crouch
        super
        viable
        electric
        bamboo
        nephew
        hold
        whip
        nation
        ankle
        wonder
        bottom
        win
        bomb
        dog
        search
        globe
        shrug
        primary
        spy
        limb
        knock
        */
        //public static async Task TestMessageSigning()
        //{
        //    using (var trezorManager = await TrezorFactory.GetWindowsConnectedLedgerManagerAsync(GetPin))
        //    {
        //        await trezorManager.InitializeAsync();
        //        var signer = new TrezorExternalSigner(trezorManager, 0);
        //        var address = await signer.GetAddressAsync();
        //        var messageSignature = await signer.SignAsync(Encoding.UTF8.GetBytes("Hello"));

        //        var nethereumMessageSigner = new Nethereum.Signer.EthereumMessageSigner();
        //        var nethereumMessageSignature = nethereumMessageSigner.EncodeUTF8AndSign("Hello", new EthECKey(
        //            "0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29"));
        //        System.Console.WriteLine("Trezor: " + EthECDSASignature.CreateStringSignature(messageSignature));
        //        System.Console.WriteLine("Nethereum: " + nethereumMessageSignature);

        //    }
        //}

        public class StandardTokenDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x60606040526040516020806106f5833981016040528080519060200190919050505b80600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005081905550806000600050819055505b506106868061006f6000396000f360606040523615610074576000357c010000000000000000000000000000000000000000000000000000000090048063095ea7b31461008157806318160ddd146100b657806323b872dd146100d957806370a0823114610117578063a9059cbb14610143578063dd62ed3e1461017857610074565b61007f5b610002565b565b005b6100a060048080359060200190919080359060200190919050506101ad565b6040518082815260200191505060405180910390f35b6100c36004805050610674565b6040518082815260200191505060405180910390f35b6101016004808035906020019091908035906020019091908035906020019091905050610281565b6040518082815260200191505060405180910390f35b61012d600480803590602001909190505061048d565b6040518082815260200191505060405180910390f35b61016260048080359060200190919080359060200190919050506104cb565b6040518082815260200191505060405180910390f35b610197600480803590602001909190803590602001909190505061060b565b6040518082815260200191505060405180910390f35b600081600260005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008573ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905061027b565b92915050565b600081600160005060008673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561031b575081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505410155b80156103275750600082115b1561047c5781600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a381600160005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505403925050819055506001905061048656610485565b60009050610486565b5b9392505050565b6000600160005060008373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505490506104c6565b919050565b600081600160005060003373ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561050c5750600082115b156105fb5781600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a36001905061060556610604565b60009050610605565b5b92915050565b6000600260005060008473ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005054905061066e565b92915050565b60006000600050549050610683565b9056";

            public StandardTokenDeployment() : base(BYTECODE)
            {
            }

            [Parameter("uint256", "totalSupply")] public int TotalSupply { get; set; }
        }


        public static async Task TestDeployment1559Async()
        {
            var trezorBroker = NethereumTrezorManagerBrokerFactory.CreateWindowsHidUsb(GetPin, GetPassphrase, _loggerFactory);
            var trezorManager = await trezorBroker.WaitForFirstTrezorAsync();


            await trezorManager.InitializeAsync();
            var signer = new TrezorExternalSigner(trezorManager, 0);

            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            account.InitialiseDefaultTransactionManager(rpcClient);
            var web3 = new Web3.Web3(account, rpcClient);
            var deployment = new StandardTokenDeployment();
            deployment.FromAddress = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399";
            deployment.TotalSupply = 1000;
            deployment.Nonce = 1;
            deployment.MaxPriorityFeePerGas = 10;
            deployment.MaxFeePerGas = 10;
            deployment.Gas = 21000;

            var transactionInput = deployment.CreateTransactionInput();
            transactionInput.To = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399";

            var signature = await web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            var web32 = new Web3.Web3(new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1));
            var signatureNethereum = await web32.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            System.Console.WriteLine(signature.IsTheSameHex(signatureNethereum));
        }

        public static async Task TestDeploymentAsync()
        {
            var trezorBroker = NethereumTrezorManagerBrokerFactory.CreateWindowsHidUsb(GetPin, GetPassphrase, _loggerFactory);
            var trezorManager = await trezorBroker.WaitForFirstTrezorAsync();


            await trezorManager.InitializeAsync();
            var signer = new TrezorExternalSigner(trezorManager, 0);

            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            account.InitialiseDefaultTransactionManager(rpcClient);
            var web3 = new Web3.Web3(account, rpcClient);
            var deployment = new StandardTokenDeployment();
            deployment.FromAddress = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399";
            deployment.TotalSupply = 1000;
            deployment.Nonce = 1;
            deployment.GasPrice = 10;
            deployment.Gas = 21000;

            var transactionInput = deployment.CreateTransactionInput();
            transactionInput.To = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399";

            var signature = await web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            var web32 = new Web3.Web3(new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1));
            var signatureNethereum = await web32.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            System.Console.WriteLine(signature.IsTheSameHex(signatureNethereum));

        }

        public static async Task TestTransactionSigning()
        {
            var trezorBroker = NethereumTrezorManagerBrokerFactory.CreateWindowsHidUsb(GetPin, GetPassphrase, _loggerFactory);
            var trezorManager = await trezorBroker.WaitForFirstTrezorAsync();
            await trezorManager.InitializeAsync();
            var signer = new TrezorExternalSigner(trezorManager, 0);

            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            account.InitialiseDefaultTransactionManager(new RpcClient(new Uri("http://localhost:8545")));
            
            var tx = new TransactionInput()
            {
                Nonce = new HexBigInteger(10),
                GasPrice = new HexBigInteger(10),
                Gas = new HexBigInteger(21000),
                To = "0x689c56aef474df92d44a1b70850f808488f9769c",
                Value = new HexBigInteger(BigInteger.Parse("10000000000000000000")),
                From = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399"
            };
            var signature = await account.TransactionManager.SignTransactionAsync(tx);

            var accountNethereum = new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1);
            accountNethereum.TransactionManager.Client = new RpcClient(new Uri("http://localhost:8545"));
            var signatureNethereum = await accountNethereum.TransactionManager.SignTransactionAsync(tx);
            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            
        }


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
        

        public static async Task TestTransferTokenSigning()
        {
            var trezorBroker = NethereumTrezorManagerBrokerFactory.CreateWindowsHidUsb(GetPin, GetPassphrase, _loggerFactory);
            var trezorManager = await trezorBroker.WaitForFirstTrezorAsync();


                await trezorManager.InitializeAsync();
                var signer = new TrezorExternalSigner(trezorManager, 0);

                var account = new ExternalAccount(signer, 1);
                await account.InitialiseAsync();
                var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
                account.InitialiseDefaultTransactionManager(rpcClient);
                var web3 = new Web3.Web3(account, rpcClient);
                var tx = new TransferFunction()
                {
                    Nonce =10,
                    GasPrice = 10,
                    Gas = 21000,
                    To = "0x689c56aef474df92d44a1b70850f808488f9769c",
                    Value = 100,
                    FromAddress = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399"
                };

                var signature = await web3.Eth.GetContractTransactionHandler<TransferFunction>().SignTransactionAsync("0x6810e776880c02933d47db1b9fc05908e5386b96", tx);
                
                var web32 = new Web3.Web3(new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1));
                var signatureNethereum = await web32.Eth.GetContractTransactionHandler<TransferFunction>()
                    .SignTransactionAsync("0x6810e776880c02933d47db1b9fc05908e5386b96", tx);
                
                System.Console.WriteLine("Trezor: " + signature);
                System.Console.WriteLine("Nethereum: " + signatureNethereum);
            
        }


        /// <summary>
        /// When entering your pin remember that the order of the keys is in the format of:
        /// 
        /// 7 8 9
        /// 4 5 6
        /// 1 2 3
        /// 
        /// like in your keyboard
        /// 
        /// so if it is displayed
        /// 9 8 7
        /// 5 6 4
        /// 3 1 2
        /// 
        /// and you want to enter 2 2 2 2
        /// you will input 3 3 3 3 
        /// </summary>

        private async static Task<string> GetPin()
        {
            System.Console.WriteLine("Enter PIN based on Trezor values: ");
            return System.Console.ReadLine().Trim();
        }

        private async static Task<string> GetPassphrase()
        {
            System.Console.WriteLine("Enter passphrase based on Trezor values: ");
            return System.Console.ReadLine().Trim();
        }
    }
}
