using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Util;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Mozilla;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class EIP1559Test
    {


        [Theory]
        [InlineData(1559, 2, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "")]
        [InlineData(1559, 2, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "0x1232")]
        [InlineData(1559, 100, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "0x1232")]
        [InlineData(1559, 0, 3, 0, 3500, null, 0, "0x1232")]
        [InlineData(1559, 0, 3, 4, 3500, null, 10, null)]
        [InlineData(2, 0, 3, 4, 3500, null, 10, null)]
        [InlineData(0, 0, 0, 0, 0, null, 0, null)]
        public void ShouldEncodeDecodeSignTransaction(ulong chainId, ulong nonce, ulong maxPriorityFeePerGas, ulong maxFeePerGas,
            ulong gasLimit, string receiverAddress, ulong amount, string data)
        {
            var transaction1559 = new Transaction1559(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, null);
            transaction1559.Sign(new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));
            var encodedData = transaction1559.GetRLPEncodedRaw();
            var signedData = transaction1559.GetRLPEncoded();
            Debug.WriteLine(chainId + "," + nonce + "," + maxPriorityFeePerGas + "," + maxFeePerGas + "," + gasLimit + "," + receiverAddress + "," + amount + "," + data);
            Debug.WriteLine(encodedData.ToHex());
            Debug.WriteLine(signedData.ToHex());
            var decodedTransaction1559 = Transaction1559Encoder.Current.Decode(signedData);
            Assert.Equal(transaction1559.Amount, decodedTransaction1559.Amount);
            Assert.Equal(transaction1559.Nonce, decodedTransaction1559.Nonce);
            Assert.Equal(transaction1559.ChainId, decodedTransaction1559.ChainId);
            if (string.IsNullOrEmpty(transaction1559.Data))
            {
                Assert.Null(decodedTransaction1559.Data);
            }
            else
            {
                Assert.Equal(transaction1559.Data, decodedTransaction1559.Data);
            }

            Assert.Equal(transaction1559.GasLimit, decodedTransaction1559.GasLimit);
            Assert.Equal(transaction1559.MaxFeePerGas, decodedTransaction1559.MaxFeePerGas);
            Assert.Equal(transaction1559.MaxPriorityFeePerGas, decodedTransaction1559.MaxPriorityFeePerGas);

            if (string.IsNullOrEmpty(transaction1559.ReceiverAddress))
            {
                Assert.Null(decodedTransaction1559.ReceiverAddress);
            }
            else
            {
                Assert.Equal(transaction1559.ReceiverAddress, decodedTransaction1559.ReceiverAddress);
            }
    

            Assert.Equal(transaction1559.Signature.V.ToHex(), decodedTransaction1559.Signature.V.ToHex());
            Assert.Equal(transaction1559.Signature.R.ToHex(), decodedTransaction1559.Signature.R.ToHex());
            Assert.Equal(transaction1559.Signature.S.ToHex(), decodedTransaction1559.Signature.S.ToHex());

            Assert.True(decodedTransaction1559.VerifyTransaction());
            Assert.True(decodedTransaction1559.GetSenderAddress().IsTheSameAddress("0x12890D2cce102216644c59daE5baed380d84830c"));
        }

        [Theory]
        [InlineData(1559, 2, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "")]
        [InlineData(1559, 2, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "0x1232")]
        [InlineData(1559, 100, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "0x1232")]
        [InlineData(1559, 0, 3, 0, 3500, null, 0, "0x1232")]
        [InlineData(1559, 0, 3, 4, 3500, null, 10, null)]
        [InlineData(2, 0, 3, 4, 3500, null, 10, null)]
        [InlineData(0, 0, 0, 0, 0, null, 0, null)]
        public void ShouldEncodeDecodeSignTransactionAccessLists(ulong chainId, ulong nonce, ulong maxPriorityFeePerGas, ulong maxFeePerGas,
           ulong gasLimit, string receiverAddress, ulong amount, string data)
        {

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

            var transaction1559 = new Transaction1559(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, accessLists);
            transaction1559.Sign(new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));

            var encodedData = transaction1559.GetRLPEncodedRaw();
            var signedData = transaction1559.GetRLPEncoded();
            Debug.WriteLine(chainId + "," + nonce + "," + maxPriorityFeePerGas + "," + maxFeePerGas + "," + gasLimit + "," + receiverAddress + "," + amount + "," + data);
            Debug.WriteLine("");
            Debug.WriteLine(encodedData.ToHex());
            Debug.WriteLine(signedData.ToHex());
            var decodedTransaction1559 = Transaction1559Encoder.Current.Decode(signedData);
            Assert.Equal(transaction1559.Amount, decodedTransaction1559.Amount);
            Assert.Equal(transaction1559.Nonce, decodedTransaction1559.Nonce);
            Assert.Equal(transaction1559.ChainId, decodedTransaction1559.ChainId);
            if (string.IsNullOrEmpty(transaction1559.Data))
            {
                Assert.Null(decodedTransaction1559.Data);
            }
            else
            {
                Assert.Equal(transaction1559.Data, decodedTransaction1559.Data);
            }

            Assert.Equal(transaction1559.GasLimit, decodedTransaction1559.GasLimit);
            Assert.Equal(transaction1559.MaxFeePerGas, decodedTransaction1559.MaxFeePerGas);
            Assert.Equal(transaction1559.MaxPriorityFeePerGas, decodedTransaction1559.MaxPriorityFeePerGas);

            var decodedRlp = decodedTransaction1559.AccessList;

            Assert.True(accessLists[0].Address.IsTheSameAddress(decodedRlp[0].Address));
            Assert.Equal(accessLists[0].StorageKeys[0].ToHex(true), decodedRlp[0].StorageKeys[0].ToHex(true));
            Assert.Equal(accessLists[0].StorageKeys[1].ToHex(true), decodedRlp[0].StorageKeys[1].ToHex(true));
            Assert.True(accessLists[1].Address.IsTheSameAddress(decodedRlp[1].Address));
            Assert.Equal(accessLists[1].StorageKeys[0].ToHex(true), decodedRlp[1].StorageKeys[0].ToHex(true));
            Assert.Equal(accessLists[1].StorageKeys[1].ToHex(true), decodedRlp[1].StorageKeys[1].ToHex(true));

            if (string.IsNullOrEmpty(transaction1559.ReceiverAddress))
            {
                Assert.Null(decodedTransaction1559.ReceiverAddress);
            }
            else
            {
                Assert.Equal(transaction1559.ReceiverAddress, decodedTransaction1559.ReceiverAddress);
            }


            Assert.Equal(transaction1559.Signature.V.ToHex(), decodedTransaction1559.Signature.V.ToHex());
            Assert.Equal(transaction1559.Signature.R.ToHex(), decodedTransaction1559.Signature.R.ToHex());
            Assert.Equal(transaction1559.Signature.S.ToHex(), decodedTransaction1559.Signature.S.ToHex());
        }

        //INTEGRATION TESTING

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

            var web3 = new Web3.Web3();
            var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(
                "0x12890D2cce102216644c59daE5baed380d84830c");
            var lastBlock =
                await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
                    BlockParameter.CreateLatest());
            var baseFee = lastBlock.BaseFeePerGas;
            var maxPriorityFeePerGas = 2000000000;
            var maxFeePerGas = baseFee.Value * 2 + 2000000000;

            var transaction1559 = new Transaction1559(chainId, nonce.Value, maxPriorityFeePerGas, maxFeePerGas, 45000, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "", accessLists);
            transaction1559.Sign(new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));


            var txnHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(transaction1559.GetRLPEncoded().ToHex());
            // create recover signature
            var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txnHash);

            Assert.True(txn.To.IsTheSameAddress("0x1ad91ee08f21be3de0ba2ba6918e714da6b45836"));
            Assert.Equal(10, txn.Value.Value);

        }


        [Fact]
        public async void ShouldSendTransactionCalculatingTheDefaultFees()
        {
            var chainId = 444444444500;
            var web3 = new Web3.Web3();
            var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(
                "0x12890D2cce102216644c59daE5baed380d84830c");
            var lastBlock =
                await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
                    BlockParameter.CreateLatest());
            var baseFee = lastBlock.BaseFeePerGas;
            var maxPriorityFeePerGas = 2000000000;
            var maxFeePerGas = baseFee.Value * 2 + 2000000000;

            var transaction1559 = new Transaction1559(chainId, nonce.Value, maxPriorityFeePerGas, maxFeePerGas, 45000, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "", null);
            transaction1559.Sign(new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));
           
            
            var txnHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(transaction1559.GetRLPEncoded().ToHex());
            // create recover signature
            var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txnHash);
            //what I want is to get the right Transaction checking the type or chainid etc and do a recovery
            Assert.True(txn.To.IsTheSameAddress("0x1ad91ee08f21be3de0ba2ba6918e714da6b45836"));
            Assert.Equal(10, txn.Value.Value);

            var transaction1559FromChain = TransactionFactory.CreateTransaction(chainId, (byte)txn.Type.Value, txn.Nonce, txn.MaxPriorityFeePerGas,
                txn.MaxFeePerGas, null, txn.Gas, txn.To, txn.Value, txn.Input, null, txn.R, txn.S, txn.V);

            Assert.True(transaction1559FromChain.GetSenderAddress().IsTheSameAddress("0x12890D2cce102216644c59daE5baed380d84830c"));

            var transactionReceipt =
                await new TransactionReceiptPollingService(web3.TransactionManager).PollForReceiptAsync(txnHash, new CancellationTokenSource());

        }



    }
}
