using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.RPC.Web3;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Signer.IntegrationTests
{
    public class EIP1559Test
    {

        [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
        public class SignedEIP155
        {
            private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

            public SignedEIP155(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
            {
                _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
            }

            //[Fact]
            public async void ShouldCheckFeeHistory()
            {
                //besu
                // var web3 = new Nethereum.Web3.Web3("http://18.116.30.130:8545/");
                //calavera
                var web3 = new Nethereum.Web3.Web3("http://18.224.51.102:8545/");
                //var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Goerli);
                var version = await new Web3ClientVersion(web3.Client).SendRequestAsync().ConfigureAwait(false);

                var x = new TimePreferenceFeeSuggestionStrategy(web3.Client);
                var fees = await x.SuggestFeesAsync();

                //var block =
                //    await web3.Eth.FeeHistory.SendRequestAsync(7, new BlockParameter(10), new []{10,20, 30}
                //         );
                var count = fees.Length;

            }

            [Fact]
            public async void ShouldSendTransactionWithAccessLists()
            {
                var chainId = 444444444500;

                var accessLists = new List<AccessListItem>();
                accessLists.Add(new AccessListItem("0x527306090abaB3A6e1400e9345bC60c78a8BEf57",
                    new List<byte[]>
                    {
                        "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abab".HexToByteArray(),
                        "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abac".HexToByteArray()
                    }
                ));
                accessLists.Add(new AccessListItem("0x427306090abaB3A6e1400e9345bC60c78a8BEf5c",
                    new List<byte[]>
                    {
                        "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abaa".HexToByteArray(),
                        "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abad".HexToByteArray()
                    }
                ));

                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var nonce = await web3.Eth.TransactionManager.Account.NonceService.GetNextNonceAsync();
                var lastBlock =
                    await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
                        BlockParameter.CreateLatest());
                var baseFee = lastBlock.BaseFeePerGas;
                var maxPriorityFeePerGas = 2000000000;
                var maxFeePerGas = baseFee.Value * 2 + 2000000000;

                var transaction1559 = new Transaction1559(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, 45000,
                    "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "", accessLists);
                transaction1559.Sign(new EthECKey(EthereumClientIntegrationFixture.AccountPrivateKey));


                var txnHash =
                    await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(transaction1559.GetRLPEncoded()
                        .ToHex());
                // create recover signature
                var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txnHash);

                Assert.True(txn.To.IsTheSameAddress("0x1ad91ee08f21be3de0ba2ba6918e714da6b45836"));
                Assert.Equal(10, txn.Value.Value);

            }


            [Fact]
            public async void ShouldSendTransactionCalculatingTheDefaultFees()
            {
                var chainId = 444444444500;
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var nonce = await web3.Eth.TransactionManager.Account.NonceService.GetNextNonceAsync();
                var lastBlock =
                    await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
                        BlockParameter.CreateLatest());
                var baseFee = lastBlock.BaseFeePerGas;
                var maxPriorityFeePerGas = 2000000000;
                var maxFeePerGas = baseFee.Value * 2 + 2000000000;

                var transaction1559 = new Transaction1559(chainId, nonce.Value, maxPriorityFeePerGas, maxFeePerGas,
                    45000,
                    "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "", null);
                transaction1559.Sign(new EthECKey(EthereumClientIntegrationFixture.AccountPrivateKey));


                var txnHash =
                    await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(transaction1559.GetRLPEncoded()
                        .ToHex());
                // create recover signature
                var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txnHash);
                //what I want is to get the right Transaction checking the type or chainid etc and do a recovery
                Assert.True(txn.To.IsTheSameAddress("0x1ad91ee08f21be3de0ba2ba6918e714da6b45836"));
                Assert.Equal(10, txn.Value.Value);

                var transaction1559FromChain = TransactionFactory.CreateTransaction(chainId, (byte) txn.Type.Value,
                    txn.Nonce, txn.MaxPriorityFeePerGas,
                    txn.MaxFeePerGas, null, txn.Gas, txn.To, txn.Value, txn.Input, null, txn.R, txn.S, txn.V);

                Assert.True(transaction1559FromChain.GetSenderAddress()
                    .IsTheSameAddress("0x12890D2cce102216644c59daE5baed380d84830c"));

                var transactionReceipt =
                    await new TransactionReceiptPollingService(web3.TransactionManager).PollForReceiptAsync(txnHash,
                        new CancellationTokenSource());

            }

        }
    }
}