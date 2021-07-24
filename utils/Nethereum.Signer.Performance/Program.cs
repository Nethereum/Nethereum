using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using System;
using System.Text;
using Nethereum.Util;

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
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.22000
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET Core SDK=6.0.100-preview.5.21302.13
  [Host]     : .NET Core 3.1.15 (CoreCLR 4.700.21.21202, CoreFX 4.700.21.21402), X64 RyuJIT  [AttachedDebugger]
  DefaultJob : .NET Core 3.1.15 (CoreCLR 4.700.21.21202, CoreFX 4.700.21.21402), X64 RyuJIT

|               Method |       Mean |    Error |   StdDev |
|--------------------- |-----------:|---------:|---------:|
|    MessageSigningRec |   231.1 us |  1.42 us |  1.19 us |
| MessageSigningBouncy | 2,897.7 us | 57.92 us | 68.95 us |
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

        [Benchmark]
        public EthECDSASignature MessageSigningRec()
        {
            EthECKey.SignRecoverable = true;
            return key.SignAndCalculateV(message);
        }

        [Benchmark]
        public EthECDSASignature MessageSigningBouncy()
        {
            EthECKey.SignRecoverable = false;
            return key.SignAndCalculateV(message);
            
        }

        [Benchmark]
        public string Recovery()
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
    }

}
