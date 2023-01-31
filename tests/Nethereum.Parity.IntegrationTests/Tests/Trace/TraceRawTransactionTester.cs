using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.Parity.RPC.Trace;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Trace
{
    public class TraceRawTransactionTester : RPCRequestTester<JObject>
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
            var byteCode =
                "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

            var multiplier = 7;

            var web3 = new Web3.Web3(new Web3.Accounts.Account(privateKey), client);

            var receipt = await
                web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress,
                    new HexBigInteger(900000), null, multiplier).ConfigureAwait(false);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            var function = contract.GetFunction("multiply");
            var transactionInput = function.CreateTransactionInput(senderAddress, null, null, 7);
            var signer = new LegacyTransactionSigner();
            var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(senderAddress).ConfigureAwait(false);
            var signedTransaction = signer.SignTransaction(privateKey, transactionInput.To, 0, nonce.Value,
                LegacyTransaction.DEFAULT_GAS_PRICE, 900000,
                transactionInput.Data);

            var traceTransaction = new TraceRawTransaction(client);
            return await traceTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix(),
                new[] { TraceType.vmTrace }, BlockParameter.CreateLatest()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(TraceRawTransaction);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}