using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using System;
using System.Text;
using Nethereum.Util;
using System.Diagnostics;

namespace Nethereum.Signer.Performance
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MessageSignerTest>();
        }
    }

    /*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.1256 (1909/November2018Update/19H2)
Intel Core i7-7560U CPU 2.40GHz (Kaby Lake), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=5.0.400-preview.21328.4
  [Host]     : .NET Core 3.1.17 (CoreCLR 4.700.21.31506, CoreFX 4.700.21.31502), X64 RyuJIT
  DefaultJob : .NET Core 3.1.17 (CoreCLR 4.700.21.31506, CoreFX 4.700.21.31502), X64 RyuJIT


|               Method |       Mean |     Error |    StdDev |     Median |
|--------------------- |-----------:|----------:|----------:|-----------:|
|    MessageSigningRec |  0.6303 ns | 0.0354 ns | 0.1044 ns |  0.6065 ns |
| MessageSigningBouncy |  5.8634 ns | 0.2128 ns | 0.6072 ns |  5.8039 ns |
|             Recovery |  1.5782 ns | 0.1643 ns | 0.4792 ns |  1.8315 ns |
|       RecoveryBouncy |  4.9051 ns | 0.1879 ns | 0.5450 ns |  4.9124 ns |
|         FullRoundRec |  7.0034 ns | 0.6793 ns | 1.9923 ns |  6.6981 ns |
|      FullRoundBouncy | 16.9897 ns | 0.6262 ns | 1.8462 ns | 17.0129 ns |
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
    }

}
