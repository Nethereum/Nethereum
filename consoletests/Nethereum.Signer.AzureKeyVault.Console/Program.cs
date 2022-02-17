using System;
using System.Numerics;
using System.Threading.Tasks;
using Azure.Identity;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3.Accounts;

namespace Nethereum.Signer.AzureKeyVault.Console
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

    class Program
    {
        private const string APP_ID = "a73d5252-12f0-4b3e-80a2-8c13870bbcab";
        private const string APP_PASSWORD = "P3RpF4Ux2KHX9aoMAk4tUJtn8A5bAECCo/OmnwyeIW8=";
        private const string TENANT_ID = "TENANTID";
        private const string KEY_NAME = "nethereumec"; // "nethereumec/7ed70afdbf7d43bda5a8090515b154d2"
        private const string URI = "https://juanakv.vault.azure.net";
        static void Main(string[] args)
        {
            //var credential = new DefaultAzureCredential(
            //    new DefaultAzureCredentialOptions
            //    {
            //        //VisualStudioTenantId = TENANT_ID,
            //        //VisualStudioCodeTenantId = TENANT_ID,
            //    });
            var credential = new ClientSecretCredential(TENANT_ID, APP_ID, APP_PASSWORD);
            var signer = new AzureKeyVaultExternalSigner(KEY_NAME, URI, credential);
            var address = signer.GetAddressAsync().Result;
            System.Console.WriteLine(address);

            var msgHash = new Util.Sha3Keccack().CalculateHash("Hello").HexToByteArray();

            var signature = signer.SignAsync(msgHash).Result;
            var publicKeyRecovered = EthECKey.RecoverFromSignature(signature, msgHash);
            System.Console.WriteLine(publicKeyRecovered.GetPubKey().ToHex());

            var transfer = new TransferFunction();
            transfer.To = "0x1996a57077877D38e18A1BE44A55100D77b8fA1D";
            transfer.FromAddress = publicKeyRecovered.GetPublicAddress();
            transfer.Value = 1;
            transfer.Nonce = 1;
            transfer.GasPrice = 100;
            transfer.Gas = 1000;

            var rpcClient = new RpcClient(new Uri("http://localhost:8545"));
            var transactionInput = transfer.CreateTransactionInput("0x12890d2cce102216644c59daE5baed380d84830c");

            var externalAccount = new ExternalAccount(signer, 1);
            externalAccount.InitialiseAsync().Wait();
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);


            var signature2 = externalAccount.TransactionManager.SignTransactionAsync(transactionInput).Result;
            var publicKeyRecovered2 = TransactionVerificationAndRecovery.GetPublicKey(signature2);
            var transactionFromSignature = TransactionFactory.CreateTransaction(signature2);

            System.Console.WriteLine("Recovered public key");
            System.Console.WriteLine(publicKeyRecovered2.ToHex());
            System.Console.WriteLine("Recovered transaction Type");
            System.Console.WriteLine(transactionFromSignature.TransactionType.ToString());


            System.Console.WriteLine("Signing EIP1559");


            var transferEip1559 = new TransferFunction();
            transferEip1559.To = "0x1996a57077877D38e18A1BE44A55100D77b8fA1D";
            transferEip1559.FromAddress = publicKeyRecovered.GetPublicAddress();
            transferEip1559.Value = 1;
            transferEip1559.Nonce = 1;
            transferEip1559.MaxFeePerGas = 1000;
            transferEip1559.MaxPriorityFeePerGas = 999;
            transferEip1559.Gas = 1000;

            var transactionInputEip1559 = transferEip1559.CreateTransactionInput("0x12890d2cce102216644c59daE5baed380d84830c");
            var signature3 = externalAccount.TransactionManager.SignTransactionAsync(transactionInputEip1559).Result;
            var publicKeyRecovered3 = TransactionVerificationAndRecovery.GetPublicKey(signature3);
            var transactionFromSignatureEIP1559 = TransactionFactory.CreateTransaction(signature3);

            System.Console.WriteLine("Recovered public key");
            System.Console.WriteLine(publicKeyRecovered3.ToHex());
            System.Console.WriteLine("Recovered transaction Type");
            System.Console.WriteLine(transactionFromSignatureEIP1559.TransactionType.ToString());
            System.Console.ReadLine();

        }
    }
}
