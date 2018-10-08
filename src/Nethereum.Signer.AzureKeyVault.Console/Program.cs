using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer.Crypto;
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
        private static string APP_ID = "a73d5252-12f0-4b3e-80a2-8c13870bbcab";
        private static string APP_PASSWORD = "";
        private static string URI = "https://juanakv.vault.azure.net/keys/nethereumec/7ed70afdbf7d43bda5a8090515b154d2";
        static void Main(string[] args)
        {
            KeyVaultClient kvc = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
            var signer = new AzureKeyVaultExternalSigner(kvc, URI);
            var publicKey = signer.GetPublicKeyAsync().Result;
            System.Console.WriteLine(publicKey.ToHex());

            var msgHash = new Util.Sha3Keccack().CalculateHash("Hello").HexToByteArray();
            var ethExternalSigner = new EthECKeyExternalSigner(signer);

            var signature = ethExternalSigner.SignAndCalculateVAsync(msgHash).Result;
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

            var externalAccount = new ExternalAccount(ethExternalSigner, 1);
            externalAccount.InitialiseAsync().Wait();
            externalAccount.InitialiseDefaultTransactionManager(rpcClient);
            var signature2 = externalAccount.TransactionManager.SignTransactionAsync(transactionInput).Result;
            var transactionSigner = new TransactionSigner();
            var publicKeyRecovered2 =  transactionSigner.GetPublicKey(signature2, 1);

            System.Console.WriteLine(publicKeyRecovered2.ToHex());
            System.Console.ReadLine();

        }

        // https://docs.microsoft.com/en-us/azure/key-vault/key-vault-use-from-web-application
        public static async Task<string> GetToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            var clientCred = new ClientCredential(APP_ID, APP_PASSWORD);
            var result = await authContext.AcquireTokenAsync(resource, clientCred);

            if (result == null)
                throw new InvalidOperationException("Failed to obtain the JWT token");

            return result.AccessToken;
        }
    }
}
