using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Trezor.Net.Contracts.Ethereum;

namespace Nethereum.Signer.Trezor.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            TestMessageSigning().Wait();
            TestTransactionSigning().Wait();
            TestTransferTokenSigning().Wait();
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
        public static async Task TestMessageSigning()
        {
            using (var trezorManager = await TrezorFactory.GetWindowsConnectedLedgerManagerAsync(GetPin))
            {
                await trezorManager.InitializeAsync();
                var signer = new TrezorExternalSigner(trezorManager, 0);
                var address = await signer.GetAddressAsync();
                var messageSignature = await signer.SignAsync(Encoding.UTF8.GetBytes("Hello"));

                var nethereumMessageSigner = new Nethereum.Signer.EthereumMessageSigner();
                var nethereumMessageSignature = nethereumMessageSigner.EncodeUTF8AndSign("Hello", new EthECKey(
                    "0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29"));
                System.Console.WriteLine("Trezor: " + EthECDSASignature.CreateStringSignature(messageSignature));
                System.Console.WriteLine("Nethereum: " + nethereumMessageSignature);
                
            }
        }


        public static async Task TestTransactionSigning()
        {
            using (var trezorManager = await TrezorFactory.GetWindowsConnectedLedgerManagerAsync(GetPin))
            {
                await trezorManager.InitializeAsync();
                var signer = new TrezorExternalSigner(trezorManager, 0);

                var account = new ExternalAccount(signer);
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
                
                var accountNethereum = new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29");
                accountNethereum.TransactionManager.Client = new RpcClient(new Uri("http://localhost:8545"));
                var signatureNethereum = await accountNethereum.TransactionManager.SignTransactionAsync(tx);
                System.Console.WriteLine("Trezor: " + signature);
                System.Console.WriteLine("Nethereum: " + signatureNethereum);
            }
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
            using (var trezorManager = await TrezorFactory.GetWindowsConnectedLedgerManagerAsync(GetPin))
            {
                await trezorManager.InitializeAsync();
                var signer = new TrezorExternalSigner(trezorManager, 0);

                var account = new ExternalAccount(signer);
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
                
                var web32 = new Web3.Web3(new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29"));
                var signatureNethereum = await web32.Eth.GetContractTransactionHandler<TransferFunction>()
                    .SignTransactionAsync("0x6810e776880c02933d47db1b9fc05908e5386b96", tx);
                
                System.Console.WriteLine("Trezor: " + signature);
                System.Console.WriteLine("Nethereum: " + signatureNethereum);
            }
        }

        private async static Task<string> GetPin()
        {
            System.Console.WriteLine("Enter PIN based on Trezor values: ");
            return System.Console.ReadLine().Trim();
        }
    }
}
