using Microsoft.Extensions.Logging;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Signer.Trezor;
using Nethereum.Web3.Accounts;
using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Trezor.Net;
using Account = Nethereum.Web3.Accounts.Account;

// ReSharper disable AsyncConverter.AsyncWait

namespace Nethereum.Signer.Trezor.Console
{
    class Program
    {
        private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                })
                .AddDebug()
                .SetMinimumLevel(LogLevel.Debug);
        });
        private static readonly ITrezorPromptHandler _promptHandler = new ConsolePromptHandler();

        private static ILogger logger = _loggerFactory.CreateLogger<Program>();

        static async Task Main(string[] args)
        {
            var signer = await CreateSignerAsync();

            await TestMessageSigningAsync(signer);
            await TestTypedDataSigningAsync(signer);
            await TestDeployment1559Async(signer);
            //await TestDeploymentAsync(signer);
            await TestTransactionSigning(signer);
            await TestTransferTokenSigning(signer);
        }

        private static async Task<TrezorSessionExternalSigner> CreateSignerAsync()
        {
            logger.LogInformation("Creating Trezor Signer...");   
            var platformProviders = new NethereumTrezorManagerBrokerFactory.PlatformDeviceFactoryProviders();
            // TODO: assign platformProviders.LinuxProvider/MacProvider/AndroidProvider with custom implementations when targeting those OSes.
            logger.LogInformation("Creating Trezor Broker...");
            var trezorBroker = NethereumTrezorManagerBrokerFactory.CreateDefault(_promptHandler, _loggerFactory, platformProviders);
            logger.LogInformation("Waiting for Trezor Device...");
            var trezorManager = await trezorBroker.WaitForFirstTrezorAsync();
            var signer = new TrezorSessionExternalSigner(trezorManager, 0);
            logger.LogInformation("Initializing Trezor Signer...");
            await signer.InitializeAsync();
            var address = await signer.GetAddressAsync();
            System.Console.WriteLine($"Trezor Address: {address}");
            logger.LogInformation($"Using Trezor Address: {address}");
            logger.LogInformation("Trezor Signer is ready.");
            return signer;
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
        public static async Task TestMessageSigningAsync(TrezorSessionExternalSigner signer)
        {
            System.Console.WriteLine("Testing Message Signing...");
            var address = await signer.GetAddressAsync();
            var message = "hello hello hello";
            var padmessagetimes = 100;
            for (int i = 0; i < padmessagetimes; i++)
            {
                message += " hello";
            }
            var messageSignature = await signer.SignAsync(Encoding.UTF8.GetBytes(message));

            var nethereumMessageSigner = new Nethereum.Signer.EthereumMessageSigner();
            var nethereumMessageSignature = nethereumMessageSigner.EncodeUTF8AndSign(message, new EthECKey(
                "0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29"));
            System.Console.WriteLine("Trezor: " + EthECDSASignature.CreateStringSignature(messageSignature));
            System.Console.WriteLine("Nethereum: " + nethereumMessageSignature);
            var recovered = nethereumMessageSigner.EncodeUTF8AndEcRecover(message, messageSignature.CreateStringSignature());
            System.Console.WriteLine("Recovered: " + recovered);
            System.Console.WriteLine(EthECDSASignature.CreateStringSignature(messageSignature).IsTheSameHex(nethereumMessageSignature));

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


        private static async Task TestTypedDataSigningAsync(TrezorSessionExternalSigner signer)
        {
            const string typedDataJson = @"{
  ""types"": {
    ""EIP712Domain"": [
      { ""name"": ""name"", ""type"": ""string"" },
      { ""name"": ""version"", ""type"": ""string"" },
      { ""name"": ""chainId"", ""type"": ""uint256"" },
      { ""name"": ""verifyingContract"", ""type"": ""address"" }
    ],
    ""Group"": [
      { ""name"": ""name"", ""type"": ""string"" },
      { ""name"": ""members"", ""type"": ""Person[]"" }
    ],
    ""Mail"": [
      { ""name"": ""from"", ""type"": ""Person"" },
      { ""name"": ""to"", ""type"": ""Person[]"" },
      { ""name"": ""contents"", ""type"": ""string"" }
    ],
    ""Person"": [
      { ""name"": ""name"", ""type"": ""string"" },
      { ""name"": ""wallets"", ""type"": ""address[]"" }
    ]
  },
  ""primaryType"": ""Mail"",
  ""domain"": {
    ""name"": ""Ether Mail"",
    ""version"": ""1"",
    ""chainId"": 1,
    ""verifyingContract"": ""0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC""
  },
  ""message"": {
    ""from"": {
      ""name"": ""Cow"",
      ""wallets"": [
        ""0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826"",
        ""0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF""
      ]
    },
    ""to"": [
      {
        ""name"": ""Bob"",
        ""wallets"": [
          ""0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB"",
          ""0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57"",
          ""0xB0B0b0b0b0b0B000000000000000000000000000""
        ]
      }
    ],
    ""contents"": ""Hello, Bob!""
  }
}";

            System.Console.WriteLine("Signing typed data (JSON) with Trezor...");
            var signature = await signer.SignTypedDataJsonAsync(typedDataJson);

            var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(typedDataJson);
            var hashResult = Eip712TypedDataEncoder.Current.CalculateTypedDataHashes(typedDataRaw);
            System.Console.WriteLine("Typed Data DomainHash: " + hashResult.DomainHash.ToHex());
            System.Console.WriteLine("Typed Data MessageHash: " + (hashResult.MessageHash?.ToHex() ?? "null"));
            System.Console.WriteLine("Typed Data Hash: " + hashResult.TypedDataHash.ToHex());
            System.Console.WriteLine("Expected Hash: a85c2e2b118698e88db68a8105b794a8cc7cec074e89ef991cb4f5f533819cc2");

            var nethereumSigner = new Nethereum.Signer.EIP712.Eip712TypedDataSigner();
            var typedSignature = EthECDSASignature.CreateStringSignature(signature);
            var recovered = nethereumSigner.RecoverFromSignatureHashV4(hashResult.TypedDataHash, typedSignature);

            System.Console.WriteLine("Typed Data Signature: " + typedSignature);
            System.Console.WriteLine("Recovered Address: " + recovered);
        }

        public static async Task TestDeployment1559Async(TrezorSessionExternalSigner signer)
        {
            System.Console.WriteLine("Testing Deployment Signing EIP-1559...");
            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            var address = await signer.GetAddressAsync();
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            account.InitialiseDefaultTransactionManager(rpcClient);
            var web3 = new Web3.Web3(account, rpcClient);
            var deployment = new StandardTokenDeployment();
           // deployment.FromAddress = "0x6A1D4583b83E5ef91EeA1E591aD333BD04853399";
            deployment.TotalSupply = 1000;
            deployment.Nonce = 1;
            deployment.MaxPriorityFeePerGas = 10;
            deployment.MaxFeePerGas = 10;
            deployment.Gas = 21000;
            

            var signature = await web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            var web32 = new Web3.Web3(new Web3.Accounts.Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1));
            deployment.FromAddress = web32.TransactionManager.Account.Address;
            var signatureNethereum = await web32.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            System.Console.WriteLine(signature.IsTheSameHex(signatureNethereum));
        }

        public static async Task TestDeploymentAsync(TrezorSessionExternalSigner signer)
        {
            System.Console.WriteLine("Testing Deployment Signing...");
            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            var address = await signer.GetAddressAsync();
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            account.InitialiseDefaultTransactionManager(rpcClient);
            var web3 = new Web3.Web3(account, rpcClient);
            var deployment = new StandardTokenDeployment();
            
            deployment.TotalSupply = 1000;
            deployment.Nonce = 1;
            deployment.GasPrice = 10;
            deployment.Gas = 21000;

          
            

            var signature = await web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            var web32 = new Web3.Web3(new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1));
            deployment.FromAddress = web32.TransactionManager.Account.Address;
            var signatureNethereum = await web32.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SignTransactionAsync(deployment);

            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            System.Console.WriteLine(signature.IsTheSameHex(signatureNethereum));

        }

        public static async Task TestTransactionSigning(TrezorSessionExternalSigner signer)
        {
            System.Console.WriteLine("Testing Transaction Signing...");
            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            var address = await signer.GetAddressAsync();
            account.InitialiseDefaultTransactionManager(new RpcClient(new Uri("http://localhost:8545")));

            var tx = new TransactionInput()
            {
                Nonce = new HexBigInteger(10),
                GasPrice = new HexBigInteger(10),
                Gas = new HexBigInteger(21000),
                To = "0x689c56aef474df92d44a1b70850f808488f9769c",
                Value = new HexBigInteger(BigInteger.Parse("10000000000000000000")),
                From = address,
                
            };
            var signature = await account.TransactionManager.SignTransactionAsync(tx);

            var accountNethereum = new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1);
            accountNethereum.TransactionManager.Client = new RpcClient(new Uri("http://localhost:8545"));
            tx.From = accountNethereum.Address;
            var signatureNethereum = await accountNethereum.TransactionManager.SignTransactionAsync(tx);
            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            System.Console.WriteLine(signature.IsTheSameHex(signatureNethereum));

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


        public static async Task TestTransferTokenSigning(TrezorSessionExternalSigner signer)
        {
            System.Console.WriteLine("Testing Transfer Token Signing...");
            var account = new ExternalAccount(signer, 1);
            await account.InitialiseAsync();
            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            account.InitialiseDefaultTransactionManager(rpcClient);
            var web3 = new Web3.Web3(account, rpcClient);
            var tx = new TransferFunction()
            {
                Nonce = 10,
                GasPrice = 10,
                Gas = 21000,
                To = "0x689c56aef474df92d44a1b70850f808488f9769c",
                Value = 100,
               
            };

            var signature = await web3.Eth.GetContractTransactionHandler<TransferFunction>().SignTransactionAsync("0x6810e776880c02933d47db1b9fc05908e5386b96", tx);

            var web32 = new Web3.Web3(new Account("0x2e14c29aaecd1b7c681154d41f50c4bb8b6e4299a431960ed9e860e39cae6d29", 1));
            tx.FromAddress = web32.TransactionManager.Account.Address;
            var signatureNethereum = await web32.Eth.GetContractTransactionHandler<TransferFunction>()
                .SignTransactionAsync("0x6810e776880c02933d47db1b9fc05908e5386b96", tx);

            System.Console.WriteLine("Trezor: " + signature);
            System.Console.WriteLine("Nethereum: " + signatureNethereum);
            System.Console.WriteLine(signature.IsTheSameHex(signatureNethereum));

        }
    }
}
