using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using System;
using System.Text;
using Nethereum.Util;
using System.Diagnostics;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using BenchmarkDotNet.Configs;
using Nethereum.Model;

namespace Nethereum.Signer.Performance
{
    //dotnet run -c Release
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MessageSignerTest>(DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator));
        }
    }

    /*
// * Summary *

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.22000
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET Core SDK=6.0.400-preview.22301.10
  [Host]     : .NET Core 6.0.5 (CoreCLR 6.0.522.21309, CoreFX 6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET Core 6.0.5 (CoreCLR 6.0.522.21309, CoreFX 6.0.522.21309), X64 RyuJIT


|                    Method |          Mean |      Error |     StdDev |
|-------------------------- |--------------:|-----------:|-----------:|
|         MessageSigningRec |     0.1944 ns |  0.0028 ns |  0.0025 ns |
|      MessageSigningBouncy |     2.5965 ns |  0.0275 ns |  0.0257 ns |
|                  Recovery |     0.2177 ns |  0.0035 ns |  0.0033 ns |
|            RecoveryBouncy |     1.7579 ns |  0.0300 ns |  0.0281 ns |
|              FullRoundRec |     1.7343 ns |  0.0069 ns |  0.0061 ns |
|           FullRoundBouncy |     6.7162 ns |  0.1183 ns |  0.1408 ns |
|       SignFunctionMessage | 4,051.9825 ns | 16.8164 ns | 15.7300 ns |
| SignFunctionMessageBouncy | 4,051.6605 ns | 17.6386 ns | 15.6361 ns |

4,051ns == 0.004045 milliseconds
    */
    public class MessageSignerTest
    {

        EthECKey key;
        byte[] message;

        public MessageSignerTest()
        {
            key = new EthECKey("b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7".HexToByteArray(), true);
            message = Nethereum.Util.Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("Hello"));
        }

        [Benchmark(OperationsPerInvoke = 1000000)]
        public EthECDSASignature MessageSigningRec()
        {
            EthECKey.SignRecoverable = true;
            return key.SignAndCalculateV(message);
        }





        [Benchmark(OperationsPerInvoke = 1000000)]
        public EthECDSASignature MessageSigningBouncy()
        {
            EthECKey.SignRecoverable = false;
            return key.SignAndCalculateV(message);
            
        }

        [Benchmark(OperationsPerInvoke = 1000000)]
        public string Recovery()
        {
            EthECKey.SignRecoverable = true;
            var signature =
                "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var hasher = new Sha3Keccack();
            var hash = hasher.CalculateHash(text);
            var signer = new EthereumMessageSigner();
            var account = signer.EcRecover(hash.HexToByteArray(), signature);
            return account;
        }

        [Benchmark(OperationsPerInvoke = 1000000)]
        public string RecoveryBouncy()
        {
            EthECKey.SignRecoverable = false;
            var signature =
                "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var hasher = new Sha3Keccack();
            var hash = hasher.CalculateHash(text);
            var signer = new EthereumMessageSigner();
            var account = signer.EcRecover(hash.HexToByteArray(), signature);
            return account;
        }

        [Benchmark(OperationsPerInvoke = 1000000)]
        public string FullRoundRec()
        {
            EthECKey.SignRecoverable = true;
            var key = EthECKey.GenerateKey();
            var address = key.GetPublicAddress();
            var signature = key.SignAndCalculateV(message);
            var recAdress = EthECKey.RecoverFromSignature(signature, message).GetPublicAddress();
            if (!address.IsTheSameAddress(recAdress))
            {
                Debug.WriteLine(key.GetPrivateKey());
            }
            return recAdress;
        }

        [Benchmark(OperationsPerInvoke = 1000000)]
        public string FullRoundBouncy()
        {
            EthECKey.SignRecoverable = false;
            var key = EthECKey.GenerateKey();
            var address = key.GetPublicAddress();
            var signature = key.SignAndCalculateV(message);
            var recAdress = EthECKey.RecoverFromSignature(signature, message).GetPublicAddress();
            if (!address.IsTheSameAddress(recAdress))
            {
                Debug.WriteLine(key.GetPrivateKey());
            }
            return recAdress;
        }



        [Benchmark(OperationsPerInvoke = 1000000)]
        public string SignFunctionMessage()
        {
            EthECKey.SignRecoverable = true;
            var web3 = new Web3.Web3(new Web3.Accounts.Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));
            var newAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var fromAddress = web3.TransactionManager.Account.Address;
            var transactionMessage = new TransferFunction
            {
                FromAddress = fromAddress,
                To = newAddress,
                Value = 1000,
                MaxFeePerGas = 1000,
                MaxPriorityFeePerGas = 1000,
                Nonce = 0,
                TransactionType = TransactionType.EIP1559.AsByte(),
                Gas = 1000
            };

            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var signature =  transferHandler.SignTransactionAsync(fromAddress, transactionMessage).Result;
            return signature;
        }


        [Benchmark(OperationsPerInvoke = 1000000)]
        public string SignFunctionMessageBouncy()
        {
            EthECKey.SignRecoverable = false;
            var web3 = new Web3.Web3(new Web3.Accounts.Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));
            var newAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var fromAddress = web3.TransactionManager.Account.Address;
            var transactionMessage = new TransferFunction
            {
                FromAddress = fromAddress,
                To = newAddress,
                Value = 1000,
                MaxFeePerGas = 1000,
                MaxPriorityFeePerGas = 1000,
                Nonce = 0,
                TransactionType = TransactionType.EIP1559.AsByte(),
                Gas = 1000
            };

            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var signature = transferHandler.SignTransactionAsync(fromAddress, transactionMessage).Result;
            return signature;
        }
    }

}
