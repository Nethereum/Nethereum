using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class EIP1559Test
    {
        //Expanded RLP test cases can be found here: https://gist.github.com/juanfranblanco/4cc998247aecd822d6088e382d94a6f1
        [Theory]
        [InlineData(1559, 2, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "", "0x02f864820617020304820dac941ad91ee08f21be3de0ba2ba6918e714da6b458360a80c080a02b7505766dabb65f8ef497955459f9ea43ff4a092153a8acb277321a80b784a8a0276140649dae47bbb8f6d8fdc3e0daddb58bba498aa4e0b8c547d0d8ebdbf9a5")]
        [InlineData(1559, 100, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "0x1232", "0x02f866820617640304820dac941ad91ee08f21be3de0ba2ba6918e714da6b458360a821232c001a04e731e02022a10b97312998630d3dcaabda660e4a5f53d0fc1ebf4ba0cf8597fa01f4639e24823c565e3ac8e094e6eda571d1691022de83285925f9979b8ad7365")]
        [InlineData(2, 0, 3, 4, 3500, null, 10, null, "0x02f84e02800304820dac800a80c001a046cfe7dde69e52b91eafd3b213e4547d9ff6294a5ad79383bdb347828fe20102a041e5ab79953b91967bf790a138d9c380d856e6d8b783f1c1751bc446610e6cc6")]
        [InlineData(0, 0, 0, 0, 0, null, 0, null, "0x02f84c8080808080808080c001a001d4a14026b819394d91fef9336d00d3febed6fbe5d0a993c0d29a3b275c03b6a00cf6961f932346b5e6e5774c063e7a8794cd2dace75464d1fe5f38f3ba744cb5")]
        [InlineData(1559, 2, 3, 4, 3500, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 10, "0x1232", "0x02f866820617020304820dac941ad91ee08f21be3de0ba2ba6918e714da6b458360a821232c080a0d5ee3f01ce51d2b2930b268361be6fe9fc542e09311336d335cc4658d7bd7128a0038501925930d090429373c7855220d33a6cb949ea3bea273edcd540271c59ce")]
        [InlineData(1559, 0, 3, 4, 3500, null, 10, null, "0x02f850820617800304820dac800a80c080a0de12484b58bd47130bf9964740b4d68e42bcbbbc39b2eed5b917f0ae66f5e630a01b0d7aa6a810d63c25c115ef217e37023bbe3146b9bb1fe580d004d6432f7f32")]
        public void ShouldDecodeRLPAndRecoverSigner(ulong chainId, ulong nonce, ulong maxPriorityFeePerGas, ulong maxFeePerGas,
            ulong gasLimit, string receiverAddress, ulong amount, string data, string hexSigned)
        {
            var decodedTransaction1559 = Transaction1559Encoder.Current.Decode(hexSigned.HexToByteArray());
            Assert.Equal(amount, decodedTransaction1559.Amount);
            Assert.Equal(nonce, decodedTransaction1559.Nonce);
            Assert.Equal(chainId, decodedTransaction1559.ChainId);
            if (string.IsNullOrEmpty(data))
            {
                Assert.Null(decodedTransaction1559.Data);
            }
            else
            {
                Assert.Equal(data, decodedTransaction1559.Data);
            }

            Assert.Equal(gasLimit, decodedTransaction1559.GasLimit);
            Assert.Equal(maxFeePerGas, decodedTransaction1559.MaxFeePerGas);
            Assert.Equal(maxPriorityFeePerGas, decodedTransaction1559.MaxPriorityFeePerGas);

            if (string.IsNullOrEmpty(receiverAddress))
            {
                Assert.Null(decodedTransaction1559.ReceiverAddress);
            }
            else
            {
                Assert.Equal(receiverAddress, decodedTransaction1559.ReceiverAddress);
            }

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
    }
}
